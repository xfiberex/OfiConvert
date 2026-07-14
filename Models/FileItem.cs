using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media.Imaging;
using OfiConvert.Core;

namespace OfiConvert.Models;

public partial class FileItem : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public string Extension { get; set; } = string.Empty;

    public OutputFormat[] AvailableFormats => OutputFormatHelper.GetFormatsForExtension(Extension);

    // Propiedades parciales, no campos (MVVMTK0045: sobre campo, el código generado no es
    // AOT-compatible en WinUI 3). No admiten inicializador: los valores por defecto van al constructor.

    [ObservableProperty]
    public partial FileConversionState State { get; set; }

    [ObservableProperty]
    public partial string StateMessage { get; set; }

    [ObservableProperty]
    public partial ConversionOptions Options { get; set; }

    [ObservableProperty]
    public partial BitmapImage? Thumbnail { get; set; }

    [ObservableProperty]
    public partial int RetryCount { get; set; }

    [ObservableProperty]
    public partial string ValidationMessage { get; set; }

    public FileItem()
    {
        State = FileConversionState.Pending;
        StateMessage = "Pendiente";
        Options = new ConversionOptions();
        ValidationMessage = string.Empty;
    }
}
