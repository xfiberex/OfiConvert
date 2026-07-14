using System.Text.RegularExpressions;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// Caza el texto de interfaz escrito <b>a fuego</b> en el código, en vez de traducido.
/// </summary>
/// <remarks>
/// Este proyecto ha cometido el MISMO fallo tres veces, y las tres se descubrieron por casualidad:
/// <list type="number">
///   <item>El <b>diálogo de cierre</b> («Confirmar cierre», «Sí», «No») — lo cazó <c>LocalizationUsageTests</c>.</item>
///   <item>La <b>barra de actualización</b> («Descargando… 42%», «Instalar ahora») — se vio probando el
///         instalador de punta a punta.</item>
///   <item>Los <b>diálogos de <c>DialogService</c></b> («Aceptar», «Error», «Información») — se vio al
///         arreglar el anterior.</item>
/// </list>
/// Los tres salían <b>en español en los ocho idiomas</b>. Y ninguno rompía nada: la app compilaba, los
/// tests pasaban y la traducción existía… sin usarse.
///
/// <c>LocalizationUsageTests</c> no puede cazar esto: comprueba que las claves <b>usadas</b> existan, no
/// que no haya literales. Esta prueba mira lo contrario — que no se asigne un literal a una propiedad de
/// texto de la UI.
/// </remarks>
public sealed class HardcodedUiTextTests
{
    /// <summary>Archivos que pintan interfaz: aquí ningún texto puede nacer en duro.</summary>
    private static readonly string[] UiFiles =
    [
        "MainWindow.xaml.cs",
        Path.Combine("Services", "DialogService.cs"),
    ];

    /// <summary>
    /// Asignaciones de texto a la UI. La cadena puede ser normal (<c>"Aceptar"</c>) o interpolada, y una
    /// interpolada puede llevar <b>comillas dentro de sus huecos</b> (<c>$"{loc["Clave"]}: {x}"</c>) — que es
    /// justo donde una expresión ingenua se parte y empieza a inventarse falsos positivos.
    /// </summary>
    private static readonly Regex Assignment = new(
        """\b(?:Title|Message|Content|Text|Header|PrimaryButtonText|SecondaryButtonText|CloseButtonText)\s*=\s*(?:\$"((?:[^"{}]|\{[^{}]*\})*)"|"([^"]*)")""",
        RegexOptions.Compiled);

    /// <summary>Secuencias de escape: <c>⬆</c> (el glifo de la flecha), <c>\n</c>…</summary>
    private static readonly Regex Escapes = new(@"\\u[0-9a-fA-F]{4}|\\.", RegexOptions.Compiled);

    /// <summary>Huecos de interpolación: <c>{loc["Clave"]}</c>, <c>{p:P0}</c>…</summary>
    private static readonly Regex InterpolationHole = new(@"\{[^{}]*\}", RegexOptions.Compiled);

    /// <summary>
    /// Lo que quedaría escrito a fuego aunque la traducción funcionase: se vacían los huecos (su contenido
    /// SÍ puede estar traducido) y los escapes. Lo que sobreviva con letras, lo leería el usuario en
    /// español pasara lo que pasara.
    /// </summary>
    private static string LiteralPart(string text) =>
        InterpolationHole.Replace(Escapes.Replace(text, ""), "");

    /// <summary>
    /// Literales permitidos: los que NO son texto que el usuario tenga que entender. Añadir aquí exige
    /// justificarlo — si es una frase, va a los ocho diccionarios.
    /// </summary>
    private static bool IsAllowed(string literal)
    {
        var value = LiteralPart(literal).Trim();

        if (value.Length == 0) return true;                       // "" o puro interpolado
        if (!value.Any(char.IsLetter)) return true;               // símbolos, glifos, separadores…

        // La línea de "Acerca de": nombre del producto, licencia y autor. No hay nada que traducir, y la
        // versión sale del ensamblado.
        if (value.Contains("OfiConvert", StringComparison.Ordinal) &&
            value.Contains("MIT", StringComparison.Ordinal))
            return true;

        return false;
    }

    [Fact]
    public void NoUiTextIsHardcodedInsteadOfTranslated()
    {
        var offenders = new List<string>();

        foreach (var relative in UiFiles)
        {
            var path = Path.Combine(TestPaths.RepoRoot, relative);
            Assert.True(File.Exists(path), $"No se encontró {relative}: la lista de archivos de UI se ha quedado obsoleta.");

            var code = File.ReadAllText(path);
            foreach (Match match in Assignment.Matches(code))
            {
                var literal = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;

                if (!IsAllowed(literal))
                    offenders.Add($"{relative}: \"{literal}\"");
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Texto de interfaz EN DURO (saldría en español en los 8 idiomas). Dale de alta una clave en "
                + "Lang/*.xaml y úsala:\n  " + string.Join("\n  ", offenders));
    }
}
