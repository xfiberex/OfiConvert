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

        // La app SIGUE el acento del sistema (WinUI lo hace solo, sin override). Esta puerta trasera es
        // EXCLUSIVA para las capturas: tools/capture-ui-states.ps1 fija OFICONVERT_ACCENT para que las
        // imágenes del README no salgan con el acento personal de quien las genera, sino con uno neutro y
        // reproducible. Sin la variable —es decir, SIEMPRE en producción— este método no hace nada.
        ApplyCaptureAccentOverride();

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

    // ── Acento reproducible para las capturas (ver OnLaunched) ────────────────────────────────────
    // WinUI construye los pinceles de acento (AccentFillColor*, AccentTextFillColor*, el subrayado del
    // Pivot, AccentButtonStyle…) a partir de SIETE recursos Color: SystemAccentColor y sus seis tintes
    // Light1-3 / Dark1-3, que en un equipo real los pone Windows. Se inyectan aquí, en el diccionario de
    // la aplicación, para que sombreen a los del XamlControlsResources ANTES de que se cargue la ventana.

    private void ApplyCaptureAccentOverride()
    {
        var hex = Environment.GetEnvironmentVariable("OFICONVERT_ACCENT");
        if (string.IsNullOrWhiteSpace(hex) || !TryParseHexColor(hex, out var accent))
            return;

        var res = Resources;
        res["SystemAccentColor"] = accent;
        res["SystemAccentColorLight1"] = Blend(accent, 255, 0.20);
        res["SystemAccentColorLight2"] = Blend(accent, 255, 0.40);
        res["SystemAccentColorLight3"] = Blend(accent, 255, 0.60);
        res["SystemAccentColorDark1"] = Blend(accent, 0, 0.20);
        res["SystemAccentColorDark2"] = Blend(accent, 0, 0.40);
        res["SystemAccentColorDark3"] = Blend(accent, 0, 0.60);
    }

    private static bool TryParseHexColor(string hex, out Windows.UI.Color color)
    {
        color = default;
        hex = hex.Trim().TrimStart('#');
        if (hex.Length != 6 ||
            !byte.TryParse(hex.AsSpan(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var r) ||
            !byte.TryParse(hex.AsSpan(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g) ||
            !byte.TryParse(hex.AsSpan(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
            return false;

        color = Windows.UI.Color.FromArgb(255, r, g, b);
        return true;
    }

    // Mezcla cada canal hacia 'target' (255 = aclarar, 0 = oscurecer) en la proporción 'amount'.
    private static Windows.UI.Color Blend(Windows.UI.Color c, int target, double amount)
    {
        byte Mix(byte channel) => (byte)Math.Round(channel + (target - channel) * amount);
        return Windows.UI.Color.FromArgb(255, Mix(c.R), Mix(c.G), Mix(c.B));
    }
}
