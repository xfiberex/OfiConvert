using OfiConvert.Services;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// El log tiene que seguir escribiendo cuando llega al límite de tamaño (TJ-31).
/// </summary>
/// <remarks>
/// Con <c>fileSizeLimitBytes</c> y sin <c>rollOnFileSizeLimit</c>, Serilog se calla al llegar al límite
/// hasta el día siguiente. Aquí se usa el logger real con un límite diminuto: si deja de rotar, lo que
/// se escribe después del límite no aparece en ningún archivo.
/// </remarks>
public sealed class LoggingServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"OfiConvertLog-{Guid.NewGuid():N}");

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch { /* limpieza best-effort */ }
    }

    [Fact]
    public void AlLlegarAlLimite_AbreOtroArchivo_YNoPierdeLoQueSigue()
    {
        using (var logger = LoggingService.CreateLogger(_folder, fileSizeLimitBytes: 1024))
        {
            for (int i = 0; i < 50; i++)
                logger.Information("Entrada {Numero} de un lote grande con errores", i);

            logger.Information("ÚLTIMA entrada, la que se iba a consultar");
        }

        string[] files = Directory.GetFiles(_folder, "oficonvert-*.log");

        Assert.Contains(files, f => Path.GetFileNameWithoutExtension(f).EndsWith("_001", StringComparison.Ordinal));
        Assert.Contains(files, f => File.ReadAllText(f).Contains("ÚLTIMA entrada", StringComparison.Ordinal));
    }
}
