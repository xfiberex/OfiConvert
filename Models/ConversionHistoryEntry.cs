namespace OfiConvert.Models;

public record ConversionHistoryEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public string SourcePath { get; init; } = string.Empty;
    public string SourceFileName { get; init; } = string.Empty;
    public string OutputPath { get; init; } = string.Empty;
    public OutputFormat Format { get; init; } = OutputFormat.PDF;
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public double DurationSeconds { get; init; }
    public long FileSizeBytes { get; init; }
}
