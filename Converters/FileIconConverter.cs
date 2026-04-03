using Microsoft.UI.Xaml.Data;

namespace OfiConvert.Converters;

/// <summary>
/// Converts a file extension string to a Segoe Fluent Icons glyph string.
/// </summary>
public class FileIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string extension)
        {
            return extension.ToUpper() switch
            {
                "DOC" or "DOCX" => "\uE8A5",   // Document
                "XLS" or "XLSX" => "\uE80A",   // Table
                "PPT" or "PPTX" => "\uECA5",   // Slideshow
                "PDF" => "\uEA90",              // PDF
                _ => "\uE7C3"                   // Page
            };
        }
        return "\uE7C3";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
