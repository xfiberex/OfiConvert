using System.ComponentModel;
using System.Xml.Linq;

namespace OfiConvert.Helpers;

/// <summary>
/// Provides runtime localization by loading string resources from embedded XAML dictionaries.
/// Strings are accessed via the indexer: Loc.Instance["KeyName"]
/// Bind in XAML with: Text="{Binding [KeyName], Source={StaticResource Loc}}"
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationService> _instance = new(() => new LocalizationService());
    public static LocalizationService Instance => _instance.Value;

    /// <summary>
    /// Idiomas admitidos. Fuente única: cualquier código fuera de esta lista cae a español, y
    /// <see cref="Services.SettingsService"/> valida contra ella (antes solo aceptaba es/en, y los
    /// otros seis no sobrevivían a un reinicio).
    /// </summary>
    public static readonly string[] SupportedLanguages = ["es", "en", "pt", "fr", "de", "it", "zh", "ja"];

    public const string DefaultLanguage = "es";

    public static bool IsSupported(string? languageCode) =>
        languageCode is not null && SupportedLanguages.Contains(languageCode);

    private Dictionary<string, string> _strings = new();
    private string _currentLanguage = DefaultLanguage;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string this[string key] => _strings.TryGetValue(key, out var value) ? value : key;

    public LocalizationService()
    {
        LoadLanguage(DefaultLanguage);
    }

    public string CurrentLanguage => _currentLanguage;

    public void LoadLanguage(string languageCode)
    {
        if (!IsSupported(languageCode))
            languageCode = DefaultLanguage;

        _currentLanguage = languageCode;
        var cultureName = languageCode switch
        {
            "en" => "en-US",
            "pt" => "pt-BR",
            "fr" => "fr-FR",
            "de" => "de-DE",
            "it" => "it-IT",
            "zh" => "zh-CN",
            "ja" => "ja-JP",
            _ => "es-ES"
        };

        var filePath = Path.Combine(AppContext.BaseDirectory, "Lang", $"{cultureName}.xaml");
        if (!File.Exists(filePath))
        {
            filePath = Path.Combine(AppContext.BaseDirectory, "Lang", "es-ES.xaml");
        }

        if (!File.Exists(filePath)) return;

        try
        {
            var doc = XDocument.Load(filePath);
            XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
            XNamespace system = "clr-namespace:System;assembly=System.Runtime";

            var newStrings = new Dictionary<string, string>();

            foreach (var element in doc.Root?.Elements() ?? [])
            {
                var key = element.Attribute(x + "Key")?.Value;
                if (key is not null)
                {
                    newStrings[key] = element.Value;
                }
            }

            _strings = newStrings;
        }
        catch
        {
            // Keep existing strings on error
        }

        // Notify all bindings to refresh
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    public string Get(string key) => this[key];
}
