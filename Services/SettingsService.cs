using System.IO;
using System.Text.Json;
using OfiConvert.Helpers;
using OfiConvert.Models;
using Serilog;

namespace OfiConvert.Services;

public class SettingsService
{
    private static readonly string SettingsFolder = AppPaths.DataFolder;
    private static readonly string SettingsPath = AppPaths.Settings;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
                return ValidateSettings(settings);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error loading settings, using defaults");
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsFolder);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error saving settings");
        }
    }

    private static AppSettings ValidateSettings(AppSettings settings)
    {
        settings.MaxParallelConversions = Math.Clamp(settings.MaxParallelConversions, 1, 8);
        settings.MaxRetryCount = Math.Clamp(settings.MaxRetryCount, 1, 10);
        if (settings.Theme is not ("System" or "Dark" or "Light"))
            settings.Theme = "System";
        if (!LocalizationService.IsSupported(settings.Language))
            settings.Language = LocalizationService.DefaultLanguage;
        return settings;
    }
}
