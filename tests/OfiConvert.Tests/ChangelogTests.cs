using System.Text.RegularExpressions;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// Guarda el contrato entre <c>CHANGELOG.md</c> y el corte de versión.
///
/// Desde el Tier J, <c>release.ps1</c> saca las notas del GitHub Release de la sección
/// <c>## [X.Y.Z]</c> del changelog y aborta si no está escrita. Estas pruebas comprueban lo mismo
/// desde el otro lado —el de C#— para que el fallo salga en <c>dotnet test</c>, y no a mitad de un
/// corte: la versión que hoy declara el <c>.csproj</c> tiene que estar contada en el changelog.
///
/// Antes del Tier J las notas eran una plantilla genérica idéntica en las nueve versiones publicadas.
/// </summary>
public sealed class ChangelogTests
{
    private static string ChangelogPath => Path.Combine(TestPaths.RepoRoot, "CHANGELOG.md");
    private static string CsprojPath => Path.Combine(TestPaths.RepoRoot, "OfiConvert.csproj");

    private static string CsprojVersion()
    {
        var match = Regex.Match(File.ReadAllText(CsprojPath), @"<Version>(.*?)</Version>");
        Assert.True(match.Success, "El .csproj no declara <Version>: es la fuente única de la versión.");
        return match.Groups[1].Value.Trim();
    }

    /// <summary>
    /// Extrae el cuerpo de una sección, igual que <c>Get-ChangelogSection</c> en <c>release.ps1</c>:
    /// desde el encabezado hasta el siguiente <c>## </c> o el separador <c>---</c>, sin incluirlos.
    /// </summary>
    private static string? Section(string version)
    {
        var body = new List<string>();
        bool inside = false;

        foreach (string line in File.ReadAllLines(ChangelogPath))
        {
            if (!inside)
            {
                if (Regex.IsMatch(line, $@"^##\s+\[{Regex.Escape(version)}\]")) inside = true;
                continue;
            }
            if (Regex.IsMatch(line, @"^##\s") || Regex.IsMatch(line, @"^---\s*$")) break;
            body.Add(line);
        }

        if (!inside) return null;
        string text = string.Join("\n", body).Trim();
        return text.Length == 0 ? null : text;
    }

    [Fact]
    public void Changelog_TieneSeccionParaLaVersionDelCsproj()
    {
        string version = CsprojVersion();

        Assert.True(Section(version) is not null,
            $"CHANGELOG.md no tiene una sección '## [{version}]' con contenido. release.ps1 abortaría el corte: " +
            "las notas del release salen de ahí.");
    }

    [Fact]
    public void Changelog_NoDejaVersionesPublicadasSinFecha()
    {
        // "## [2.6.1] — 2026-08-29". La única sección sin fecha permitida es la de lo no publicado.
        foreach (Match heading in Regex.Matches(File.ReadAllText(ChangelogPath), @"^##\s+\[(?<v>[^\]]+)\](?<rest>.*)$", RegexOptions.Multiline))
        {
            string version = heading.Groups["v"].Value;
            if (!Regex.IsMatch(version, @"^\d+\.\d+\.\d+$")) continue;  // [Sin publicar]

            Assert.True(Regex.IsMatch(heading.Groups["rest"].Value, @"\d{4}-\d{2}-\d{2}"),
                $"La versión {version} no lleva fecha absoluta (AAAA-MM-DD) en su encabezado del CHANGELOG.");
        }
    }
}
