using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using Microsoft.UI.Dispatching;
using OfiConvert.Core;
using OfiConvert.Helpers;
using OfiConvert.Models;
using OfiConvert.Services;
using Serilog;

namespace OfiConvert.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
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
    private bool _isLoadingSettings;

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

        // Cada asignación de una propiedad de ajustes dispara su OnXChanged → SaveSettings. Durante la
        // carga eso escribiría en disco el estado a MEDIO cargar: el guardado con SelectedTheme aún
        // llevaría el DefaultOutputFormat y el LastOutputFolder por defecto, pisando los del usuario.
        _isLoadingSettings = true;

        SelectedFiles = [];
        ConversionErrors = [];
        ConversionHistory = [];
        TotalSize = "0 KB";
        ProgressMaximum = 100;
        ProgressPercentage = "0%";
        OutputFolder = string.Empty;
        ConversionResultTitle = string.Empty;
        ConversionResultMessage = string.Empty;
        SelectedTheme = "System";
        SelectedLanguage = LocalizationService.DefaultLanguage;
        DefaultOutputFormat = OutputFormat.PDF;
        MaxParallelConversions = 2;
        AutoRetryEnabled = true;
        MaxRetryCount = 3;
        ShowNotifications = true;

        LoadSettings();

        _isLoadingSettings = false;

        LoadPersistedQueue();
        RefreshHistory();
    }

    public MainViewModel()
        : this(new OfficeFileConversionService(), new DialogService(), new ConversionHistoryService())
    {
    }

    #region Observable Properties

    // Propiedades PARCIALES, no campos: [ObservableProperty] sobre un campo genera código que no es
    // AOT-compatible en WinUI 3 (MVVMTK0045). Una propiedad parcial no admite inicializador, así que
    // los valores por defecto se asignan en el constructor (los de ajustes, en LoadSettings).

    [ObservableProperty]
    public partial ObservableCollection<FileItem> SelectedFiles { get; set; }

    [ObservableProperty]
    public partial string TotalSize { get; set; }

    [ObservableProperty]
    public partial int FileCount { get; set; }

    [ObservableProperty]
    public partial int ProgressValue { get; set; }

    [ObservableProperty]
    public partial int ProgressMaximum { get; set; }

    [ObservableProperty]
    public partial string ProgressPercentage { get; set; }

    [ObservableProperty]
    public partial bool IsConverting { get; set; }

    [ObservableProperty]
    public partial bool IsPaused { get; set; }

    [ObservableProperty]
    public partial string OutputFolder { get; set; }

    [ObservableProperty]
    public partial bool UseCustomOutputFolder { get; set; }

    [ObservableProperty]
    public partial bool ShowConversionResults { get; set; }

    [ObservableProperty]
    public partial string ConversionResultTitle { get; set; }

    [ObservableProperty]
    public partial string ConversionResultMessage { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<string> ConversionErrors { get; set; }

    [ObservableProperty]
    public partial bool HasConversionErrors { get; set; }

    [ObservableProperty]
    public partial int SuccessfulConversions { get; set; }

    [ObservableProperty]
    public partial int FailedConversions { get; set; }

    [ObservableProperty]
    public partial OutputFormat DefaultOutputFormat { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<ConversionHistoryEntry> ConversionHistory { get; set; }

    // Settings
    [ObservableProperty]
    public partial string SelectedTheme { get; set; }

    [ObservableProperty]
    public partial string SelectedLanguage { get; set; }

    [ObservableProperty]
    public partial int MaxParallelConversions { get; set; }

    [ObservableProperty]
    public partial bool AutoRetryEnabled { get; set; }

    [ObservableProperty]
    public partial int MaxRetryCount { get; set; }

    [ObservableProperty]
    public partial bool MinimizeToTray { get; set; }

    [ObservableProperty]
    public partial bool ShowNotifications { get; set; }

    [ObservableProperty]
    public partial bool IsContextMenuRegistered { get; set; }

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
                Size = ByteSize.Format(fileInfo.Length),
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
        try
        {
            var thumbnail = await ThumbnailService.GetThumbnailAsync(fileItem.Path, 48, 48);
            if (thumbnail is not null)
            {
                fileItem.Thumbnail = thumbnail;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error loading thumbnail for {File}", fileItem.Name);
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

        // El lote se fija AQUÍ. La cola sigue viva: se pueden soltar archivos nuevos (o llegar por el
        // menú contextual) mientras esto corre, y esos NO entran en este lote ni se tocan al acabar.
        // Antes se iteraba y se limpiaba SelectedFiles directamente, así que un archivo añadido a mitad
        // de un lote acababa borrado sin convertir.
        var batch = SelectedFiles.ToList();

        try
        {
            ProgressMaximum = batch.Count;
            ProgressValue = 0;

            foreach (var file in batch)
            {
                file.State = FileConversionState.Pending;
                file.StateMessage = GetLocalizedString("StatePending");
                file.RetryCount = 0;
            }

            var tasks = batch.Select(async (fileItem, index) =>
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

            ShowConversionSummary(batch, errors);

            if (errors.Count == 0 && !ct.IsCancellationRequested)
            {
                RemoveBatchFromQueue(batch);
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
            foreach (var f in batch.Where(f => f.State is FileConversionState.Pending or FileConversionState.Paused))
            {
                f.State = FileConversionState.Skipped;
                f.StateMessage = GetLocalizedString("StateCancelled");
            }
            ShowConversionSummary(batch, errors);
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
        return OutputPath.GetSafe(OutputFolder, outputFileName);
    }

    /// <summary>Retira del listado los archivos del lote recién convertido, respetando los que se hayan añadido mientras corría.</summary>
    private void RemoveBatchFromQueue(List<FileItem> batch)
    {
        foreach (var file in batch)
        {
            SelectedFiles.Remove(file);
            _selectedFilePaths.Remove(file.Path);
        }

        UpdateTotals();

        if (SelectedFiles.Count == 0)
            _queueService.ClearQueue();
        else
            PersistQueue();
    }

    private void ShowConversionSummary(List<FileItem> batch, List<string> errors)
    {
        SuccessfulConversions = batch.Count(f => f.State == FileConversionState.Completed);
        FailedConversions = batch.Count(f => f.State == FileConversionState.Error);

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
        var dq = DispatcherQueue.GetForCurrentThread();
        if (dq is not null)
        {
            dq.TryEnqueue(() =>
            {
                ConversionHistory.Insert(0, entry);
                if (ConversionHistory.Count > 500) ConversionHistory.RemoveAt(ConversionHistory.Count - 1);
            });
        }
        else
        {
            ConversionHistory.Insert(0, entry);
            if (ConversionHistory.Count > 500) ConversionHistory.RemoveAt(ConversionHistory.Count - 1);
        }
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
        if (_isLoadingSettings) return;

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
        // Theme application is handled by MainWindow.xaml.cs via PropertyChanged event
    }

    private static void ApplyLanguage(string language)
    {
        LocalizationService.Instance.LoadLanguage(language);
    }

    [RelayCommand]
    private void RegisterContextMenu()
    {
        ShellIntegrationService.Register();
        IsContextMenuRegistered = true;
        _dialogService.ShowInformation(
            GetLocalizedString("MsgContextMenuRegistered"),
            GetLocalizedString("MsgInfo"));
    }

    [RelayCommand]
    private void UnregisterContextMenu()
    {
        ShellIntegrationService.Unregister();
        IsContextMenuRegistered = false;
        _dialogService.ShowInformation(
            GetLocalizedString("MsgContextMenuUnregistered"),
            GetLocalizedString("MsgInfo"));
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
        TotalSize = ByteSize.Format(totalBytes);
    }

    private void IncrementProgress()
    {
        var dq = DispatcherQueue.GetForCurrentThread();
        if (dq is not null)
        {
            dq.TryEnqueue(() =>
            {
                ProgressValue++;
                var percentage = ProgressMaximum > 0
                    ? (int)Math.Round(((double)ProgressValue / ProgressMaximum) * 100)
                    : 0;
                ProgressPercentage = $"{percentage}%";
            });
        }
        else
        {
            ProgressValue++;
            var percentage = ProgressMaximum > 0
                ? (int)Math.Round(((double)ProgressValue / ProgressMaximum) * 100)
                : 0;
            ProgressPercentage = $"{percentage}%";
        }
    }

    private void ResetProgress()
    {
        ProgressValue = 0;
        ProgressPercentage = "0%";
    }

    private static string GetLocalizedString(string key)
    {
        return LocalizationService.Instance[key];
    }

    public bool CanClose()
    {
        return !IsConverting;
    }

    #endregion

    #region IDisposable

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pauseEvent.Dispose();
        _cancellationTokenSource?.Dispose();
        _parallelSemaphore?.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}