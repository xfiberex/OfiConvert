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
    private AppWindow _appWindow = null!;

    public MainWindow()
    {
        ViewModel = new MainViewModel();
        InitializeComponent();

        // Set DataContext for Binding (x:Bind on Window not supported - Window is not FrameworkElement)
        if (Content is FrameworkElement root)
            root.DataContext = ViewModel;

        // Set up AppWindow reference
        var hWnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(hWnd);
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

        DispatcherQueue.TryEnqueue(async () =>
        {
            var loc = LocalizationService.Instance;
            var title = "OfiConvert";
            var text = e.ErrorCount == 0
                ? string.Format(loc["TrayNotifSuccess"], e.SuccessCount)
                : string.Format(loc["TrayNotifErrors"], e.SuccessCount, e.ErrorCount);

            var dialog = new ContentDialog
            {
                Title = title,
                Content = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap },
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot,
                DefaultButton = ContentDialogButton.Close
            };

            await dialog.ShowAsync();
        });
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

            var dialog = new ContentDialog
            {
                Title = LocalizationService.Instance["TitleConfirmClose"] is string t && t != "TitleConfirmClose" ? t : "Confirmar cierre",
                Content = new TextBlock
                {
                    Text = "Hay una conversi\u00f3n en curso. Si cierras ahora, los procesos de Office podr\u00edan quedar abiertos.\n\n\u00bfDeseas cancelar la conversi\u00f3n y salir?",
                    TextWrapping = TextWrapping.Wrap
                },
                PrimaryButtonText = LocalizationService.Instance["BtnYes"] is string y && y != "BtnYes" ? y : "S\u00ed",
                CloseButtonText = LocalizationService.Instance["BtnNo"] is string n && n != "BtnNo" ? n : "No",
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
            string installerPath = await OfiConvert.Services.GitHubUpdateService
                .DownloadInstallerAsync(_appUpdateUrl, progress);
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
            infoBarUpdate.Message = $"Error al descargar: {ex.Message}";
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
