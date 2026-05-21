using ExifTool.Models;

namespace ExifTool.Services;

/// <summary>
/// Reads the timestamp that should be used to rename a photo file.
/// </summary>
public interface IPhotoTimestampReader
{
    /// <summary>
    /// Reads the preferred timestamp for a photo file.
    /// </summary>
    /// <param name="filePath">The local photo path.</param>
    /// <returns>The timestamp and its source.</returns>
    PhotoTimestamp ReadTimestamp(string filePath);
}
