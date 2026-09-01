using System.Xml.Linq;
using OfiConvert.Helpers;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// Completitud de los 8 diccionarios de idioma.
/// </summary>
/// <remarks>
/// Nada de esto rompe el build ni se ve en una revisión: <see cref="LocalizationService"/> devuelve <b>la
/// propia clave</b> cuando no la conoce, y si falta el archivo entero cae a español. Una clave sin traducir
/// al japonés, o un archivo con una clave de menos, se manifiesta como texto raro en la UI de un idioma
/// que probablemente nadie de la casa abra nunca.
/// </remarks>
[Collection(LocalizationCollection.Name)]   // el idioma es estado ESTATICO: ver LocalizationCollection
public sealed class LocalizationTests
{
    /// <summary>El de referencia: el idioma en el que se escriben las claves nuevas.</summary>
    private const string Reference = "es-ES";

    private static readonly string[] ExpectedFiles =
        ["es-ES", "en-US", "pt-BR", "fr-FR", "de-DE", "it-IT", "zh-CN", "ja-JP"];

    public static TheoryData<string> AllLanguages() => [.. ExpectedFiles];

    private static Dictionary<string, string> LoadKeys(string culture)
    {
        var path = Path.Combine(TestPaths.LangFolder, $"{culture}.xaml");
        Assert.True(File.Exists(path), $"Falta el diccionario de idioma: {path}");

        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var keys = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var element in XDocument.Load(path).Root?.Elements() ?? [])
        {
            var key = element.Attribute(x + "Key")?.Value;
            if (key is null) continue;

            Assert.False(keys.ContainsKey(key), $"{culture}.xaml declara '{key}' dos veces: gana la última, en silencio.");
            keys[key] = element.Value;
        }

        return keys;
    }

    /// <summary>
    /// Los 8 idiomas de <see cref="LocalizationService.SupportedLanguages"/> tienen que tener archivo. La
    /// lista es la fuente única (en el Tier A, tener DOS listas dejó 6 idiomas sin persistir).
    /// </summary>
    [Fact]
    public void EverySupportedLanguage_HasItsDictionaryFile()
    {
        Assert.Equal(LocalizationService.SupportedLanguages.Length, ExpectedFiles.Length);

        Assert.All(ExpectedFiles, culture =>
            Assert.True(
                File.Exists(Path.Combine(TestPaths.LangFolder, $"{culture}.xaml")),
                $"Falta Lang/{culture}.xaml"));
    }

    [Theory]
    [MemberData(nameof(AllLanguages))]
    public void EveryDictionary_HasExactlyTheSameKeysAsSpanish(string culture)
    {
        var reference = LoadKeys(Reference).Keys.ToHashSet(StringComparer.Ordinal);
        var actual = LoadKeys(culture).Keys.ToHashSet(StringComparer.Ordinal);

        var missing = reference.Except(actual).Order().ToList();
        var extra = actual.Except(reference).Order().ToList();

        Assert.True(
            missing.Count == 0,
            $"Lang/{culture}.xaml no tiene estas claves (se verían en español, o como el nombre de la clave):\n  "
                + string.Join("\n  ", missing));

        Assert.True(
            extra.Count == 0,
            $"Lang/{culture}.xaml tiene claves que ya no existen en {Reference}.xaml (sobran):\n  "
                + string.Join("\n  ", extra));
    }

    [Theory]
    [MemberData(nameof(AllLanguages))]
    public void NoTranslationIsEmpty(string culture)
    {
        Assert.All(LoadKeys(culture), pair =>
            Assert.False(
                string.IsNullOrWhiteSpace(pair.Value),
                $"Lang/{culture}.xaml: la clave '{pair.Key}' está vacía."));
    }

    /// <summary>
    /// No basta con que los archivos estén bien: el parseo en runtime (XDocument, a mano) tiene que
    /// producir un diccionario lleno. Si <c>LoadLanguage</c> falla, se traga la excepción y se queda con
    /// las cadenas anteriores — o sea, con las españolas, sin que nada avise.
    /// </summary>
    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    [InlineData("pt")]
    [InlineData("fr")]
    [InlineData("de")]
    [InlineData("it")]
    [InlineData("zh")]
    [InlineData("ja")]
    public void LoadLanguage_ReallyLoadsTheDictionaryAtRuntime(string code)
    {
        var service = new LocalizationService();

        service.LoadLanguage(code);

        Assert.Equal(code, service.CurrentLanguage);
        Assert.False(string.IsNullOrWhiteSpace(service["BtnConvert"]));
        Assert.NotEqual("BtnConvert", service["BtnConvert"]);   // el indexer devuelve la clave si no la tiene
    }

    /// <summary>Cada idioma dice "Convertir" a su manera: si dos coinciden, uno no se cargó.</summary>
    [Fact]
    public void EachLanguage_ActuallyShowsDifferentText()
    {
        var service = new LocalizationService();

        service.LoadLanguage("es");
        var spanish = service["BtnConvert"];

        service.LoadLanguage("ja");
        var japanese = service["BtnConvert"];

        Assert.NotEqual(spanish, japanese);
    }

    [Theory]
    [InlineData("xx")]      // idioma inexistente
    [InlineData("")]
    [InlineData(null)]
    public void UnsupportedLanguage_FallsBackToSpanish(string? code)
    {
        var service = new LocalizationService();

        service.LoadLanguage(code!);

        Assert.Equal(LocalizationService.DefaultLanguage, service.CurrentLanguage);
    }

    [Fact]
    public void UnknownKey_ReturnsTheKeyItself()
        => Assert.Equal("ClaveQueNoExiste", new LocalizationService()["ClaveQueNoExiste"]);

    [Theory]
    [InlineData("es", true)]
    [InlineData("ja", true)]
    [InlineData("ru", false)]
    [InlineData(null, false)]
    public void IsSupported_IsTheSingleSourceOfTruth(string? code, bool expected)
        => Assert.Equal(expected, LocalizationService.IsSupported(code));
}
