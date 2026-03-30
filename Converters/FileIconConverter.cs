using System;
using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace OfiConvert.Converters
{
    public class FileIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string extension)
            {
                return extension.ToUpper() switch
                {
                    "DOC" or "DOCX" => SymbolRegular.DocumentText24,
                    "XLS" or "XLSX" => SymbolRegular.TableSimple24,
                    "PPT" or "PPTX" => SymbolRegular.Presenter24,
                    "PDF" => SymbolRegular.DocumentPdf24,
                    _ => SymbolRegular.Document24
                };
            }
            return SymbolRegular.Document24;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
