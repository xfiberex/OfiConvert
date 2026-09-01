using System.Text.Json;
using OfiConvert.Core;
using Xunit;

namespace OfiConvert.Tests.Core;

/// <summary>
/// Todo paquete NuGet cuya DLL viaja en el instalador tiene que estar <b>citado</b> en los avisos.
/// </summary>
/// <remarks>
/// 🔴 <b>Esta prueba existe porque la anterior avisaba de que no bastaba.</b> <c>LegalTextTests</c> dice
/// de sí misma: «<i>si mañana entra una dependencia nueva y nadie toca el archivo de avisos, esto no lo
/// caza</i>». Pasó exactamente eso, y no con una sino con <b>cuatro</b>:
/// <c>System.Drawing.Common</c>, <c>Microsoft.Win32.SystemEvents</c>, <c>System.Numerics.Tensors</c> y
/// <c>H.GeneratedIcons.System.Drawing</c> — 949 KB de DLL redistribuidas sin una línea de atribución. La
/// re-auditoría solo había visto la primera.
///
/// Ninguna llegó por decisión de nadie: son <b>dependencias transitivas</b> de
/// <c>H.NotifyIcon.WinUI</c> y del Windows App SDK. Por eso una lista escrita a mano se queda atrás sola,
/// y hay que <b>leer lo que de verdad se publica</b>.
///
/// La comparación se hace contra el grafo real (<c>obj/project.assets.json</c>) cruzado con las DLL que
/// hay en la salida de compilación, que es lo que el instalador empaqueta.
/// </remarks>
public sealed class RedistributedPackagesTests
{
    /// <summary>
    /// Paquetes que NO hace falta citar por su nombre, cada uno con su motivo.
    /// </summary>
    /// <remarks>Añadir aquí exige un motivo escrito: es una exención legal, no una comodidad.</remarks>
    private static readonly Dictionary<string, string> NoRequierenCita = new(StringComparer.OrdinalIgnoreCase)
    {
        // Solo se usan al compilar; no viaja nada suyo en el instalador. Ya está dicho en la sección
        // "NO REDISTRIBUIDO" de los avisos.
        ["Microsoft.Windows.SDK.BuildTools"] = "solo compilación",
        ["Microsoft.Windows.SDK.BuildTools.MSIX"] = "solo compilación",
    };

    /// <summary>Nombre con el que cada paquete aparece citado, cuando no coincide con su id.</summary>
    private static readonly Dictionary<string, string> CitadoComo = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Microsoft.Web.WebView2"] = "WebView2",
        ["Microsoft.WindowsAppSDK"] = "Windows App SDK",
    };

    private static string SalidaDeCompilacion()
    {
        // Las pruebas corren desde tests/OfiConvert.Tests/bin/<config>/<tfm>/; la app publica en
        // bin/<config>/<tfm>/win-x64/ desde la raíz del repo.
        var raiz = Path.Combine(TestPaths.RepoRoot, "bin");
        Assert.True(Directory.Exists(raiz), $"No hay carpeta de compilación en {raiz}: compila antes.");

        var exe = Directory.EnumerateFiles(raiz, "OfiConvert.dll", SearchOption.AllDirectories)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();

        Assert.NotNull(exe);
        return Path.GetDirectoryName(exe)!;
    }

    /// <summary>
    /// Los componentes que los avisos <b>atribuyen de verdad</b>: lo que abre una entrada numerada.
    /// </summary>
    /// <remarks>
    /// 🔴 Buscar el nombre suelto con <c>Contains</c> <b>no vale</b>, y casi cuela: al comprobar esta
    /// prueba en rojo —quitando la entrada de <c>System.Drawing.Common</c>— siguió en verde, porque el
    /// nombre aparecía en la línea «<i>9) Microsoft.Win32.SystemEvents (dependencia de
    /// System.Drawing.Common)</i>». Una <b>mención</b> no es una <b>atribución</b>: hay que exigir que el
    /// componente abra su propia entrada.
    /// </remarks>
    private static readonly System.Text.RegularExpressions.Regex EntradaNumerada =
        new(@"(?m)^\s*\d+\)\s*(?<nombre>[^\r\n(]+)", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static List<string> ComponentesAtribuidos()
        => EntradaNumerada.Matches(LegalText.ThirdParty())
            .Select(m => m.Groups["nombre"].Value.Trim())
            .ToList();

    private static IEnumerable<string> PaquetesDelGrafo()
    {
        var assets = Path.Combine(TestPaths.RepoRoot, "obj", "project.assets.json");
        Assert.True(File.Exists(assets), $"No existe {assets}: restaura los paquetes antes.");

        using var doc = JsonDocument.Parse(File.ReadAllText(assets));
        if (!doc.RootElement.TryGetProperty("libraries", out var libs))
            yield break;

        foreach (var lib in libs.EnumerateObject())
        {
            if (lib.Value.TryGetProperty("type", out var t) && t.GetString() == "package")
                yield return lib.Name.Split('/')[0];
        }
    }

    /// <summary>EL TEST QUE IMPORTA: se lee lo que se publica, no lo que alguien recordó apuntar.</summary>
    [Fact]
    public void TodoPaqueteCuyaDllSePublica_EstaCitadoEnLosAvisos()
    {
        var atribuidos = ComponentesAtribuidos();
        Assert.True(atribuidos.Count > 5,
            $"Solo se encontraron {atribuidos.Count} entradas numeradas en los avisos: el formato del "
                + "archivo ha cambiado y esta prueba ya no sabe leerlo.");

        var salida = SalidaDeCompilacion();
        var paquetes = PaquetesDelGrafo().ToList();
        Assert.True(paquetes.Count > 5, $"Solo se leyeron {paquetes.Count} paquetes del grafo.");

        var sinCitar = new List<string>();

        foreach (var paquete in paquetes.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (NoRequierenCita.ContainsKey(paquete)) continue;

            // ¿Viaja una DLL con su nombre? Es la definición práctica de "se redistribuye".
            var dll = Path.Combine(salida, paquete + ".dll");
            if (!File.Exists(dll)) continue;

            var nombreCitado = CitadoComo.TryGetValue(paquete, out var alias) ? alias : paquete;

            // Tiene que estar en el TÍTULO de una entrada, no de pasada en la descripción de otra. Una
            // entrada puede cubrir dos paquetes ("6) Serilog 4.2.0 y Serilog.Sinks.File 6.0.0"), así que
            // se busca dentro del título; la regex ya lo corta en el primer paréntesis, que es donde
            // viven las descripciones del tipo "(dependencia de X)".
            if (!atribuidos.Any(c => c.Contains(nombreCitado, StringComparison.OrdinalIgnoreCase)))
                sinCitar.Add($"{paquete} ({new FileInfo(dll).Length / 1024} KB)");
        }

        Assert.True(sinCitar.Count == 0,
            "Estas DLL de paquetes NuGet viajan en el instalador y NO están atribuidas en "
                + "THIRD-PARTY-NOTICES.txt. Casi todas llegan como dependencias transitivas, sin que nadie "
                + "las pida: por eso una lista escrita a mano no basta. Comprueba su licencia en el "
                + ".nuspec del paquete (NO de memoria) y añádelas:\n  " + string.Join("\n  ", sinCitar));
    }

    /// <summary>Y las cuatro que faltaban, nombradas, para que nadie las quite «limpiando».</summary>
    [Theory]
    [InlineData("System.Drawing.Common")]
    [InlineData("Microsoft.Win32.SystemEvents")]
    [InlineData("System.Numerics.Tensors")]
    [InlineData("H.GeneratedIcons.System.Drawing")]
    public void LasCuatroQueFaltaban_SiguenCitadas(string paquete)
        => Assert.True(
            ComponentesAtribuidos().Any(c => c.Contains(paquete, StringComparison.OrdinalIgnoreCase)),
            $"{paquete} ya no abre su propia entrada en THIRD-PARTY-NOTICES.txt. Mencionarlo dentro de "
                + "otra entrada NO es atribuirlo.");
}
