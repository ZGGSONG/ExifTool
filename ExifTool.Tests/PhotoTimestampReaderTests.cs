using ExifTool.Models;
using ExifTool.Services;

namespace ExifTool.Tests;

public sealed class PhotoTimestampReaderTests
{
    [Fact]
    public void ReadTimestamp_UsesExifTimestampWhenAvailable()
    {
        var exifTimestamp = new DateTime(2024, 5, 6, 7, 8, 9, 123);
        var creationTime = new DateTime(2023, 1, 2, 3, 4, 5, 6);
        var reader = new PhotoTimestampReader(_ => exifTimestamp, _ => creationTime);

        var result = reader.ReadTimestamp("photo.jpg");

        Assert.Equal(exifTimestamp, result.Value);
        Assert.Equal(PhotoTimestampSource.Exif, result.Source);
    }

    [Fact]
    public void ReadTimestamp_FallsBackToCreationTimeWhenExifTimestampIsMissing()
    {
        var creationTime = new DateTime(2023, 1, 2, 3, 4, 5, 6);
        var reader = new PhotoTimestampReader(_ => null, _ => creationTime);

        var result = reader.ReadTimestamp("photo.jpg");

        Assert.Equal(creationTime, result.Value);
        Assert.Equal(PhotoTimestampSource.CreationTime, result.Source);
    }
}
