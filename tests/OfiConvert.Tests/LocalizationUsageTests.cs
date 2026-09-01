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

    // Las SIETE formas de pedir una clave. Cada una entró por detrás de este escáner:
    //
    //   * `loc["Clave"]` FALTABA, y por eso se le escapó `MsgCheckingUpdate`: una clave que NO existía,
    //     usada en el botón de buscar actualizaciones, tapada por un fallback defensivo.
    //   * `T("Clave")` —el envoltorio que estrenó DialogService— faltaba también, y con él seis claves
    //     que no miraba nadie: existían por suerte, no por cobertura (TJ-18).
    //   * `new UserMessage("Clave")` y `Failed("Clave")` son de TJ-06, donde los servicios pasaron a
    //     devolver claves en vez de texto en español. Se añaden AQUÍ, en el mismo cambio que las crea:
    //     dejarlo para después es exactamente cómo nacieron los dos agujeros anteriores.
    //
    // Un escáner que no mira donde se usa el código no prueba nada.
    private static readonly Regex CodeUsage = new(
        """(?:GetLocalizedString\(\s*"([^"]+)"|LocalizationService\.Instance\[\s*"([^"]+)"\]|\bloc\[\s*"([^"]+)"\]|\bT\(\s*"([^"]+)"|\bnew UserMessage\(\s*"([^"]+)"|\bUserMessage\.Of\(\s*"([^"]+)"|\bFailed\(\s*"([^"]+)")""",
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
                var key = match.Groups
                    .Cast<Group>()
                    .Skip(1)
                    .First(g => g.Success)
                    .Value;

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

    /// <summary>
    /// Claves <b>declaradas y sin usar</b> que ya estaban ahí. Es un trinquete: esta lista solo puede
    /// MENGUAR.
    /// </summary>
    /// <remarks>
    /// El escáner miraba solo en un sentido —que lo usado exista—, así que una clave traducida a ocho
    /// idiomas y usada por nadie no molestaba a nadie. Hay <b>33</b>, y no se borran a lo bruto porque no
    /// todas significan lo mismo: unas son restos, y otras son una función a medio construir cuya interfaz
    /// nunca se escribió (`TJ-26`). Revisarlas una a una es `TJ-29`.
    ///
    /// Lo que este trinquete impide desde hoy es que aparezca la número 34. Al usar o borrar una, hay que
    /// quitarla también de aquí — y el test lo exige, para que la lista no se quede mintiendo.
    /// </remarks>
    private static readonly HashSet<string> DeclaredButUnusedToday = new(StringComparer.Ordinal)
    {
        // Una función a medio construir: las opciones existen en el modelo y en los ocho diccionarios,
        // pero no hay interfaz que las ofrezca. Ver TJ-26 — estas hay que USARLAS, no borrarlas.
        "LblPageRange", "LblSlideRange", "LblSheetNames", "LblImageDpi", "LblImageQuality",
        "TipPageRange", "TipSlideRange", "TipSheetNames",

        // El menú de la bandeja, que se quedó sin traducir.
        "TrayShow", "TrayExit", "TrayStartConversion", "TrayNotifSuccess", "TrayNotifErrors",

        // Nombres de idioma: el desplegable los muestra en su propio idioma, no traducidos.
        "LblSpanish", "LblEnglish", "LblFrench", "LblGerman",
        "LblItalian", "LblJapanese", "LblPortuguese", "LblChinese",

        // Columnas del historial, rotuladas de otra forma.
        "LblDate", "LblDuration", "LblResult", "LblSourceFile", "LblDestination", "LblSuccess",

        // Sueltas. OJO con AppTitle y BtnExport: parecen usadas de un grep rápido, pero lo que aparece en
        // el código es "AppTitleBar" (un x:Name) y "BtnExportCsv"/"BtnExportTxt" (otras claves).
        "AppTitle", "BtnDownloadUpdate", "BtnExport",
        "MsgConversionComplete", "MsgOfficeNotFound", "StateSkipped",
    };

    /// <summary>
    /// El sentido que faltaba: <b>nada declarado puede quedarse sin usar</b> a partir de ahora.
    /// </summary>
    /// <remarks>
    /// Traducir a ocho idiomas una clave que nadie pide es trabajo tirado, y peor: esconde el caso de
    /// TJ-06, donde la traducción existía —correcta, en los ocho— mientras el código escribía la frase en
    /// español a fuego. Nadie se dio cuenta durante versiones porque <b>ningún test miraba en esta
    /// dirección</b>.
    /// </remarks>
    [Fact]
    public void NoAppearNewKeysDeclaredAndNeverUsed()
    {
        var declared = DeclaredKeys();
        var used = UsedKeys();

        Assert.True(used.Count > 50, $"El escaneo solo encontró {used.Count} claves usadas.");

        var nuevas = declared
            .Where(k => !used.ContainsKey(k) && !DeclaredButUnusedToday.Contains(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(nuevas.Count == 0,
            "Claves nuevas declaradas y usadas por NADIE. Traducirlas a ocho idiomas no sirve de nada si "
                + "el código no las pide — y así es como se coló TJ-06, con la traducción escrita y el "
                + "texto en español a fuego al lado:\n  " + string.Join("\n  ", nuevas));
    }

    /// <summary>La lista de excepciones tiene que quedarse a cero, no envejecer llena de mentiras.</summary>
    [Fact]
    public void TheExceptionListDoesNotLie()
    {
        var declared = DeclaredKeys();
        var used = UsedKeys();

        var yaNoAplican = DeclaredButUnusedToday
            .Where(k => !declared.Contains(k) || used.ContainsKey(k))
            .OrderBy(k => k, StringComparer.Ordinal)
            .ToList();

        Assert.True(yaNoAplican.Count == 0,
            "Estas claves ya se usan o ya no existen, así que sobran de DeclaredButUnusedToday. Quítalas: "
                + "una lista de excepciones que no se limpia deja de ser un trinquete y pasa a tapar "
                + "casos nuevos:\n  " + string.Join("\n  ", yaNoAplican));
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
