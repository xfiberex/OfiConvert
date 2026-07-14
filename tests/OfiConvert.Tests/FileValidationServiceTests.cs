using System.IO.Compression;
using OfiConvert.Services;
using Xunit;

namespace OfiConvert.Tests;

/// <summary>
/// La otra mitad de <c>FileSignature</c>: la que sí toca el disco. Se prueba contra archivos REALES
/// (incluido un OpenXML construido con <see cref="ZipArchive"/>) porque lo que puede fallar aquí es
/// precisamente la E/S: un archivo bloqueado por Word, un ZIP que no se deja abrir, un tamaño de cero.
///
/// Esta validación corre ANTES de lanzar Office: cada caso que se le escapa es un proceso WINWORD.EXE
/// arrancando contra un archivo que nunca iba a poder convertir.
/// </summary>
public sealed class FileValidationServiceTests : IDisposable
{
    private readonly FileValidationService _service = new();
    private readonly string _folder;

    public FileValidationServiceTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "OfiConvert_ValidationTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch { /* limpieza best-effort */ }
    }

    private string Write(string name, byte[] content)
    {
        var path = Path.Combine(_folder, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private static byte[] Padded(byte[] magic, int total = 64)
    {
        var buffer = new byte[total];
        magic.CopyTo(buffer, 0);
        return buffer;
    }

    private static readonly byte[] Ole = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    /// <summary>Un .docx de verdad: un ZIP con su <c>[Content_Types].xml</c> dentro.</summary>
    private string WriteOpenXml(string name, bool withContentTypes)
    {
        var path = Path.Combine(_folder, name);
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            if (withContentTypes)
            {
                using var entry = zip.CreateEntry("[Content_Types].xml").Open();
                using var writer = new StreamWriter(entry);
                writer.Write("<Types/>");
            }
            else
            {
                // Lo que se ve en un OpenXML cifrado que no llegó a envolverse en OLE: el paquete está,
                // pero su manifiesto no es legible.
                using var entry = zip.CreateEntry("EncryptedPackage").Open();
                using var writer = new StreamWriter(entry);
                writer.Write("cifrado");
            }
        }
        return path;
    }

    [Fact]
    public void MissingFile_IsRejected()
    {
        var result = _service.Validate(Path.Combine(_folder, "no-existe.docx"));

        Assert.False(result.IsValid);
        Assert.False(result.IsPasswordProtected);
    }

    [Fact]
    public void EmptyFile_IsRejected()
    {
        var result = _service.Validate(Write("vacio.docx", []));

        Assert.False(result.IsValid);
        Assert.Contains("vacío", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FileTooSmallToHaveAHeader_IsRejected()
    {
        var result = _service.Validate(Write("diminuto.docx", [0x50, 0x4B, 0x03, 0x04]));

        Assert.False(result.IsValid);
        Assert.Contains("pequeño", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FileLockedByAnotherProcess_IsRejectedWithoutLaunchingOffice()
    {
        var path = WriteOpenXml("abierto-en-word.docx", withContentTypes: true);

        // FileShare.None = lo que hace Word con el documento que tiene abierto.
        using var _ = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);

        var result = _service.Validate(path);

        Assert.False(result.IsValid);
        Assert.Contains("bloqueado", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HealthyOpenXml_IsValid()
    {
        var result = _service.Validate(WriteOpenXml("informe.docx", withContentTypes: true));

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    /// <summary>Un ZIP sin <c>[Content_Types].xml</c>: paquete cifrado, no archivo roto.</summary>
    [Fact]
    public void OpenXmlZipWithoutItsManifest_IsReportedAsPasswordProtected()
    {
        var result = _service.Validate(WriteOpenXml("cifrado.docx", withContentTypes: false));

        Assert.False(result.IsValid);
        Assert.True(result.IsPasswordProtected);
    }

    /// <summary>
    /// El caso que hay que contar bien: un .docx protegido con contraseña viaja dentro de un contenedor
    /// OLE. Decirle al usuario "corrupto" lo mandaría a intentar recuperar un archivo que está sano.
    /// </summary>
    [Fact]
    public void OpenXmlWrappedInOle_IsPasswordProtectedNotCorrupt()
    {
        var result = _service.Validate(Write("con-clave.docx", Padded(Ole)));

        Assert.False(result.IsValid);
        Assert.True(result.IsPasswordProtected);
        Assert.Contains("contraseña", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void OpenXmlThatIsNeitherZipNorOle_IsCorrupt()
    {
        var result = _service.Validate(Write("roto.docx", Padded([0xFF, 0xD8, 0xFF, 0xE0])));

        Assert.False(result.IsValid);
        Assert.False(result.IsPasswordProtected);
        Assert.Contains("corrupto", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LegacyDocWithOleHeader_IsValid()
    {
        var result = _service.Validate(Write("antiguo.doc", Padded(Ole)));

        Assert.True(result.IsValid);
    }

    /// <summary>Un .docx renombrado a .doc se convierte igual: Office lo abre, y aquí no se le estorba.</summary>
    [Fact]
    public void OpenXmlRenamedToLegacyDoc_IsLetThrough()
    {
        var path = WriteOpenXml("renombrado.docx", withContentTypes: true);
        var renamed = Path.Combine(_folder, "renombrado.doc");
        File.Move(path, renamed);

        Assert.True(_service.Validate(renamed).IsValid);
    }
}
