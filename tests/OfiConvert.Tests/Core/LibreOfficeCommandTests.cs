using OfiConvert.Core;
using Xunit;

namespace OfiConvert.Tests.Core;

/// <summary>
/// La línea de comandos de <c>soffice</c>, comprobada <b>sin LibreOffice instalado</b>.
/// </summary>
/// <remarks>
/// LibreOffice no admite dos procesos headless sobre el mismo perfil: el segundo se enchufa al primero
/// por IPC o falla, así que un lote con paralelismo se degradaba a serie o devolvía errores raros
/// (TJ-25). La cura es un perfil por proceso, y lo delicado no es la idea sino <b>la forma exacta</b> del
/// argumento: si se le pasa una ruta de Windows en vez de una URL, LibreOffice <b>no protesta</b> — la
/// ignora y vuelve al perfil compartido. El fallo seguiría ahí, en silencio, y estas pruebas pasarían si
/// solo miraran «que aparezca <c>-env:</c>».
///
/// ⚠️ <b>Lo que estas pruebas NO cubren:</b> que ocho documentos con paralelismo 4 se conviertan de
/// verdad. Eso necesita LibreOffice, que no está en la máquina donde se escribió esto. Queda anotado en
/// <c>ROADMAP.md</c> como pendiente de verificación.
/// </remarks>
public sealed class LibreOfficeCommandTests
{
    [Fact]
    public void LaRutaSeConvierteEnUrlDeArchivo()
    {
        Assert.Equal(
            "file:///C:/Users/Ana/AppData/Local/Temp/perfil",
            LibreOfficeCommand.ToFileUrl(@"C:\Users\Ana\AppData\Local\Temp\perfil"));
    }

    /// <summary>EL DETALLE QUE IMPORTA: sin barras normales, LibreOffice ignora el perfil en silencio.</summary>
    [Fact]
    public void LaUrlNoConservaNingunaBarraInvertida()
    {
        var url = LibreOfficeCommand.ToFileUrl(@"C:\a\b\c");

        Assert.DoesNotContain('\\', url);
        Assert.StartsWith("file:///", url, StringComparison.Ordinal);
    }

    [Fact]
    public void CadaConversionRecibeUnPerfilDISTINTO()
    {
        var root = Directory.CreateTempSubdirectory("OfiConvertTests_").FullName;
        try
        {
            var perfiles = Enumerable.Range(0, 8)
                .Select(_ => LibreOfficeCommand.CreateProfileFolder(root))
                .ToList();

            Assert.Equal(8, perfiles.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(perfiles, p => Assert.True(Directory.Exists(p)));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* best-effort */ }
        }
    }

    [Fact]
    public void LosArgumentosLlevanElPerfilPROPIO_YVaDelante()
    {
        var args = LibreOfficeCommand.BuildArguments(
            "pdf", @"C:\tmp\trabajo", @"C:\tmp\perfil", @"C:\docs\informe.docx");

        Assert.StartsWith("-env:UserInstallation=file:///C:/tmp/perfil ", args, StringComparison.Ordinal);

        // Delante de --convert-to: LibreOffice decide si arranca motor propio antes de mirar qué convertir.
        Assert.True(
            args.IndexOf("-env:", StringComparison.Ordinal) < args.IndexOf("--convert-to", StringComparison.Ordinal),
            $"El parámetro de entorno tiene que ir ANTES de --convert-to. Argumentos: {args}");
    }

    [Fact]
    public void LosArgumentosConservanLoQueYaFuncionaba()
    {
        var args = LibreOfficeCommand.BuildArguments(
            "csv", @"C:\tmp\trabajo", @"C:\tmp\perfil", @"C:\docs\datos.xlsx");

        Assert.Contains("--headless", args, StringComparison.Ordinal);
        Assert.Contains("--norestore", args, StringComparison.Ordinal);
        Assert.Contains("--convert-to csv", args, StringComparison.Ordinal);
        Assert.Contains(@"--outdir ""C:\tmp\trabajo""", args, StringComparison.Ordinal);
        Assert.EndsWith(@"""C:\docs\datos.xlsx""", args, StringComparison.Ordinal);
    }

    /// <summary>Una comilla en la ruta rompería el citado: se rechaza antes de construir nada.</summary>
    [Theory]
    [InlineData(@"C:\docs\raro"".docx", @"C:\tmp\t", @"C:\tmp\p")]
    [InlineData(@"C:\docs\a.docx", @"C:\tmp\ra""ro", @"C:\tmp\p")]
    [InlineData(@"C:\docs\a.docx", @"C:\tmp\t", @"C:\tmp\ra""ro")]
    public void UnaComillaEnCualquieraDeLasTresRutasSeDetecta(string source, string work, string profile)
        => Assert.True(LibreOfficeCommand.HasUnquotablePath(source, work, profile));

    [Fact]
    public void SinComillas_NoSeRechazaNada()
        => Assert.False(LibreOfficeCommand.HasUnquotablePath(
            @"C:\docs\a.docx", @"C:\tmp\t", @"C:\tmp\p"));
}
