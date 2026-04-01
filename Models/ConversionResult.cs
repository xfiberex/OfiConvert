namespace OfiConvert.Models;

public record ConversionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string OutputPath { get; init; } = string.Empty;
    public TimeSpan Duration { get; init; }
    public bool WasRetried { get; init; }
    public int RetryCount { get; init; }

    public static ConversionResult Successful(string outputPath, TimeSpan duration = default) =>
        new() { Success = true, OutputPath = outputPath, Duration = duration };

    public static ConversionResult Failed(string errorMessage, int retryCount = 0) =>
        new() { Success = false, ErrorMessage = errorMessage, WasRetried = retryCount > 0, RetryCount = retryCount };
}