using System.Windows;
using OfiConvert.Services;

namespace OfiConvert
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            LoggingService.Initialize();

            // Load saved settings and apply theme/language
            var settings = new SettingsService().Load();

            // Apply theme (System = auto-detect from Windows)
            if (settings.Theme == "System")
            {
                Wpf.Ui.Appearance.ApplicationThemeManager.ApplySystemTheme();
            }
            else
            {
                var theme = settings.Theme == "Dark"
                    ? Wpf.Ui.Appearance.ApplicationTheme.Dark
                    : Wpf.Ui.Appearance.ApplicationTheme.Light;
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(
                    theme,
                    Wpf.Ui.Controls.WindowBackdropType.Mica,
                    true);
            }

            // Apply language
            var cultureName = settings.Language == "en" ? "en-US" : "es-ES";
            var langDict = new ResourceDictionary
            {
                Source = new System.Uri($"pack://application:,,,/Lang/{cultureName}.xaml")
            };
            var existing = Resources.MergedDictionaries
                .FirstOrDefault(d => d.Source?.OriginalString.Contains("/Lang/") == true);
            if (existing is not null)
                Resources.MergedDictionaries.Remove(existing);
            Resources.MergedDictionaries.Add(langDict);

            // Handle command-line arguments (from Explorer context menu)
            if (e.Args.Length > 0)
            {
                var mainWindow = new MainWindow();
                mainWindow.ViewModel.AddFiles(e.Args);
                MainWindow = mainWindow;
                mainWindow.Show();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            LoggingService.Shutdown();
            base.OnExit(e);
        }
    }
}
