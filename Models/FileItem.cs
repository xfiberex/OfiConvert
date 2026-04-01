using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media.Imaging;

namespace OfiConvert.Models;

public partial class FileItem : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public string Extension { get; set; } = string.Empty;

    public OutputFormat[] AvailableFormats => OutputFormatHelper.GetFormatsForExtension(Extension);

    [ObservableProperty]
    private FileConversionState _state = FileConversionState.Pending;

    [ObservableProperty]
    private string _stateMessage = "Pendiente";

    [ObservableProperty]
    private ConversionOptions _options = new();

    [ObservableProperty]
    private BitmapSource? _thumbnail;

    [ObservableProperty]
    private int _retryCount;

    [ObservableProperty]
    private string _validationMessage = string.Empty;
}
