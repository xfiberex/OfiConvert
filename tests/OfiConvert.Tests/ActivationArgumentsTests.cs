using OfiConvert.Helpers;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// El menú contextual del Explorador entra por aquí. En el Tier A este camino estaba <b>muerto</b>: la app
/// guardaba los argumentos y no los usaba nunca. Ahora que funciona, estas son sus dos trampas de
/// plataforma, y las dos son silenciosas — si vuelven, la app abre vacía sin decir nada:
///
/// 1. En una app <b>unpackaged</b>, los argumentos llegan como UNA cadena que <b>incluye la ruta del
///    propio .exe</b> como primer token. No se descarta a ciegas: se cae solo al filtrar por extensión.
/// 2. Las rutas del Explorador vienen <b>entrecomilladas</b> y casi siempre llevan espacios.
/// </summary>
public sealed class ActivationArgumentsTests : IDisposable
{
    private readonly string _folder;

    public ActivationArgumentsTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "OfiConvert_ActivationTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch { /* limpieza best-effort */ }
    }

    private string CreateFile(string name)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllText(path, "contenido");
        return path;
    }

    [Fact]
    public void CommandLine_FromAnUnpackagedActivation_DropsTheExePathAndKeepsTheDocuments()
    {
        var exe = CreateFile("OfiConvert.exe");
        var doc = CreateFile("informe.docx");

        var files = ActivationArguments.GetOfficeFiles($"\"{exe}\" \"{doc}\"");

        Assert.Equal([doc], files);
    }

    /// <summary>La ruta con espacios: el motivo de que exista el tokenizador propio.</summary>
    [Fact]
    public void QuotedPathsWithSpaces_SurviveTokenisation()
    {
        var doc = CreateFile("informe del año.docx");
        var sheet = CreateFile("cuentas 2026.xlsx");

        var files = ActivationArguments.GetOfficeFiles($"\"{doc}\" \"{sheet}\"");

        Assert.Equal([doc, sheet], files);
    }

    /// <summary>Sin comillas, una ruta con espacios se parte: es lo que pasa, y no debe colar medio archivo.</summary>
    [Fact]
    public void UnquotedPathWithSpaces_IsNotSalvaged()
    {
        var doc = CreateFile("informe del año.docx");

        Assert.Empty(ActivationArguments.GetOfficeFiles(doc));
    }

    [Fact]
    public void NonOfficeFiles_AreFilteredOut()
    {
        var doc = CreateFile("informe.docx");
        var text = CreateFile("notas.txt");
        var pdf = CreateFile("ya-convertido.pdf");

        var files = ActivationArguments.GetOfficeFiles($"\"{text}\" \"{doc}\" \"{pdf}\"");

        Assert.Equal([doc], files);
    }

    /// <summary>Seleccionar 5 archivos en el Explorador y que uno ya no esté (se movió, se borró).</summary>
    [Fact]
    public void PathsThatNoLongerExist_AreDropped()
    {
        var doc = CreateFile("informe.docx");
        var ghost = Path.Combine(_folder, "fantasma.docx");

        var files = ActivationArguments.GetOfficeFiles($"\"{ghost}\" \"{doc}\"");

        Assert.Equal([doc], files);
    }

    /// <summary>El mismo archivo dos veces en la línea de comandos no se encola dos veces.</summary>
    [Fact]
    public void DuplicatePaths_AreQueuedOnce()
    {
        var doc = CreateFile("informe.docx");

        var files = ActivationArguments.GetOfficeFiles($"\"{doc}\" \"{doc.ToUpperInvariant()}\"");

        Assert.Single(files);
    }

    [Fact]
    public void OrderIsPreserved()
    {
        var a = CreateFile("a.docx");
        var b = CreateFile("b.xlsx");
        var c = CreateFile("c.pptx");

        Assert.Equal([a, b, c], ActivationArguments.GetOfficeFiles($"\"{a}\" \"{b}\" \"{c}\""));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoArguments_MeansNoFiles(string? commandLine)
        => Assert.Empty(ActivationArguments.GetOfficeFiles(commandLine));

    /// <summary>Basura o una opción suelta no debe reventar el arranque de la app.</summary>
    [Fact]
    public void GarbageTokens_AreIgnoredWithoutThrowing()
        => Assert.Empty(ActivationArguments.GetOfficeFiles("--verbose | ??? \"<>:*\""));
}
