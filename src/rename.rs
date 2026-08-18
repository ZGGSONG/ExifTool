//! In-place media filename generation and collision handling.

use std::{
    ffi::OsStr,
    fs, io,
    path::{Path, PathBuf},
};

use chrono::NaiveDateTime;

use crate::{
    MetadataTimestampReader, PhotoRenameResult, RenameStatus, TimestampReader, TimestampSource,
};

/// The timestamp pattern used at the beginning of generated filenames.
pub const FILE_NAME_TIMESTAMP_FORMAT: &str = "%Y%m%d_%H%M%S";
const FILE_NAME_TIMESTAMP_LENGTH: usize = 15;

const SUPPORTED_MEDIA_EXTENSIONS: &[&str] = &[
    "3fr", "arw", "avif", "bmp", "cr2", "cr3", "crw", "crx", "dng", "eps", "gif", "gpr", "heic",
    "heif", "ico", "jfif", "jpe", "jpeg", "jpg", "kdc", "mov", "nef", "orf", "pcx", "pef", "png",
    "psd", "raf", "rw2", "rwl", "srw", "tga", "tif", "tiff", "webp",
];

/// Renames media files using capture metadata or file creation time.
pub struct PhotoRenameService {
    timestamp_reader: Box<dyn TimestampReader>,
}

impl Default for PhotoRenameService {
    fn default() -> Self {
        Self::new(MetadataTimestampReader)
    }
}

impl PhotoRenameService {
    /// Creates a service with an injectable timestamp reader.
    pub fn new(timestamp_reader: impl TimestampReader + 'static) -> Self {
        Self {
            timestamp_reader: Box::new(timestamp_reader),
        }
    }

    /// Renames all requested paths in order and continues after failures.
    pub fn rename_files<I, P>(&self, paths: I) -> Vec<PhotoRenameResult>
    where
        I: IntoIterator<Item = P>,
        P: AsRef<Path>,
    {
        paths
            .into_iter()
            .map(|path| self.rename_file(path.as_ref()))
            .collect()
    }

    /// Renames one supported media file in its source directory.
    pub fn rename_file(&self, source_path: &Path) -> PhotoRenameResult {
        match self.try_rename_file(source_path) {
            Ok(result) => result,
            Err(error) => failure(source_path, error),
        }
    }

    fn try_rename_file(&self, source_path: &Path) -> Result<PhotoRenameResult, String> {
        if source_path.as_os_str().is_empty() {
            return Err("文件路径为空。".to_owned());
        }
        if !source_path.is_file() {
            return Err("文件不存在或不是本地文件。".to_owned());
        }
        if !Self::is_supported_media_file(source_path) {
            return Err("不支持的媒体格式。".to_owned());
        }

        let timestamp = self.timestamp_reader.read_timestamp(source_path)?;
        let target_path = Self::resolve_target_path(source_path, timestamp.value)
            .map_err(|error| error.to_string())?;

        if paths_equal(source_path, &target_path) {
            return Ok(success(
                source_path,
                target_path,
                RenameStatus::AlreadyNamed,
                timestamp.source,
            ));
        }

        fs::rename(source_path, &target_path).map_err(|error| error.to_string())?;
        Ok(success(
            source_path,
            target_path,
            RenameStatus::Renamed,
            timestamp.source,
        ))
    }

    /// Builds the unsuffixed target path for a source path and timestamp.
    pub fn build_target_path(
        source_path: &Path,
        timestamp: NaiveDateTime,
    ) -> Result<PathBuf, io::Error> {
        let directory = source_directory(source_path)?;
        let extension = lowercase_extension(source_path);
        let file_name = format!(
            "{}({}){}",
            timestamp.format(FILE_NAME_TIMESTAMP_FORMAT),
            original_file_stem(source_path),
            extension
        );

        Ok(directory.join(file_name))
    }

    /// Finds the first available target path, adding a three-digit suffix when needed.
    pub fn resolve_target_path(
        source_path: &Path,
        timestamp: NaiveDateTime,
    ) -> Result<PathBuf, io::Error> {
        let base_path = Self::build_target_path(source_path, timestamp)?;
        let directory = source_directory(source_path)?;
        let extension = lowercase_extension(source_path);
        let base_stem = base_path
            .file_stem()
            .and_then(OsStr::to_str)
            .ok_or_else(|| invalid_path("无法生成目标文件名。"))?;

        for suffix in 0..=999 {
            let target_path = if suffix == 0 {
                base_path.clone()
            } else {
                directory.join(format!("{base_stem}_{suffix:03}{extension}"))
            };

            if paths_equal(source_path, &target_path) || !target_path.exists() {
                return Ok(target_path);
            }
        }

        Err(io::Error::new(
            io::ErrorKind::AlreadyExists,
            "同一拍摄时间的文件过多，无法生成可用文件名。",
        ))
    }

    /// Returns whether the path has one of the supported media extensions.
    pub fn is_supported_media_file(path: &Path) -> bool {
        path.extension()
            .and_then(OsStr::to_str)
            .is_some_and(|extension| {
                SUPPORTED_MEDIA_EXTENSIONS
                    .iter()
                    .any(|supported| extension.eq_ignore_ascii_case(supported))
            })
    }
}

fn source_directory(source_path: &Path) -> Result<&Path, io::Error> {
    source_path
        .parent()
        .filter(|parent| !parent.as_os_str().is_empty())
        .ok_or_else(|| invalid_path("源文件必须位于文件夹中。"))
}

fn invalid_path(message: &'static str) -> io::Error {
    io::Error::new(io::ErrorKind::InvalidInput, message)
}

fn lowercase_extension(path: &Path) -> String {
    path.extension()
        .map(|extension| format!(".{}", extension.to_string_lossy().to_lowercase()))
        .unwrap_or_default()
}

fn original_file_stem(path: &Path) -> String {
    let stem = path
        .file_stem()
        .unwrap_or_default()
        .to_string_lossy()
        .to_lowercase();

    generated_original_stem(&stem).unwrap_or(stem)
}

fn generated_original_stem(stem: &str) -> Option<String> {
    if !has_timestamp_prefix(stem) || stem.as_bytes().get(FILE_NAME_TIMESTAMP_LENGTH) != Some(&b'(')
    {
        return None;
    }

    let close_parenthesis = stem.rfind(')')?;
    let suffix = &stem[close_parenthesis + 1..];
    let has_valid_suffix = suffix.is_empty()
        || (suffix.len() == 4
            && suffix.starts_with('_')
            && suffix[1..].bytes().all(|byte| byte.is_ascii_digit()));

    has_valid_suffix.then(|| stem[FILE_NAME_TIMESTAMP_LENGTH + 1..close_parenthesis].to_owned())
}

fn has_timestamp_prefix(stem: &str) -> bool {
    let Some(prefix) = stem.as_bytes().get(..FILE_NAME_TIMESTAMP_LENGTH) else {
        return false;
    };

    prefix.iter().enumerate().all(|(index, byte)| {
        if index == 8 {
            *byte == b'_'
        } else {
            byte.is_ascii_digit()
        }
    })
}

fn paths_equal(first: &Path, second: &Path) -> bool {
    let first = absolute_path(first);
    let second = absolute_path(second);

    if cfg!(target_os = "windows") {
        first
            .to_string_lossy()
            .eq_ignore_ascii_case(&second.to_string_lossy())
    } else {
        first == second
    }
}

fn absolute_path(path: &Path) -> PathBuf {
    if path.is_absolute() {
        path.to_owned()
    } else {
        std::env::current_dir()
            .map(|directory| directory.join(path))
            .unwrap_or_else(|_| path.to_owned())
    }
}

fn success(
    source_path: &Path,
    target_path: PathBuf,
    status: RenameStatus,
    timestamp_source: TimestampSource,
) -> PhotoRenameResult {
    PhotoRenameResult {
        source_path: source_path.to_owned(),
        target_path: Some(target_path),
        status,
        timestamp_source: Some(timestamp_source),
        error_message: None,
    }
}

fn failure(source_path: &Path, error_message: String) -> PhotoRenameResult {
    PhotoRenameResult {
        source_path: source_path.to_owned(),
        target_path: None,
        status: RenameStatus::Failed,
        timestamp_source: None,
        error_message: Some(error_message),
    }
}
