use std::{fs, path::Path};

use chrono::NaiveDateTime;
use exif_tool::{
    PhotoRenameService, PhotoTimestamp, RenameStatus, TimestampReader, TimestampSource,
};
use tempfile::TempDir;

fn test_timestamp() -> NaiveDateTime {
    NaiveDateTime::parse_from_str("2024-01-02 03:04:05", "%F %T").unwrap()
}

#[derive(Clone, Copy)]
struct StubTimestampReader {
    timestamp: NaiveDateTime,
}

impl TimestampReader for StubTimestampReader {
    fn read_timestamp(&self, _path: &Path) -> Result<PhotoTimestamp, String> {
        Ok(PhotoTimestamp {
            value: self.timestamp,
            source: TimestampSource::Metadata,
        })
    }
}

struct FailingTimestampReader;

impl TimestampReader for FailingTimestampReader {
    fn read_timestamp(&self, _path: &Path) -> Result<PhotoTimestamp, String> {
        Err("无法读取文件创建时间：测试错误".to_owned())
    }
}

fn service() -> PhotoRenameService {
    PhotoRenameService::new(StubTimestampReader {
        timestamp: test_timestamp(),
    })
}

fn write_file(directory: &TempDir, name: &str, content: &str) -> std::path::PathBuf {
    let path = directory.path().join(name);
    fs::write(&path, content).unwrap();
    path
}

#[test]
fn rename_file_uses_timestamp_format_and_source_directory() {
    let directory = TempDir::new().unwrap();
    let source = write_file(&directory, "IMG_0001.JPG", "new content");

    let result = service().rename_file(&source);

    let expected = directory.path().join("20240102_030405(img_0001).jpg");
    assert_eq!(result.status, RenameStatus::Renamed);
    assert_eq!(result.target_path.as_deref(), Some(expected.as_path()));
    assert!(!source.exists());
    assert_eq!(fs::read_to_string(expected).unwrap(), "new content");
}

#[test]
fn rename_file_adds_suffix_when_target_exists() {
    let directory = TempDir::new().unwrap();
    let source = write_file(&directory, "source.jpg", "new content");
    let occupied = write_file(&directory, "20240102_030405(source).jpg", "old content");

    let result = service().rename_file(&source);

    let expected = directory.path().join("20240102_030405(source)_001.jpg");
    assert_eq!(result.status, RenameStatus::Renamed);
    assert_eq!(result.target_path.as_deref(), Some(expected.as_path()));
    assert_eq!(fs::read_to_string(occupied).unwrap(), "old content");
    assert_eq!(fs::read_to_string(expected).unwrap(), "new content");
}

#[test]
fn rename_file_increments_suffix_until_available() {
    let directory = TempDir::new().unwrap();
    let source = write_file(&directory, "source.jpg", "new content");
    write_file(&directory, "20240102_030405(source).jpg", "existing");
    write_file(&directory, "20240102_030405(source)_001.jpg", "existing");

    let result = service().rename_file(&source);

    let expected = directory.path().join("20240102_030405(source)_002.jpg");
    assert_eq!(result.target_path.as_deref(), Some(expected.as_path()));
    assert_eq!(fs::read_to_string(expected).unwrap(), "new content");
}

#[test]
fn rename_file_supports_mov_files() {
    let directory = TempDir::new().unwrap();
    let source = write_file(&directory, "IMG_0001.MOV", "content");

    let result = service().rename_file(&source);

    let expected = directory.path().join("20240102_030405(img_0001).mov");
    assert_eq!(result.status, RenameStatus::Renamed);
    assert_eq!(result.target_path.as_deref(), Some(expected.as_path()));
}

#[test]
fn rename_file_adds_suffix_when_mov_target_exists() {
    let directory = TempDir::new().unwrap();
    let source = write_file(&directory, "IMG_0001.MOV", "new content");
    write_file(&directory, "20240102_030405(img_0001).mov", "old content");

    let result = service().rename_file(&source);

    assert_eq!(
        result.target_path,
        Some(directory.path().join("20240102_030405(img_0001)_001.mov"))
    );
}

#[test]
fn rename_file_returns_already_named_when_target_matches_source() {
    let directory = TempDir::new().unwrap();
    let source = write_file(&directory, "20240102_030405(img_0001).jpg", "already named");

    let result = service().rename_file(&source);

    assert_eq!(result.status, RenameStatus::AlreadyNamed);
    assert_eq!(result.target_path.as_deref(), Some(source.as_path()));
    assert_eq!(fs::read_to_string(source).unwrap(), "already named");
}

#[test]
fn rename_file_returns_already_named_for_suffixed_source() {
    let directory = TempDir::new().unwrap();
    write_file(&directory, "20240102_030405(img_0001).jpg", "base target");
    let source = write_file(
        &directory,
        "20240102_030405(img_0001)_001.jpg",
        "already named",
    );

    let result = service().rename_file(&source);

    assert_eq!(result.status, RenameStatus::AlreadyNamed);
    assert_eq!(result.target_path.as_deref(), Some(source.as_path()));
}

#[test]
fn rename_files_continues_after_one_failure() {
    let directory = TempDir::new().unwrap();
    let source = write_file(&directory, "source.jpg", "content");
    let missing = directory.path().join("missing.jpg");

    let results = service().rename_files([missing, source]);

    assert_eq!(results.len(), 2);
    assert_eq!(results[0].status, RenameStatus::Failed);
    assert_eq!(results[1].status, RenameStatus::Renamed);
}

#[test]
fn build_target_path_uses_configured_format() {
    let result =
        PhotoRenameService::build_target_path(Path::new("photos/image.png"), test_timestamp())
            .unwrap();

    assert_eq!(result, Path::new("photos/20240102_030405(image).png"));
}

#[test]
fn build_target_path_reuses_original_stem_from_generated_name() {
    let result = PhotoRenameService::build_target_path(
        Path::new("photos/20231201_120000(IMG_0001)_001.JPG"),
        test_timestamp(),
    )
    .unwrap();

    assert_eq!(result, Path::new("photos/20240102_030405(img_0001).jpg"));
}

#[test]
fn resolve_target_path_adds_suffix_when_base_exists() {
    let directory = TempDir::new().unwrap();
    let source = write_file(&directory, "image.png", "new content");
    write_file(&directory, "20240102_030405(image).png", "existing");

    let result = PhotoRenameService::resolve_target_path(&source, test_timestamp()).unwrap();

    assert_eq!(
        result,
        directory.path().join("20240102_030405(image)_001.png")
    );
}

#[test]
fn empty_path_is_reported_as_failure() {
    let result = service().rename_file(Path::new(""));

    assert_eq!(result.status, RenameStatus::Failed);
    assert_eq!(result.error_message.as_deref(), Some("文件路径为空。"));
}

#[test]
fn unsupported_extension_is_reported_as_failure() {
    let directory = TempDir::new().unwrap();
    let source = write_file(&directory, "notes.txt", "content");

    let result = service().rename_file(&source);

    assert_eq!(result.status, RenameStatus::Failed);
    assert_eq!(result.error_message.as_deref(), Some("不支持的媒体格式。"));
}

#[test]
fn directory_path_is_reported_as_failure() {
    let directory = TempDir::new().unwrap();

    let result = service().rename_file(directory.path());

    assert_eq!(result.status, RenameStatus::Failed);
    assert_eq!(
        result.error_message.as_deref(),
        Some("文件不存在或不是本地文件。")
    );
}

#[test]
fn unavailable_creation_time_is_reported_as_failure() {
    let directory = TempDir::new().unwrap();
    let source = write_file(&directory, "image.jpg", "content");
    let service = PhotoRenameService::new(FailingTimestampReader);

    let result = service.rename_file(&source);

    assert_eq!(result.status, RenameStatus::Failed);
    assert_eq!(
        result.error_message.as_deref(),
        Some("无法读取文件创建时间：测试错误")
    );
}

#[test]
fn resolve_target_path_fails_after_suffix_999() {
    let directory = TempDir::new().unwrap();
    let source = write_file(&directory, "source.jpg", "new content");
    write_file(&directory, "20240102_030405(source).jpg", "occupied");
    for suffix in 1..=999 {
        write_file(
            &directory,
            &format!("20240102_030405(source)_{suffix:03}.jpg"),
            "occupied",
        );
    }

    let error = PhotoRenameService::resolve_target_path(&source, test_timestamp()).unwrap_err();

    assert_eq!(error.kind(), std::io::ErrorKind::AlreadyExists);
}
