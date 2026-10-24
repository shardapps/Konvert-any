using System.Text.Json;

namespace Konvert;

public sealed class SettingsStore(string? directory = null)
{
    public string DirectoryPath { get; } = directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shardapps", "Konvert");

    public ConversionSettings Load()
    {
        try
        {
            var path = Path.Combine(DirectoryPath, "settings.json");
            if (!File.Exists(path)) return new();
            if (new FileInfo(path).Length > 64 * 1024) return new();
            return (JsonSerializer.Deserialize<ConversionSettings>(File.ReadAllText(path)) ?? new()).Validate();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException)
        {
            return new();
        }
    }

    public void Save(ConversionSettings settings)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temp = Path.Combine(DirectoryPath, $"settings-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(settings.Validate(), new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, Path.Combine(DirectoryPath, "settings.json"), true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
