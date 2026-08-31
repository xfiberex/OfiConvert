using System.Diagnostics;
using OfiConvert.Services;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// El cuelgue eterno de la tubería (TJ-02), reproducido sin necesitar LibreOffice.
/// </summary>
/// <remarks>
/// Con <c>stdout</c> y <c>stderr</c> redirigidos y sin leer, el búfer de la tubería (~4 KB) se llena, el
/// proceso hijo <b>se bloquea escribiendo</b> y la espera no vuelve nunca. En la app eso era una
/// conversión congelada <b>para siempre</b>, ocupando una plaza del semáforo de paralelismo, sin error y
/// sin registro. Aquí se le hace escupir 64 KB, dieciséis veces el búfer: si alguien vuelve a esperar
/// antes de leer, estas pruebas dejan de terminar y el <c>Wait</c> con plazo las pone en rojo.
/// </remarks>
public sealed class ProcessRunnerTests : IDisposable
{
    private static readonly TimeSpan Plazo = TimeSpan.FromSeconds(30);
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"OfiConvertTests-{Guid.NewGuid():N}");

    public ProcessRunnerTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch { /* limpieza best-effort */ }
    }

    /// <summary>Un archivo de 64 KB que el proceso volcará por el flujo que se le pida.</summary>
    private string BigFile()
    {
        string path = Path.Combine(_folder, "ruido.txt");
        File.WriteAllText(path, string.Concat(Enumerable.Repeat("LibreOffice avisa de algo muy repetitivo.\n", 1600)));
        Assert.True(new FileInfo(path).Length > 64 * 1024);
        return path;
    }

    private static ProcessStartInfo Cmd(string command)
        => new("cmd.exe", $"/c {command}");

    /// <summary>Se esperaría eternamente si se leyera después de esperar: 64 KB por stdout.</summary>
    [Fact]
    public async Task SalidaEnorme_PorStdout_NoCuelga()
    {
        var run = await SinColgarse(ProcessRunner.RunAsync(Cmd($"type \"{BigFile()}\"")),
            "el búfer de stdout se llenó y nadie estaba leyendo");

        Assert.Equal(0, run.ExitCode);
        Assert.True(run.StandardOutput.Length > 64 * 1024);
    }

    /// <summary>Y por stderr, que es por donde soffice escribe sus avisos.</summary>
    [Fact]
    public async Task SalidaEnorme_PorStderr_NoCuelga()
    {
        var run = await SinColgarse(ProcessRunner.RunAsync(Cmd($"type \"{BigFile()}\" 1>&2")),
            "el búfer de stderr se llenó y nadie estaba leyendo");

        Assert.True(run.StandardError.Length > 64 * 1024);
    }

    /// <summary>
    /// Los DOS a la vez. Leer solo el flujo que se usa no basta: el que se queda sin leer es exactamente
    /// el que llena la tubería y bloquea al hijo.
    /// </summary>
    [Fact]
    public async Task SalidaEnorme_PorLosDosFlujos_NoCuelga()
    {
        string big = BigFile();
        var run = await SinColgarse(ProcessRunner.RunAsync(Cmd($"type \"{big}\" & type \"{big}\" 1>&2")),
            "los dos flujos se llenaron a la vez");

        Assert.True(run.StandardOutput.Length > 64 * 1024);
        Assert.True(run.StandardError.Length > 64 * 1024);
    }

    [Fact]
    public async Task CodigoDeSalidaYFlujos_LleganCompletos()
    {
        var run = await SinColgarse(ProcessRunner.RunAsync(Cmd("echo hola & echo ay 1>&2 & exit /b 3")),
            "ni siquiera con una salida diminuta");

        Assert.Equal(3, run.ExitCode);
        Assert.Contains("hola", run.StandardOutput);
        Assert.Contains("ay", run.StandardError);
    }

    /// <summary>
    /// Espera con plazo: si <see cref="ProcessRunner"/> vuelve a leer después de esperar, esto es lo que
    /// convierte un test colgado —que no dice nada y se queda ahí— en un fallo con su explicación.
    /// </summary>
    private static async Task<ProcessOutput> SinColgarse(Task<ProcessOutput> run, string porque)
    {
        try
        {
            return await run.WaitAsync(Plazo);
        }
        catch (TimeoutException)
        {
            Assert.Fail($"ProcessRunner se colgó: {porque}.");
            throw;   // inalcanzable; Assert.Fail siempre lanza
        }
    }
}
