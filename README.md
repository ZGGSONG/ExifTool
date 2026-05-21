# ExifTool

一个基于 Avalonia 的跨平台图片重命名工具，支持在 Windows 和 macOS 上通过拖拽图片文件进行原地重命名。

## 功能

- 支持拖拽单个或多个图片文件。
- 优先读取 EXIF 拍摄时间作为文件名。
- 读取 EXIF 失败时，回退使用文件创建时间。
- 在源文件所在目录中直接重命名，不复制文件，也不移动到固定输出目录。
- 文件名格式为 `yyyyMMdd_HHmmss`，例如 `20240521_153012.jpg`。
- 如果目标文件名已存在，会自动添加 `_001`、`_002` 等后缀，例如 `20240521_153012_001.jpg`。
- 单个文件处理失败不会中断其他文件。

## 支持格式

工具会优先支持常见图片格式，包括：

- JPEG / JPG
- PNG
- TIFF / TIF
- WebP
- BMP
- GIF
- HEIC / HEIF / AVIF
- PSD、ICO、PCX、TGA
- 常见相机 RAW 格式，如 DNG、ARW、CR2、CR3、NEF、ORF、RAF、RW2 等

部分格式可能没有可读取的 EXIF 拍摄时间，此时会自动使用文件创建时间命名。

## 使用方式

1. 启动应用。
2. 将一个或多个图片文件拖入窗口。
3. 应用会在图片原目录中直接重命名文件。
4. 处理结果会显示原文件名、新文件名、状态和时间来源。

注意：这是原地重命名工具，不会生成备份文件。处理重要图片前，建议先确认已有备份。

## 开发

需要安装 .NET 10 SDK。

还原依赖：

```powershell
dotnet restore ExifTool.slnx
```

构建项目：

```powershell
dotnet build ExifTool.slnx
```

运行测试：

```powershell
dotnet test ExifTool.slnx
```

启动应用：

```powershell
dotnet run --project ExifTool/ExifTool.csproj
```

## 技术栈

- Avalonia：跨平台桌面界面
- CommunityToolkit.Mvvm：视图模型与属性通知
- MetadataExtractor：读取图片元数据和 EXIF 信息
- xUnit：单元测试
