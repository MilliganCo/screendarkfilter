using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ScreenDarkFilter.Services;

internal static class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ScreenDarkFilter",
        "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                {
                    ApplyMissingDefaults(loaded, json);
                    return loaded;
                }
            }
        }
        catch
        {
            // оставляем значения по умолчанию
        }

        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
    }

    private static void ApplyMissingDefaults(AppSettings settings, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (!HasProperty(root, "Strength"))
        {
            settings.Strength = 100;
        }

        if (!HasProperty(root, "Warmth"))
        {
            settings.Warmth = 50;
        }

        if (!HasProperty(root, "Midtones"))
        {
            settings.Midtones = 50;
        }

        if (!HasProperty(root, "SoftenOneCText"))
        {
            settings.SoftenOneCText = false;
        }
    }

    private static bool HasProperty(JsonElement root, string name) =>
        root.TryGetProperty(name, out _);
}
