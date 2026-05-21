using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ExifTool.Models;
using ExifTool.Services;

namespace ExifTool.ViewModels;

/// <summary>
/// Coordinates drag-and-drop media renaming and exposes results for the main window.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
/// </remarks>
/// <param name="renameService">The service that performs file renaming.</param>
public partial class MainWindowViewModel(PhotoRenameService renameService) : ViewModelBase
{
    [ObservableProperty] public partial bool IsProcessing { get; set; }

    [ObservableProperty] public partial string StatusMessage { get; set; } = "拖拽图片或 MOV 到窗口中";

    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindowViewModel"/> class.
    /// </summary>
    public MainWindowViewModel()
        : this(new PhotoRenameService())
    {
    }

    /// <summary>
    /// Gets the current batch results.
    /// </summary>
    public ObservableCollection<PhotoRenameResult> Results { get; } = [];

    /// <summary>
    /// Renames the dropped local files and updates the result list.
    /// </summary>
    /// <param name="filePaths">The dropped local file paths.</param>
    /// <returns>A task that completes when the batch finishes.</returns>
    public async Task RenameFilesAsync(IEnumerable<string> filePaths)
    {
        if (IsProcessing)
        {
            return;
        }

        var paths = filePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();

        if (paths.Length == 0)
        {
            StatusMessage = "没有找到本地文件";
            return;
        }

        IsProcessing = true;
        Results.Clear();
        StatusMessage = $"正在处理 {paths.Length} 个文件...";

        try
        {
            var results = await Task.Run(() => renameService.RenameFiles(paths));

            foreach (var result in results)
            {
                Results.Add(result);
            }

            var succeeded = results.Count(result => result.Status is PhotoRenameStatus.Renamed or PhotoRenameStatus.AlreadyNamed);
            var failed = results.Count - succeeded;

            StatusMessage = failed == 0
                ? $"完成：{succeeded} 个文件已处理"
                : $"完成：{succeeded} 个成功，{failed} 个失败";
        }
        finally
        {
            IsProcessing = false;
        }
    }
}
