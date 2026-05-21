using System.IO;

namespace ExifTool.Models;

/// <summary>
/// Describes the result of renaming one media file.
/// </summary>
/// <param name="SourcePath">The original path that was requested for renaming.</param>
/// <param name="TargetPath">The resulting path, when a target path could be computed.</param>
/// <param name="Status">The final rename status.</param>
/// <param name="TimestampSource">The source used to decide the new file name.</param>
/// <param name="ErrorMessage">A failure message when the file could not be renamed.</param>
public sealed record PhotoRenameResult(
    string SourcePath,
    string? TargetPath,
    PhotoRenameStatus Status,
    PhotoTimestampSource? TimestampSource,
    string? ErrorMessage)
{
    /// <summary>
    /// Gets the original file name for UI display.
    /// </summary>
    public string SourceFileName => Path.GetFileName(SourcePath);

    /// <summary>
    /// Gets the renamed file name for UI display.
    /// </summary>
    public string TargetFileName => TargetPath is null ? "-" : Path.GetFileName(TargetPath);

    /// <summary>
    /// Gets a short localized status label.
    /// </summary>
    public string StatusText => Status switch
    {
        PhotoRenameStatus.Renamed => "已重命名",
        PhotoRenameStatus.AlreadyNamed => "无需处理",
        PhotoRenameStatus.Failed => "失败",
        _ => Status.ToString()
    };

    /// <summary>
    /// Gets a short localized timestamp-source label.
    /// </summary>
    public string TimestampSourceText => TimestampSource switch
    {
        PhotoTimestampSource.Exif => "元数据时间",
        PhotoTimestampSource.CreationTime => "文件创建时间",
        null => "-",
        _ => TimestampSource.ToString() ?? "-"
    };

    /// <summary>
    /// Gets detailed result text for UI display.
    /// </summary>
    public string DetailText => ErrorMessage ?? TargetPath ?? string.Empty;
}
