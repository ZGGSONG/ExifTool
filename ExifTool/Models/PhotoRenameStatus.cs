namespace ExifTool.Models;

/// <summary>
/// Represents the outcome of a media rename request.
/// </summary>
public enum PhotoRenameStatus
{
    /// <summary>
    /// The source file was renamed to the computed target path.
    /// </summary>
    Renamed,

    /// <summary>
    /// The source file already had the computed target path.
    /// </summary>
    AlreadyNamed,

    /// <summary>
    /// The source file could not be renamed.
    /// </summary>
    Failed
}
