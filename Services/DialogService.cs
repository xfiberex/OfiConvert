using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OfiConvert.Helpers;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace OfiConvert.Services;

public class DialogService : IDialogService
{
    private static nint GetWindowHandle()
    {
        var window = App.MainWindow;
        return window is not null ? WindowNative.GetWindowHandle(window) : nint.Zero;
    }

    public async Task<string[]?> ShowOpenFileDialogAsync(string filter, string title)
    {
        var picker = new FileOpenPicker();
        InitializeWithWindow.Initialize(picker, GetWindowHandle());
        picker.ViewMode = PickerViewMode.List;
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

        // Parse WPF-style filter and add extensions
        ParseFilterExtensions(filter, picker);

        var files = await picker.PickMultipleFilesAsync();
        if (files is not null && files.Count > 0)
            return files.Select(f => f.Path).ToArray();

        return null;
    }

    public async Task<string?> ShowFolderBrowserDialogAsync(string title)
    {
        var picker = new FolderPicker();
        InitializeWithWindow.Initialize(picker, GetWindowHandle());
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add("*");

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }

    public async Task<string?> ShowSaveFileDialogAsync(string filter, string title, string defaultFileName = "")
    {
        var picker = new FileSavePicker();
        InitializeWithWindow.Initialize(picker, GetWindowHandle());
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = defaultFileName;

        // Parse WPF-style filter
        ParseSaveFilterExtensions(filter, picker);

        var file = await picker.PickSaveFileAsync();
        return file?.Path;
    }

    // Los títulos y los botones de estos diálogos estaban EN DURO en español ("Sí", "No", "Aceptar",
    // "Error"…): salían así en los ocho idiomas. Es la tercera vez que aparece el mismo fallo en este
    // proyecto —antes, el diálogo de cierre y la barra de actualización—, y por eso ahora lo caza una
    // prueba (HardcodedUiTextTests).
    private static string T(string key) => LocalizationService.Instance[key];

    public async void ShowInformation(string message, string? title = null)
    {
        await ShowDialogAsync(title ?? T("MsgInformation"), message);
    }

    public async void ShowError(string message, string? title = null)
    {
        await ShowDialogAsync(title ?? T("MsgError"), message);
    }

    public async Task<bool> ShowConfirmationAsync(string message, string? title = null)
    {
        var window = App.MainWindow;
        if (window?.Content?.XamlRoot is null) return false;

        var dialog = new ContentDialog
        {
            Title = title ?? T("MsgConfirmation"),
            Content = message,
            PrimaryButtonText = T("BtnYes"),
            CloseButtonText = T("BtnNo"),
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = window.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private static async Task ShowDialogAsync(string title, string message)
    {
        var window = App.MainWindow;
        if (window?.Content?.XamlRoot is null) return;

        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = T("BtnOk"),
            XamlRoot = window.Content.XamlRoot
        };

        await dialog.ShowAsync();
    }

    private static void ParseFilterExtensions(string filter, FileOpenPicker picker)
    {
        // Parse "Desc|*.ext1;*.ext2|Desc2|*.ext3" WPF format
        var parts = filter.Split('|');
        var added = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 1; i < parts.Length; i += 2)
        {
            foreach (var ext in parts[i].Split(';'))
            {
                var trimmed = ext.Trim();
                if (trimmed == "*.*")
                {
                    if (added.Count == 0) picker.FileTypeFilter.Add("*");
                }
                else if (trimmed.StartsWith("*."))
                {
                    var extension = trimmed[1..]; // ".ext"
                    if (added.Add(extension))
                        picker.FileTypeFilter.Add(extension);
                }
            }
        }

        if (picker.FileTypeFilter.Count == 0)
            picker.FileTypeFilter.Add("*");
    }

    private static void ParseSaveFilterExtensions(string filter, FileSavePicker picker)
    {
        var parts = filter.Split('|');
        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            var desc = parts[i].Trim();
            var extensions = parts[i + 1].Split(';')
                .Select(e => e.Trim().TrimStart('*'))
                .Where(e => e.StartsWith('.'))
                .ToList();

            if (extensions.Count > 0)
                picker.FileTypeChoices.Add(desc, extensions);
        }

        if (picker.FileTypeChoices.Count == 0)
            picker.FileTypeChoices.Add("Archivo", [".pdf"]);
    }
}