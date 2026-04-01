using System.ComponentModel;
using System.Drawing;
using System.Windows;
using Wpf.Ui.Controls;
using OfiConvert.ViewModels;

namespace OfiConvert;

public partial class MainWindow : FluentWindow
{
    public MainViewModel ViewModel { get; }
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    public MainWindow()
    {
        ViewModel = new MainViewModel();
        DataContext = ViewModel;
        InitializeComponent();
        Closing += OnWindowClosing;
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        InitializeTrayIcon();
        ViewModel.OnConversionCompleted += OnConversionCompleted;
        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        // Only watch system theme changes when theme is "System"
        UpdateThemeWatcher(ViewModel.SelectedTheme);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedTheme))
        {
            UpdateThemeWatcher(ViewModel.SelectedTheme);
        }
    }

    private void UpdateThemeWatcher(string theme)
    {
        if (theme == "System")
        {
            Wpf.Ui.Appearance.SystemThemeWatcher.Watch(
                this,
                Wpf.Ui.Controls.WindowBackdropType.Mica,
                updateAccents: true);
        }
        else
        {
            Wpf.Ui.Appearance.SystemThemeWatcher.UnWatch(this);
        }
    }

    private void InitializeTrayIcon()
    {
        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "OfiConvert",
            Visible = false
        };

        // Use the app icon or a default one
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
                _trayIcon.Icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
        }
        catch
        {
            _trayIcon.Icon = System.Drawing.SystemIcons.Application;
        }

        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();
        contextMenu.Items.Add(GetLocalizedString("TrayShow"), null, (_, _) => RestoreFromTray());
        contextMenu.Items.Add("-");
        contextMenu.Items.Add(GetLocalizedString("TrayExit"), null, (_, _) =>
        {
            _trayIcon.Visible = false;
            System.Windows.Application.Current.Shutdown();
        });

        _trayIcon.ContextMenuStrip = contextMenu;
    }

    private void OnConversionCompleted(object? sender, MainViewModel.ConversionCompletedEventArgs e)
    {
        if (_trayIcon is null || !ViewModel.ShowNotifications) return;

        var title = "OfiConvert";
        var text = e.ErrorCount == 0
            ? string.Format(GetLocalizedString("TrayNotifSuccess"), e.SuccessCount)
            : string.Format(GetLocalizedString("TrayNotifErrors"), e.SuccessCount, e.ErrorCount);

        var icon = e.ErrorCount == 0
            ? System.Windows.Forms.ToolTipIcon.Info
            : System.Windows.Forms.ToolTipIcon.Warning;

        _trayIcon.Visible = true;
        _trayIcon.ShowBalloonTip(3000, title, text, icon);

        // Hide tray icon after a short delay if window is visible
        if (Visibility == Visibility.Visible)
        {
            HideTrayIconAfterDelayAsync();
        }
    }

    private async void HideTrayIconAfterDelayAsync()
    {
        await Task.Delay(5000);
        if (Visibility == Visibility.Visible && _trayIcon is not null)
            _trayIcon.Visible = false;
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        if (_trayIcon is not null)
            _trayIcon.Visible = false;
    }

    private void MinimizeToTray()
    {
        if (_trayIcon is not null)
        {
            _trayIcon.Visible = true;
            Hide();
        }
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        // Minimize to tray instead of closing if setting is enabled
        if (ViewModel.MinimizeToTray && ViewModel.CanClose())
        {
            e.Cancel = true;
            MinimizeToTray();
            return;
        }

        if (!ViewModel.CanClose())
        {
            var result = System.Windows.MessageBox.Show(
                "Hay una conversión en curso. Si cierras ahora, los procesos de Office podrían quedar abiertos.\n\n¿Deseas cancelar la conversión y salir?",
                "Confirmar cierre",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.No)
            {
                e.Cancel = true;
                return;
            }

            ViewModel.CancelConversionCommand.Execute(null);
        }

        // Cleanup
        Wpf.Ui.Appearance.SystemThemeWatcher.UnWatch(this);
        ViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        ViewModel.OnConversionCompleted -= OnConversionCompleted;
        ViewModel.SaveSettings();
        ViewModel.Dispose();

        if (_trayIcon is not null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }

    private static string GetLocalizedString(string key)
    {
        return System.Windows.Application.Current.TryFindResource(key) as string ?? key;
    }
}

