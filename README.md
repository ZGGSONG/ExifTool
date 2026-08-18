# ExifTool

一个使用 Rust 和 GPUI 构建的跨平台媒体重命名工具，支持在 Windows 和 macOS 上通过拖拽图片或 MOV 文件进行原地重命名。

## 功能

- 支持拖拽单个或多个图片 / MOV 文件。
- 图片优先读取 EXIF 拍摄时间，MOV 优先读取 QuickTime 创建时间。
- 读取元数据时间失败时，回退使用文件创建时间。
- 在源文件所在目录中直接重命名，不复制文件，也不移动到固定输出目录。
- 文件名格式为 `yyyyMMdd_HHmmss(原始文件名)`，原始文件名不含扩展名并转为小写，例如 `IMG_0001.JPG` 会重命名为 `20240521_153012(img_0001).jpg`。
- 如果目标文件名已存在，会自动添加 `_001`、`_002` 等后缀。
- 单个文件处理失败不会中断其他文件。
- 界面自动跟随系统浅色或深色外观。

## 支持格式

工具接受以下扩展名：

- JPEG / JPG、PNG、TIFF / TIF、WebP、BMP、GIF
- HEIC / HEIF / AVIF
- MOV
- PSD、ICO、PCX、TGA、EPS
- 常见相机 RAW 格式，如 DNG、ARW、CR2、CR3、NEF、ORF、RAF、RW2 等

`nom-exif` 会从其支持的图片和视频容器中读取元数据。没有可解析元数据的文件会使用文件系统创建时间。

## 使用方式

1. 启动应用。
2. 将一个或多个图片 / MOV 文件拖入窗口。
3. 应用会在原目录中直接重命名文件。
4. 处理结果会显示原文件名、新文件名、状态和时间来源。

> [!WARNING]
> 这是原地重命名工具，不会生成备份文件。处理重要媒体文件前，请先确认已有备份。

## 开发

需要安装最新稳定版 Rust。Windows 还需要 Visual Studio Build Tools 的“使用 C++ 的桌面开发”组件；macOS 需要 Xcode Command Line Tools。

运行应用：

```powershell
cargo run
```

运行测试：

```powershell
cargo test --all-targets
```

执行完整检查：

```powershell
cargo fmt --check
cargo clippy --all-targets -- -D warnings
cargo build --release
```

首次构建 GPUI 图形依赖会花费较长时间，之后会使用 Cargo 构建缓存。

## 技术栈

- [GPUI](https://www.gpui.rs/)：GPU 加速桌面界面与系统文件拖放
- [nom-exif](https://crates.io/crates/nom-exif)：图片 EXIF 和视频轨道元数据解析
- [chrono](https://crates.io/crates/chrono)：时间和时区处理
- Rust 内置测试框架：核心行为与回归测试
