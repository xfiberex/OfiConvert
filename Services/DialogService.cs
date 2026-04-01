using Microsoft.Win32;
using System.Windows;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace OfiConvert.Services;

public class DialogService : IDialogService
{
    public Task<string[]?> ShowOpenFileDialogAsync(string filter, string title)
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var dialog = new OpenFileDialog
            {
                Filter = filter,
                Multiselect = true,
                Title = title
            };

            return dialog.ShowDialog() == true ? dialog.FileNames : null;
        }).Task;
    }

    public Task<string?> ShowFolderBrowserDialogAsync(string title)
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var dialog = new OpenFolderDialog
            {
                Title = title,
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                return dialog.FolderName;
            }

            return null as string;
        }).Task;
    }

    public Task<string?> ShowSaveFileDialogAsync(string filter, string title, string defaultFileName = "")
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var dialog = new SaveFileDialog
            {
                Filter = filter,
                Title = title,
                FileName = defaultFileName
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null as string;
        }).Task;
    }

    public void ShowInformation(string message, string title = "Información")
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            MessageBox.Show(message, title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        });
    }

    public void ShowError(string message, string title = "Error")
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            MessageBox.Show(message, title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        });
    }

    public Task<bool> ShowConfirmationAsync(string message, string title = "Confirmación")
    {
        return Application.Current.Dispatcher.InvokeAsync(() =>
        {
            var result = MessageBox.Show(message, title,
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            return result == MessageBoxResult.Yes;
        }).Task;
    }
}