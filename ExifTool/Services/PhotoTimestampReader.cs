using ExifTool.Models;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;

namespace ExifTool.Services;

/// <summary>
/// Reads metadata timestamps from media files and falls back to file creation time.
/// </summary>
public sealed class PhotoTimestampReader : IPhotoTimestampReader
{
    private readonly Func<string, DateTime?> _metadataTimestampReader;
    private readonly Func<string, DateTime> _creationTimeReader;

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotoTimestampReader"/> class.
    /// </summary>
    public PhotoTimestampReader()
        : this(ReadMetadataTimestamp, File.GetCreationTime)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PhotoTimestampReader"/> class with custom readers.
    /// </summary>
    /// <param name="metadataTimestampReader">Reads a metadata timestamp or returns null when none is available.</param>
    /// <param name="creationTimeReader">Reads the file-system creation time.</param>
    public PhotoTimestampReader(
        Func<string, DateTime?> metadataTimestampReader,
        Func<string, DateTime> creationTimeReader)
    {
        _metadataTimestampReader = metadataTimestampReader;
        _creationTimeReader = creationTimeReader;
    }

    /// <inheritdoc />
    public PhotoTimestamp ReadTimestamp(string filePath)
    {
        var metadataTimestamp = _metadataTimestampReader(filePath);

        if (metadataTimestamp is DateTime timestamp)
        {
            return new PhotoTimestamp(timestamp, PhotoTimestampSource.Exif);
        }

        return new PhotoTimestamp(_creationTimeReader(filePath), PhotoTimestampSource.CreationTime);
    }

    private static DateTime? ReadMetadataTimestamp(string filePath)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);

            return ReadExifTimestamp(directories) ?? ReadQuickTimeTimestamp(directories);
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
        catch (NotSupportedException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static DateTime? ReadExifTimestamp(IEnumerable<MetadataExtractor.Directory> directories)
    {
        // DateTimeOriginal is the capture time users expect; the other fields are looser
        // fallbacks for images that were exported or edited by software.
        return ReadTag<ExifSubIfdDirectory>(directories, ExifDirectoryBase.TagDateTimeOriginal)
            ?? ReadTag<ExifSubIfdDirectory>(directories, ExifDirectoryBase.TagDateTimeDigitized)
            ?? ReadTag<ExifIfd0Directory>(directories, ExifDirectoryBase.TagDateTime);
    }

    private static DateTime? ReadQuickTimeTimestamp(IEnumerable<MetadataExtractor.Directory> directories)
    {
        var timestamp = ReadTag<QuickTimeMetadataHeaderDirectory>(
                directories,
                QuickTimeMetadataHeaderDirectory.TagCreationDate)
            ?? ReadTag<QuickTimeMovieHeaderDirectory>(
                directories,
                QuickTimeMovieHeaderDirectory.TagCreated)
            ?? ReadTag<QuickTimeTrackHeaderDirectory>(
                directories,
                QuickTimeTrackHeaderDirectory.TagCreated);

        return NormalizeMetadataTimestamp(timestamp);
    }

    private static DateTime? NormalizeMetadataTimestamp(DateTime? timestamp)
    {
        if (timestamp is not DateTime value)
        {
            return null;
        }

        // Offset-bearing QuickTime dates are parsed as UTC; filenames should match local capture time.
        return value.Kind == DateTimeKind.Utc ? value.ToLocalTime() : value;
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
