using System.Globalization;
using ExifTool.Models;

namespace ExifTool.Services;

/// <summary>
/// Renames media files in place using metadata capture time or file creation time.
/// </summary>
public sealed class PhotoRenameService
{
    /// <summary>
    /// The file-name timestamp pattern used by the app.
    /// </summary>
    public const string FileNameTimestampFormat = "yyyyMMdd_HHmmss";

    private static readonly HashSet<string> SupportedMediaExtensions = new(StringComparer.OrdinalIgnoreCase)
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
        ".mov",
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

            if (!IsSupportedMediaFile(sourcePath))
            {
                return Failure(sourcePath, "不支持的媒体格式。");
            }

            var timestamp = _timestampReader.ReadTimestamp(sourcePath);
            var targetPath = ResolveTargetPath(sourcePath, timestamp.Value);

            if (PathsEqual(sourcePath, targetPath))
            {
                return new PhotoRenameResult(
                    sourcePath,
                    targetPath,
                    PhotoRenameStatus.AlreadyNamed,
                    timestamp.Source,
                    null);
            }

            File.Move(sourcePath, targetPath);

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
        var fileName = BuildTargetBaseName(sourcePath, timestamp) + extension;

        return Path.Combine(sourceDirectory, fileName);
    }

    /// <summary>
    /// Resolves an in-place target path, adding a numeric suffix when the base name already exists.
    /// </summary>
    /// <param name="sourcePath">The local source path.</param>
    /// <param name="timestamp">The timestamp to format as the file name.</param>
    /// <returns>The first available target path, or the source path when it is already correctly named.</returns>
    public static string ResolveTargetPath(string sourcePath, DateTime timestamp)
    {
        var sourceDirectory = Path.GetDirectoryName(sourcePath)
            ?? throw new ArgumentException("源文件必须位于文件夹中。", nameof(sourcePath));
        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        var baseName = BuildTargetBaseName(sourcePath, timestamp);

        for (var suffix = 0; suffix <= 999; suffix++)
        {
            var targetFileName = suffix == 0
                ? baseName + extension
                : string.Create(CultureInfo.InvariantCulture, $"{baseName}_{suffix:000}{extension}");
            var targetPath = Path.Combine(sourceDirectory, targetFileName);

            if (PathsEqual(sourcePath, targetPath) || !File.Exists(targetPath))
            {
                return targetPath;
            }
        }

        throw new IOException("同一拍摄时间的文件过多，无法生成可用文件名。");
    }

    /// <summary>
    /// Determines whether a file extension is one of the supported media formats.
    /// </summary>
    /// <param name="filePath">The local path to test.</param>
    /// <returns>True when the extension is supported; otherwise false.</returns>
    public static bool IsSupportedMediaFile(string filePath)
    {
        return SupportedMediaExtensions.Contains(Path.GetExtension(filePath));
    }

    /// <summary>
    /// Determines whether a file extension is supported by the rename workflow.
    /// </summary>
    /// <param name="filePath">The local path to test.</param>
    /// <returns>True when the extension is supported; otherwise false.</returns>
    public static bool IsSupportedImageFile(string filePath)
    {
        return IsSupportedMediaFile(filePath);
    }

    private static string BuildTargetBaseName(string sourcePath, DateTime timestamp)
    {
        var timestampName = timestamp.ToString(FileNameTimestampFormat, CultureInfo.InvariantCulture);
        var originalStem = GetOriginalFileNameStem(sourcePath);

        return string.Concat(timestampName, "(", originalStem, ")");
    }

    private static string GetOriginalFileNameStem(string sourcePath)
    {
        var stem = Path.GetFileNameWithoutExtension(sourcePath).ToLowerInvariant();

        return TryReadOriginalStem(stem, out var originalStem)
            ? originalStem
            : stem;
    }

    private static bool TryReadOriginalStem(string stem, out string originalStem)
    {
        originalStem = string.Empty;

        if (!HasTimestampPrefix(stem) || stem.Length <= FileNameTimestampFormat.Length || stem[FileNameTimestampFormat.Length] != '(')
        {
            return false;
        }

        var closeParenIndex = stem.LastIndexOf(')');
        if (closeParenIndex < FileNameTimestampFormat.Length + 1 || !HasGeneratedSuffix(stem, closeParenIndex + 1))
        {
            return false;
        }

        originalStem = stem[(FileNameTimestampFormat.Length + 1)..closeParenIndex];
        return true;
    }

    private static bool HasTimestampPrefix(string stem)
    {
        if (stem.Length < FileNameTimestampFormat.Length)
        {
            return false;
        }

        for (var index = 0; index < FileNameTimestampFormat.Length; index++)
        {
            if (index == 8)
            {
                if (stem[index] != '_')
                {
                    return false;
                }

                continue;
            }

            if (!char.IsAsciiDigit(stem[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasGeneratedSuffix(string stem, int suffixStartIndex)
    {
        if (suffixStartIndex == stem.Length)
        {
            return true;
        }

        if (stem.Length - suffixStartIndex != 4 || stem[suffixStartIndex] != '_')
        {
            return false;
        }

        return char.IsAsciiDigit(stem[suffixStartIndex + 1])
            && char.IsAsciiDigit(stem[suffixStartIndex + 2])
            && char.IsAsciiDigit(stem[suffixStartIndex + 3]);
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
