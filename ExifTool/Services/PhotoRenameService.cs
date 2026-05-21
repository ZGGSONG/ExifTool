using System.Globalization;
using ExifTool.Models;

namespace ExifTool.Services;

/// <summary>
/// Renames photo files in place using EXIF capture time or file creation time.
/// </summary>
public sealed class PhotoRenameService
{
    /// <summary>
    /// The file-name timestamp pattern used by the app.
    /// </summary>
    public const string FileNameTimestampFormat = "yyyyMMdd_HHmmss_fff";

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".3fr",
        ".arw",
        ".avif",
        ".bmp",
        ".cr2",
        ".cr3",
        ".crw",
        ".crx",
        ".dng",
        ".eps",
        ".gif",
        ".gpr",
        ".heic",
        ".heif",
        ".ico",
        ".jfif",
        ".jpe",
        ".jpeg",
        ".jpg",
        ".kdc",
        ".nef",
        ".orf",
        ".pcx",
        ".pef",
        ".png",
        ".psd",
        ".raf",
        ".rw2",
        ".rwl",
        ".srw",
        ".tga",
        ".tif",
        ".tiff",
        ".webp"
    };

    private readonly IPhotoTimestampReader _timestampReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotoRenameService"/> class.
    /// </summary>
    public PhotoRenameService()
        : this(new PhotoTimestampReader())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotoRenameService"/> class.
    /// </summary>
    /// <param name="timestampReader">The timestamp reader used to compute file names.</param>
    public PhotoRenameService(IPhotoTimestampReader timestampReader)
    {
        _timestampReader = timestampReader;
    }

    /// <summary>
    /// Renames several files and keeps processing when an individual file fails.
    /// </summary>
    /// <param name="filePaths">The local file paths to rename.</param>
    /// <returns>The result for each requested file.</returns>
    public IReadOnlyList<PhotoRenameResult> RenameFiles(IEnumerable<string> filePaths)
    {
        var results = new List<PhotoRenameResult>();

        foreach (var filePath in filePaths)
        {
            results.Add(RenameFile(filePath));
        }

        return results;
    }

    /// <summary>
    /// Renames one file in its source directory.
    /// </summary>
    /// <param name="sourcePath">The local path to rename.</param>
    /// <returns>The rename result.</returns>
    public PhotoRenameResult RenameFile(string sourcePath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                return Failure(sourcePath, "文件路径为空。");
            }

            if (!File.Exists(sourcePath))
            {
                return Failure(sourcePath, "文件不存在或不是本地文件。");
            }

            if (!IsSupportedImageFile(sourcePath))
            {
                return Failure(sourcePath, "不支持的图片格式。");
            }

            var timestamp = _timestampReader.ReadTimestamp(sourcePath);
            var targetPath = BuildTargetPath(sourcePath, timestamp.Value);

            if (PathsEqual(sourcePath, targetPath))
            {
                return new PhotoRenameResult(
                    sourcePath,
                    targetPath,
                    PhotoRenameStatus.AlreadyNamed,
                    timestamp.Source,
                    null);
            }

            // Overwriting is intentional here: the product requirement says same-name targets
            // should be replaced instead of skipped or de-duplicated.
            File.Move(sourcePath, targetPath, overwrite: true);

            return new PhotoRenameResult(
                sourcePath,
                targetPath,
                PhotoRenameStatus.Renamed,
                timestamp.Source,
                null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return Failure(sourcePath, ex.Message);
        }
    }

    /// <summary>
    /// Builds the in-place target path for a source file and timestamp.
    /// </summary>
    /// <param name="sourcePath">The local source path.</param>
    /// <param name="timestamp">The timestamp to format as the file name.</param>
    /// <returns>The target path in the same directory as the source.</returns>
    public static string BuildTargetPath(string sourcePath, DateTime timestamp)
    {
        var sourceDirectory = Path.GetDirectoryName(sourcePath)
            ?? throw new ArgumentException("源文件必须位于文件夹中。", nameof(sourcePath));
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        var fileName = timestamp.ToString(FileNameTimestampFormat, CultureInfo.InvariantCulture) + extension;

        return Path.Combine(sourceDirectory, fileName);
    }

    /// <summary>
    /// Determines whether a file extension is one of the supported image formats.
    /// </summary>
    /// <param name="filePath">The local path to test.</param>
    /// <returns>True when the extension is supported; otherwise false.</returns>
    public static bool IsSupportedImageFile(string filePath)
    {
        return SupportedExtensions.Contains(Path.GetExtension(filePath));
    }

    private static PhotoRenameResult Failure(string sourcePath, string errorMessage)
    {
        return new PhotoRenameResult(
            sourcePath,
            null,
            PhotoRenameStatus.Failed,
            null,
            errorMessage);
    }

    private static bool PathsEqual(string firstPath, string secondPath)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(
            Path.GetFullPath(firstPath),
            Path.GetFullPath(secondPath),
            comparison);
    }
}
