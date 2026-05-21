using ExifTool.Models;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace ExifTool.Services;

/// <summary>
/// Reads EXIF timestamps from image files and falls back to file creation time.
/// </summary>
public sealed class PhotoTimestampReader : IPhotoTimestampReader
{
    private readonly Func<string, DateTime?> _exifTimestampReader;
    private readonly Func<string, DateTime> _creationTimeReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotoTimestampReader"/> class.
    /// </summary>
    public PhotoTimestampReader()
        : this(ReadExifTimestamp, File.GetCreationTime)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotoTimestampReader"/> class with custom readers.
    /// </summary>
    /// <param name="exifTimestampReader">Reads a metadata timestamp or returns null when none is available.</param>
    /// <param name="creationTimeReader">Reads the file-system creation time.</param>
    public PhotoTimestampReader(
        Func<string, DateTime?> exifTimestampReader,
        Func<string, DateTime> creationTimeReader)
    {
        _exifTimestampReader = exifTimestampReader;
        _creationTimeReader = creationTimeReader;
    }

    /// <inheritdoc />
    public PhotoTimestamp ReadTimestamp(string filePath)
    {
        var exifTimestamp = _exifTimestampReader(filePath);

        if (exifTimestamp is DateTime timestamp)
        {
            return new PhotoTimestamp(timestamp, PhotoTimestampSource.Exif);
        }

        return new PhotoTimestamp(_creationTimeReader(filePath), PhotoTimestampSource.CreationTime);
    }

    private static DateTime? ReadExifTimestamp(string filePath)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);

            // DateTimeOriginal is the capture time users expect; the other fields are looser
            // fallbacks for images that were exported or edited by software.
            return ReadTag<ExifSubIfdDirectory>(directories, ExifDirectoryBase.TagDateTimeOriginal)
                ?? ReadTag<ExifSubIfdDirectory>(directories, ExifDirectoryBase.TagDateTimeDigitized)
                ?? ReadTag<ExifIfd0Directory>(directories, ExifDirectoryBase.TagDateTime);
        }
        catch (ImageProcessingException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static DateTime? ReadTag<TDirectory>(
        IEnumerable<MetadataExtractor.Directory> directories,
        int tagType)
        where TDirectory : MetadataExtractor.Directory
    {
        foreach (var directory in directories.OfType<TDirectory>())
        {
            if (directory.TryGetDateTime(tagType, out var timestamp))
            {
                return timestamp;
            }
        }

        return null;
    }
}
