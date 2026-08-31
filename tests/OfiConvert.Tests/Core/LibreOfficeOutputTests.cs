using OfiConvert.Core;
using Xunit;

namespace OfiConvert.Tests.Core;

/// <summary>
/// La ruta por la que el resultado de LibreOffice llega al destino del usuario.
/// </summary>
/// <remarks>
/// Nace de una pérdida de datos silenciosa (TJ-03): <c>--outdir</c> apuntaba a la carpeta del usuario y
/// LibreOffice escribe con el nombre del original, <b>pisando</b> lo que hubiera. Con un
/// <c>informe.pdf</c> ya presente, lo sobrescribía y el <c>File.Move</c> siguiente se llevaba el nuevo a
/// <c>informe (1).pdf</c>: el archivo anterior desaparecía sin que nada lo dijera. Estas pruebas no
/// necesitan LibreOffice instalado — comprueban la lógica, que es donde estaba el fallo.
/// </remarks>
public sealed class LibreOfficeOutputTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"OfiConvertTests-{Guid.NewGuid():N}");

    public LibreOfficeOutputTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* limpieza best-effort */ }
    }

    private string Touch(string path, string content = "x")
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void CreateWorkFolder_EsExclusivaYNueva()
    {
        string a = LibreOfficeOutput.CreateWorkFolder(_root);
        string b = LibreOfficeOutput.CreateWorkFolder(_root);

        Assert.True(Directory.Exists(a));
        Assert.NotEqual(a, b);                       // dos conversiones en paralelo no comparten carpeta
        Assert.Empty(Directory.GetFiles(a));
    }

    [Theory]
    [InlineData(@"C:\docs\informe.docx", "pdf", "informe.pdf")]
    [InlineData(@"C:\docs\hoja de cálculo.xlsx", "csv", "hoja de cálculo.csv")]
    [InlineData(@"C:\docs\presentación.pptx", "png", "presentación.png")]
    public void ExpectedFileName_EsElNombreDelOriginalConLaExtensionNueva(string source, string ext, string expected)
        => Assert.Equal(expected, LibreOfficeOutput.ExpectedFileName(source, ext));

    [Fact]
    public void PickProduced_PrefiereElNombreEsperado()
    {
        string[] produced = [@"C:\tmp\otro.log", @"C:\tmp\informe.pdf"];

        Assert.Equal(@"C:\tmp\informe.pdf", LibreOfficeOutput.PickProduced(produced, "informe.pdf"));
    }

    /// <summary>Si solo hay un archivo, es ese: LibreOffice no siempre respeta el nombre.</summary>
    [Fact]
    public void PickProduced_ConUnSoloArchivo_LoElige()
        => Assert.Equal(@"C:\tmp\otro-nombre.pdf",
            LibreOfficeOutput.PickProduced([@"C:\tmp\otro-nombre.pdf"], "informe.pdf"));

    /// <summary>Con varios y ninguno esperado no se adivina: devolver el equivocado sería peor que fallar.</summary>
    [Fact]
    public void PickProduced_ConVariosYNingunoEsperado_NoAdivina()
        => Assert.Null(LibreOfficeOutput.PickProduced([@"C:\tmp\a.pdf", @"C:\tmp\b.pdf"], "informe.pdf"));

    [Fact]
    public void PickProduced_SinNada_EsNull()
        => Assert.Null(LibreOfficeOutput.PickProduced([], "informe.pdf"));

    /// <summary>
    /// LA REGRESIÓN DE TJ-03: convertir con el destino ya ocupado deja <b>los dos</b> archivos intactos.
    /// </summary>
    [Fact]
    public void MoveToFinal_ConElDestinoOcupado_NoDestruyeElArchivoAnterior()
    {
        string destino = Path.Combine(_root, "salida");
        string anterior = Touch(Path.Combine(destino, "informe.pdf"), "EL PDF DE ANTES");
        string producido = Touch(Path.Combine(LibreOfficeOutput.CreateWorkFolder(_root), "informe.pdf"), "EL NUEVO");

        string final = LibreOfficeOutput.MoveToFinal(producido, anterior);

        Assert.NotEqual(anterior, final);
        Assert.Equal("EL PDF DE ANTES", File.ReadAllText(anterior));   // intacto: esto es lo que se perdía
        Assert.Equal("EL NUEVO", File.ReadAllText(final));
        Assert.Equal("informe (1).pdf", Path.GetFileName(final));
    }

    [Fact]
    public void MoveToFinal_ConElDestinoLibre_UsaLaRutaPedida()
    {
        string destino = Path.Combine(_root, "salida2");
        string producido = Touch(Path.Combine(LibreOfficeOutput.CreateWorkFolder(_root), "informe.pdf"), "EL NUEVO");
        string outputPath = Path.Combine(destino, "informe.pdf");

        string final = LibreOfficeOutput.MoveToFinal(producido, outputPath);

        Assert.Equal(outputPath, final);
        Assert.Equal("EL NUEVO", File.ReadAllText(final));
        Assert.False(File.Exists(producido));   // se mueve, no se copia: nada queda en %TEMP%
    }

    /// <summary>
    /// Dos documentos con el MISMO nombre en carpetas distintas: cada uno en su carpeta de trabajo, así
    /// que ninguno ve el resultado del otro. Con una carpeta compartida se pisarían.
    /// </summary>
    [Fact]
    public void DosConversionesDelMismoNombre_NoSePisan()
    {
        string a = Touch(Path.Combine(LibreOfficeOutput.CreateWorkFolder(_root), "informe.pdf"), "A");
        string b = Touch(Path.Combine(LibreOfficeOutput.CreateWorkFolder(_root), "informe.pdf"), "B");

        string destino = Path.Combine(_root, "salida3");
        string finalA = LibreOfficeOutput.MoveToFinal(a, Path.Combine(destino, "informe.pdf"));
        string finalB = LibreOfficeOutput.MoveToFinal(b, Path.Combine(destino, "informe.pdf"));

        Assert.NotEqual(finalA, finalB);
        Assert.Equal("A", File.ReadAllText(finalA));
        Assert.Equal("B", File.ReadAllText(finalB));
    }
}
