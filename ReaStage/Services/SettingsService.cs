using Microsoft.Extensions.Logging;
using ReaStage.Core;
using System;
using System.IO;
using System.Text.Json;

namespace ReaStage.Services;

internal class SettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly ILogger<SettingsService> logger;
    private readonly string settingsPath;

    public AppSettings Settings { get; }

    public SettingsService(ILogger<SettingsService> logger)
    {
        this.logger = logger;

        // On Linux ApplicationData maps to ~/.config
        string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ReaStage");
        settingsPath = Path.Combine(folder, "settings.json");
        Settings = Load();

        // Create the file on first run so the user has something to edit
        if (!File.Exists(settingsPath))
        {
            Save();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
            File.WriteAllText(settingsPath, JsonSerializer.Serialize(Settings, JsonOptions));
            logger.LogDebug("Settings saved to {Path}", settingsPath);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save settings to {Path}", settingsPath);
        }
    }

    private AppSettings Load()
    {
        try
        {
            if (File.Exists(settingsPath))
            {
                AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(settingsPath), JsonOptions);
                if (loaded is not null)
                {
                    logger.LogDebug("Settings loaded from {Path}", settingsPath);
                    return loaded;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load settings from {Path}, using defaults", settingsPath);
        }

        return new AppSettings();
    }
}
