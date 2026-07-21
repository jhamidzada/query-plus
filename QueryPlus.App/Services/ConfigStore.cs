using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using QueryPlus.App.Models;

namespace QueryPlus.App.Services;

/// <summary>Loads/saves <see cref="AppConfig"/> as JSON under %APPDATA%\QueryPlus.</summary>
public sealed class ConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string Directory { get; }

    public string ConfigPath { get; }

    public ConfigStore(string? directory = null)
    {
        Directory = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "QueryPlus");
        ConfigPath = Path.Combine(Directory, "config.json");
    }

    public AppConfig Load()
    {
        try
        {
            MigrateFromMultiScriptPlus();
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, Options);
                if (config != null)
                    return config;
            }
        }
        catch
        {
            // Corrupt/unreadable config should not prevent the app from starting.
        }

        return new AppConfig();
    }

    /// <summary>
    /// One-time migration from the app's pre-rename config folder (%APPDATA%\MultiScriptPlus):
    /// if no QueryPlus config exists yet but the old one does, copy it over so users keep their
    /// lists, targets, and (DPAPI-encrypted) remembered passwords.
    /// </summary>
    private void MigrateFromMultiScriptPlus()
    {
        try
        {
            if (File.Exists(ConfigPath))
                return;
            var oldPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "MultiScriptPlus", "config.json");
            if (!File.Exists(oldPath))
                return;
            System.IO.Directory.CreateDirectory(Directory);
            File.Copy(oldPath, ConfigPath);
        }
        catch
        {
            // Migration is best-effort; a fresh config is an acceptable fallback.
        }
    }

    public void Save(AppConfig config)
    {
        System.IO.Directory.CreateDirectory(Directory);
        var json = JsonSerializer.Serialize(config, Options);
        File.WriteAllText(ConfigPath, json);
    }
}
