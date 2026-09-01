using System.Text.RegularExpressions;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// Ningún diálogo puede abrirse <b>dentro de un bucle</b>.
/// </summary>
/// <remarks>
/// 🔴 WinUI admite <b>un</b> <c>ContentDialog</c> a la vez: el segundo <b>lanza</b>. Y
/// <c>IDialogService.ShowInformation</c> / <c>ShowError</c> son <c>async void</c> —dispara y olvida—, así
/// que esa excepción sale <b>sin dueño</b>, la recoge <c>App.UnhandledException</c> y el usuario <b>no ve
/// nada</b>: ni el aviso que se le quería dar ni el error. Se queda mirando una lista a la que le faltan
/// archivos, sin explicación.
///
/// Así estaba <c>AddFiles</c>: avisaba archivo por archivo dentro del bucle, y bastaba soltar dos
/// documentos de más de 500 MB (TJ-13). La forma correcta es <b>acumular y avisar una vez al terminar</b>.
///
/// Esta prueba es estructural, como <c>InstallerScriptTests</c>: el fallo solo se ve arrancando WinUI con
/// dos archivos enormes de verdad, así que se vigila la forma del código, que es lo que sí se puede leer.
/// </remarks>
public sealed class DialogsInLoopsTests
{
    private static readonly Regex AbreBucle = new(@"^\s*(foreach|for|while|do)\s*[\(\{]", RegexOptions.Compiled);
    private static readonly Regex AbreDialogo = new(
        @"_dialogService\.(ShowInformation|ShowError|ShowWarning|ShowConfirmationAsync)\s*\(",
        RegexOptions.Compiled);

    [Fact]
    public void NingunDialogoSeAbreDentroDeUnBucle()
    {
        var culpables = new List<string>();
        var revisados = 0;

        foreach (var archivo in TestPaths.AppSourceFiles("*.cs"))
        {
            revisados++;
            var relativo = Path.GetRelativePath(TestPaths.RepoRoot, archivo);
            var lineas = File.ReadAllLines(archivo);

            // Profundidad de llaves a la que empezó cada bucle todavía abierto.
            var buclesAbiertos = new Stack<int>();
            var profundidad = 0;
            var bucleEsperandoLlave = false;

            foreach (var (linea, numero) in lineas.Select((l, i) => (l, i + 1)))
            {
                var codigo = SinComentario(linea);

                if (AbreBucle.IsMatch(codigo))
                    bucleEsperandoLlave = true;

                if (AbreDialogo.IsMatch(codigo) && buclesAbiertos.Count > 0)
                    culpables.Add($"{relativo}:{numero} → {codigo.Trim()}");

                foreach (var c in codigo)
                {
                    if (c == '{')
                    {
                        profundidad++;
                        if (bucleEsperandoLlave)
                        {
                            buclesAbiertos.Push(profundidad);
                            bucleEsperandoLlave = false;
                        }
                    }
                    else if (c == '}')
                    {
                        if (buclesAbiertos.Count > 0 && buclesAbiertos.Peek() == profundidad)
                            buclesAbiertos.Pop();
                        profundidad--;
                    }
                }
            }
        }

        Assert.True(revisados > 10, $"Solo se revisaron {revisados} archivos: el descubrimiento está roto.");

        Assert.True(culpables.Count == 0,
            "Diálogos abiertos dentro de un bucle. WinUI solo admite uno a la vez y estos métodos son "
                + "async void, así que el segundo lanza sin dueño y el usuario no ve NADA. Acumula y avisa "
                + "una sola vez al terminar el bucle:\n  " + string.Join("\n  ", culpables));
    }

    /// <summary>Se quitan los comentarios: uno que hable de ShowInformation no es una llamada.</summary>
    private static string SinComentario(string linea)
    {
        var i = linea.IndexOf("//", StringComparison.Ordinal);
        return i >= 0 ? linea[..i] : linea;
    }
}
