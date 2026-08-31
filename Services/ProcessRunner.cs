using System.Diagnostics;

namespace OfiConvert.Services;

/// <summary>Salida completa de un proceso ya terminado.</summary>
/// <param name="ExitCode">Código de salida.</param>
/// <param name="StandardOutput">Todo lo que escribió por <c>stdout</c>.</param>
/// <param name="StandardError">Todo lo que escribió por <c>stderr</c>.</param>
public readonly record struct ProcessOutput(int ExitCode, string StandardOutput, string StandardError);

/// <summary>
/// Lanza un proceso y recoge su salida <b>sin que se pueda quedar colgado</b>.
/// </summary>
/// <remarks>
/// Existe por un cuelgue eterno, no por gusto de abstraer (TJ-02, 2026-08-31).
///
/// Con <c>stdout</c> y <c>stderr</c> redirigidos y <b>sin leer</b>, el búfer de la tubería (~4 KB) se
/// llena, el hijo <b>se bloquea escribiendo</b> y <c>WaitForExitAsync</c> no vuelve <b>nunca</b>: la
/// conversión se congelaba para siempre ocupando una plaza del semáforo de paralelismo — y unas cuantas
/// así dejaban la app sin convertir nada más, sin error, sin registro y sin forma de saber por qué.
/// LibreOffice llega a ese tamaño con solo arrastrar unos avisos de fuentes o de macros.
///
/// La regla, que no admite atajos: <b>empezar a leer los dos flujos ANTES de esperar al proceso</b>. Y no
/// vale con leer solo el que se usa: el que se queda sin leer es exactamente el que llena la tubería.
/// Está aquí, fuera del servicio, para poder probarlo con un proceso que escupa 64 KB sin necesitar
/// LibreOffice instalado (<c>ProcessRunnerTests</c>).
/// </remarks>
public static class ProcessRunner
{
    /// <summary>Ejecuta el proceso hasta el final y devuelve su código de salida y sus dos flujos.</summary>
    public static async Task<ProcessOutput> RunAsync(ProcessStartInfo startInfo, CancellationToken cancellationToken = default)
    {
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        // Las dos lecturas ARRANCAN antes del WaitForExitAsync. Invertir este orden es el cuelgue.
        var stdout = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderr = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        return new ProcessOutput(process.ExitCode, await stdout, await stderr);
    }
}
