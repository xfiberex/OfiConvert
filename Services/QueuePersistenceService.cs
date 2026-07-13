using System.IO;
using System.Text.Json;
using OfiConvert.Helpers;
using Serilog;

namespace OfiConvert.Services;

public class QueuePersistenceService
{
    private static readonly string QueueFolder = AppPaths.DataFolder;
    private static readonly string QueuePath = AppPaths.Queue;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public List<string> LoadQueue()
    {
        try
        {
            if (File.Exists(QueuePath))
            {
                var json = File.ReadAllText(QueuePath);
                var paths = JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
                return paths
                    .Where(p => !string.IsNullOrWhiteSpace(p)
                        && Path.IsPathRooted(p)
                        && !p.StartsWith(@"\\", StringComparison.Ordinal)
                        && File.Exists(p))
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error loading persisted queue");
        }
        return [];
    }

    public void SaveQueue(IEnumerable<string> filePaths)
    {
        try
        {
            Directory.CreateDirectory(QueueFolder);
            var json = JsonSerializer.Serialize(filePaths.ToList(), JsonOptions);
            File.WriteAllText(QueuePath, json);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error saving queue");
        }
    }

    public void ClearQueue()
    {
        try
        {
            if (File.Exists(QueuePath))
                File.Delete(QueuePath);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error clearing queue");
        }
    }
}
