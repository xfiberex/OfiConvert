using System.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OfiConvert.Behaviors;
using OfiConvert.Helpers;
using OfiConvert.Models;
using OfiConvert.ViewModels;
using Windows.Graphics;
using WinRT.Interop;

namespace OfiConvert;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }
    private H.NotifyIcon.TaskbarIcon? _trayIcon;
    private string? _appUpdateUrl;
    private string? _appUpdateChecksumUrl;
    private AppWindow _appWindow = null!;
    private nint _hWnd;

    public MainWindow()
    {
        ViewModel = new MainViewModel();
        InitializeComponent();

        // Set DataContext for Binding (x:Bind on Window not supported - Window is not FrameworkElement)
        if (Content is FrameworkElement root)
            root.DataContext = ViewModel;

        // Set up AppWindow reference
        _hWnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        // Custom title bar
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Mica backdrop
        SystemBackdrop = new MicaBackdrop();

        // Window size
        _appWindow.Resize(new SizeInt32(1050, 800));

        // Window closing event
        _appWindow.Closing += OnAppWindowClosing;

        // Setup drag-drop on the DropZone border
        DropZone.Loaded += (_, _) => FileDragDropBehavior.Attach(DropZone, ViewModel, DropZone);

        // Set up event handlers after load
        DropZone.Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        ViewModel.OnConversionCompleted += OnConversionCompleted;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        ApplyTheme(ViewModel.SelectedTheme);
        ApplyFormatSelection(ViewModel.DefaultOutputFormat);
        ApplyThemeComboSelection(ViewModel.SelectedTheme);
        ApplyLanguageSelection(ViewModel.SelectedLanguage);

        _ = CheckForAppUpdateAsync();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedTheme))
        {
            ApplyTheme(ViewModel.SelectedTheme);
        }
    }

    private void ApplyTheme(string theme)
    {
        if (Content is FrameworkElement rootElement)
        {
            rootElement.RequestedTheme = theme switch
            {
                "Light" => ElementTheme.Light,
                "Dark" => ElementTheme.Dark,
                _ => ElementTheme.Default
            };
        }
    }

    private void InitializeTrayIcon()
    {
        if (_trayIcon is not null) return;

        _trayIcon = new H.NotifyIcon.TaskbarIcon
        {
            ToolTipText = "OfiConvert"
        };

        // Set icon from exe
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
            {
                _trayIcon.Icon = new System.Drawing.Icon(
                    System.Drawing.Icon.ExtractAssociatedIcon(exePath)!, 
                    new System.Drawing.Size(32, 32));
            }
        }
        catch
        {
            _trayIcon.Icon = System.Drawing.SystemIcons.Application;
        }

        _trayIcon.DoubleClickCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() =>
        {
            DispatcherQueue.TryEnqueue(() => RestoreFromTray());
        });
    }

    private void OnConversionCompleted(object? sender, MainViewModel.ConversionCompletedEventArgs e)
    {
        if (!ViewModel.ShowNotifications) return;

        // El resultado ya se muestra en el panel de la propia ventana; esto solo reclama la atención
        // de quien se fue a otra cosa. Notifier no hace nada si la ventana está en primer plano.
        DispatcherQueue.TryEnqueue(() => Notifier.NotifyCompleted(_hWnd, e.ErrorCount > 0));
    }

    /// <summary>
    /// Encola los archivos de una activación (menú contextual del Explorador, "Abrir con", o una
    /// segunda instancia redirigida a esta) y trae la ventana al frente.
    /// </summary>
    public void EnqueueFromActivation(IReadOnlyList<string> files)
    {
        if (files.Count == 0) return;

        ViewModel.AddFiles(files);
        BringToFront();
    }

    private void BringToFront()
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Visibility = Visibility.Collapsed;
            _appWindow.Show();
        }

        // Restore() es lo que saca la ventana de minimizada y la pone al frente; AppWindow.Show() sola
        // no lo hace si el usuario la había minimizado.
        if (_appWindow.Presenter is OverlappedPresenter presenter)
            presenter.Restore();

        Activate();
    }

    private void RestoreFromTray()
    {
        _appWindow.Show();
        if (_trayIcon is not null)
            _trayIcon.Visibility = Visibility.Collapsed;
    }

    private void MinimizeToTray()
    {
        InitializeTrayIcon();
        if (_trayIcon is not null)
        {
            _trayIcon.Visibility = Visibility.Visible;
            _appWindow.Hide();
        }
    }

    private async void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        // Minimize to tray instead of closing if setting is enabled
        if (ViewModel.MinimizeToTray && ViewModel.CanClose())
        {
            args.Cancel = true;
            MinimizeToTray();
            return;
        }

        if (!ViewModel.CanClose())
        {
            args.Cancel = true;

            // Las claves se piden directamente, sin red de seguridad: ped\u00eda "TitleConfirmClose" y "BtnYes",
            // que NO exist\u00edan, y ca\u00eda a un texto espa\u00f1ol a fuego \u2014 as\u00ed que este di\u00e1logo sal\u00eda en espa\u00f1ol a
            // un usuario japon\u00e9s o alem\u00e1n. Sus traducciones ya estaban en los 8 idiomas, con otro nombre
            // (MsgCancelConfirm/MsgCancelConfirmTitle) y sin usarse en ning\u00fan sitio. Los fallbacks eran
            // justo lo que imped\u00eda notarlo; ahora una clave que falte la caza LocalizationUsageTests.
            var dialog = new ContentDialog
            {
                Title = LocalizationService.Instance["MsgCancelConfirmTitle"],
                Content = new TextBlock
                {
                    Text = LocalizationService.Instance["MsgCancelConfirm"],
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = LocalizationService.Instance["BtnYes"],
                CloseButtonText = LocalizationService.Instance["BtnNo"],
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary) return;

            ViewModel.CancelConversionCommand.Execute(null);
        }

        // Cleanup
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.OnConversionCompleted -= OnConversionCompleted;
        ViewModel.SaveSettings();
        ViewModel.Dispose();

        if (_trayIcon is not null)
        {
            _trayIcon.Visibility = Visibility.Collapsed;
            _trayIcon.Dispose();
            _trayIcon = null;
        }

        Close();
    }

    private async Task CheckForAppUpdateAsync()
    {
        OfiConvert.Services.GitHubReleaseInfo? info =
            await OfiConvert.Services.GitHubUpdateService.CheckForUpdateAsync();
        if (info is null) return;

        _appUpdateUrl = info.DownloadUrl;
        _appUpdateChecksumUrl = info.ChecksumUrl;
        var loc = LocalizationService.Instance;
        infoBarUpdate.Title = $"\u2b06 {loc["TitleUpdateAvailable"]}: {info.Version}";
        infoBarUpdate.Message = loc["MsgUpdateAvailable"];
        infoBarUpdate.IsOpen = true;
        btnBuscarActualizacion.Content = $"\u2b06 {info.Version}";
    }

    private async void BtnDownloadUpdate_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_appUpdateUrl)) return;

        btnInstalarUpdate.IsEnabled = false;
        btnInstalarUpdate.Content = "Descargando...";
        pbUpdate.Visibility = Visibility.Visible;
        infoBarUpdate.IsClosable = false;

        var progress = new Progress<double>(p =>
        {
            pbUpdate.Value = p;
            infoBarUpdate.Message = $"Descargando... {p:P0}";
        });

        try
        {
            // DownloadInstallerAsync VERIFICA lo descargado (firma Authenticode o, en su defecto, el
            // SHA-256 publicado como asset) y lanza si no lo supera, tras borrar el archivo. Si llega
            // aqu\u00ed una ruta, es de un instalador en el que se conf\u00eda.
            string installerPath = await OfiConvert.Services.GitHubUpdateService
                .DownloadInstallerAsync(_appUpdateUrl, _appUpdateChecksumUrl, progress);
            infoBarUpdate.Message = "Instalando... La aplicaci\u00f3n se reiniciar\u00e1 autom\u00e1ticamente.";

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(installerPath)
            {
                Arguments = "/VERYSILENT /NORESTART /autoinstall=1",
                UseShellExecute = true
            });
            await Task.Delay(1500);
            Application.Current.Exit();
        }
        catch (Exception ex)
        {
            pbUpdate.Visibility = Visibility.Collapsed;
            btnInstalarUpdate.IsEnabled = true;
            btnInstalarUpdate.Content = "Instalar ahora";
            infoBarUpdate.IsClosable = true;
            infoBarUpdate.Severity = InfoBarSeverity.Error;
            infoBarUpdate.Message = ex.Message;
            Serilog.Log.Error(ex, "Actualizaci\u00f3n rechazada o fallida");
        }
    }

    private void BtnRemoveFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is FileItem file)
        {
            ViewModel.RemoveFileCommand.Execute(file);
        }
    }

    private void CmbFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbFormat.SelectedItem is ComboBoxItem item)
        {
            var formatStr = item.Content?.ToString();
            if (Enum.TryParse<OutputFormat>(formatStr, out var format))
            {
                ViewModel.DefaultOutputFormat = format;
            }
        }
    }

    private void CmbTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbTheme.SelectedItem is ComboBoxItem item && item.Tag is string theme)
        {
            ViewModel.SelectedTheme = theme;
        }
    }

    private void CmbLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbLanguage.SelectedItem is ComboBoxItem item && item.Tag is string lang)
        {
            ViewModel.SelectedLanguage = lang;
            LocalizationService.Instance.LoadLanguage(lang);
        }
    }

    private void CmbDefaultFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (cmbDefaultFormat.SelectedItem is ComboBoxItem item)
        {
            var formatStr = item.Content?.ToString();
            if (Enum.TryParse<OutputFormat>(formatStr, out var format))
            {
                ViewModel.DefaultOutputFormat = format;
            }
        }
    }

    private async void BtnBuscarActualizacion_Click(object sender, RoutedEventArgs e)
    {
        btnBuscarActualizacion.IsEnabled = false;
        var loc = LocalizationService.Instance;
        string originalContent = btnBuscarActualizacion.Content as string ?? "";
        btnBuscarActualizacion.Content = loc["MsgCheckingUpdate"] is string s && s != "MsgCheckingUpdate"
            ? s : "Comprobando...";
        try
        {
            OfiConvert.Services.GitHubReleaseInfo? info =
                await OfiConvert.Services.GitHubUpdateService.CheckForUpdateAsync();

            if (info is null)
            {
                btnBuscarActualizacion.Content = originalContent;
                var dialog = new ContentDialog
                {
                    Title = loc["TitleNoUpdates"] is string t && t != "TitleNoUpdates" ? t : "Sin actualizaciones",
                    Content = new TextBlock { Text = loc["MsgNoUpdates"], TextWrapping = TextWrapping.Wrap },
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await dialog.ShowAsync();
            }
            else
            {
                _appUpdateUrl = info.DownloadUrl;
                _appUpdateChecksumUrl = info.ChecksumUrl;
                btnBuscarActualizacion.Content = $"\u2b06 {info.Version}";
                infoBarUpdate.Title = $"\u2b06 {loc["TitleUpdateAvailable"]}: {info.Version}";
                infoBarUpdate.Message = loc["MsgUpdateAvailable"];
                infoBarUpdate.IsOpen = true;

                var dialog = new ContentDialog
                {
                    Title = loc["TitleUpdateAvailable"],
                    Content = new TextBlock
                    {
                        Text = $"{loc["TitleUpdateAvailable"]}: {info.Version}\n\n{loc["MsgUpdateAvailable"]}",
                        TextWrapping = TextWrapping.Wrap
                    },
                    PrimaryButtonText = loc["BtnYes"] is string y && y != "BtnYes" ? y : "S\u00ed",
                    CloseButtonText = loc["BtnNo"] is string n && n != "BtnNo" ? n : "No",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = Content.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary)
                    BtnDownloadUpdate_Click(sender, e);
            }
        }
        finally
        {
            btnBuscarActualizacion.IsEnabled = true;
        }
    }

    private void ApplyFormatSelection(OutputFormat format)
    {
        var idx = format switch
        {
            OutputFormat.PDF => 0,
            OutputFormat.HTML => 1,
            OutputFormat.CSV => 2,
            OutputFormat.PNG => 3,
            OutputFormat.JPG => 4,
            _ => 0
        };
        if (cmbFormat.Items.Count > idx) cmbFormat.SelectedIndex = idx;
        if (cmbDefaultFormat.Items.Count > idx) cmbDefaultFormat.SelectedIndex = idx;
    }

    private void ApplyThemeComboSelection(string theme)
    {
        for (int i = 0; i < cmbTheme.Items.Count; i++)
        {
            if (cmbTheme.Items[i] is ComboBoxItem item && item.Tag as string == theme)
            {
                cmbTheme.SelectedIndex = i;
                break;
            }
        }
    }

    private void ApplyLanguageSelection(string lang)
    {
        for (int i = 0; i < cmbLanguage.Items.Count; i++)
        {
            if (cmbLanguage.Items[i] is ComboBoxItem item && item.Tag as string == lang)
            {
                cmbLanguage.SelectedIndex = i;
                break;
            }
        }
    }
}
