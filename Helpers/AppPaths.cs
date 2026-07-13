namespace OfiConvert.Helpers;

/// <summary>
/// Rutas de los datos del usuario, todas bajo %AppData%\OfiConvert\.
/// </summary>
public static class AppPaths
{
    /// <summary>%AppData%\OfiConvert</summary>
    public static string DataFolder { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OfiConvert");

    public static string LogFolder { get; } = Path.Combine(DataFolder, "logs");
    public static string CrashLog { get; } = Path.Combine(DataFolder, "crash.log");
    public static string Settings { get; } = Path.Combine(DataFolder, "settings.json");
    public static string History { get; } = Path.Combine(DataFolder, "history.json");
    public static string Queue { get; } = Path.Combine(DataFolder, "queue.json");

    /// <summary>
    /// Vuelca un fallo irrecuperable a %AppData%. No puede usar Serilog: se invoca también cuando el
    /// arranque revienta antes de inicializarlo.
    /// </summary>
    public static void WriteCrashLog(string content)
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            File.WriteAllText(CrashLog, content);
        }
        catch
        {
            // Un fallo al registrar el fallo no debe generar otro.
        }
    }
}
