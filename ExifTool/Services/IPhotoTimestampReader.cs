using ExifTool.Models;

namespace ExifTool.Services;

/// <summary>
/// Reads the timestamp that should be used to rename a media file.
/// </summary>
public interface IPhotoTimestampReader
{
    /// <summary>
    /// Reads the preferred timestamp for a media file.
    /// </summary>
    /// <param name="filePath">The local media path.</param>
    /// <returns>The timestamp and its source.</returns>
    PhotoTimestamp ReadTimestamp(string filePath);
}
