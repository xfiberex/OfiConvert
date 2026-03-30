namespace OfiConvert.Models;

public record ConversionProgress
{
    public int CurrentFile { get; init; }
    public int TotalFiles { get; init; }
    public string CurrentFileName { get; init; } = string.Empty;
    public FileConversionState State { get; init; }
}