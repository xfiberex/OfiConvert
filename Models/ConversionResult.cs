namespace OfiConvert.Models;

public record ConversionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string OutputPath { get; init; } = string.Empty;

    public static ConversionResult Successful(string outputPath) =>
        new() { Success = true, OutputPath = outputPath };

    public static ConversionResult Failed(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}