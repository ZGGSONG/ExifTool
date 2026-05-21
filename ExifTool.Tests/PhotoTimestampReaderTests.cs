using ExifTool.Models;
using ExifTool.Services;

namespace ExifTool.Tests;

public sealed class PhotoTimestampReaderTests
{
    [Fact]
    public void ReadTimestamp_UsesMetadataTimestampWhenAvailable()
    {
        var metadataTimestamp = new DateTime(2024, 5, 6, 7, 8, 9, 123);
        var creationTime = new DateTime(2023, 1, 2, 3, 4, 5, 6);
        var reader = new PhotoTimestampReader(_ => metadataTimestamp, _ => creationTime);

        var result = reader.ReadTimestamp("clip.mov");

        Assert.Equal(metadataTimestamp, result.Value);
        Assert.Equal(PhotoTimestampSource.Exif, result.Source);
    }

    [Fact]
    public void ReadTimestamp_UsesQuickTimeCreatedTimestampForMovWhenAvailable()
    {
        using var temp = new TempDirectory();
        var quickTimeTimestamp = new DateTime(2024, 1, 2, 3, 4, 5);
        var filePath = temp.WriteQuickTimeMovie("clip.mov", quickTimeTimestamp);
        var reader = new PhotoTimestampReader();

        var result = reader.ReadTimestamp(filePath);

        Assert.Equal(quickTimeTimestamp, result.Value);
        Assert.Equal(PhotoTimestampSource.Exif, result.Source);
    }

    [Fact]
    public void ReadTimestamp_FallsBackToCreationTimeWhenMetadataTimestampIsMissing()
    {
        var creationTime = new DateTime(2023, 1, 2, 3, 4, 5, 6);
        var reader = new PhotoTimestampReader(_ => null, _ => creationTime);

        var result = reader.ReadTimestamp("photo.jpg");

        Assert.Equal(creationTime, result.Value);
        Assert.Equal(PhotoTimestampSource.CreationTime, result.Source);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "ExifTool.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public string WriteQuickTimeMovie(string fileName, DateTime created)
        {
            var filePath = System.IO.Path.Combine(Path, fileName);

            using var writer = new BinaryWriter(File.Create(filePath));
            var quickTimeSeconds = GetQuickTimeSeconds(created);

            // A minimal ftyp+moov/mvhd file keeps the test independent of checked-in binary assets.
            WriteUInt32BigEndian(writer, 20);
            WriteAscii(writer, "ftyp");
            WriteAscii(writer, "qt  ");
            WriteUInt32BigEndian(writer, 0);
            WriteAscii(writer, "qt  ");

            WriteUInt32BigEndian(writer, 116);
            WriteAscii(writer, "moov");

            WriteUInt32BigEndian(writer, 108);
            WriteAscii(writer, "mvhd");
            writer.Write((byte)0);
            writer.Write(new byte[] { 0, 0, 0 });
            WriteUInt32BigEndian(writer, quickTimeSeconds);
            WriteUInt32BigEndian(writer, quickTimeSeconds);
            WriteUInt32BigEndian(writer, 600);
            WriteUInt32BigEndian(writer, 0);
            WriteUInt32BigEndian(writer, 0x00010000);
            WriteInt16BigEndian(writer, 0x0100);
            writer.Write(new byte[10]);

            foreach (var value in new uint[] { 0x00010000, 0, 0, 0, 0x00010000, 0, 0, 0, 0x40000000 })
            {
                WriteUInt32BigEndian(writer, value);
            }

            foreach (var value in new uint[] { 0, 0, 0, 0, 0, 0 })
            {
                WriteUInt32BigEndian(writer, value);
            }

            WriteUInt32BigEndian(writer, 1);

            return filePath;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }

        private static uint GetQuickTimeSeconds(DateTime created)
        {
            var quickTimeEpoch = new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var createdUtc = DateTime.SpecifyKind(created, DateTimeKind.Utc);

            return checked((uint)(createdUtc - quickTimeEpoch).TotalSeconds);
        }

        private static void WriteAscii(BinaryWriter writer, string text)
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes(text));
        }

        private static void WriteInt16BigEndian(BinaryWriter writer, short value)
        {
            var bytes = BitConverter.GetBytes(value);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            writer.Write(bytes);
        }

        private static void WriteUInt32BigEndian(BinaryWriter writer, uint value)
        {
            var bytes = BitConverter.GetBytes(value);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            writer.Write(bytes);
        }
    }
}
