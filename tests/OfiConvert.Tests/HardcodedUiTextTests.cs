using System.Text.RegularExpressions;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// Caza el texto de interfaz escrito <b>a fuego</b> en el código, en vez de traducido.
/// </summary>
/// <remarks>
/// Este proyecto ha cometido el MISMO fallo cinco veces, y las cinco se descubrieron por casualidad:
/// <list type="number">
///   <item>El <b>diálogo de cierre</b> («Confirmar cierre», «Sí», «No») — lo cazó <c>LocalizationUsageTests</c>.</item>
///   <item>La <b>barra de actualización</b> («Descargando… 42%», «Instalar ahora») — se vio probando el
///         instalador de punta a punta.</item>
///   <item>Los <b>diálogos de <c>DialogService</c></b> («Aceptar», «Error», «Información») — se vio al
///         arreglar el anterior.</item>
///   <item>Los <b>18 mensajes de los servicios</b> («El archivo no existe.», los errores de LibreOffice) —
///         los cazó la re-auditoría del Tier J (TJ-06), con las traducciones ya escritas y sin usar.</item>
///   <item>…y <b>esta misma prueba</b>, que solo miraba dos archivos de veintitantos (TJ-17): ninguno de
///         esos 18 literales vivía en los archivos que vigilaba.</item>
/// </list>
/// Todos salían <b>en español en los ocho idiomas</b>. Y ninguno rompía nada: la app compilaba, los
/// tests pasaban y la traducción existía… sin usarse.
///
/// <c>LocalizationUsageTests</c> no puede cazar esto: comprueba que las claves <b>usadas</b> existan, no
/// que no haya literales. Esta prueba mira lo contrario — que no se asigne un literal a una propiedad de
/// texto de la UI.
/// </remarks>
public sealed class HardcodedUiTextTests
{
    /// <summary>
    /// Archivos que pueden acabar hablándole al usuario: aquí ningún texto puede nacer en duro.
    /// </summary>
    /// <remarks>
    /// Hasta el Tier J esta lista tenía <b>dos</b> nombres escritos a mano (TJ-17), y los 18 literales de
    /// TJ-06 vivían todos fuera de ella, en <c>Services/</c> y <c>ViewModels/</c>. Una prueba que solo
    /// mira donde ya se miró no protege de nada. Ahora los archivos se <b>descubren</b>, para que uno
    /// nuevo entre solo el día que se cree.
    /// </remarks>
    private static IEnumerable<string> UiFiles =>
        TestPaths.AppSourceFiles("*.cs")
            .Select(f => Path.GetRelativePath(TestPaths.RepoRoot, f))
            // Los textos legales embebidos son licencias íntegras, que deben ir en su idioma original, y
            // LocalizationService necesariamente nombra idiomas.
            .Where(f => !f.EndsWith("LegalText.cs", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.EndsWith("LocalizationService.cs", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Asignaciones de texto a la UI. La cadena puede ser normal (<c>"Aceptar"</c>) o interpolada, y una
    /// interpolada puede llevar <b>comillas dentro de sus huecos</b> (<c>$"{loc["Clave"]}: {x}"</c>) — que es
    /// justo donde una expresión ingenua se parte y empieza a inventarse falsos positivos.
    /// </summary>
    /// <remarks>
    /// 🔴 <b>El prefijo <c>[A-Za-z_]*</c> no es adorno: sin él la lista de nombres se lee mal.</b> Con un
    /// <c>\b</c> pegado a <c>Message</c>, <c>StateMessage = "Pendiente"</c> <b>no casa</b> — entre la «e»
    /// de <c>State</c> y la «M» de <c>Message</c> no hay frontera de palabra. Y ese literal existía, en
    /// <c>Models/FileItem.cs</c>, mientras esta prueba pasaba en verde: el vigésimo del Tier J, y el
    /// segundo que se le escapa a su propio guardián. Lo que importa es cómo <b>acaba</b> el nombre de la
    /// propiedad, no cómo empieza.
    /// </remarks>
    private static readonly Regex Assignment = new(
        """\b[A-Za-z_]*(?:Title|Message|Content|Text|Header)\s*=\s*(?:\$"((?:[^"{}]|\{[^{}]*\})*)"|"([^"]*)")""",
        RegexOptions.Compiled);

    /// <summary>
    /// Literales que viajan como <b>argumento</b> hacia algo que acaba en pantalla.
    /// </summary>
    /// <remarks>
    /// El patrón de asignación no ve <c>ShowError("Error general: …")</c> ni
    /// <c>ConversionResult.Failed("El archivo de origen no existe")</c> — que es exactamente por donde se
    /// colaron los 18 mensajes de TJ-06, en archivos que además nadie vigilaba. Aquí lo único admitido es
    /// una <b>clave</b> (<c>MsgFileNotFound</c>): una frase, con sus espacios y su punto final, delata que
    /// alguien volvió a escribir texto para el usuario dentro del código.
    /// </remarks>
    private static readonly Regex Argument = new(
        """\b(?:ShowError|ShowInformation|ShowWarning|ShowConfirmation|Failed|UserMessage|FileValidationResult)\s*\(\s*(?:\$"((?:[^"{}]|\{[^{}]*\})*)"|"([^"]*)")""",
        RegexOptions.Compiled);

    /// <summary>Una clave de <c>Lang/*.xaml</c>: una palabra, sin espacios ni puntuación de frase.</summary>
    private static readonly Regex TranslationKey = new("^[A-Za-z][A-Za-z0-9_]*$", RegexOptions.Compiled);

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

        // El nombre del producto a secas (el tooltip de la bandeja). Un nombre propio no se traduce.
        if (value == "OfiConvert") return true;

        // ⚠️ EXCEPCIÓN CON FECHA DE CADUCIDAD: el rótulo del menú contextual del Explorador, que hoy se
        // escribe en el registro en español para los ocho idiomas. Es la única superficie de la app FUERA
        // de su ventana, y arreglarlo no es traducir una cadena: hay que REESCRIBIR el registro al cambiar
        // de idioma. Está fichado como TJ-22; cuando se cierre, esta excepción se borra y el test vuelve a
        // vigilarlo.
        if (value == "Convertir con OfiConvert") return true;

        return false;
    }

    [Fact]
    public void NoUiTextIsHardcodedInsteadOfTranslated()
    {
        var offenders = new List<string>();
        int revisados = 0;

        foreach (var relative in UiFiles)
        {
            var path = Path.Combine(TestPaths.RepoRoot, relative);
            var code = File.ReadAllText(path);
            revisados++;

            foreach (Match match in Assignment.Matches(code))
            {
                var literal = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;

                if (!IsAllowed(literal))
                    offenders.Add($"{relative}: [ASIGNADO A LA UI] {literal}");
            }

            foreach (Match match in Argument.Matches(code))
            {
                var literal = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;

                // Como argumento solo se admite una CLAVE; lo demás es una frase escrita a mano.
                if (!IsAllowed(literal) && !TranslationKey.IsMatch(LiteralPart(literal).Trim()))
                    offenders.Add($"{relative}: [MENSAJE AL USUARIO, pasa una clave] {literal}");
            }
        }

        // Una prueba que no encuentra archivos pasa en verde sin haber mirado nada.
        Assert.True(revisados > 10, $"Solo se revisaron {revisados} archivos: el descubrimiento está roto.");

        Assert.True(
            offenders.Count == 0,
            "Texto de interfaz EN DURO (saldría en español en los 8 idiomas). Dale de alta una clave en "
                + "Lang/*.xaml y úsala:\n  " + string.Join("\n  ", offenders));
    }
}
