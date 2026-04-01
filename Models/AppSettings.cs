namespace OfiConvert.Models;

public class AppSettings
{
    public string Theme { get; set; } = "System";
    public string Language { get; set; } = "es";
    public int MaxParallelConversions { get; set; } = 2;
    public bool AutoRetryEnabled { get; set; } = true;
    public int MaxRetryCount { get; set; } = 3;
    public bool MinimizeToTray { get; set; } = false;
    public bool ShowNotifications { get; set; } = true;
    public string LastOutputFolder { get; set; } = string.Empty;
    public OutputFormat DefaultOutputFormat { get; set; } = OutputFormat.PDF;
}
