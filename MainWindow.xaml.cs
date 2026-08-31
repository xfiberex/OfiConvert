using System.ComponentModel;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OfiConvert.Behaviors;
using OfiConvert.Core;
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

        // La versión sale del ensamblado, no de una constante: es la MISMA fuente contra la que el
        // updater compara el tag del release, así que aquí no puede mentir.
        txtAboutVersion.Text = $"OfiConvert {LegalText.Version()}  ·  MIT  ·  Ricky Angel Jimenez Bueno";

        _ = CheckForAppUpdateAsync();
    }

    private void BtnLicencia_Click(object sender, RoutedEventArgs e)
        => _ = ShowLegalDialogAsync(LocalizationService.Instance["BtnLicense"], LegalText.License());

    private void BtnAvisosTerceros_Click(object sender, RoutedEventArgs e)
        => _ = ShowLegalDialogAsync(LocalizationService.Instance["BtnThirdParty"], LegalText.ThirdParty());

    /// <summary>
    /// Muestra un texto legal embebido en el <c>.exe</c>. Monoespaciado porque los avisos vienen
    /// maquetados a 88 columnas (y la licencia Apache, con su sangrado): con fuente proporcional se
    /// deshace.
    /// </summary>
    private async Task ShowLegalDialogAsync(string title, string body)
    {
        // LegalText es defensivo y devuelve "" si el recurso embebido faltara. Se dice con todas las
        // letras, en vez de abrir un diálogo en blanco que parece un cuelgue de la app.
        if (string.IsNullOrWhiteSpace(body))
            body = LocalizationService.Instance["MsgLegalUnavailable"];

        var text = new TextBlock
        {
            Text = body,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12
        };
        AutomationProperties.SetAutomationId(text, "txtLegalBody");

        var dialog = new ContentDialog
        {
            Title = title,
            Content = new ScrollViewer
            {
                Content = text,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MaxHeight = 440,
                // Los avisos vienen maquetados a ~80 columnas monoespaciadas. Con el ancho por defecto del
                // ContentDialog (~548 px) cada línea larga se parte una vez y deja un resto suelto ("copy",
                // "deal"…): un texto legal que parece roto. Se le da sitio para que la línea entera quepa.
                MinWidth = 700
            },
            CloseButtonText = LocalizationService.Instance["TipClose"],
            XamlRoot = Content.XamlRoot,
            RequestedTheme = RootTheme
        };
        dialog.Resources["ContentDialogMaxWidth"] = 760.0;

        await dialog.ShowAsync();
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

    // Un ContentDialog se enraíza en la capa de POPUPS (hermana de Content), no dentro de Content, así que
    // NO hereda el RequestedTheme que ApplyTheme fija en el root: se queda en el tema del SISTEMA. Con la app
    // en Claro sobre un Windows en Oscuro, los diálogos salían negros. Se les pasa el tema del root a mano.
    private ElementTheme RootTheme => (Content as FrameworkElement)?.RequestedTheme ?? ElementTheme.Default;

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
                XamlRoot = Content.XamlRoot,
                RequestedTheme = RootTheme
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
        // El botón se deshabilita y ya está: el progreso lo cuenta la InfoBar ("Descargando... 42%"). Antes
        // se le metía aquí un texto en duro, en español, para los ocho idiomas.
        pbUpdate.Visibility = Visibility.Visible;
        infoBarUpdate.IsClosable = false;

        var loc = LocalizationService.Instance;

        var progress = new Progress<double>(p =>
        {
            pbUpdate.Value = p;
            infoBarUpdate.Message = string.Format(loc["MsgDownloading"], p.ToString("P0"));
        });

        try
        {
            // DownloadInstallerAsync VERIFICA lo descargado (firma Authenticode o, en su defecto, el
            // SHA-256 publicado como asset) y lanza si no lo supera, tras borrar el archivo. Si llega
            // aqu\u00ed una ruta, es de un instalador en el que se conf\u00eda.
            string installerPath = await OfiConvert.Services.GitHubUpdateService
                .DownloadInstallerAsync(_appUpdateUrl, _appUpdateChecksumUrl, progress);
            infoBarUpdate.Message = loc["MsgInstalling"];

            // /ALLUSERS o /CURRENTUSER: SIN ellos, Inno planta el di\u00e1logo "Seleccione el modo de
            // instalaci\u00f3n" AUNQUE se le pase /VERYSILENT, y se queda esperando un clic \u2014 con esta app ya
            // cerrada. Se le manda el modo con el que el usuario instal\u00f3 (ver Core/InstallScope).
            var scope = InstallScope.InnoSwitchForCurrentInstall();

            var installer = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(installerPath)
            {
                Arguments = InstallScope.SilentInstallArguments(scope),
                UseShellExecute = true
            });

            // Instalada para todos los usuarios, el instalador PIDE UAC. Si el usuario dice que no, o si
            // muere enseguida por cualquier motivo, la app NO se cierra: antes se cerraba igual, 1,5 s
            // despu\u00e9s de lanzarlo y sin mirar nada, as\u00ed que el usuario ve\u00eda su programa esfumarse, segu\u00eda
            // en la versi\u00f3n vieja y no recib\u00eda explicaci\u00f3n alguna.
            if (installer is not null && installer.WaitForExit(4000) && installer.ExitCode != 0)
                throw new InvalidOperationException(string.Format(loc["MsgUpdateInstallFailed"], installer.ExitCode));

            Application.Current.Exit();
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)   // ERROR_CANCELLED
        {
            // El usuario dijo que NO al UAC. No es un fallo: es una decisi\u00f3n suya, y se le trata como tal.
            pbUpdate.Visibility = Visibility.Collapsed;
            btnInstalarUpdate.IsEnabled = true;
            infoBarUpdate.IsClosable = true;
            infoBarUpdate.Severity = InfoBarSeverity.Warning;
            infoBarUpdate.Message = loc["MsgUpdateElevationDenied"];
            Serilog.Log.Information("Actualizaci\u00f3n cancelada: el usuario no concedi\u00f3 permisos de administrador");
        }
        catch (Exception ex)
        {
            pbUpdate.Visibility = Visibility.Collapsed;
            btnInstalarUpdate.IsEnabled = true;
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

        // Las claves se piden directamente. Los "fallbacks defensivos" (loc[k] != k ? loc[k] : "texto")
        // son los que llevan tapando claves inexistentes desde el principio: MsgCheckingUpdate NO existía,
        // y este botón decía "Comprobando..." en los ocho idiomas sin que nada fallara. Si falta una clave,
        // que la cacen LocalizationUsageTests y HardcodedUiTextTests.
        btnBuscarActualizacion.Content = loc["MsgCheckingUpdate"];
        try
        {
            OfiConvert.Services.GitHubReleaseInfo? info =
                await OfiConvert.Services.GitHubUpdateService.CheckForUpdateAsync();

            if (info is null)
            {
                btnBuscarActualizacion.Content = originalContent;
                var dialog = new ContentDialog
                {
                    Title = loc["TitleNoUpdates"],
                    Content = new TextBlock { Text = loc["MsgNoUpdates"], TextWrapping = TextWrapping.Wrap },
                    CloseButtonText = loc["BtnOk"],
                    XamlRoot = Content.XamlRoot,
                    RequestedTheme = RootTheme
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
                    PrimaryButtonText = loc["BtnYes"],
                    CloseButtonText = loc["BtnNo"],
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = Content.XamlRoot,
                    RequestedTheme = RootTheme
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
