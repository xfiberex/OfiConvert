using System.IO;
using System.IO.Compression;
using OfiConvert.Core;

namespace OfiConvert.Services;

public record FileValidationResult(bool IsValid, string? ErrorMessage, bool IsPasswordProtected = false);

/// <summary>
/// Valida un archivo ANTES de lanzar Office contra él. Aquí vive solo la parte con E/S (existe, está
/// vacío, está bloqueado, qué hay dentro del ZIP); la tabla de decisión sobre los magic bytes es
/// <see cref="FileSignature"/>, que se puede probar sin tocar el disco.
/// </summary>
public class FileValidationService
{
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

        var extension = Path.GetExtension(filePath);

        try
        {
            var header = new byte[FileSignature.HeaderLength];
            using (var fs = File.OpenRead(filePath))
            {
                if (fs.Length < FileSignature.HeaderLength)
                    return new FileValidationResult(false, "El archivo es demasiado pequeño para ser válido.");
                fs.ReadExactly(header);
            }

            return FileSignature.Classify(header, extension) switch
            {
                FileSignatureVerdict.TooSmall =>
                    new FileValidationResult(false, "El archivo es demasiado pequeño para ser válido."),

                FileSignatureVerdict.Corrupt =>
                    new FileValidationResult(false, "El archivo parece estar corrupto (formato inválido)."),

                FileSignatureVerdict.PasswordProtected =>
                    new FileValidationResult(false, "El archivo está protegido con contraseña.", true),

                // Cabecera ZIP correcta: solo abriendo el paquete se distingue un OpenXML sano de uno
                // cifrado (que llega aquí como un ZIP sin [Content_Types].xml).
                FileSignatureVerdict.NeedsZipInspection when IsOpenXmlEncrypted(filePath) =>
                    new FileValidationResult(false, "El archivo está protegido con contraseña.", true),

                _ => new FileValidationResult(true, null)
            };
        }
        catch (Exception ex)
        {
            return new FileValidationResult(false, $"Error al validar: {ex.Message}");
        }
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
