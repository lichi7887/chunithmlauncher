using System.Reflection;
using System.Text.Json;
using ChunithmLauncher.Models;

namespace ChunithmLauncher.Services;

public sealed class LauncherBridge
{
    private readonly ConfigService _configService;
    private readonly DisplayService _displayService;
    private readonly LauncherRuntime _runtime;
    private readonly LauncherConfig _config;

    public LauncherBridge(ConfigService configService, DisplayService displayService, LauncherRuntime runtime, LauncherConfig config)
    {
        _configService = configService;
        _displayService = displayService;
        _runtime = runtime;
        _config = config;
    }

    public string GetBootstrapData()
    {
        var monitors = _displayService.GetMonitors();

        if (_config.OriginalDisplayMode is null)
        {
            var initial = monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors.FirstOrDefault();
            if (initial is not null)
            {
                _config.OriginalDisplayMode = initial.CurrentMode;
            }
        }

        var payload = new
        {
            config = _config,
            monitors,
            version = GetVersionString()
        };

        return JsonSerializer.Serialize(payload);
    }

    public string SaveConfig(string json)
    {
        var incoming = JsonSerializer.Deserialize<LauncherConfig>(json);
        if (incoming is null)
        {
            return "配置序列化失败。";
        }

        _config.StartBatPath = incoming.StartBatPath;
        _config.PrimaryMonitorId = incoming.PrimaryMonitorId;
        _config.OriginalDisplayMode = incoming.OriginalDisplayMode;
        _config.TargetDisplayMode = incoming.TargetDisplayMode;
        _config.ThemeColorHex = incoming.ThemeColorHex;
        _config.WindowTitle = incoming.WindowTitle;
        _config.BackgroundSource = incoming.BackgroundSource;
        _config.IsFirstRunCompleted = incoming.IsFirstRunCompleted;

        _configService.Save(_config);
        return "ok";
    }

    public string LaunchGame() => _runtime.LaunchGame();

    public string TestSwitch() => _runtime.TestSwitch();

    public string RestoreResolution() => _runtime.Restore();

    private static string GetVersionString()
    {
        var informational = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        return string.IsNullOrWhiteSpace(informational)
            ? DateTimeOffset.Now.ToString("yyyy.MM.dd.HHmm")
            : informational;
    }
}
