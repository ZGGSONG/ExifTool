using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using ExifTool.ViewModels;

namespace ExifTool.Views;

/// <summary>
/// Hosts the drag-and-drop photo renaming surface.
/// </summary>
public partial class MainWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(this, true);
        DragDrop.AddDragOverHandler(this, OnDragOver);
        DragDrop.AddDropHandler(this, OnDrop);
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = HasLocalFiles(e) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        e.Handled = true;

        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var filePaths = e.DataTransfer.TryGetFiles()?
            .Select(item => item.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path!)
            .ToArray() ?? [];

        await viewModel.RenameFilesAsync(filePaths);
    }

    private static bool HasLocalFiles(DragEventArgs e)
    {
        return e.DataTransfer.TryGetFiles()?
            .Any(item => !string.IsNullOrWhiteSpace(item.TryGetLocalPath())) == true;
    }
}
