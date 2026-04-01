using System.IO;
using System.Text.Json;
using OfiConvert.Models;
using Serilog;

namespace OfiConvert.Services;

public interface IConversionHistoryService
{
    List<ConversionHistoryEntry> GetHistory();
    void AddEntry(ConversionHistoryEntry entry);
    void ClearHistory();
    void ExportToCsv(string filePath);
    void ExportToTxt(string filePath);
}

public class ConversionHistoryService : IConversionHistoryService
{
    private static readonly string HistoryFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OfiConvert");
    private static readonly string HistoryPath = Path.Combine(HistoryFolder, "history.json");

    private readonly object _lock = new();
    private List<ConversionHistoryEntry> _history = [];
    private bool _loaded;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public List<ConversionHistoryEntry> GetHistory()
    {
        lock (_lock)
        {
            if (!_loaded) LoadHistory();
            return [.. _history];
        }
    }

    public void AddEntry(ConversionHistoryEntry entry)
    {
        lock (_lock)
        {
            if (!_loaded) LoadHistory();
            _history.Insert(0, entry);
            if (_history.Count > 1000) _history.RemoveRange(1000, _history.Count - 1000);
            SaveHistory();
        }
    }

    public void ClearHistory()
    {
        lock (_lock)
        {
            _history.Clear();
            SaveHistory();
        }
    }

    public void ExportToCsv(string filePath)
    {
        lock (_lock)
        {
            if (!_loaded) LoadHistory();
            var lines = new List<string>
            {
                "Fecha,Archivo,Salida,Formato,Resultado,Error,Duración(s),Tamaño(bytes)"
            };
            foreach (var entry in _history)
            {
                var source = SanitizeCsvField(entry.SourceFileName);
                var output = SanitizeCsvField(entry.OutputPath);
                var error = SanitizeCsvField(entry.ErrorMessage);
                lines.Add($"\"{entry.Timestamp:yyyy-MM-dd HH:mm:ss}\",\"{source}\",\"{output}\",{entry.Format},{(entry.Success ? "OK" : "Error")},\"{error}\",{entry.DurationSeconds:F1},{entry.FileSizeBytes}");
            }
            File.WriteAllLines(filePath, lines, System.Text.Encoding.UTF8);
        }
    }

    private static string SanitizeCsvField(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var sanitized = value.Replace("\"", "\"\"");
        if (sanitized[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
            sanitized = "'" + sanitized;
        return sanitized;
    }

    public void ExportToTxt(string filePath)
    {
        lock (_lock)
        {
            if (!_loaded) LoadHistory();
        var lines = new List<string>
        {
            "╔══════════════════════════════════════════════════════════════╗",
            "║           Historial de Conversiones - OfiConvert            ║",
            "╚══════════════════════════════════════════════════════════════╝",
            ""
        };

        foreach (var entry in _history)
        {
            var status = entry.Success ? "✓ Éxito" : "✗ Error";
            lines.Add($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] {entry.SourceFileName}");
            lines.Add($"  Formato: {entry.Format} | Resultado: {status}");
            lines.Add($"  Salida: {entry.OutputPath}");
            if (!string.IsNullOrEmpty(entry.ErrorMessage))
                lines.Add($"  Error: {entry.ErrorMessage}");
            lines.Add($"  Duración: {entry.DurationSeconds:F1}s | Tamaño: {FormatSize(entry.FileSizeBytes)}");
            lines.Add("");
        }

        lines.Add($"Total: {_history.Count} conversiones | {_history.Count(e => e.Success)} exitosas | {_history.Count(e => !e.Success)} fallidas");
            File.WriteAllLines(filePath, lines, System.Text.Encoding.UTF8);
        }
    }

    private void LoadHistory()
    {
        try
        {
            if (File.Exists(HistoryPath))
            {
                var json = File.ReadAllText(HistoryPath);
                _history = JsonSerializer.Deserialize<List<ConversionHistoryEntry>>(json, JsonOptions) ?? [];
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error loading conversion history");
            _history = [];
        }
        _loaded = true;
    }

    private void SaveHistory()
    {
        try
        {
            Directory.CreateDirectory(HistoryFolder);
            var json = JsonSerializer.Serialize(_history, JsonOptions);
            File.WriteAllText(HistoryPath, json);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving conversion history");
        }
    }

    private static string FormatSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1) { order++; len /= 1024; }
        return $"{len:0.##} {sizes[order]}";
    }
}
