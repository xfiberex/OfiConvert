using System.IO;
using System.IO.Compression;
using OfiConvert.Core;

namespace OfiConvert.Services;

/// <param name="IsValid">¿El archivo se puede intentar convertir?</param>
/// <param name="Error">
/// El motivo del rechazo como <b>clave de traducción</b> (ver <see cref="UserMessage"/>): este servicio
/// corre en hilos de fondo y no sabe en qué idioma está la app. Lo traduce el borde de la UI.
/// </param>
/// <param name="IsPasswordProtected">Cierto solo si el rechazo es por contraseña.</param>
public record FileValidationResult(bool IsValid, UserMessage? Error, bool IsPasswordProtected = false);

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
            return new FileValidationResult(false, new UserMessage("MsgFileNotFound"));

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
            return new FileValidationResult(false, new UserMessage("MsgFileEmpty"));

        try
        {
            using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        catch (IOException)
        {
            return new FileValidationResult(false, new UserMessage("MsgFileLocked"));
        }

        var extension = Path.GetExtension(filePath);

        try
        {
            var header = new byte[FileSignature.HeaderLength];
            using (var fs = File.OpenRead(filePath))
            {
                if (fs.Length < FileSignature.HeaderLength)
                    return new FileValidationResult(false, new UserMessage("MsgFileTooSmall"));
                fs.ReadExactly(header);
            }

            return FileSignature.Classify(header, extension) switch
            {
                FileSignatureVerdict.TooSmall =>
                    new FileValidationResult(false, new UserMessage("MsgFileTooSmall")),

                FileSignatureVerdict.Corrupt =>
                    new FileValidationResult(false, new UserMessage("MsgCorruptFile")),

                FileSignatureVerdict.PasswordProtected =>
                    new FileValidationResult(false, new UserMessage("MsgPasswordProtected"), true),

                // Cabecera ZIP correcta: solo abriendo el paquete se distingue un OpenXML sano de uno
                // cifrado (que llega aquí como un ZIP sin [Content_Types].xml).
                FileSignatureVerdict.NeedsZipInspection when IsOpenXmlEncrypted(filePath) =>
                    new FileValidationResult(false, new UserMessage("MsgPasswordProtected"), true),

                _ => new FileValidationResult(true, null)
            };
        }
        catch (Exception ex)
        {
            return new FileValidationResult(false, new UserMessage("MsgValidationError", ex.Message));
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
