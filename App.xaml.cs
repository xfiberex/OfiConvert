using Microsoft.UI.Xaml;
using OfiConvert.Services;

namespace OfiConvert;

public partial class App : Application
{
    private Window? _window;
    private readonly string[] _args;

    public static Window? MainWindow { get; private set; }

    public App(string[] args)
    {
        _args = args;
        InitializeComponent();
        UnhandledException += (s, e) =>
        {
            File.WriteAllText(
                Path.Combine(AppContext.BaseDirectory, "crash.log"),
                $"UnhandledException: {e.Exception}");
            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        LoggingService.Initialize();

        _window = new MainWindow();
        MainWindow = _window;
        _window.Activate();
    }
}
