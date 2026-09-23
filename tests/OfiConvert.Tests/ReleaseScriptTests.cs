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

        // Las clases con puerta de entorno tienen que estar cubiertas, o el corte llenaría la pantalla de
        // avisos y se acabaría ignorando el mecanismo entero.
        //
        // 🔴 Esta lista se DESCUBRE. Estuvo escrita a mano —tres nombres— hasta que llegó una cuarta
        // puerta (`LibreOfficeFact`, TJ-25 verificado de punta a punta) y hubo que acordarse de venir
        // aquí. Es el fallo de TJ-17 otra vez: un guardián que solo mira donde ya se miró no protege del
        // caso siguiente, que es justo el que se olvida.
        var conPuerta = ClasesConPuertaDeEntorno();
        Assert.True(conPuerta.Count >= 4,
            $"Solo se descubrieron {conPuerta.Count} clases con puerta de entorno: el descubrimiento está roto.");

        foreach (var clase in conPuerta)
            Assert.True(regla.IsMatch(clase),
                $"{clase} se omite por diseño (usa una puerta de entorno) y no está en ExpectedSkipPattern: "
                    + "el corte la avisaría como omisión imprevista en cada versión.");

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

    /// <summary>
    /// Las clases de prueba que se omiten por diseño, descubiertas a partir de las <b>puertas de
    /// entorno</b> que existan hoy.
    /// </summary>
    /// <remarks>
    /// Una puerta es un <c>*FactAttribute</c> que pone <c>Skip</c> según una variable de entorno
    /// (<c>NetworkFact</c>, <c>OfficeFact</c>, <c>LibreOfficeFact</c>…). Se buscan esos atributos y luego
    /// quién los usa: así, la puerta número cinco entra sola el día que se escriba.
    /// </remarks>
    private static List<string> ClasesConPuertaDeEntorno()
    {
        var testsDir = Path.Combine(TestPaths.RepoRoot, "tests");

        var puertas = Directory
            .EnumerateFiles(testsDir, "*FactAttribute.cs", SearchOption.AllDirectories)
            .Where(f => File.ReadAllText(f).Contains("GetEnvironmentVariable", StringComparison.Ordinal))
            .Select(f => Path.GetFileNameWithoutExtension(f).Replace("Attribute", "", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(puertas);

        var clases = new List<string>();
        foreach (var archivo in Directory.EnumerateFiles(testsDir, "*.cs", SearchOption.AllDirectories))
        {
            if (archivo.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") ||
                archivo.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
                continue;

            var codigo = File.ReadAllText(archivo);
            if (!puertas.Any(p => codigo.Contains($"[{p}]", StringComparison.Ordinal))) continue;

            var nombre = Regex.Match(codigo, @"\bclass\s+(?<n>\w+)");
            if (nombre.Success) clases.Add(nombre.Groups["n"].Value);
        }
        return clases;
    }

    // ── TJ-27 y TJ-37: la codificación de los scripts ─────────────────────────

    /// <summary>Todo <c>.ps1</c> del repo, sin <c>bin</c>, <c>obj</c> ni <c>publish</c>.</summary>
    public static TheoryData<string> Scripts()
    {
        var data = new TheoryData<string>();
        foreach (string path in Directory.EnumerateFiles(TestPaths.RepoRoot, "*.ps1", SearchOption.AllDirectories))
        {
            string rel = Path.GetRelativePath(TestPaths.RepoRoot, path);
            string[] partes = rel.Split(Path.DirectorySeparatorChar);
            if (partes.Any(p => p is "bin" or "obj" or "publish" or ".git")) continue;
            data.Add(rel);
        }
        return data;
    }

    /// <summary>
    /// Todo <c>.ps1</c> empieza por el BOM de UTF-8 (<c>EF BB BF</c>).
    /// </summary>
    /// <remarks>
    /// Sin BOM, PowerShell 5.1 lo lee con la página de códigos ANSI: los acentos de los mensajes salen
    /// corruptos (<c>encontrÃ³</c>) y un <c>—</c> dentro de una cadena basta para el «Falta el paréntesis de
    /// cierre» que ya pagaron los hermanos. <c>tools/capture-dropdown.ps1</c> nació sin él (TJ-27). Se
    /// descubren solos: un script nuevo entra en la prueba sin tocarla.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Scripts))]
    public void CadaScript_EmpiezaPorElBomDeUtf8(string script)
    {
        byte[] head = File.ReadAllBytes(Path.Combine(TestPaths.RepoRoot, script)).Take(3).ToArray();

        Assert.True(head.SequenceEqual(new byte[] { 0xEF, 0xBB, 0xBF }),
            $"{script} no empieza por el BOM de UTF-8 (empieza por {Convert.ToHexString(head)}). " +
            "Guárdalo como «UTF-8 con BOM»: PowerShell 5.1 lo leería con la página de códigos ANSI.");
    }

    /// <summary>
    /// Ningún script lee el <c>.csproj</c> con <c>Get-Content</c> (TJ-37).
    /// </summary>
    /// <remarks>
    /// En PS 5.1, <c>Get-Content</c> usa la página de códigos ANSI si el archivo no trae BOM, y el
    /// <c>.csproj</c> es la fuente única de la versión: se lee con <c>[System.IO.File]::ReadAllText</c>,
    /// como hace <c>release.ps1</c>. Hoy es inocuo en <c>build-installer.ps1</c>, pero contradecir la regla
    /// allí donde está escrita es como se pierden las reglas.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Scripts))]
    public void NingunScript_LeeElCsprojConGetContent(string script)
    {
        string code = File.ReadAllText(Path.Combine(TestPaths.RepoRoot, script));
        // Solo líneas de código: los comentarios que EXPLICAN la regla la mencionan.
        var culpables = code.Split('\n')
            .Select((linea, i) => (linea, n: i + 1))
            .Where(x => !x.linea.TrimStart().StartsWith('#'))
            .Where(x => Regex.IsMatch(x.linea, @"Get-Content[^|;]*\$csproj", RegexOptions.IgnoreCase))
            .Select(x => $"línea {x.n}: {x.linea.Trim()}")
            .ToList();

        Assert.True(culpables.Count == 0,
            $"{script} lee el .csproj con Get-Content; usa [System.IO.File]::ReadAllText:\n  "
                + string.Join("\n  ", culpables));
    }

    /// <summary>
    /// Ningún script escribe con <c>Out-File</c>/<c>Set-Content</c> <c>-Encoding utf8</c>.
    /// </summary>
    /// <remarks>
    /// En PowerShell 5.1, <c>utf8</c> significa <b>UTF-8 con BOM</b>, siempre. Así salían las notas del
    /// release: <c>gh release create --notes-file</c> sube el archivo tal cual, y las notas de la v2.7.0 y la
    /// v2.8.0 empiezan por un U+FEFF invisible en GitHub pero presente para quien las lea por la API. Lo que
    /// no deba llevar BOM se escribe con <c>[System.IO.File]::WriteAllText</c> y
    /// <c>UTF8Encoding($false)</c>.
    /// </remarks>
    [Theory]
    [MemberData(nameof(Scripts))]
    public void NingunScript_EscribeUtf8ConElBomImplicitoDePs51(string script)
    {
        string code = File.ReadAllText(Path.Combine(TestPaths.RepoRoot, script));
        var culpables = code.Split('\n')
            .Select((linea, i) => (linea, n: i + 1))
            .Where(x => !x.linea.TrimStart().StartsWith('#'))
            .Where(x => Regex.IsMatch(x.linea, @"\b(Out-File|Set-Content|Add-Content)\b.*-Encoding\s+['""]?utf8\b", RegexOptions.IgnoreCase))
            .Select(x => $"línea {x.n}: {x.linea.Trim()}")
            .ToList();

        Assert.True(culpables.Count == 0,
            $"{script} escribe con -Encoding utf8, que en PS 5.1 añade BOM. Usa " +
            "[System.IO.File]::WriteAllText(ruta, texto, (New-Object System.Text.UTF8Encoding($false))):\n  "
                + string.Join("\n  ", culpables));
    }
}
