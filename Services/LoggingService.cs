using OfiConvert.Helpers;
using Serilog;
using System.IO;

namespace OfiConvert.Services;

public static class LoggingService
{
    private static readonly string LogFolder = AppPaths.LogFolder;

    public static void Initialize()
    {
        Directory.CreateDirectory(LogFolder);

        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Information()
#endif
            .WriteTo.File(
                Path.Combine(LogFolder, "oficonvert-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                fileSizeLimitBytes: 10 * 1024 * 1024,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("=== OfiConvert iniciado ===");
    }

    public static void Shutdown()
    {
        Log.Information("=== OfiConvert cerrado ===");
        Log.CloseAndFlush();
    }
}
