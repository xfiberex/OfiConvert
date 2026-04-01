using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using OfiConvert.Models;
using OfiConvert.Services;
using Serilog;

namespace OfiConvert.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IFileConversionService _officeService;
    private readonly LibreOfficeConversionService _libreOfficeService;
    private readonly IDialogService _dialogService;
    private readonly IConversionHistoryService _historyService;
    private readonly FileValidationService _validationService;
    private readonly SettingsService _settingsService;
    private readonly QueuePersistenceService _queueService;
    private readonly HashSet<string> _selectedFilePaths = [];
    private CancellationTokenSource? _cancellationTokenSource;
    private readonly ManualResetEventSlim _pauseEvent = new(true);
    private SemaphoreSlim? _parallelSemaphore;

    private const long MaxFileSizeBytes = 500 * 1024 * 1024;

    public MainViewModel(
        IFileConversionService officeService,
        IDialogService dialogService,
        IConversionHistoryService historyService)
    {
        _officeService = officeService;
        _dialogService = dialogService;
        _historyService = historyService;
        _libreOfficeService = new LibreOfficeConversionService();
        _validationService = new FileValidationService();
        _settingsService = new SettingsService();
        _queueService = new QueuePersistenceService();

        LoadSettings();
        LoadPersistedQueue();
        RefreshHistory();
    }

    public MainViewModel()
        : this(new OfficeFileConversionService(), new DialogService(), new ConversionHistoryService())
    {
    }

    #region Observable Properties

    [ObservableProperty]
    private ObservableCollection<FileItem> _selectedFiles = [];

    [ObservableProperty]
    private string _totalSize = "0 KB";

    [ObservableProperty]
    private int _fileCount;

    [ObservableProperty]
    private int _progressValue;

    [ObservableProperty]
    private int _progressMaximum = 100;

    [ObservableProperty]
    private string _progressPercentage = "0%";

    [ObservableProperty]
    private bool _isConverting;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private string _outputFolder = string.Empty;

    [ObservableProperty]
    private bool _useCustomOutputFolder;

    [ObservableProperty]
    private bool _showConversionResults;

    [ObservableProperty]
    private string _conversionResultTitle = string.Empty;

    [ObservableProperty]
    private string _conversionResultMessage = string.Empty;

    [ObservableProperty]
    private ObservableCollection<string> _conversionErrors = [];

    [ObservableProperty]
    private bool _hasConversionErrors;

    [ObservableProperty]
    private int _successfulConversions;

    [ObservableProperty]
    private int _failedConversions;

    [ObservableProperty]
    private OutputFormat _defaultOutputFormat = OutputFormat.PDF;

    [ObservableProperty]
    private ObservableCollection<ConversionHistoryEntry> _conversionHistory = [];

    // Settings
    [ObservableProperty]
    private string _selectedTheme = "System";

    [ObservableProperty]
    private string _selectedLanguage = "es";

    [ObservableProperty]
    private int _maxParallelConversions = 2;

    [ObservableProperty]
    private bool _autoRetryEnabled = true;

    [ObservableProperty]
    private int _maxRetryCount = 3;

    [ObservableProperty]
    private bool _minimizeToTray;

    [ObservableProperty]
    private bool _showNotifications = true;

    [ObservableProperty]
    private bool _isContextMenuRegistered;

    #endregion

    #region File Selection

    [RelayCommand]
    private async Task SelectFilesAsync()
    {
        var filter = GetLocalizedString("FilterOfficeFiles");
        var title = GetLocalizedString("TitleSelectFiles");
        var files = await _dialogService.ShowOpenFileDialogAsync(filter, title);

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

            if (!_officeService.IsValidOfficeFile(extension))
                continue;

            if (fileInfo.Length > MaxFileSizeBytes)
            {
                var msg = string.Format(GetLocalizedString("MsgFileTooBig"), fileInfo.Name);
                _dialogService.ShowInformation(msg, GetLocalizedString("MsgWarning"));
                continue;
            }

            var ext = fileInfo.Extension.TrimStart('.').ToUpper();
            var fileItem = new FileItem
            {
                Name = fileInfo.Name,
                Path = fileName,
                Size = FormatFileSize(fileInfo.Length),
                SizeInBytes = fileInfo.Length,
                Extension = ext,
                State = FileConversionState.Pending,
                StateMessage = GetLocalizedString("StatePending"),
                Options = new ConversionOptions { OutputFormat = DefaultOutputFormat }
            };

            SelectedFiles.Add(fileItem);
            _selectedFilePaths.Add(fileName);

            _ = LoadThumbnailAsync(fileItem);
        }

        UpdateTotals();
        PersistQueue();
    }

    private async Task LoadThumbnailAsync(FileItem fileItem)
    {
        var thumbnail = await ThumbnailService.GetThumbnailAsync(fileItem.Path, 48, 48);
        if (thumbnail is not null)
        {
            fileItem.Thumbnail = thumbnail;
        }
    }

    [RelayCommand]
    private void RemoveFile(FileItem? file)
    {
        if (file is not null && !IsConverting)
        {
            SelectedFiles.Remove(file);
            _selectedFilePaths.Remove(file.Path);
            UpdateTotals();
            PersistQueue();
        }
    }

    [RelayCommand]
    private void ClearFiles()
    {
        if (IsConverting)
        {
            _dialogService.ShowInformation(
                GetLocalizedString("MsgCannotClearConverting"),
                GetLocalizedString("MsgWarning"));
            return;
        }

        if (SelectedFiles.Count == 0)
        {
            _dialogService.ShowInformation(GetLocalizedString("MsgNoFilesToClear"));
            return;
        }

        SelectedFiles.Clear();
        _selectedFilePaths.Clear();
        UpdateTotals();
        ResetProgress();
        _queueService.ClearQueue();
    }

    #endregion

    #region Output Folder Management

    [RelayCommand]
    private async Task SelectOutputFolderAsync()
    {
        var folder = await _dialogService.ShowFolderBrowserDialogAsync(
            GetLocalizedString("TitleSelectOutputFolder"));

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

    #region Format Selection

    partial void OnDefaultOutputFormatChanged(OutputFormat value)
    {
        foreach (var file in SelectedFiles)
        {
            var available = OutputFormatHelper.GetFormatsForExtension(file.Extension);
            file.Options.OutputFormat = available.Contains(value) ? value : OutputFormat.PDF;
        }
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
            _dialogService.ShowInformation(GetLocalizedString("MsgNoFiles"));
            return;
        }

        if (!await EnsureOutputFolderSelectedAsync())
            return;

        // Check for available conversion engine
        bool officeAvailable = _officeService.IsOfficeInstalled();
        bool libreOfficeAvailable = _libreOfficeService.IsOfficeInstalled();

        if (!officeAvailable && !libreOfficeAvailable)
        {
            _dialogService.ShowError(
                GetLocalizedString("MsgNoConverterAvailable"),
                GetLocalizedString("MsgError"));
            return;
        }

        if (!officeAvailable)
        {
            Log.Warning("Office no disponible, usando LibreOffice como alternativa");
        }

        await PerformConversionAsync(officeAvailable);
    }

    [RelayCommand]
    private void CancelConversion()
    {
        _cancellationTokenSource?.Cancel();
        _pauseEvent.Set(); // Unblock paused threads
        IsPaused = false;
    }

    [RelayCommand]
    private void PauseConversion()
    {
        if (IsConverting && !IsPaused)
        {
            _pauseEvent.Reset();
            IsPaused = true;
            Log.Information("Conversión pausada");

            foreach (var file in SelectedFiles.Where(f => f.State == FileConversionState.Pending))
            {
                file.State = FileConversionState.Paused;
                file.StateMessage = GetLocalizedString("StatePaused");
            }
        }
    }

    [RelayCommand]
    private void ResumeConversion()
    {
        if (IsConverting && IsPaused)
        {
            IsPaused = false;
            _pauseEvent.Set();
            Log.Information("Conversión reanudada");

            foreach (var file in SelectedFiles.Where(f => f.State == FileConversionState.Paused))
            {
                file.State = FileConversionState.Pending;
                file.StateMessage = GetLocalizedString("StatePending");
            }
        }
    }

    private async Task<bool> EnsureOutputFolderSelectedAsync()
    {
        if (UseCustomOutputFolder && !string.IsNullOrEmpty(OutputFolder))
            return true;

        var shouldSelect = await _dialogService.ShowConfirmationAsync(
            GetLocalizedString("MsgSelectOutputFolder"),
            GetLocalizedString("MsgSelectOutputFolderTitle"));

        if (!shouldSelect)
            return false;

        var folder = await _dialogService.ShowFolderBrowserDialogAsync(
            GetLocalizedString("TitleSelectOutputFolder"));

        if (string.IsNullOrEmpty(folder))
            return false;

        OutputFolder = folder;
        UseCustomOutputFolder = true;
        return true;
    }

    private async Task PerformConversionAsync(bool officeAvailable)
    {
        IsConverting = true;
        IsPaused = false;
        _pauseEvent.Set();
        _cancellationTokenSource = new CancellationTokenSource();
        _parallelSemaphore = new SemaphoreSlim(MaxParallelConversions, MaxParallelConversions);
        var errors = new List<string>();
        var ct = _cancellationTokenSource.Token;

        try
        {
            ProgressMaximum = SelectedFiles.Count;
            ProgressValue = 0;

            foreach (var file in SelectedFiles)
            {
                file.State = FileConversionState.Pending;
                file.StateMessage = GetLocalizedString("StatePending");
                file.RetryCount = 0;
            }

            var tasks = SelectedFiles.Select(async (fileItem, index) =>
            {
                await _parallelSemaphore.WaitAsync(ct);
                try
                {
                    // Wait if paused
                    await Task.Run(() => _pauseEvent.Wait(ct), ct);

                    if (ct.IsCancellationRequested)
                    {
                        fileItem.State = FileConversionState.Skipped;
                        fileItem.StateMessage = GetLocalizedString("StateCancelled");
                        return;
                    }

                    // Validate file
                    fileItem.State = FileConversionState.Validating;
                    fileItem.StateMessage = GetLocalizedString("StateValidating");

                    var validationResult = _validationService.Validate(fileItem.Path);
                    if (!validationResult.IsValid)
                    {
                        fileItem.State = FileConversionState.Error;
                        fileItem.StateMessage = GetLocalizedString("StateError");
                        fileItem.ValidationMessage = validationResult.ErrorMessage ?? "";
                        lock (errors) errors.Add($"{fileItem.Name}: {validationResult.ErrorMessage}");
                        AddHistoryEntry(fileItem, null, validationResult.ErrorMessage);
                        IncrementProgress();
                        return;
                    }

                    // Convert
                    fileItem.State = FileConversionState.Converting;
                    fileItem.StateMessage = GetLocalizedString("StateConverting");

                    var outputPath = GetOutputPath(fileItem);
                    var result = await ConvertWithRetryAsync(fileItem, outputPath, officeAvailable, ct);

                    if (result.Success)
                    {
                        fileItem.State = FileConversionState.Completed;
                        fileItem.StateMessage = GetLocalizedString("StateCompleted");
                    }
                    else
                    {
                        fileItem.State = FileConversionState.Error;
                        fileItem.StateMessage = GetLocalizedString("StateError");
                        lock (errors) errors.Add($"{fileItem.Name}: {result.ErrorMessage}");
                    }

                    AddHistoryEntry(fileItem, result, null);
                    IncrementProgress();
                }
                finally
                {
                    _parallelSemaphore.Release();
                }
            }).ToArray();

            await Task.WhenAll(tasks);

            ShowConversionSummary(errors);

            if (errors.Count == 0 && !ct.IsCancellationRequested)
            {
                SelectedFiles.Clear();
                _selectedFilePaths.Clear();
                UpdateTotals();
                _queueService.ClearQueue();
            }

            // Send notification
            if (ShowNotifications)
            {
                OnConversionCompleted?.Invoke(this, new ConversionCompletedEventArgs
                {
                    SuccessCount = SuccessfulConversions,
                    ErrorCount = FailedConversions
                });
            }
        }
        catch (OperationCanceledException)
        {
            foreach (var f in SelectedFiles.Where(f => f.State is FileConversionState.Pending or FileConversionState.Paused))
            {
                f.State = FileConversionState.Skipped;
                f.StateMessage = GetLocalizedString("StateCancelled");
            }
            ShowConversionSummary(errors);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error general durante la conversión");
            _dialogService.ShowError($"Error general:\n\n{ex.Message}", GetLocalizedString("MsgError"));
        }
        finally
        {
            IsConverting = false;
            IsPaused = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            _parallelSemaphore?.Dispose();
            _parallelSemaphore = null;
            ResetProgress();
        }
    }

    private async Task<ConversionResult> ConvertWithRetryAsync(
        FileItem file, string outputPath, bool officeAvailable, CancellationToken ct)
    {
        int maxRetries = AutoRetryEnabled ? MaxRetryCount : 0;
        ConversionResult result = ConversionResult.Failed("No intentado");

        IFileConversionService service = officeAvailable
            ? _officeService
            : _libreOfficeService;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            if (attempt > 0)
            {
                file.State = FileConversionState.Retrying;
                file.StateMessage = $"{GetLocalizedString("StateRetrying")} ({attempt}/{maxRetries})";
                file.RetryCount = attempt;
                Log.Information("Reintentando conversión de {File}, intento {Attempt}/{Max}",
                    file.Name, attempt, maxRetries);

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), ct);
            }

            // Wait if paused
            await Task.Run(() => _pauseEvent.Wait(ct), ct);

            var progress = new Progress<ConversionProgress>(p =>
            {
                file.StateMessage = $"{GetLocalizedString("StateConverting")} {p.CurrentFile}/{p.TotalFiles}";
            });

            result = await service.ConvertAsync(file.Path, outputPath, file.Options, progress, ct);

            if (result.Success) break;

            // If Office fails and LibreOffice is available, try LibreOffice
            if (officeAvailable && !result.Success && attempt == maxRetries && _libreOfficeService.IsOfficeInstalled())
            {
                Log.Information("Intentando con LibreOffice como fallback para {File}", file.Name);
                file.StateMessage = GetLocalizedString("MsgLibreOfficeFallback");
                result = await _libreOfficeService.ConvertAsync(file.Path, outputPath, file.Options, progress, ct);
                if (result.Success) break;
            }
        }

        return result;
    }

    private string GetOutputPath(FileItem fileItem)
    {
        var format = fileItem.Options.OutputFormat;

        if (format is OutputFormat.PNG or OutputFormat.JPG &&
            fileItem.Extension.ToUpper() is "PPT" or "PPTX")
        {
            var folderName = Path.GetFileNameWithoutExtension(fileItem.Name);
            var outputDir = Path.Combine(OutputFolder, folderName);
            Directory.CreateDirectory(outputDir);
            return outputDir;
        }

        var ext = OutputFormatHelper.GetFileExtension(format);
        var outputFileName = Path.ChangeExtension(fileItem.Name, ext);
        return GetSafeOutputPath(OutputFolder, outputFileName);
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
            ConversionResultTitle = GetLocalizedString("MsgConversionErrors");
            ConversionResultMessage = string.Format(GetLocalizedString("MsgFilesConverted"), SuccessfulConversions) + "\n" +
                                    string.Format(GetLocalizedString("MsgFilesFailed"), FailedConversions) + "\n\n" +
                                    string.Format(GetLocalizedString("MsgFilesSavedTo"), OutputFolder);

            ConversionErrors.Clear();
            foreach (var error in errors)
                ConversionErrors.Add(error);
            HasConversionErrors = true;
        }
        else
        {
            ConversionResultTitle = GetLocalizedString("MsgConversionSuccess");
            ConversionResultMessage = string.Format(GetLocalizedString("MsgFilesConverted"), SuccessfulConversions) + "\n\n" +
                                    string.Format(GetLocalizedString("MsgFilesSavedTo"), OutputFolder);
            HasConversionErrors = false;
        }

        ShowConversionResults = true;
        Log.Information("Conversión completada: {Success} exitosas, {Failed} fallidas", SuccessfulConversions, FailedConversions);
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

    #region History

    private void AddHistoryEntry(FileItem file, ConversionResult? result, string? validationError)
    {
        var entry = new ConversionHistoryEntry
        {
            SourcePath = file.Path,
            SourceFileName = file.Name,
            OutputPath = result?.OutputPath ?? "",
            Format = file.Options.OutputFormat,
            Success = result?.Success ?? false,
            ErrorMessage = result?.ErrorMessage ?? validationError,
            DurationSeconds = result?.Duration.TotalSeconds ?? 0,
            FileSizeBytes = file.SizeInBytes
        };

        _historyService.AddEntry(entry);
        Application.Current.Dispatcher.Invoke(() =>
        {
            ConversionHistory.Insert(0, entry);
            if (ConversionHistory.Count > 500) ConversionHistory.RemoveAt(ConversionHistory.Count - 1);
        });
    }

    private void RefreshHistory()
    {
        var history = _historyService.GetHistory();
        ConversionHistory = new ObservableCollection<ConversionHistoryEntry>(history.Take(500));
    }

    [RelayCommand]
    private void ClearHistory()
    {
        _historyService.ClearHistory();
        ConversionHistory.Clear();
    }

    [RelayCommand]
    private async Task ExportHistoryCsvAsync()
    {
        var path = await _dialogService.ShowSaveFileDialogAsync(
            GetLocalizedString("FilterCsv"),
            GetLocalizedString("BtnExportCsv"),
            "historial_conversiones.csv");

        if (!string.IsNullOrEmpty(path))
        {
            _historyService.ExportToCsv(path);
            Log.Information("Historial exportado a CSV: {Path}", path);
        }
    }

    [RelayCommand]
    private async Task ExportHistoryTxtAsync()
    {
        var path = await _dialogService.ShowSaveFileDialogAsync(
            GetLocalizedString("FilterTxt"),
            GetLocalizedString("BtnExportTxt"),
            "historial_conversiones.txt");

        if (!string.IsNullOrEmpty(path))
        {
            _historyService.ExportToTxt(path);
            Log.Information("Historial exportado a TXT: {Path}", path);
        }
    }

    #endregion

    #region Settings

    private void LoadSettings()
    {
        var settings = _settingsService.Load();
        SelectedTheme = settings.Theme;
        SelectedLanguage = settings.Language;
        MaxParallelConversions = settings.MaxParallelConversions;
        AutoRetryEnabled = settings.AutoRetryEnabled;
        MaxRetryCount = settings.MaxRetryCount;
        MinimizeToTray = settings.MinimizeToTray;
        ShowNotifications = settings.ShowNotifications;
        DefaultOutputFormat = settings.DefaultOutputFormat;
        IsContextMenuRegistered = ShellIntegrationService.IsRegistered();

        if (!string.IsNullOrEmpty(settings.LastOutputFolder) && Directory.Exists(settings.LastOutputFolder))
        {
            OutputFolder = settings.LastOutputFolder;
            UseCustomOutputFolder = true;
        }
    }

    public void SaveSettings()
    {
        var settings = new AppSettings
        {
            Theme = SelectedTheme,
            Language = SelectedLanguage,
            MaxParallelConversions = MaxParallelConversions,
            AutoRetryEnabled = AutoRetryEnabled,
            MaxRetryCount = MaxRetryCount,
            MinimizeToTray = MinimizeToTray,
            ShowNotifications = ShowNotifications,
            LastOutputFolder = OutputFolder,
            DefaultOutputFormat = DefaultOutputFormat
        };
        _settingsService.Save(settings);
    }

    partial void OnSelectedThemeChanged(string value)
    {
        ApplyTheme(value);
        SaveSettings();
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        ApplyLanguage(value);
        SaveSettings();
    }

    partial void OnMaxParallelConversionsChanged(int value) => SaveSettings();
    partial void OnAutoRetryEnabledChanged(bool value) => SaveSettings();
    partial void OnMaxRetryCountChanged(int value) => SaveSettings();
    partial void OnMinimizeToTrayChanged(bool value) => SaveSettings();
    partial void OnShowNotificationsChanged(bool value) => SaveSettings();

    private static void ApplyTheme(string theme)
    {
        if (theme == "System")
        {
            Wpf.Ui.Appearance.ApplicationThemeManager.ApplySystemTheme();
        }
        else
        {
            var appTheme = theme == "Dark"
                ? Wpf.Ui.Appearance.ApplicationTheme.Dark
                : Wpf.Ui.Appearance.ApplicationTheme.Light;
            Wpf.Ui.Appearance.ApplicationThemeManager.Apply(appTheme);
        }
    }

    private static void ApplyLanguage(string language)
    {
        var cultureName = language == "en" ? "en-US" : "es-ES";
        var newDict = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Lang/{cultureName}.xaml")
        };

        var existing = Application.Current.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source?.OriginalString.Contains("/Lang/") == true);

        if (existing is not null)
            Application.Current.Resources.MergedDictionaries.Remove(existing);

        Application.Current.Resources.MergedDictionaries.Add(newDict);
    }

    [RelayCommand]
    private void RegisterContextMenu()
    {
        ShellIntegrationService.Register();
        IsContextMenuRegistered = true;
    }

    [RelayCommand]
    private void UnregisterContextMenu()
    {
        ShellIntegrationService.Unregister();
        IsContextMenuRegistered = false;
    }

    #endregion

    #region Queue Persistence

    private void PersistQueue()
    {
        _queueService.SaveQueue(SelectedFiles.Select(f => f.Path));
    }

    private void LoadPersistedQueue()
    {
        var paths = _queueService.LoadQueue();
        if (paths.Count > 0)
        {
            AddFiles(paths);
        }
    }

    #endregion

    #region Events

    public event EventHandler<ConversionCompletedEventArgs>? OnConversionCompleted;

    public class ConversionCompletedEventArgs : EventArgs
    {
        public int SuccessCount { get; init; }
        public int ErrorCount { get; init; }
    }

    #endregion

    #region Helper Methods

    private void UpdateTotals()
    {
        FileCount = SelectedFiles.Count;
        long totalBytes = SelectedFiles.Sum(f => f.SizeInBytes);
        TotalSize = FormatFileSize(totalBytes);
    }

    private void IncrementProgress()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ProgressValue++;
            var percentage = ProgressMaximum > 0
                ? (int)Math.Round(((double)ProgressValue / ProgressMaximum) * 100)
                : 0;
            ProgressPercentage = $"{percentage}%";
        });
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

    private static string GetLocalizedString(string key)
    {
        return Application.Current.TryFindResource(key) as string ?? key;
    }

    public bool CanClose()
    {
        return !IsConverting;
    }

    #endregion
}