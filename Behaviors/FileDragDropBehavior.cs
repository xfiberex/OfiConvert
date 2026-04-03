using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using OfiConvert.ViewModels;

namespace OfiConvert.Behaviors;

/// <summary>
/// Provides file drag-and-drop support for WinUI 3.
/// Attach via code-behind: FileDragDropBehavior.Attach(element, viewModel, highlightBorder);
/// </summary>
public static class FileDragDropBehavior
{
    public static void Attach(UIElement element, MainViewModel viewModel, Border? highlightBorder)
    {
        element.AllowDrop = true;

        element.DragEnter += (s, e) =>
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                SetHighlightState(highlightBorder, true);
            }
            else
            {
                e.AcceptedOperation = DataPackageOperation.None;
            }
        };

        element.DragLeave += (s, e) =>
        {
            SetHighlightState(highlightBorder, false);
        };

        element.DragOver += (s, e) =>
        {
            e.AcceptedOperation = e.DataView.Contains(StandardDataFormats.StorageItems)
                ? DataPackageOperation.Copy
                : DataPackageOperation.None;
        };

        element.Drop += async (s, e) =>
        {
            SetHighlightState(highlightBorder, false);

            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var files = items
                    .OfType<Windows.Storage.StorageFile>()
                    .Select(f => f.Path)
                    .ToArray();

                if (files.Length > 0)
                {
                    viewModel.AddFiles(files);
                }
            }
        };
    }

    private static void SetHighlightState(Border? border, bool isHighlighted)
    {
        if (border is null) return;

        if (isHighlighted)
        {
            border.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 120, 212));
            border.BorderThickness = new Thickness(3);
        }
        else
        {
            border.BorderBrush = Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(
                "CardStrokeColorDefaultBrush", out var brush) && brush is Brush b
                ? b
                : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 224, 224, 224));
            border.BorderThickness = new Thickness(2);
        }
    }
}
