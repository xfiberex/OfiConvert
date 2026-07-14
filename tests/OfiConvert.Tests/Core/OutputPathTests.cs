using OfiConvert.Core;
using Xunit;

namespace OfiConvert.Tests.Core;

/// <summary>
/// <see cref="OutputPath.GetSafe"/> decide dónde se escribe el resultado de una conversión, y sus dos
/// garantías son de seguridad: la salida no se sale de la carpeta elegida, y nunca se pisa un archivo
/// que ya existe. Las pruebas van contra una carpeta temporal REAL porque la comprobación de existencia
/// es <c>File.Exists</c>: simularla probaría otra cosa.
/// </summary>
public sealed class OutputPathTests : IDisposable
{
    private readonly string _folder;

    public OutputPathTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "OfiConvert_OutputPathTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch { /* limpieza best-effort */ }
    }

    [Fact]
    public void GetSafe_WhenNothingExists_KeepsTheName()
    {
        var result = OutputPath.GetSafe(_folder, "informe.pdf");

        Assert.Equal(Path.Combine(_folder, "informe.pdf"), result);
    }

    /// <summary>Nunca sobrescribir: el archivo previo del usuario sobrevive intacto.</summary>
    [Fact]
    public void GetSafe_WhenTheFileExists_RenamesInsteadOfOverwriting()
    {
        File.WriteAllText(Path.Combine(_folder, "informe.pdf"), "el de antes");

        var result = OutputPath.GetSafe(_folder, "informe.pdf");

        Assert.Equal(Path.Combine(_folder, "informe (1).pdf"), result);
        Assert.Equal("el de antes", File.ReadAllText(Path.Combine(_folder, "informe.pdf")));
    }

    [Fact]
    public void GetSafe_WithSeveralCollisions_CountsUpUntilItFindsAFreeName()
    {
        File.WriteAllText(Path.Combine(_folder, "informe.pdf"), "x");
        File.WriteAllText(Path.Combine(_folder, "informe (1).pdf"), "x");
        File.WriteAllText(Path.Combine(_folder, "informe (2).pdf"), "x");

        Assert.Equal(Path.Combine(_folder, "informe (3).pdf"), OutputPath.GetSafe(_folder, "informe.pdf"));
    }

    /// <summary>
    /// El nombre viene del archivo de origen. Si trae componentes de directorio, se descartan: la salida
    /// va SIEMPRE a la carpeta que eligió el usuario, no a donde diga el nombre.
    /// </summary>
    [Theory]
    [InlineData(@"..\..\evil.pdf")]
    [InlineData(@"C:\Windows\System32\evil.pdf")]
    [InlineData(@"subcarpeta\evil.pdf")]
    public void GetSafe_StripsAnyDirectoryComponentFromTheName(string fileName)
    {
        var result = OutputPath.GetSafe(_folder, fileName);

        Assert.Equal(Path.Combine(_folder, "evil.pdf"), result);
    }

    /// <summary>Un nombre que resuelve fuera de la carpeta destino no se corrige: se rechaza.</summary>
    [Theory]
    [InlineData("..")]
    [InlineData(@"..\")]
    public void GetSafe_WhenTheNameEscapesTheFolder_Throws(string fileName)
        => Assert.Throws<InvalidOperationException>(() => OutputPath.GetSafe(_folder, fileName));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void GetSafe_WithoutAName_Throws(string fileName)
        => Assert.Throws<ArgumentException>(() => OutputPath.GetSafe(_folder, fileName));

    /// <summary>
    /// Una presentación a PNG/JPG son N imágenes y van juntas en su propia subcarpeta. Aquí NO se renombra
    /// si ya existe (convertir dos veces la misma presentación reescribe sus imágenes, que es lo esperable),
    /// pero la garantía de contención se mantiene igual.
    /// </summary>
    [Fact]
    public void GetSafeFolder_PutsTheImagesInASubfolderOfTheDestination()
    {
        var result = OutputPath.GetSafeFolder(_folder, "Presentacion ventas");

        Assert.Equal(Path.Combine(_folder, "Presentacion ventas"), result);
    }

    [Fact]
    public void GetSafeFolder_ReusesTheFolderIfItAlreadyExists()
    {
        var existing = Path.Combine(_folder, "Presentacion ventas");
        Directory.CreateDirectory(existing);

        Assert.Equal(existing, OutputPath.GetSafeFolder(_folder, "Presentacion ventas"));
    }

    [Theory]
    [InlineData("..")]
    [InlineData(@"..\..\evil")]
    public void GetSafeFolder_CannotEscapeTheDestination(string folderName)
    {
        var result = Record.Exception(() => OutputPath.GetSafeFolder(_folder, folderName));

        // O bien lo rechaza, o bien se queda dentro: lo que NO puede es acabar fuera de la carpeta.
        if (result is null)
            Assert.StartsWith(_folder + Path.DirectorySeparatorChar, OutputPath.GetSafeFolder(_folder, folderName), StringComparison.OrdinalIgnoreCase);
        else
            Assert.IsType<InvalidOperationException>(result);
    }

    /// <summary>
    /// La comprobación de contención compara con separador final. Sin él, una carpeta hermana cuyo nombre
    /// empieza igual ("Salida" / "SalidaOtra") pasaría por dentro de la carpeta destino.
    /// </summary>
    [Fact]
    public void GetSafe_DoesNotConfuseASiblingFolderWithTheSamePrefix()
    {
        var salida = Path.Combine(_folder, "Salida");
        var salidaOtra = Path.Combine(_folder, "SalidaOtra");
        Directory.CreateDirectory(salida);
        Directory.CreateDirectory(salidaOtra);

        var result = OutputPath.GetSafe(salida, "informe.pdf");

        Assert.StartsWith(salida + Path.DirectorySeparatorChar, result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SalidaOtra", result, StringComparison.OrdinalIgnoreCase);
    }
}
