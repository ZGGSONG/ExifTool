namespace ExifTool.Models;

/// <summary>
/// Identifies the source used to compute a media file's new name.
/// </summary>
public enum PhotoTimestampSource
{
    /// <summary>
    /// The timestamp came from file metadata.
    /// </summary>
    Exif,

    /// <summary>
    /// The timestamp came from the file system creation time.
    /// </summary>
    CreationTime
}
