using OfiConvert.Models;

namespace OfiConvert.Core;

/// <summary>
/// Qué se puede sacar de cada tipo de documento, y con qué extensión. Es la tabla que decide lo que ve
/// el usuario en el desplegable de formatos, así que un error aquí le ofrece una conversión que el motor
/// no sabe hacer.
/// </summary>
public static class OutputFormatHelper
{
    /// <param name="extension">Extensión, con punto o sin él; no distingue mayúsculas.</param>
    public static OutputFormat[] GetFormatsForExtension(string extension)
    {
        return extension.TrimStart('.').ToUpperInvariant() switch
        {
            "DOC" or "DOCX" => [OutputFormat.PDF, OutputFormat.HTML],
            "XLS" or "XLSX" => [OutputFormat.PDF, OutputFormat.CSV],
            "PPT" or "PPTX" => [OutputFormat.PDF, OutputFormat.PNG, OutputFormat.JPG],
            _ => [OutputFormat.PDF]
        };
    }

    public static string GetFileExtension(OutputFormat format)
    {
        return format switch
        {
            OutputFormat.PDF => ".pdf",
            OutputFormat.HTML => ".html",
            OutputFormat.CSV => ".csv",
            OutputFormat.PNG => ".png",
            OutputFormat.JPG => ".jpg",
            _ => ".pdf"
        };
    }

    public static string GetDisplayName(OutputFormat format)
    {
        return format switch
        {
            OutputFormat.PDF => "PDF",
            OutputFormat.HTML => "HTML",
            OutputFormat.CSV => "CSV",
            OutputFormat.PNG => "PNG (Imágenes)",
            OutputFormat.JPG => "JPG (Imágenes)",
            _ => format.ToString()
        };
    }
}
