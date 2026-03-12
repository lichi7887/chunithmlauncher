using System.Diagnostics;
using ChunithmLauncher.Models;

namespace ChunithmLauncher.Services;

public sealed class LauncherRuntime : IDisposable
{
    private readonly DisplayService _displayService;
    private readonly LauncherConfig _config;
    private readonly GameWindowWatcher _watcher;
    private Process? _gameProcess;
    private bool _holdingTargetForTest;

    public event Action<string>? StatusChanged;

    public LauncherRuntime(DisplayService displayService, LauncherConfig config)
    {
        _displayService = displayService;
        _config = config;
        _watcher = new GameWindowWatcher(() => _config.WindowTitle);
        _watcher.WindowPresenceChanged += OnWindowPresenceChanged;
    }

    public string LaunchGame()
    {
        if (string.IsNullOrWhiteSpace(_config.StartBatPath) || !File.Exists(_config.StartBatPath))
        {
            return "start.bat 路径无效。";
        }

        var monitor = ResolveMonitor();
        if (monitor is null)
        {
            return "未找到已配置显示器。";
        }

        _config.OriginalDisplayMode ??= monitor.CurrentMode;

        if (!_displayService.TryChangeResolution(monitor.DeviceName, _config.TargetDisplayMode))
        {
            return "切换目标分辨率失败。";
        }

        _holdingTargetForTest = false;
        _gameProcess = Process.Start(new ProcessStartInfo
        {
            FileName = _config.StartBatPath,
            WorkingDirectory = Path.GetDirectoryName(_config.StartBatPath)!,
            UseShellExecute = true
        });

        _watcher.Start();
        StatusChanged?.Invoke("游戏启动中，已切换至目标分辨率。");
        return "ok";
    }

    public string TestSwitch()
    {
        var monitor = ResolveMonitor();
        if (monitor is null)
        {
            return "未找到已配置显示器。";
        }

        _config.OriginalDisplayMode ??= monitor.CurrentMode;

        if (!_displayService.TryChangeResolution(monitor.DeviceName, _config.TargetDisplayMode))
        {
            return "测试切换失败。";
        }

        _holdingTargetForTest = true;
        StatusChanged?.Invoke("测试模式：已切换并保持目标分辨率。");
        return "ok";
    }

    public string Restore()
    {
        var monitor = ResolveMonitor();
        if (monitor is null || _config.OriginalDisplayMode is null)
        {
            return "没有可恢复的原始分辨率。";
        }

        var restored = _displayService.TryChangeResolution(monitor.DeviceName, _config.OriginalDisplayMode);
        if (restored)
        {
            _holdingTargetForTest = false;
            StatusChanged?.Invoke("已恢复原始分辨率。");
            return "ok";
        }

        return "恢复分辨率失败。";
    }

    private void OnWindowPresenceChanged(bool isPresent)
    {
        if (_holdingTargetForTest)
        {
            return;
        }

        if (isPresent)
        {
            StatusChanged?.Invoke("检测到游戏窗口，保持目标分辨率。");
            return;
        }

        if (_gameProcess is not null && _gameProcess.HasExited)
        {
            Restore();
            _watcher.Stop();
        }
    }

    private DisplayMonitor? ResolveMonitor()
    {
        var monitors = _displayService.GetMonitors();
        if (monitors.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(_config.PrimaryMonitorId))
        {
            var selected = monitors.FirstOrDefault(m => m.Id == _config.PrimaryMonitorId);
            if (selected is not null)
            {
                return selected;
            }
        }

        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
    }

    public void Dispose()
    {
        _watcher.Dispose();
    }
}
