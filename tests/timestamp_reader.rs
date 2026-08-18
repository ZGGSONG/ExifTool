use std::{
    fs::File,
    io::{self, Write},
    path::Path,
};

use chrono::{DateTime, Local, NaiveDateTime, Utc};
use exif_tool::{MetadataTimestampReader, TimestampReader, TimestampSource};
use tempfile::TempDir;

#[test]
fn quicktime_created_timestamp_is_read_and_localized() {
    let directory = TempDir::new().unwrap();
    let created = NaiveDateTime::parse_from_str("2024-01-02 03:04:05", "%F %T").unwrap();
    let path = directory.path().join("clip.mov");
    write_quicktime_movie(&path, created).unwrap();

    let result = MetadataTimestampReader.read_timestamp(&path).unwrap();
    let expected = DateTime::<Utc>::from_naive_utc_and_offset(created, Utc)
        .with_timezone(&Local)
        .naive_local();

    assert_eq!(result.value, expected);
    assert_eq!(result.source, TimestampSource::Metadata);
}

#[test]
fn missing_metadata_falls_back_to_creation_time() {
    let directory = TempDir::new().unwrap();
    let path = directory.path().join("photo.jpg");
    std::fs::write(&path, "not an image").unwrap();

    let result = MetadataTimestampReader.read_timestamp(&path).unwrap();

    assert_eq!(result.source, TimestampSource::CreationTime);
}

fn write_quicktime_movie(path: &Path, created: NaiveDateTime) -> io::Result<()> {
    let mut writer = File::create(path)?;
    let epoch = NaiveDateTime::parse_from_str("1904-01-01 00:00:00", "%F %T").unwrap();
    let seconds: u32 = (created - epoch).num_seconds().try_into().unwrap();

    write_u32(&mut writer, 20)?;
    writer.write_all(b"ftypqt  ")?;
    write_u32(&mut writer, 0)?;
    writer.write_all(b"qt  ")?;

    write_u32(&mut writer, 116)?;
    writer.write_all(b"moov")?;
    write_u32(&mut writer, 108)?;
    writer.write_all(b"mvhd")?;
    writer.write_all(&[0, 0, 0, 0])?;
    write_u32(&mut writer, seconds)?;
    write_u32(&mut writer, seconds)?;
    write_u32(&mut writer, 600)?;
    write_u32(&mut writer, 0)?;
    write_u32(&mut writer, 0x0001_0000)?;
    writer.write_all(&0x0100_u16.to_be_bytes())?;
    writer.write_all(&[0; 10])?;

    for value in [0x0001_0000, 0, 0, 0, 0x0001_0000, 0, 0, 0, 0x4000_0000] {
        write_u32(&mut writer, value)?;
    }
    for _ in 0..6 {
        write_u32(&mut writer, 0)?;
    }
    write_u32(&mut writer, 1)
}

fn write_u32(writer: &mut impl Write, value: u32) -> io::Result<()> {
    writer.write_all(&value.to_be_bytes())
}
