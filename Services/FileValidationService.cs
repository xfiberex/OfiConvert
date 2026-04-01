using System.IO;
using System.IO.Compression;

namespace OfiConvert.Services;

public record FileValidationResult(bool IsValid, string? ErrorMessage, bool IsPasswordProtected = false);

public class FileValidationService
{
    private static readonly byte[] OleMagic = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly byte[] ZipMagic = [0x50, 0x4B, 0x03, 0x04];

    public FileValidationResult Validate(string filePath)
    {
        if (!File.Exists(filePath))
            return new FileValidationResult(false, "El archivo no existe.");

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
            return new FileValidationResult(false, "El archivo está vacío.");

        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (IOException)
        {
            return new FileValidationResult(false, "El archivo está bloqueado por otro proceso.");
        }

        var extension = Path.GetExtension(filePath).ToLowerInvariant();

        try
        {
            var header = new byte[8];
            using (var fs = File.OpenRead(filePath))
            {
                if (fs.Length < 8)
                    return new FileValidationResult(false, "El archivo es demasiado pequeño para ser válido.");
                fs.ReadExactly(header);
            }

            bool isOpenXml = extension is ".docx" or ".xlsx" or ".pptx";
            bool isOle = extension is ".doc" or ".xls" or ".ppt";

            if (isOpenXml)
            {
                if (header.AsSpan(0, 4).SequenceEqual(ZipMagic))
                {
                    if (IsOpenXmlEncrypted(filePath))
                        return new FileValidationResult(false, "El archivo está protegido con contraseña.", true);
                }
                else if (header.AsSpan(0, 8).SequenceEqual(OleMagic))
                {
                    // Open XML file encrypted as OLE compound document
                    return new FileValidationResult(false, "El archivo está protegido con contraseña.", true);
                }
                else
                {
                    return new FileValidationResult(false, "El archivo parece estar corrupto (formato inválido).");
                }
            }
            else if (isOle)
            {
                if (!header.AsSpan(0, 8).SequenceEqual(OleMagic))
                {
                    if (header.AsSpan(0, 4).SequenceEqual(ZipMagic))
                        return new FileValidationResult(true, null); // Renamed Open XML file
                    return new FileValidationResult(false, "El archivo parece estar corrupto (formato inválido).");
                }
            }
        }
        catch (Exception ex)
        {
            return new FileValidationResult(false, $"Error al validar: {ex.Message}");
        }

        return new FileValidationResult(true, null);
    }

    private static bool IsOpenXmlEncrypted(string filePath)
    {
        try
        {
            using var zip = ZipFile.OpenRead(filePath);
            var contentTypes = zip.GetEntry("[Content_Types].xml");
            return contentTypes is null;
        }
        catch
        {
            return true;
        }
    }
}
