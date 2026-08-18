//! Metadata and file-system timestamp extraction.

use std::{fs, path::Path};

use chrono::{DateTime, Local, NaiveDateTime};
use nom_exif::{EntryValue, Exif, ExifTag, MediaKind, MediaParser, MediaSource, TrackInfoTag};

use crate::{PhotoTimestamp, TimestampSource};

/// Reads the timestamp used to name a media file.
pub trait TimestampReader: Send + Sync {
    /// Reads a metadata timestamp or falls back to file creation time.
    fn read_timestamp(&self, path: &Path) -> Result<PhotoTimestamp, String>;
}

/// Reads image EXIF and video track metadata with a file-system fallback.
#[derive(Clone, Copy, Debug, Default)]
pub struct MetadataTimestampReader;

impl TimestampReader for MetadataTimestampReader {
    fn read_timestamp(&self, path: &Path) -> Result<PhotoTimestamp, String> {
        if let Some(value) = read_metadata_timestamp(path) {
            return Ok(PhotoTimestamp {
                value,
                source: TimestampSource::Metadata,
            });
        }

        read_creation_timestamp(path).map(|value| PhotoTimestamp {
            value,
            source: TimestampSource::CreationTime,
        })
    }
}

fn read_metadata_timestamp(path: &Path) -> Option<NaiveDateTime> {
    let source = MediaSource::open(path).ok()?;
    let kind = source.kind();
    let mut parser = MediaParser::new();

    match kind {
        MediaKind::Image => {
            let exif: Exif = parser.parse_exif(source).ok()?.into();
            [
                ExifTag::DateTimeOriginal,
                ExifTag::CreateDate,
                ExifTag::ModifyDate,
            ]
            .into_iter()
            .find_map(|tag| exif.get(tag).and_then(entry_local_datetime))
        }
        MediaKind::Track => parser
            .parse_track(source)
            .ok()?
            .get(TrackInfoTag::CreateDate)
            .and_then(entry_local_datetime),
    }
}

fn entry_local_datetime(value: &EntryValue) -> Option<NaiveDateTime> {
    match value {
        EntryValue::DateTime(value) => Some(value.with_timezone(&Local).naive_local()),
        EntryValue::NaiveDateTime(value) => Some(*value),
        _ => None,
    }
}

fn read_creation_timestamp(path: &Path) -> Result<NaiveDateTime, String> {
    let metadata = fs::metadata(path).map_err(|error| error.to_string())?;
    let created = metadata
        .created()
        .map_err(|error| format!("无法读取文件创建时间：{error}"))?;

    Ok(DateTime::<Local>::from(created).naive_local())
}

#[cfg(test)]
mod tests {
    use chrono::{FixedOffset, TimeZone};

    use super::*;

    #[test]
    fn aware_metadata_timestamp_is_converted_to_local_time() {
        let offset = FixedOffset::east_opt(8 * 60 * 60).unwrap();
        let source = offset.with_ymd_and_hms(2024, 5, 6, 7, 8, 9).unwrap();
        let expected = source.with_timezone(&Local).naive_local();

        assert_eq!(
            entry_local_datetime(&EntryValue::DateTime(source)),
            Some(expected)
        );
    }

    #[test]
    fn naive_metadata_timestamp_keeps_wall_clock_value() {
        let source = NaiveDateTime::parse_from_str("2024-05-06 07:08:09", "%F %T").unwrap();

        assert_eq!(
            entry_local_datetime(&EntryValue::NaiveDateTime(source)),
            Some(source)
        );
    }
}
