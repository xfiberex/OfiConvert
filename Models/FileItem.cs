using CommunityToolkit.Mvvm.ComponentModel;

namespace OfiConvert.Models;

public partial class FileItem : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public long SizeInBytes { get; set; }
    public string Extension { get; set; } = string.Empty;

    [ObservableProperty]
    private FileConversionState _state = FileConversionState.Pending;

    [ObservableProperty]
    private string _stateMessage = "Pendiente";
}
