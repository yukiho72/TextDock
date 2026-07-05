using System.IO;
using System.Text.Json;
using TextDock.Models;

namespace TextDock.Services;

public class SettingsCorruptedException : Exception
{
    public SettingsCorruptedException(Exception inner) : base("設定ファイルが破損しています。", inner) { }
}

public class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _filePath;

    public SettingsService(string folder)
    {
        _filePath = Path.Combine(folder, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_filePath))
        {
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions)
                ?? throw new SettingsCorruptedException(new JsonException("null"));
        }
        catch (JsonException ex)
        {
            throw new SettingsCorruptedException(ex);
        }
    }

    public void Save(AppSettings settings)
    {
        File.WriteAllText(_filePath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    public AppSettings ResetToDefault()
    {
        var defaults = new AppSettings();
        Save(defaults);
        return defaults;
    }
}
