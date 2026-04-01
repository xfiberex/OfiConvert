namespace OfiConvert.Models;

public enum OutputFormat
{
    PDF,
    HTML,
    CSV,
    PNG,
    JPG
}

public static class OutputFormatHelper
{
    public static OutputFormat[] GetFormatsForExtension(string extension)
    {
        return extension.ToUpperInvariant() switch
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
