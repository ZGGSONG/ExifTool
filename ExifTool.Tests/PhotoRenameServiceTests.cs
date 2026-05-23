using ExifTool.Models;
using ExifTool.Services;

namespace ExifTool.Tests;

public sealed class PhotoRenameServiceTests
{
    private static readonly DateTime TestTimestamp = new(2024, 1, 2, 3, 4, 5, 6);

    [Fact]
    public void RenameFile_UsesTimestampFormatAndSourceDirectory()
    {
        using var temp = new TempDirectory();
        var sourcePath = temp.WriteFile("IMG_0001.JPG", "new content");
        var service = CreateService(TestTimestamp);

        var result = service.RenameFile(sourcePath);

        var expectedPath = Path.Combine(temp.Path, "20240102_030405(img_0001).jpg");
        Assert.Equal(PhotoRenameStatus.Renamed, result.Status);
        Assert.Equal(expectedPath, result.TargetPath);
        Assert.False(File.Exists(sourcePath));
        Assert.Equal("new content", File.ReadAllText(expectedPath));
    }

    [Fact]
    public void RenameFile_AddsSuffixWhenTargetExists()
    {
        using var temp = new TempDirectory();
        var sourcePath = temp.WriteFile("source.jpg", "new content");
        var targetPath = temp.WriteFile("20240102_030405(source).jpg", "old content");
        var service = CreateService(TestTimestamp);

        var result = service.RenameFile(sourcePath);

        var expectedPath = Path.Combine(temp.Path, "20240102_030405(source)_001.jpg");
        Assert.Equal(PhotoRenameStatus.Renamed, result.Status);
        Assert.Equal(expectedPath, result.TargetPath);
        Assert.False(File.Exists(sourcePath));
        Assert.True(File.Exists(targetPath));
        Assert.Equal("old content", File.ReadAllText(targetPath));
        Assert.Equal("new content", File.ReadAllText(expectedPath));
    }

    [Fact]
    public void RenameFile_IncrementsSuffixUntilTargetIsAvailable()
    {
        using var temp = new TempDirectory();
        var sourcePath = temp.WriteFile("source.jpg", "new content");
        temp.WriteFile("20240102_030405(source).jpg", "existing");
        temp.WriteFile("20240102_030405(source)_001.jpg", "existing");
        var service = CreateService(TestTimestamp);

        var result = service.RenameFile(sourcePath);

        var expectedPath = Path.Combine(temp.Path, "20240102_030405(source)_002.jpg");
        Assert.Equal(PhotoRenameStatus.Renamed, result.Status);
        Assert.Equal(expectedPath, result.TargetPath);
        Assert.False(File.Exists(sourcePath));
        Assert.Equal("new content", File.ReadAllText(expectedPath));
    }

    [Fact]
    public void RenameFile_SupportsMovFiles()
    {
        using var temp = new TempDirectory();
        var sourcePath = temp.WriteFile("IMG_0001.MOV", "new content");
        var service = CreateService(TestTimestamp);

        var result = service.RenameFile(sourcePath);

        var expectedPath = Path.Combine(temp.Path, "20240102_030405(img_0001).mov");
        Assert.Equal(PhotoRenameStatus.Renamed, result.Status);
        Assert.Equal(expectedPath, result.TargetPath);
        Assert.False(File.Exists(sourcePath));
        Assert.Equal("new content", File.ReadAllText(expectedPath));
    }

    [Fact]
    public void RenameFile_AddsSuffixWhenMovTargetExists()
    {
        using var temp = new TempDirectory();
        var sourcePath = temp.WriteFile("IMG_0001.MOV", "new content");
        var targetPath = temp.WriteFile("20240102_030405(img_0001).mov", "old content");
        var service = CreateService(TestTimestamp);

        var result = service.RenameFile(sourcePath);

        var expectedPath = Path.Combine(temp.Path, "20240102_030405(img_0001)_001.mov");
        Assert.Equal(PhotoRenameStatus.Renamed, result.Status);
        Assert.Equal(expectedPath, result.TargetPath);
        Assert.False(File.Exists(sourcePath));
        Assert.True(File.Exists(targetPath));
        Assert.Equal("old content", File.ReadAllText(targetPath));
        Assert.Equal("new content", File.ReadAllText(expectedPath));
    }

    [Fact]
    public void RenameFile_ReturnsAlreadyNamedWhenTargetMatchesSource()
    {
        using var temp = new TempDirectory();
        var sourcePath = temp.WriteFile("20240102_030405(img_0001).jpg", "already named");
        var service = CreateService(TestTimestamp);

        var result = service.RenameFile(sourcePath);

        Assert.Equal(PhotoRenameStatus.AlreadyNamed, result.Status);
        Assert.Equal(sourcePath, result.TargetPath);
        Assert.Equal("already named", File.ReadAllText(sourcePath));
    }

    [Fact]
    public void RenameFile_ReturnsAlreadyNamedWhenSuffixedTargetMatchesSource()
    {
        using var temp = new TempDirectory();
        temp.WriteFile("20240102_030405(img_0001).jpg", "base target");
        var sourcePath = temp.WriteFile("20240102_030405(img_0001)_001.jpg", "already named");
        var service = CreateService(TestTimestamp);

        var result = service.RenameFile(sourcePath);

        Assert.Equal(PhotoRenameStatus.AlreadyNamed, result.Status);
        Assert.Equal(sourcePath, result.TargetPath);
        Assert.Equal("already named", File.ReadAllText(sourcePath));
    }

    [Fact]
    public void RenameFiles_ContinuesAfterOneFileFails()
    {
        using var temp = new TempDirectory();
        var sourcePath = temp.WriteFile("source.jpg", "new content");
        var missingPath = Path.Combine(temp.Path, "missing.jpg");
        var service = CreateService(TestTimestamp);

        var results = service.RenameFiles([missingPath, sourcePath]);

        Assert.Equal(2, results.Count);
        Assert.Equal(PhotoRenameStatus.Failed, results[0].Status);
        Assert.Equal(PhotoRenameStatus.Renamed, results[1].Status);
        Assert.True(File.Exists(Path.Combine(temp.Path, "20240102_030405(source).jpg")));
    }

    [Fact]
    public void BuildTargetPath_UsesConfiguredTimestampFormat()
    {
        var sourcePath = Path.Combine("photos", "image.png");

        var result = PhotoRenameService.BuildTargetPath(sourcePath, TestTimestamp);

        Assert.Equal(Path.Combine("photos", "20240102_030405(image).png"), result);
    }

    [Fact]
    public void BuildTargetPath_ReusesOriginalStemFromGeneratedName()
    {
        var sourcePath = Path.Combine("photos", "20231201_120000(IMG_0001)_001.JPG");

        var result = PhotoRenameService.BuildTargetPath(sourcePath, TestTimestamp);

        Assert.Equal(Path.Combine("photos", "20240102_030405(img_0001).jpg"), result);
    }

    [Fact]
    public void ResolveTargetPath_AddsSuffixWhenBaseNameExists()
    {
        using var temp = new TempDirectory();
        var sourcePath = temp.WriteFile("image.png", "new content");
        temp.WriteFile("20240102_030405(image).png", "existing");

        var result = PhotoRenameService.ResolveTargetPath(sourcePath, TestTimestamp);

        Assert.Equal(Path.Combine(temp.Path, "20240102_030405(image)_001.png"), result);
    }

    private static PhotoRenameService CreateService(DateTime timestamp)
    {
        return new PhotoRenameService(new StubTimestampReader(timestamp));
    }

    private sealed class StubTimestampReader(DateTime timestamp) : IPhotoTimestampReader
    {
        public PhotoTimestamp ReadTimestamp(string filePath)
        {
            return new PhotoTimestamp(timestamp, PhotoTimestampSource.Exif);
        }
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

        public string WriteFile(string fileName, string content)
        {
            var filePath = System.IO.Path.Combine(Path, fileName);
            File.WriteAllText(filePath, content);
            return filePath;
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
