using OfiConvert.Helpers;
using Serilog;
using System.IO;

namespace OfiConvert.Services;

public static class LoggingService
{
    private static readonly string LogFolder = AppPaths.LogFolder;

    internal const long FileSizeLimitBytes = 10 * 1024 * 1024;

    public static void Initialize()
    {
        Log.Logger = CreateLogger(LogFolder);

        Log.Information("=== OfiConvert iniciado ===");
    }

    /// <summary>
    /// Construye el logger de la app. Separado de <see cref="Initialize"/> para poder probarlo con una
    /// carpeta y un límite de tamaño propios.
    /// </summary>
    /// <remarks>
    /// <c>rollOnFileSizeLimit</c> no es opcional (TJ-31): sin él, al llegar al límite Serilog
    /// <b>deja de escribir</b> el resto del día en vez de abrir <c>oficonvert-YYYYMMDD_001.log</c>, y un
    /// lote grande con errores pierde justo el registro que se iba a consultar.
    /// </remarks>
    internal static Serilog.Core.Logger CreateLogger(string folder, long fileSizeLimitBytes = FileSizeLimitBytes)
    {
        Directory.CreateDirectory(folder);

        return new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .WriteTo.File(
                Path.Combine(folder, "oficonvert-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: fileSizeLimitBytes,
                rollOnFileSizeLimit: true,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    public static void Shutdown()
    {
        Log.Information("=== OfiConvert cerrado ===");
        Log.CloseAndFlush();
    }
}
