using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using OfiConvert.Models;
using OfiConvert.Services;

namespace OfiConvert.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IFileConversionService _conversionService;
    private readonly IDialogService _dialogService;
    private readonly HashSet<string> _selectedFilePaths = [];
    private CancellationTokenSource? _cancellationTokenSource;

    private const long MaxFileSizeBytes = 500 * 1024 * 1024; // 500 MB

    public MainViewModel(IFileConversionService conversionService, IDialogService dialogService)
    {
        _conversionService = conversionService;
        _dialogService = dialogService;
    }

    public MainViewModel()
        : this(new OfficeFileConversionService(), new DialogService())
    {
    }

    #region Observable Properties

    [ObservableProperty]
    private ObservableCollection<FileItem> _selectedFiles = [];

    [ObservableProperty]
    private string _totalSize = "0 KB";

    [ObservableProperty]
    private int _fileCount = 0;

    [ObservableProperty]
    private int _progressValue = 0;

    [ObservableProperty]
    private int _progressMaximum = 100;

    [ObservableProperty]
    private string _progressPercentage = "0%";

    [ObservableProperty]
    private bool _isConverting = false;

    [ObservableProperty]
    private string _outputFolder = string.Empty;

    [ObservableProperty]
    private bool _useCustomOutputFolder = false;

    [ObservableProperty]
    private bool _showConversionResults = false;

    [ObservableProperty]
    private string _conversionResultTitle = string.Empty;

    [ObservableProperty]
    private string _conversionResultMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _conversionErrors = [];

    [ObservableProperty]
    private bool _hasConversionErrors = false;

    [ObservableProperty]
    private int _successfulConversions = 0;

    [ObservableProperty]
    private int _failedConversions = 0;

    #endregion

    #region File Selection

    [RelayCommand]
    private async Task SelectFilesAsync()
    {
        const string filter = "Archivos Office|*.doc;*.docx;*.xls;*.xlsx;*.ppt;*.pptx|" +
                             "Archivos Word (*.docx)|*.docx|" +
                             "Archivos Excel (*.xlsx)|*.xlsx|" +
                             "Archivos PowerPoint (*.pptx)|*.pptx|" +
                             "Todos los archivos|*.*";

        var files = await _dialogService.ShowOpenFileDialogAsync(filter, "Seleccionar archivos de Office");

        if (files is not null && files.Length > 0)
        {
            AddFiles(files);
        }
    }

    public void AddFiles(IEnumerable<string> fileNames)
    {
        foreach (var fileName in fileNames)
        {
            if (_selectedFilePaths.Contains(fileName))
                continue;

            if (!File.Exists(fileName))
                continue;

            var fileInfo = new FileInfo(fileName);
            var extension = fileInfo.Extension.ToLowerInvariant();

            if (!_conversionService.IsValidOfficeFile(extension))
                continue;

            if (fileInfo.Length > MaxFileSizeBytes)
            {
                _dialogService.ShowInformation(
                    $"El archivo '{fileInfo.Name}' excede el l\u00edmite de 500 MB y no se agregar\u00e1.",
                    "Archivo demasiado grande");
                continue;
            }

            var fileItem = new FileItem
            {
                Name = fileInfo.Name,
                Path = fileName,
                Size = FormatFileSize(fileInfo.Length),
                SizeInBytes = fileInfo.Length,
                Extension = fileInfo.Extension.TrimStart('.').ToUpper(),
                State = FileConversionState.Pending,
                StateMessage = "Pendiente"
            };

            SelectedFiles.Add(fileItem);
            _selectedFilePaths.Add(fileName);
        }

        UpdateTotals();
    }

    [RelayCommand]
    private void RemoveFile(FileItem? file)
    {
        if (file is not null && !IsConverting)
        {
            SelectedFiles.Remove(file);
            _selectedFilePaths.Remove(file.Path);
            UpdateTotals();
        }
    }

    [RelayCommand]
    private void ClearFiles()
    {
        if (IsConverting)
        {
            _dialogService.ShowInformation(
                "No se puede limpiar mientras se est\u00e1 convirtiendo.",
                "Advertencia");
            return;
        }

        if (SelectedFiles.Count == 0)
        {
            _dialogService.ShowInformation("No hay archivos para borrar.");
            return;
        }

        SelectedFiles.Clear();
        _selectedFilePaths.Clear();
        UpdateTotals();
        ResetProgress();
    }

    #endregion

    #region Output Folder Management

    [RelayCommand]
    private async Task SelectOutputFolderAsync()
    {
        var folder = await _dialogService.ShowFolderBrowserDialogAsync("Seleccionar carpeta de destino");

        if (!string.IsNullOrEmpty(folder))
        {
            OutputFolder = folder;
            UseCustomOutputFolder = true;
        }
    }

    [RelayCommand]
    private void ClearOutputFolder()
    {
        OutputFolder = string.Empty;
        UseCustomOutputFolder = false;
    }

    #endregion

    #region Conversion Process

    [RelayCommand]
    private async Task ConvertFilesAsync()
    {
        ShowConversionResults = false;
        ConversionErrors.Clear();

        if (SelectedFiles.Count == 0)
        {
            _dialogService.ShowInformation("No hay archivos seleccionados.");
            return;
        }

        if (!await EnsureOutputFolderSelectedAsync())
            return;

        if (!_conversionService.IsOfficeInstalled())
        {
            _dialogService.ShowError(
                "Microsoft Office no est\u00e1 instalado o no se puede acceder.\n\n" +
                "Esta aplicaci\u00f3n requiere Microsoft Office (Word, Excel y/o PowerPoint) " +
                "instalado en el sistema para convertir archivos a PDF.\n\n" +
                "Por favor, instale Microsoft Office e intente nuevamente.",
                "Office no encontrado");
            return;
        }

        await PerformConversionAsync();
    }

    [RelayCommand]
    private void CancelConversion()
    {
        _cancellationTokenSource?.Cancel();
    }

    private async Task<bool> EnsureOutputFolderSelectedAsync()
    {
        if (UseCustomOutputFolder && !string.IsNullOrEmpty(OutputFolder))
            return true;

        var shouldSelect = await _dialogService.ShowConfirmationAsync(
            "No has seleccionado una carpeta de destino.\n\n" +
            "\u00bfDeseas seleccionar una carpeta para guardar los archivos PDF convertidos?",
            "Seleccionar carpeta de destino");

        if (!shouldSelect)
            return false;

        var folder = await _dialogService.ShowFolderBrowserDialogAsync(
            "Seleccionar carpeta de destino para archivos PDF");

        if (string.IsNullOrEmpty(folder))
            return false;

        OutputFolder = folder;
        UseCustomOutputFolder = true;
        return true;
    }

    private async Task PerformConversionAsync()
    {
        IsConverting = true;
        _cancellationTokenSource = new CancellationTokenSource();
        var errors = new List<string>();

        try
        {
            ProgressMaximum = SelectedFiles.Count;
            ProgressValue = 0;

            foreach (var file in SelectedFiles)
            {
                file.State = FileConversionState.Pending;
                file.StateMessage = "Pendiente";
            }

            for (int i = 0; i < SelectedFiles.Count; i++)
            {
                if (_cancellationTokenSource.Token.IsCancellationRequested)
                {
                    for (int j = i; j < SelectedFiles.Count; j++)
                    {
                        SelectedFiles[j].State = FileConversionState.Skipped;
                        SelectedFiles[j].StateMessage = "Cancelado";
                    }
                    break;
                }

                var fileItem = SelectedFiles[i];

                fileItem.State = FileConversionState.Converting;
                fileItem.StateMessage = "Convirtiendo...";

                var outputFileName = Path.ChangeExtension(fileItem.Name, ".pdf");
                var pdfPath = GetSafeOutputPath(OutputFolder, outputFileName);

                var result = await _conversionService.ConvertToPdfAsync(
                    fileItem.Path,
                    pdfPath,
                    cancellationToken: _cancellationTokenSource.Token);

                if (result.Success)
                {
                    fileItem.State = FileConversionState.Completed;
                    fileItem.StateMessage = "Completado";
                }
                else
                {
                    fileItem.State = FileConversionState.Error;
                    fileItem.StateMessage = "Error";
                    errors.Add($"{fileItem.Name}: {result.ErrorMessage}");
                }

                UpdateProgress(i + 1);
            }

            ShowConversionSummary(errors);

            if (errors.Count == 0 && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                SelectedFiles.Clear();
                _selectedFilePaths.Clear();
                UpdateTotals();
            }
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(
                $"Error general durante la conversi\u00f3n:\n\n{ex.Message}",
                "Error");
        }
        finally
        {
            IsConverting = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            ResetProgress();
        }
    }

    private static string GetSafeOutputPath(string outputFolder, string fileName)
    {
        var safeName = Path.GetFileName(fileName);
        var fullOutputFolder = Path.GetFullPath(outputFolder);
        var candidate = Path.GetFullPath(Path.Combine(fullOutputFolder, safeName));

        if (!candidate.StartsWith(fullOutputFolder, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Ruta de salida no v\u00e1lida.");

        if (!File.Exists(candidate))
            return candidate;

        var nameWithoutExt = Path.GetFileNameWithoutExtension(safeName);
        var ext = Path.GetExtension(safeName);
        int counter = 1;

        do
        {
            candidate = Path.GetFullPath(Path.Combine(fullOutputFolder, $"{nameWithoutExt} ({counter}){ext}"));
            counter++;
        } while (File.Exists(candidate));

        return candidate;
    }

    private void ShowConversionSummary(List<string> errors)
    {
        SuccessfulConversions = SelectedFiles.Count(f => f.State == FileConversionState.Completed);
        FailedConversions = SelectedFiles.Count(f => f.State == FileConversionState.Error);

        if (errors.Count > 0)
        {
            ConversionResultTitle = "Conversi\u00f3n finalizada con errores";
            ConversionResultMessage = $"Se convirtieron {SuccessfulConversions} archivo(s) exitosamente.\n" +
                                    $"Fallaron {FailedConversions} archivo(s).\n\n" +
                                    $"Archivos guardados en: {OutputFolder}";

            ConversionErrors.Clear();
            foreach (var error in errors)
            {
                ConversionErrors.Add(error);
            }
            HasConversionErrors = true;
        }
        else
        {
            ConversionResultTitle = "Conversi\u00f3n exitosa";
            ConversionResultMessage = $"Se convirtieron {SuccessfulConversions} archivo(s) exitosamente.\n\n" +
                                    $"Archivos guardados en:\n{OutputFolder}";
            HasConversionErrors = false;
        }

        ShowConversionResults = true;
    }

    [RelayCommand]
    private void CloseConversionResults()
    {
        ShowConversionResults = false;
        ConversionErrors.Clear();
        ConversionResultTitle = string.Empty;
        ConversionResultMessage = string.Empty;
        HasConversionErrors = false;
    }

    #endregion

    #region Helper Methods

    private void UpdateTotals()
    {
        FileCount = SelectedFiles.Count;
        long totalBytes = SelectedFiles.Sum(f => f.SizeInBytes);
        TotalSize = FormatFileSize(totalBytes);
    }

    private void UpdateProgress(int current)
    {
        ProgressValue = current;
        var percentage = (int)Math.Round(((double)current / SelectedFiles.Count) * 100);
        ProgressPercentage = $"{percentage}%";
    }

    private void ResetProgress()
    {
        ProgressValue = 0;
        ProgressPercentage = "0%";
    }

    internal static string FormatFileSize(long bytes)
    {
        const int divisor = 1024;
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;

        while (len >= divisor && order < sizes.Length - 1)
        {
            order++;
            len /= divisor;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    public bool CanClose()
    {
        if (!IsConverting)
            return true;

        return false;
    }

    #endregion
}