using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using OfiConvert.Helpers;
using OfiConvert.Services;
using Serilog;

namespace OfiConvert;

public partial class App : Application
{
    private MainWindow? _window;
    private readonly string[] _args;

    public static Window? MainWindow { get; private set; }

    public App(string[] args)
    {
        _args = args;
        InitializeComponent();
        UnhandledException += (s, e) =>
        {
            AppPaths.WriteCrashLog($"UnhandledException: {e.Exception}");
            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        LoggingService.Initialize();

        _window = new MainWindow();
        MainWindow = _window;
        _window.Activate();

        // Activaciones de OTRAS instancias que se redirigen a esta (ver Program.TryRedirectToPrimaryInstance).
        AppInstance.GetCurrent().Activated += OnRedirectedActivation;

        // Activación propia: los archivos del menú contextual del Explorador llegan como argumentos.
        var files = ActivationArguments.GetOfficeFiles(_args);
        if (files.Count > 0)
            _window.EnqueueFromActivation(files);
    }

    private void OnRedirectedActivation(object? sender, AppActivationArguments args)
    {
        // Llega en un hilo del pool, no en el de la UI.
        var files = GetFilesFromActivation(args);
        if (files.Count == 0 || _window is null)
            return;

        _window.DispatcherQueue.TryEnqueue(() => _window.EnqueueFromActivation(files));
    }

    private static List<string> GetFilesFromActivation(AppActivationArguments args)
    {
        try
        {
            // En unpackaged, la activación de tipo Launch trae la línea de comandos completa en una
            // sola cadena (ruta del .exe incluida); ActivationArguments se encarga de filtrarla.
            if (args.Data is Windows.ApplicationModel.Activation.ILaunchActivatedEventArgs launch)
                return ActivationArguments.GetOfficeFiles(launch.Arguments);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "No se pudieron leer los argumentos de la activación redirigida");
        }

        return [];
    }
}
