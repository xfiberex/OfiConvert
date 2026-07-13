using CommunityToolkit.Mvvm.ComponentModel;

namespace OfiConvert.Models;

public partial class ConversionOptions : ObservableObject
{
    // Propiedades parciales, no campos (MVVMTK0045). No admiten inicializador: los valores por
    // defecto van al constructor.

    [ObservableProperty]
    public partial OutputFormat OutputFormat { get; set; }

    [ObservableProperty]
    public partial string PageRange { get; set; }

    [ObservableProperty]
    public partial string SheetNames { get; set; }

    [ObservableProperty]
    public partial string SlideRange { get; set; }

    [ObservableProperty]
    public partial int ImageQuality { get; set; }

    [ObservableProperty]
    public partial int ImageDpi { get; set; }

    public ConversionOptions()
    {
        OutputFormat = OutputFormat.PDF;
        PageRange = string.Empty;
        SheetNames = string.Empty;
        SlideRange = string.Empty;
        ImageQuality = 90;
        ImageDpi = 150;
    }

    public ConversionOptions Clone() => new()
    {
        OutputFormat = OutputFormat,
        PageRange = PageRange,
        SheetNames = SheetNames,
        SlideRange = SlideRange,
        ImageQuality = ImageQuality,
        ImageDpi = ImageDpi
    };
}
