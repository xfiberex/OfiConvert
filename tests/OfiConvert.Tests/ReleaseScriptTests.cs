using System.Text.RegularExpressions;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// Vigila los scripts del corte de versión como código que son.
/// </summary>
/// <remarks>
/// Solo se ejecutan al publicar, así que sus fallos se descubren tarde y caros. Estas pruebas son la
/// forma barata de fijar dos invariantes que ya se rompieron una vez.
/// </remarks>
public sealed class ReleaseScriptTests
{
    private static string Release => File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "release.ps1"));
    private static string BuildInstaller =>
        File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "installer", "build-installer.ps1"));

    // ── TJ-24: la contraseña del certificado ─────────────────────────────────

    /// <summary>
    /// La contraseña del certificado <b>no puede ser <c>[string]</c></b> en ninguno de los dos scripts.
    /// </summary>
    /// <remarks>
    /// Como cadena se teclea en la consola —y se queda en <c>ConsoleHost_history.txt</c>, en claro y para
    /// siempre— y viaja desnuda de un script al otro. <c>SecureString</c> no lo arregla todo, pero corta
    /// esas dos vías.
    /// </remarks>
    [Theory]
    [InlineData("release.ps1")]
    [InlineData("build-installer.ps1")]
    public void LaContrasenaDelCertificado_NoEsUnaCadena(string script)
    {
        var texto = script == "release.ps1" ? Release : BuildInstaller;

        Assert.DoesNotContain("[string]$CertPassword", texto.Replace(" ", ""), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[SecureString]$CertPassword", texto.Replace(" ", ""), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// EL QUE IMPORTA: la contraseña <b>nunca</b> puede llegar a la línea de comandos de <c>signtool</c>.
    /// </summary>
    /// <remarks>
    /// La línea de comandos de un proceso la lee cualquier proceso del equipo mientras dura
    /// (<c>Get-CimInstance Win32_Process</c>), sin permisos especiales. <c>signtool /p &lt;contraseña&gt;</c>
    /// la publicaba ahí durante toda la firma. Ahora el <c>.pfx</c> se importa al almacén con el
    /// <c>SecureString</c> y se firma por <b>huella</b>, que no es secreta.
    /// </remarks>
    [Fact]
    public void LaContrasena_NoLlegaALaLineaDeComandosDeSigntool()
    {
        var sinComentarios = string.Join("\n",
            BuildInstaller.Split('\n').Select(l =>
            {
                var i = l.IndexOf('#');
                return i >= 0 ? l[..i] : l;
            }));

        Assert.DoesNotContain("/p\", $CertPassword", sinComentarios, StringComparison.Ordinal);
        Assert.DoesNotContain("\"/p\"", sinComentarios, StringComparison.Ordinal);

        // Y la contrapartida: si se importa el .pfx, hay que retirarlo. Dejar la clave privada en el
        // almacén del usuario sería cambiar una fuga por otra.
        Assert.Contains("Import-PfxCertificate", sinComentarios, StringComparison.Ordinal);
        Assert.Contains("Remove-Item", sinComentarios, StringComparison.Ordinal);
        Assert.Contains("-DeleteKey", sinComentarios, StringComparison.Ordinal);
    }

    // ── TJ-08: omitido no es correcto ────────────────────────────────────────

    /// <summary>
    /// El corte tiene que <b>leer el <c>.trx</c></b>, no conformarse con el código de salida.
    /// </summary>
    /// <remarks>
    /// <c>dotnet test</c> devuelve 0 tanto si todo pasa como si media suite se omitió. El corte salía en
    /// verde con pruebas omitidas <b>sin decirlo</b> — y las que se omiten son justo las que necesitan
    /// red u Office: las más caras de perder de vista.
    /// </remarks>
    [Fact]
    public void ElCorte_LeeElTrxYNoSoloElCodigoDeSalida()
    {
        Assert.Contains("--logger", Release, StringComparison.Ordinal);
        Assert.Contains("trx", Release, StringComparison.Ordinal);
        Assert.Contains("Read-TestSummary", Release, StringComparison.Ordinal);

        // Los tres números, no solo el de las que pasan.
        Assert.Contains("$resumen.Passed", Release, StringComparison.Ordinal);
        Assert.Contains("$resumen.Skipped", Release, StringComparison.Ordinal);
        Assert.Contains("$resumen.Failed", Release, StringComparison.Ordinal);
    }

    /// <summary>
    /// Las omisiones previstas se declaran, y las que no lo estén se avisan.
    /// </summary>
    /// <remarks>
    /// Sin lista, «1 omitida» no dice nada. Con lista, una omisión nueva —una prueba que dejó de
    /// ejecutarse sin que nadie lo pidiera— salta a la vista en el corte.
    /// </remarks>
    [Fact]
    public void LasOmisionesPrevistas_EstanDeclaradasYSonLasQueSeOmitenDeVerdad()
    {
        var patron = Regex.Match(Release, @"\$ExpectedSkipPattern\s*=\s*'([^']+)'");
        Assert.True(patron.Success, "release.ps1 no declara ExpectedSkipPattern: toda omisión se avisaría.");

        var regla = new Regex(patron.Groups[1].Value);

        // Las clases que hoy llevan puerta de entorno tienen que estar cubiertas, o el corte llenaría la
        // pantalla de avisos y se acabaría ignorando el mecanismo entero.
        foreach (var clase in new[] { "PublishedReleaseTests", "PowerPointSharedInstanceTests", "OfficeAppLifetimeTests" })
            Assert.True(regla.IsMatch(clase), $"{clase} se omite por diseño y no está en ExpectedSkipPattern.");

        // Y al revés: lo declarado tiene que existir. Un patrón que nombra clases muertas deja de proteger.
        var fuentes = TestPaths.RepoRoot;
        foreach (var clase in patron.Groups[1].Value.Split('|'))
        {
            var existe = Directory
                .EnumerateFiles(Path.Combine(fuentes, "tests"), clase + ".cs", SearchOption.AllDirectories)
                .Any();
            Assert.True(existe, $"ExpectedSkipPattern nombra '{clase}', que ya no existe.");
        }
    }
}
