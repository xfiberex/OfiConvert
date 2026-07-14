using OfiConvert.Models;

namespace OfiConvert.Services;

public interface IDialogService
{
    Task<string[]?> ShowOpenFileDialogAsync(string filter, string title);
    Task<string?> ShowFolderBrowserDialogAsync(string title);
    Task<string?> ShowSaveFileDialogAsync(string filter, string title, string defaultFileName = "");
    // title = null -> se usa el t\u00edtulo traducido por defecto. Antes el valor por defecto era el texto
    // espa\u00f1ol EN DURO, as\u00ed que estos di\u00e1logos sal\u00edan en espa\u00f1ol en los ocho idiomas.
    void ShowInformation(string message, string? title = null);
    void ShowError(string message, string? title = null);
    Task<bool> ShowConfirmationAsync(string message, string? title = null);
}