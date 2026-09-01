using System.Text.RegularExpressions;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// Vigila el script de Inno Setup como código que es.
///
/// El instalador solo se ejecuta de verdad al publicar, así que sus fallos se descubren tarde y caros:
/// el Tier H encontró que <c>/VERYSILENT</c> <b>no era silencioso</b> (faltaba
/// <c>PrivilegesRequiredOverridesAllowed=commandline</c>) y el Tier J (TJ-04) encontró el mismo fallo en
/// un segundo sitio — un <c>MsgBox</c> que Inno muestra también en modo silencioso. Estas pruebas son la
/// forma barata de que no haya un tercero.
/// </summary>
public sealed class InstallerScriptTests
{
    private static string IssPath => Path.Combine(TestPaths.RepoRoot, "installer", "OfiConvert.iss");

    /// <summary>
    /// Ningún <c>MsgBox</c> del script puede aparecer sin la guarda <c>WizardSilent</c>.
    /// </summary>
    /// <remarks>
    /// Inno llama a <c>InitializeWizard</c> también en <c>/SILENT</c> y <c>/VERYSILENT</c>, y un
    /// <c>MsgBox</c> se muestra igual salvo que se pase <c>/SUPPRESSMSGBOXES</c>. La auto-actualización
    /// lanza el instalador <b>con la app ya cerrada</b>: un diálogo ahí es un cuelgue sin nadie delante.
    /// </remarks>
    [Fact]
    public void NingunMsgBox_SeMuestraEnModoSilencioso()
    {
        // Los comentarios se vacían antes de mirar nada: un comentario que MENCIONE WizardSilent —como el
        // que explica esta misma guarda en el .iss— haría pasar el test sin que la guarda exista.
        string[] lines = WithoutComments(File.ReadAllLines(IssPath));
        var sinGuarda = new List<string>();

        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains("MsgBox(", StringComparison.Ordinal)) continue;

            // Se busca hacia atrás hasta el principio del procedimiento/función que lo contiene.
            bool guarded = false;
            for (int j = i; j >= 0; j--)
            {
                if (lines[j].Contains("WizardSilent", StringComparison.OrdinalIgnoreCase)) { guarded = true; break; }
                if (Regex.IsMatch(lines[j], @"^\s*(procedure|function)\s", RegexOptions.IgnoreCase)) break;
            }

            if (!guarded) sinGuarda.Add($"línea {i + 1}: {lines[i].Trim()}");
        }

        Assert.True(sinGuarda.Count == 0,
            "Hay MsgBox del instalador sin guardar tras 'not WizardSilent'. En una instalación silenciosa " +
            "—la de la auto-actualización— se quedan esperando un clic que no llega:\n  " +
            string.Join("\n  ", sinGuarda));
    }

    /// <summary>
    /// El aviso de «no hay motor» tiene que mirar <b>los dos</b> motores, no solo Microsoft Office.
    /// </summary>
    /// <remarks>
    /// La app convierte con Office <b>o</b> con LibreOffice, y el README lo anuncia. El instalador miraba
    /// únicamente Office, así que a quien tuviera LibreOffice —una configuración soportada— le decía que
    /// su instalación no iba a funcionar. Es el instalador contradiciendo al producto (TJ-12).
    ///
    /// Comprobado sobre el instalador compilado, con los dos detectores forzados por línea de comandos:
    /// solo avisa en 0/0 (ningún motor); en 0/1, 1/0 y 1/1 calla.
    /// </remarks>
    [Fact]
    public void ElAviso_DeSinMotor_MiraTambienLibreOffice()
    {
        var iss = string.Join("\n", WithoutComments(File.ReadAllLines(IssPath)));

        Assert.Contains("IsLibreOfficeInstalled", iss, StringComparison.Ordinal);

        var condicion = Regex.Match(iss, @"if[^\n]*WizardSilent[^\n]*then");
        Assert.True(condicion.Success, "No se encontró la condición que decide si sale el aviso.");
        Assert.True(condicion.Value.Contains("IsLibreOfficeInstalled", StringComparison.Ordinal),
            "El aviso de «sin motor de conversión» no consulta LibreOffice: a quien lo tenga instalado se "
                + $"le dirá que la app no funcionará. Condición encontrada: {condicion.Value}");
    }

    /// <summary>
    /// El texto de ese aviso no puede nacer en duro: el instalador habla seis idiomas.
    /// </summary>
    /// <remarks>
    /// Es el mismo fallo que TJ-06 dentro de la app, en el instalador: el texto salía en español en los
    /// seis. Ahora vive en la sección de mensajes personalizados y el código solo pide la clave.
    /// </remarks>
    [Fact]
    public void ElTextoDelAviso_EstaEnLosSeisIdiomas()
    {
        var raw = File.ReadAllText(IssPath);

        var idiomas = Regex.Matches(raw, @"(?m)^Name:\s*""(\w+)"";\s*MessagesFile")
            .Select(m => m.Groups[1].Value)
            .ToList();

        Assert.True(idiomas.Count >= 6, $"Solo se encontraron {idiomas.Count} idiomas en el instalador.");

        var faltan = idiomas
            .SelectMany(i => new[] { $"{i}.NoEngineTitle", $"{i}.NoEngineBody" })
            .Where(clave => !raw.Contains(clave + "=", StringComparison.Ordinal))
            .ToList();

        Assert.True(faltan.Count == 0,
            "Faltan traducciones del aviso de «sin motor»; en esos idiomas saldría en español:\n  "
                + string.Join("\n  ", faltan));
    }

    /// <summary>
    /// El modo silencioso necesita <c>PrivilegesRequiredOverridesAllowed=commandline</c> (Tier H): sin
    /// <c>commandline</c>, <c>/ALLUSERS</c> y <c>/CURRENTUSER</c> quedan prohibidos y vuelve el diálogo
    /// «Seleccione el modo de instalación» que el updater no puede contestar.
    /// </summary>
    [Fact]
    public void ElAlcance_SePuedeFijarPorLineaDeComandos()
    {
        string iss = File.ReadAllText(IssPath);
        var directive = Regex.Match(iss, @"^\s*PrivilegesRequiredOverridesAllowed\s*=\s*(?<v>.+)$", RegexOptions.Multiline);

        Assert.True(directive.Success, "El .iss no declara PrivilegesRequiredOverridesAllowed.");
        Assert.Contains("commandline", directive.Groups["v"].Value, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// La línea de comandos del updater se construye en <c>Core/InstallScope</c>, que es donde está
    /// probada. Escribirla a mano en el code-behind es cómo se perdió <c>/SUPPRESSMSGBOXES</c>.
    /// </summary>
    [Fact]
    public void ElUpdater_NoEscribeLosModificadoresAMano()
    {
        foreach (string file in TestPaths.AppSourceFiles("*.cs"))
        {
            if (file.EndsWith("InstallScope.cs", StringComparison.OrdinalIgnoreCase)) continue;

            string code = File.ReadAllText(file);
            Assert.False(code.Contains("\"/VERYSILENT", StringComparison.Ordinal) ||
                         code.Contains("$\"/VERYSILENT", StringComparison.Ordinal),
                $"{Path.GetFileName(file)} arma los modificadores del instalador a mano. " +
                "Usa Core.InstallScope.SilentInstallArguments, que es la única versión probada.");
        }
    }

    /// <summary>
    /// Devuelve las líneas con los comentarios de Inno vaciados (<c>{ }</c>, <c>(* *)</c>, <c>//</c> y
    /// <c>;</c>), conservando la numeración para poder señalar la línea culpable.
    /// </summary>
    private static string[] WithoutComments(string[] lines)
    {
        var result = new string[lines.Length];
        bool inBlock = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];

            if (inBlock)
            {
                int close = line.IndexOf('}');
                if (close < 0) { result[i] = string.Empty; continue; }
                line = line[(close + 1)..];
                inBlock = false;
            }

            int open = line.IndexOf('{');
            if (open >= 0)
            {
                int close = line.IndexOf('}', open);
                if (close < 0) { inBlock = true; line = line[..open]; }
                else line = line[..open] + line[(close + 1)..];
            }

            int slashes = line.IndexOf("//", StringComparison.Ordinal);
            if (slashes >= 0) line = line[..slashes];

            if (line.TrimStart().StartsWith(';')) line = string.Empty;

            result[i] = line;
        }

        return result;
    }
}
