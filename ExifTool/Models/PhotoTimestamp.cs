namespace ExifTool.Models;

/// <summary>
/// Contains the timestamp used to name a photo and where that value came from.
/// </summary>
/// <param name="Value">The timestamp used for the output file name.</param>
/// <param name="Source">The metadata or file-system source that provided the timestamp.</param>
public sealed record PhotoTimestamp(DateTime Value, PhotoTimestampSource Source);
