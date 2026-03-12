using System.Text.Json;
using ChunithmLauncher.Models;

namespace ChunithmLauncher.Services;

public sealed class ConfigService
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private readonly string _configPath;

    public ConfigService()
    {
        var appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ChunithmLauncher");
        Directory.CreateDirectory(appDir);
        _configPath = Path.Combine(appDir, "config.json");
    }

    public LauncherConfig Load()
    {
        if (!File.Exists(_configPath))
        {
            return new LauncherConfig();
        }

        var text = File.ReadAllText(_configPath);
        var config = JsonSerializer.Deserialize<LauncherConfig>(text, _jsonOptions);
        return config ?? new LauncherConfig();
    }

    public void Save(LauncherConfig config)
    {
        var text = JsonSerializer.Serialize(config, _jsonOptions);
        File.WriteAllText(_configPath, text);
    }
}
