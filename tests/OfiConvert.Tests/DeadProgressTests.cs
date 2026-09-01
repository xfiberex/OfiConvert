using System.Text.RegularExpressions;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// Un <c>IProgress&lt;T&gt;</c> que nadie reporta es una promesa rota que compila.
/// </summary>
/// <remarks>
/// Lo hubo durante versiones (TJ-19): <c>IProgress&lt;ConversionProgress&gt;</c> atravesaba las dos
/// firmas de <c>IFileConversionService</c>, las dos implementaciones y el ViewModel — que construía un
/// <c>Progress&lt;&gt;</c> con el mensaje «Convirtiendo 3/7». <b>Ningún motor llamaba a
/// <c>Report</c></b>, así que el mensaje no apareció jamás y el modelo <c>ConversionProgress</c> estaba
/// muerto. Nada fallaba: compilaba, pasaba, y mentía.
///
/// La regla que deja escrita: <b>si un archivo declara un parámetro de progreso, ese archivo tiene que
/// reportarlo</b>. Quien no pueda reportar, que no lo pida.
/// </remarks>
public sealed class DeadProgressTests
{
    private static readonly Regex DeclaraProgreso = new(@"IProgress<[^>]+>\??\s+\w+", RegexOptions.Compiled);
    private static readonly Regex Reporta = new(@"\.Report\s*\(", RegexOptions.Compiled);

    [Fact]
    public void TodoParametroDeProgresoSeReportaDeVerdad()
    {
        var mentirosos = new List<string>();
        var revisados = 0;

        foreach (var archivo in TestPaths.AppSourceFiles("*.cs"))
        {
            revisados++;
            var codigo = File.ReadAllText(archivo);

            if (!DeclaraProgreso.IsMatch(codigo)) continue;
            if (Reporta.IsMatch(codigo)) continue;

            // Una interfaz puede declararlo sin reportarlo, pero entonces alguna implementación tiene que
            // hacerlo — y eso es justo lo que aquí no pasaba, así que no se exime a nadie.
            mentirosos.Add(Path.GetRelativePath(TestPaths.RepoRoot, archivo));
        }

        Assert.True(revisados > 10, $"Solo se revisaron {revisados} archivos: el descubrimiento está roto.");

        Assert.True(mentirosos.Count == 0,
            "Estos archivos piden un IProgress<> y no reportan nada: la barra o el mensaje que prometen no "
                + "se moverá jamás. O se reporta, o se quita el parámetro:\n  "
                + string.Join("\n  ", mentirosos));
    }
}
