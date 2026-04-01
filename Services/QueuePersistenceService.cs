using System.IO;
using System.Text.Json;

namespace OfiConvert.Services;

public class QueuePersistenceService
{
    private static readonly string QueueFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OfiConvert");
    private static readonly string QueuePath = Path.Combine(QueueFolder, "queue.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public List<string> LoadQueue()
    {
        try
        {
            if (File.Exists(QueuePath))
            {
                var json = File.ReadAllText(QueuePath);
                return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? [];
            }
        }
        catch { }
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
        catch { }
    }

    public void ClearQueue()
    {
        try
        {
            if (File.Exists(QueuePath))
                File.Delete(QueuePath);
        }
        catch { }
    }
}
