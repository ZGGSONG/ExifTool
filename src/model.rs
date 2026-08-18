//! Domain values shared by the metadata, rename, and user-interface layers.

use std::path::{Path, PathBuf};

use chrono::NaiveDateTime;

/// Identifies where the timestamp used for a filename came from.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum TimestampSource {
    /// The timestamp came from image or video metadata.
    Metadata,
    /// The timestamp came from file-system creation time.
    CreationTime,
}

/// A normalized local timestamp and its source.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub struct PhotoTimestamp {
    /// The local wall-clock value used in the target filename.
    pub value: NaiveDateTime,
    /// The source from which the value was read.
    pub source: TimestampSource,
}

/// Represents the outcome of one media rename request.
#[derive(Clone, Copy, Debug, Eq, PartialEq)]
pub enum RenameStatus {
    /// The source file was renamed successfully.
    Renamed,
    /// The source already had the computed target name.
    AlreadyNamed,
    /// Validation, metadata reading, or file-system mutation failed.
    Failed,
}

/// Describes the result of processing one dropped path.
#[derive(Clone, Debug, Eq, PartialEq)]
pub struct PhotoRenameResult {
    /// The original path requested by the caller.
    pub source_path: PathBuf,
    /// The resulting path when one could be computed.
    pub target_path: Option<PathBuf>,
    /// The final processing status.
    pub status: RenameStatus,
    /// The source of the timestamp used for a successful result.
    pub timestamp_source: Option<TimestampSource>,
    /// A localized failure description.
    pub error_message: Option<String>,
}

impl PhotoRenameResult {
    /// Returns the original filename for display.
    pub fn source_file_name(&self) -> String {
        display_file_name(&self.source_path)
    }

    /// Returns the target filename for display, or a placeholder on failure.
    pub fn target_file_name(&self) -> String {
        self.target_path
            .as_deref()
            .map(display_file_name)
            .unwrap_or_else(|| "-".to_owned())
    }

    /// Returns the localized status label used in the results table.
    pub fn status_text(&self) -> &'static str {
        match self.status {
            RenameStatus::Renamed => "已重命名",
            RenameStatus::AlreadyNamed => "无需处理",
            RenameStatus::Failed => "失败",
        }
    }

    /// Returns the localized timestamp-source label used in the results table.
    pub fn timestamp_source_text(&self) -> &'static str {
        match self.timestamp_source {
            Some(TimestampSource::Metadata) => "元数据时间",
            Some(TimestampSource::CreationTime) => "文件创建时间",
            None => "-",
        }
    }

    /// Returns the most useful detailed result text for diagnostics.
    pub fn detail_text(&self) -> String {
        self.error_message
            .clone()
            .or_else(|| {
                self.target_path
                    .as_deref()
                    .map(|path| path.to_string_lossy().into_owned())
            })
            .unwrap_or_default()
    }
}

fn display_file_name(path: &Path) -> String {
    path.file_name()
        .unwrap_or(path.as_os_str())
        .to_string_lossy()
        .into_owned()
}
