using OfiConvert.Core;
using Xunit;

namespace OfiConvert.Tests.Core;

/// <summary>
/// La tabla de decisión de los magic bytes. Lo que se prueba aquí no es "leer 8 bytes", sino que el
/// usuario reciba el mensaje CORRECTO: "protegido con contraseña" y "corrupto" son diagnósticos muy
/// distintos, y quien recibe el equivocado busca el problema donde no está.
/// </summary>
public sealed class FileSignatureTests
{
    private static byte[] Header(params byte[] first) => [.. first, .. new byte[Math.Max(0, 8 - first.Length)]];

    private static readonly byte[] OleHeader = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly byte[] ZipHeader = Header(0x50, 0x4B, 0x03, 0x04);
    private static readonly byte[] GarbageHeader = Header(0xFF, 0xFE, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0x00);

    [Theory]
    [InlineData(".docx")]
    [InlineData(".xlsx")]
    [InlineData(".pptx")]
    public void OpenXml_WithZipHeader_StillNeedsTheZipOpened(string extension)
        => Assert.Equal(FileSignatureVerdict.NeedsZipInspection, FileSignature.Classify(ZipHeader, extension));

    /// <summary>
    /// Un OpenXML cifrado se guarda DENTRO de un documento compuesto OLE. Es el caso que más se presta a
    /// confusión: la firma dice "OLE" en un archivo que dice ser ".docx", y llamarlo "corrupto" mandaría
    /// al usuario a recuperar un archivo que está perfectamente sano — solo que con contraseña.
    /// </summary>
    [Theory]
    [InlineData(".docx")]
    [InlineData(".xlsx")]
    [InlineData(".pptx")]
    public void OpenXml_WithOleHeader_IsPasswordProtectedNotCorrupt(string extension)
        => Assert.Equal(FileSignatureVerdict.PasswordProtected, FileSignature.Classify(OleHeader, extension));

    [Theory]
    [InlineData(".docx")]
    [InlineData(".xlsx")]
    [InlineData(".pptx")]
    public void OpenXml_WithNeitherSignature_IsCorrupt(string extension)
        => Assert.Equal(FileSignatureVerdict.Corrupt, FileSignature.Classify(GarbageHeader, extension));

    [Theory]
    [InlineData(".doc")]
    [InlineData(".xls")]
    [InlineData(".ppt")]
    public void LegacyOffice_WithOleHeader_IsFine(string extension)
        => Assert.Equal(FileSignatureVerdict.Ok, FileSignature.Classify(OleHeader, extension));

    /// <summary>Un .docx al que alguien le cambió la extensión a .doc: Office lo abre, así que pasa.</summary>
    [Theory]
    [InlineData(".doc")]
    [InlineData(".xls")]
    [InlineData(".ppt")]
    public void LegacyOffice_ThatIsReallyAnOpenXml_IsLetThrough(string extension)
        => Assert.Equal(FileSignatureVerdict.Ok, FileSignature.Classify(ZipHeader, extension));

    [Theory]
    [InlineData(".doc")]
    [InlineData(".xls")]
    [InlineData(".ppt")]
    public void LegacyOffice_WithNeitherSignature_IsCorrupt(string extension)
        => Assert.Equal(FileSignatureVerdict.Corrupt, FileSignature.Classify(GarbageHeader, extension));

    [Fact]
    public void Extension_IsCaseInsensitive()
        => Assert.Equal(FileSignatureVerdict.PasswordProtected, FileSignature.Classify(OleHeader, ".DOCX"));

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(7)]
    public void HeaderShorterThanEightBytes_CannotBeClassified(int length)
        => Assert.Equal(FileSignatureVerdict.TooSmall, FileSignature.Classify(new byte[length], ".docx"));

    /// <summary>
    /// Quién entra y quién no lo decide <see cref="OfficeFormats"/>. Si un archivo llega hasta aquí con
    /// otra extensión, la firma no tiene nada que opinar y no debe inventarse un "corrupto".
    /// </summary>
    [Fact]
    public void UnknownExtension_IsNotJudgedBySignature()
        => Assert.Equal(FileSignatureVerdict.Ok, FileSignature.Classify(GarbageHeader, ".txt"));
}
