using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// Comprueba que <b>toda clave usada existe en el diccionario</b>.
/// </summary>
/// <remarks>
/// Es la trampa que los proyectos hermanos ya pagaron con su <c>L.T</c>, y aquí está montada igual: el
/// indexer de <c>LocalizationService</c> devuelve <b>la propia clave</b> cuando no la conoce. Un typo en
/// una clave —en el código o en un binding del XAML— <b>no rompe el build ni ningún test</b>: el usuario ve
/// el literal "BtnConvertt" en un botón, y solo se descubre abriendo esa pantalla a mano.
///
/// Por eso este test lee el TEXTO del código fuente en vez de reflexionar sobre el ensamblado: la relación
/// "esta clave se usa" solo existe ahí.
/// </remarks>
public sealed class LocalizationUsageTests
{
    /// <summary>Claves que se construyen en runtime (no son literales) y que este test no puede resolver.</summary>
    private static readonly HashSet<string> DynamicKeysAllowed = new(StringComparer.Ordinal);

    // GetLocalizedString("Clave") y LocalizationService.Instance["Clave"] — el literal siempre va primero.
    private static readonly Regex CodeUsage = new(
        """(?:GetLocalizedString\(\s*"([^"]+)"|LocalizationService\.Instance\[\s*"([^"]+)"\])""",
        RegexOptions.Compiled);

    // {Binding [Clave], Source={StaticResource Loc}}
    private static readonly Regex XamlUsage = new(
        @"\{Binding\s+\[([A-Za-z0-9_]+)\]\s*,\s*Source=\{StaticResource\s+Loc\}\}",
        RegexOptions.Compiled);

    private static HashSet<string> DeclaredKeys()
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var path = Path.Combine(TestPaths.LangFolder, "es-ES.xaml");

        return (XDocument.Load(path).Root?.Elements() ?? [])
            .Select(e => e.Attribute(x + "Key")?.Value)
            .Where(k => k is not null)
            .Select(k => k!)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Clave usada → archivo donde se usa.</summary>
    private static SortedDictionary<string, string> UsedKeys()
    {
        var used = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var file in TestPaths.AppSourceFiles("*.cs"))
        {
            var code = File.ReadAllText(file);
            foreach (Match match in CodeUsage.Matches(code))
            {
                var key = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                used[key] = Path.GetFileName(file);
            }
        }

        foreach (var file in TestPaths.AppSourceFiles("*.xaml"))
        {
            // Los Lang/*.xaml DECLARAN claves, no las usan.
            if (file.Contains($"{Path.DirectorySeparatorChar}Lang{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                continue;

            var markup = File.ReadAllText(file);
            foreach (Match match in XamlUsage.Matches(markup))
                used[match.Groups[1].Value] = Path.GetFileName(file);
        }

        return used;
    }

    [Fact]
    public void EveryKeyUsedInCodeOrXaml_ExistsInTheDictionary()
    {
        var declared = DeclaredKeys();
        var used = UsedKeys();

        // Si el escaneo no encuentra nada, el test no está probando nada: fallaría en silencio para siempre.
        Assert.True(used.Count > 50, $"El escaneo solo encontró {used.Count} claves usadas: el patrón de búsqueda se ha quedado obsoleto.");

        var missing = used
            .Where(pair => !DynamicKeysAllowed.Contains(pair.Key) && !declared.Contains(pair.Key))
            .Select(pair => $"{pair.Key}  (usada en {pair.Value})")
            .ToList();

        Assert.True(
            missing.Count == 0,
            "Claves usadas que NO están en Lang/es-ES.xaml (el usuario vería el nombre de la clave):\n  "
                + string.Join("\n  ", missing));
    }
}
