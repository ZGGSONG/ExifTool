//! Core media timestamp and in-place renaming behavior for ExifTool.

mod model;
mod rename;
mod timestamp;

pub use model::{PhotoRenameResult, PhotoTimestamp, RenameStatus, TimestampSource};
pub use rename::{FILE_NAME_TIMESTAMP_FORMAT, PhotoRenameService};
pub use timestamp::{MetadataTimestampReader, TimestampReader};
