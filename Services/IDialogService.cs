using OfiConvert.Models;

namespace OfiConvert.Services;

public interface IDialogService
{
    Task<string[]?> ShowOpenFileDialogAsync(string filter, string title);
    Task<string?> ShowFolderBrowserDialogAsync(string title);
    Task<string?> ShowSaveFileDialogAsync(string filter, string title, string defaultFileName = "");
    void ShowInformation(string message, string title = "Informaci\u00f3n");
    void ShowError(string message, string title = "Error");
    Task<bool> ShowConfirmationAsync(string message, string title = "Confirmaci\u00f3n");
}