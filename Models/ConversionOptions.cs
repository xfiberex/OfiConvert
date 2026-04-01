using CommunityToolkit.Mvvm.ComponentModel;

namespace OfiConvert.Models;

public partial class ConversionOptions : ObservableObject
{
    [ObservableProperty]
    private OutputFormat _outputFormat = OutputFormat.PDF;

    [ObservableProperty]
    private string _pageRange = string.Empty;

    [ObservableProperty]
    private string _sheetNames = string.Empty;

    [ObservableProperty]
    private string _slideRange = string.Empty;

    [ObservableProperty]
    private int _imageQuality = 90;

    [ObservableProperty]
    private int _imageDpi = 150;

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
