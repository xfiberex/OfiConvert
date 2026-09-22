using System.Text.RegularExpressions;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// Toda salida de la app tiene que pasar por el cierre ordenado.
/// </summary>
/// <remarks>
/// El riesgo declarado de este programa es dejar <b>procesos de Office huérfanos</b>: por eso existen la
/// confirmación al cerrar con una conversión en curso, la cancelación del lote y el <c>Dispose</c> del
/// ViewModel. Todo eso colgaba de <c>OnAppWindowClosing</c>… y la instalación de una actualización
/// terminaba en <c>Application.Current.Exit()</c>, que <b>no pasa por ahí</b> (TJ-15): se saltaba las tres
/// cosas, y el botón que la dispara ni siquiera se apagaba mientras se convertía.
///
/// No hay forma de probarlo desde la interfaz —el botón vive en una <c>InfoBar</c> que solo aparece si hay
/// actualización publicada, y los UI tests no convierten nada a propósito—, así que se vigila la
/// <b>estructura</b>: es barato, y es exactamente la costura que se rompió.
/// </remarks>
public sealed class ShutdownPathsTests
{
    private static string MainWindowCode =>
        File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "MainWindow.xaml.cs"));

    /// <summary>
    /// Ningún <c>Application.Current.Exit()</c> suelto: antes tiene que soltarse lo que la app tiene
    /// cogido.
    /// </summary>
    [Fact]
    public void NingunaSalida_SeSaltaElCierreOrdenado()
    {
        string[] lines = MainWindowCode.Split('\n');
        var sueltas = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            // Los comentarios se saltan: el que explica ESTA misma trampa, unas líneas más arriba en
            // MainWindow, nombra la llamada y ponía el test en rojo sin que hubiera nada roto.
            if (lines[i].TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
            if (!lines[i].Contains("Application.Current.Exit()", StringComparison.Ordinal)) continue;

            bool ordenada = false;
            for (int j = Math.Max(0, i - 12); j < i; j++)
            {
                if (lines[j].Contains("ShutdownForUpdateAsync", StringComparison.Ordinal) ||
                    lines[j].Contains("ReleaseResources", StringComparison.Ordinal))
                {
                    ordenada = true;
                    break;
                }
            }

            if (!ordenada) sueltas.Add($"línea {i + 1}: {lines[i].Trim()}");
        }

        Assert.True(sueltas.Count == 0,
            "Hay salidas que NO pasan por el cierre ordenado (ni cancelan el lote, ni guardan ajustes, ni "
                + "sueltan el ViewModel): quedarían procesos de Office huérfanos.\n  "
                + string.Join("\n  ", sueltas));
    }

    /// <summary>
    /// El flujo de actualización comprueba <c>CanClose()</c> antes de empezar: el botón apagado es una
    /// promesa de la interfaz, no una garantía.
    /// </summary>
    [Fact]
    public void ElFlujoDeActualizacion_CompruebaQueSePuedeCerrar()
    {
        var handler = Regex.Match(
            MainWindowCode,
            @"private async void BtnDownloadUpdate_Click.*?\n    \}",
            RegexOptions.Singleline);

        Assert.True(handler.Success, "No se encontró BtnDownloadUpdate_Click: este test se ha quedado obsoleto.");
        Assert.Contains("CanClose()", handler.Value, StringComparison.Ordinal);
    }

    /// <summary>
    /// Y el botón sigue el estado de la conversión: sin esto, se puede pulsar a mitad de un lote.
    /// </summary>
    [Fact]
    public void ElBotonDeInstalar_SigueAlEstadoDeConversion()
    {
        string code = MainWindowCode;

        Assert.Contains("nameof(MainViewModel.IsConverting)", code, StringComparison.Ordinal);
        Assert.Matches(new Regex(@"btnInstalarUpdate\.IsEnabled\s*=\s*!string\.IsNullOrEmpty\(_appUpdateUrl\)\s*&&\s*ViewModel\.CanClose\(\)"), code);
    }
}
