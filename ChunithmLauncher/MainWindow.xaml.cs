using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Web.WebView2.Core;
using WinForms = System.Windows.Forms;

namespace ChunithmLauncher;

public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly List<DisplayInfo> _displays = new();
    private string? _primaryDisplayId;
    private string? _primaryDisplayName;
    private string? _startBatPath;
    private string? _originalMode;
    private const string FixedTargetMode = "1920×1080 @ 120Hz";
    private string _targetMode = FixedTargetMode;
    private string _launchMode = "smart";
    private string _themeColor = "#fdd500";
    private const string DefaultGameWindowTitle = "teaGfx DirectX Release";
    private string _gameWindowTitle = DefaultGameWindowTitle;
    private string? _backgroundImagePath;

    private Config _config = new();
    private bool _isLaunching;
    private DisplayMode? _lastKnownOriginalMode;
    private bool _testSwitchActive;
    private CancellationTokenSource? _testSwitchCts;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += (_, _) => SafeRestoreOnExit();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!EnsureWebView2RuntimeInstalled())
        {
            Close();
            return;
        }

        await WebView.EnsureCoreWebView2Async();
        WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        WebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        WebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
        WebView.NavigationCompleted += (_, _) => SendInit();

        LoadConfig();
        DetectDisplays();
        ApplyConfigToState();

        WebView.Source = new Uri(ResolveUiIndexPath());
        ApplyWindowBackdrop();
    }

    private bool EnsureWebView2RuntimeInstalled()
    {
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            if (!string.IsNullOrWhiteSpace(version))
            {
                return true;
            }
        }
        catch
        {
            // fall through and show install guidance
        }

        var result = System.Windows.MessageBox.Show(
            "未检测到 WebView2 Runtime。\n\n为精简项目体积，该运行时需要由用户自行安装。\n\n是否现在打开官方下载页？",
            "缺少运行时",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://developer.microsoft.com/microsoft-edge/webview2/",
                UseShellExecute = true,
            });
        }

        return false;
    }

    private void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        WebMessage? message = null;
        try
        {
            message = JsonSerializer.Deserialize<WebMessage>(e.WebMessageAsJson, JsonOptions);
        }
        catch
        {
            SetStatus("消息解析失败", "#ff5a6a");
            return;
        }

        if (message?.Type is null)
        {
            return;
        }

        switch (message.Type)
        {
            case "pick-start-bat":
                PickStartBat();
                break;
            case "detect-displays":
                DetectDisplays();
                PersistConfig();
                SendInit();
                break;
            case "read-current-mode":
                ReadCurrentMode();
                PersistConfig();
                SendInit();
                break;
            case "reset-target":
                _targetMode = FixedTargetMode;
                PersistConfig();
                SendInit();
                break;
            case "save-settings":
                ApplySettings(message.Payload);
                PersistConfig();
                SetStatus("设置已保存", "#7dffa0");
                SendInit();
                break;
            case "test-switch":
                _ = TestSwitchAsync();
                break;
            case "restore-original":
                _ = RestoreOriginalAsync();
                break;
            case "launch-game":
                _ = LaunchGameAsync();
                break;
            case "open-game-folder":
                OpenGameFolder();
                break;
            case "open-segatools-ini":
                OpenSegatoolsIniInVsCode();
                break;
            case "apply-recommended-segatools-gfx":
                ApplyRecommendedSegatoolsGfxConfig();
                break;
            case "set-launch-mode":
                if (message.Payload.TryGetProperty("mode", out var modeElement))
                {
                    _launchMode = modeElement.GetString() ?? "smart";
                    PersistConfig();
                }
                break;
            case "set-theme":
                if (message.Payload.TryGetProperty("color", out var colorElement))
                {
                    _themeColor = colorElement.GetString() ?? _themeColor;
                    PersistConfig();
                }
                break;
            case "pick-background-image":
                PickBackgroundImage();
                break;
            case "set-background-image":
                if (message.Payload.TryGetProperty("path", out var bgPathElement))
                {
                    _backgroundImagePath = bgPathElement.GetString();
                    PersistConfig();
                }
                break;
        }
    }

    private void LoadConfig()
    {
        try
        {
            var path = GetConfigPath();
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                _config = JsonSerializer.Deserialize<Config>(json, JsonOptions) ?? new Config();
            }
        }
        catch
        {
            _config = new Config();
        }
    }

    private void PersistConfig()
    {
        _config.StartBatPath = _startBatPath;
        _config.PrimaryDisplayId = _primaryDisplayId;
        _config.OriginalMode = _originalMode;
        _config.TargetMode = _targetMode;
        _config.LaunchMode = _launchMode;
        _config.ThemeColor = _themeColor;
        _config.GameWindowTitle = _gameWindowTitle;
        _config.BackgroundImagePath = _backgroundImagePath;

        try
        {
            var path = GetConfigPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(_config, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch
        {
            // ignore persist failures
        }
    }

    private void ApplyConfigToState()
    {
        if (!string.IsNullOrWhiteSpace(_config.StartBatPath)) _startBatPath = _config.StartBatPath;
        if (!string.IsNullOrWhiteSpace(_config.PrimaryDisplayId)) _primaryDisplayId = _config.PrimaryDisplayId;
        if (!string.IsNullOrWhiteSpace(_config.OriginalMode)) _originalMode = _config.OriginalMode;
        if (!string.IsNullOrWhiteSpace(_config.LaunchMode)) _launchMode = _config.LaunchMode;
        if (!string.IsNullOrWhiteSpace(_config.ThemeColor)) _themeColor = _config.ThemeColor;
        if (!string.IsNullOrWhiteSpace(_config.GameWindowTitle)) _gameWindowTitle = _config.GameWindowTitle;
        if (!string.IsNullOrWhiteSpace(_config.BackgroundImagePath)) _backgroundImagePath = _config.BackgroundImagePath;
        _targetMode = FixedTargetMode;

        if (string.IsNullOrWhiteSpace(_originalMode))
        {
            ReadCurrentMode();
            PersistConfig();
        }
    }

    private void PickStartBat()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择 start.bat",
            Filter = "start.bat|start.bat|Batch Files|*.bat",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() == true)
        {
            _startBatPath = dialog.FileName;
            PersistConfig();
            SetStatus("已选择 start.bat", "#7dffa0");
            SendInit();
        }
    }

    private void PickBackgroundImage()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "选择背景图片",
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.webp|All Files|*.*",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() == true)
        {
            _backgroundImagePath = dialog.FileName;
            PersistConfig();
            PostMessage("update-background-image", new { path = _backgroundImagePath });
        }
    }
    private void DetectDisplays()
    {
        _displays.Clear();
        var screens = WinForms.Screen.AllScreens;
        var primary = screens.FirstOrDefault(s => s.Primary) ?? screens.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(_primaryDisplayId))
        {
            _primaryDisplayId = primary?.DeviceName;
        }

        _primaryDisplayName = screens.FirstOrDefault(s => s.DeviceName == _primaryDisplayId)?.DeviceName
            ?? primary?.DeviceName
            ?? "未选择";

        foreach (var screen in screens)
        {
            var label = $"{screen.DeviceName} ({screen.Bounds.Width}x{screen.Bounds.Height})";
            _displays.Add(new DisplayInfo(screen.DeviceName, label, screen.DeviceName == _primaryDisplayId));
        }
    }

    private void UpdateDisplaySelection()
    {
        for (var i = 0; i < _displays.Count; i++)
        {
            var display = _displays[i];
            _displays[i] = display with { Selected = display.Id == _primaryDisplayId };
        }
    }

    private void ReadCurrentMode()
    {
        var deviceName = _primaryDisplayId ?? WinForms.Screen.PrimaryScreen?.DeviceName;
        if (deviceName is null)
        {
            SetStatus("未找到显示器", "#ff5a6a");
            return;
        }

        if (DisplayModeHelper.TryGetCurrentMode(deviceName, out var mode, out var modeStruct))
        {
            _originalMode = mode;
            _lastKnownOriginalMode = modeStruct;
            SetStatus("已读取当前分辨率", "#7dffa0");
        }
        else
        {
            SetStatus("读取分辨率失败", "#ff5a6a");
        }
    }

    private void ApplySettings(JsonElement payload)
    {
        if (payload.TryGetProperty("startBatPath", out var startBat))
        {
            _startBatPath = startBat.GetString();
        }

        if (payload.TryGetProperty("primaryDisplay", out var display))
        {
            _primaryDisplayId = display.GetString();
            UpdateDisplaySelection();
            _primaryDisplayName = _displays.FirstOrDefault(d => d.Id == _primaryDisplayId)?.Name ?? "未选择";
        }

        if (payload.TryGetProperty("originalMode", out var original))
        {
            _originalMode = original.GetString();
        }

        if (payload.TryGetProperty("backgroundImagePath", out var backgroundImagePath))
        {
            _backgroundImagePath = backgroundImagePath.GetString();
        }
    }

    private void OpenGameFolder()
    {
        if (string.IsNullOrWhiteSpace(_startBatPath))
        {
            SetStatus("尚未选择 start.bat", "#ff5a6a");
            return;
        }

        var args = $"/select,\"{_startBatPath}\"";
        Process.Start(new ProcessStartInfo("explorer", args) { UseShellExecute = true });
    }

    private void OpenSegatoolsIniInVsCode()
    {
        var iniPath = TryGetSegatoolsIniPath();
        if (iniPath is null)
        {
            return;
        }

        var opened = TryOpenInPreferredEditor(iniPath);
        if (opened)
        {
            SetStatus("已打开 segatools.ini", "#7dffa0");
        }
        else
        {
            System.Windows.MessageBox.Show(
                "你电脑连个可视化编辑器都没有？😅\n赶紧去下一个vscode！！！",
                "缺少编辑器",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://code.visualstudio.com/",
                    UseShellExecute = true,
                });
            }
            catch
            {
                // ignore browser open failures
            }

            SetStatus("未检测到可用编辑器，已打开 VS Code 下载页", "#ff5a6a");
        }
    }

    private void ApplyRecommendedSegatoolsGfxConfig()
    {
        var iniPath = TryGetSegatoolsIniPath();
        if (iniPath is null)
        {
            return;
        }

        var result = System.Windows.MessageBox.Show(
            "我们建议将segatools的gfx部分更改为\n[gfx]\n\nwindowed=1\n\nframed=0\n\nmonitor=0\n\n需要进行更改吗？",
            "使用推荐配置",
            System.Windows.MessageBoxButton.OKCancel,
            System.Windows.MessageBoxImage.Question);

        if (result != System.Windows.MessageBoxResult.OK)
        {
            return;
        }

        try
        {
            var content = File.ReadAllText(iniPath);
            var updated = ApplyRecommendedGfxSection(content);
            if (string.Equals(content, updated, StringComparison.Ordinal))
            {
                SetStatus("segatools.ini 已是推荐配置", "#7dffa0");
                return;
            }

            File.WriteAllText(iniPath, updated);
            SetStatus("已应用 segatools 推荐配置", "#7dffa0");
        }
        catch
        {
            SetStatus("修改 segatools.ini 失败", "#ff5a6a");
        }
    }

    private string? TryGetSegatoolsIniPath()
    {
        if (string.IsNullOrWhiteSpace(_startBatPath))
        {
            SetStatus("尚未选择 start.bat", "#ff5a6a");
            return null;
        }

        var gameDir = Path.GetDirectoryName(_startBatPath);
        if (string.IsNullOrWhiteSpace(gameDir))
        {
            SetStatus("无法解析游戏目录", "#ff5a6a");
            return null;
        }

        var iniPath = Path.Combine(gameDir, "segatools.ini");
        if (!File.Exists(iniPath))
        {
            SetStatus("未找到 segatools.ini", "#ff5a6a");
            return null;
        }

        return iniPath;
    }

    private static string ApplyRecommendedGfxSection(string content)
    {
        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
        var gfxStart = -1;
        var gfxEnd = lines.Count;

        for (var i = 0; i < lines.Count; i++)
        {
            if (string.Equals(lines[i].Trim(), "[gfx]", StringComparison.OrdinalIgnoreCase))
            {
                gfxStart = i;
                break;
            }
        }

        if (gfxStart < 0)
        {
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines[^1]))
            {
                lines.Add(string.Empty);
            }

            lines.Add("[gfx]");
            lines.Add("windowed=1");
            lines.Add("framed=0");
            lines.Add("monitor=0");
            return string.Join(Environment.NewLine, lines);
        }

        for (var i = gfxStart + 1; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                gfxEnd = i;
                break;
            }
        }

        var foundWindowed = false;
        var foundFramed = false;
        var foundMonitor = false;

        for (var i = gfxStart + 1; i < gfxEnd; i++)
        {
            var trimmed = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith(";", StringComparison.Ordinal))
            {
                continue;
            }

            if (TryMatchIniKey(trimmed, "windowed"))
            {
                lines[i] = ReplaceIniValue(lines[i], "1");
                foundWindowed = true;
                continue;
            }

            if (TryMatchIniKey(trimmed, "framed"))
            {
                lines[i] = ReplaceIniValue(lines[i], "0");
                foundFramed = true;
                continue;
            }

            if (TryMatchIniKey(trimmed, "monitor"))
            {
                lines[i] = ReplaceIniValue(lines[i], "0");
                foundMonitor = true;
            }
        }

        var insertIndex = gfxEnd;
        if (!foundWindowed)
        {
            lines.Insert(insertIndex++, "windowed=1");
        }

        if (!foundFramed)
        {
            lines.Insert(insertIndex++, "framed=0");
        }

        if (!foundMonitor)
        {
            lines.Insert(insertIndex, "monitor=0");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static bool TryMatchIniKey(string line, string key)
    {
        var equalsIndex = line.IndexOf('=');
        if (equalsIndex <= 0)
        {
            return false;
        }

        var currentKey = line[..equalsIndex].Trim();
        return string.Equals(currentKey, key, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReplaceIniValue(string originalLine, string value)
    {
        var equalsIndex = originalLine.IndexOf('=');
        if (equalsIndex < 0)
        {
            return originalLine;
        }

        return $"{originalLine[..equalsIndex]}={value}";
    }

    private static bool TryOpenInPreferredEditor(string filePath)
    {
        var candidates = new[]
        {
            // VS Code
            "code",
            "code.cmd",
            "code.exe",
            @"C:\Program Files\Microsoft VS Code\Code.exe",
            @"C:\Program Files (x86)\Microsoft VS Code\Code.exe",

            // Notepad++
            "notepad++",
            "notepad++.exe",
            @"C:\Program Files\Notepad++\notepad++.exe",
            @"C:\Program Files (x86)\Notepad++\notepad++.exe",

            // Sublime Text
            "subl",
            "subl.exe",
            "sublime_text",
            "sublime_text.exe",
            @"C:\Program Files\Sublime Text\sublime_text.exe",
            @"C:\Program Files\Sublime Text 3\sublime_text.exe",
            @"C:\Program Files\Sublime Text 4\sublime_text.exe",
        };

        foreach (var candidate in candidates)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = $"\"{filePath}\"",
                    UseShellExecute = true,
                });
                return true;
            }
            catch
            {
                // try next candidate
            }
        }

        return false;
    }

    private async Task LaunchGameAsync()
    {
        if (_isLaunching)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_startBatPath) || !File.Exists(_startBatPath))
        {
            SetStatus("start.bat 无效", "#ff5a6a");
            return;
        }

        _isLaunching = true;

        var deviceName = _primaryDisplayId ?? WinForms.Screen.PrimaryScreen?.DeviceName;
        if (deviceName is null)
        {
            SetStatus("未找到显示器", "#ff5a6a");
            _isLaunching = false;
            return;
        }

        _ = DisplayModeHelper.TryGetCurrentMode(deviceName, out _, out var currentStruct);
        _lastKnownOriginalMode = currentStruct;
        var title = string.IsNullOrWhiteSpace(_gameWindowTitle) ? DefaultGameWindowTitle : _gameWindowTitle;

        if (_launchMode == "smart")
        {
            if (!DisplayMode.TryParse(_targetMode, out var target))
            {
                SetStatus("目标分辨率格式错误", "#ff5a6a");
                _isLaunching = false;
                return;
            }

            SetStatus("正在切换分辨率...", "#5ee7ff");
            if (!DisplayModeHelper.TrySetMode(deviceName, target))
            {
                SetStatus("切换分辨率失败", "#ff5a6a");
                _isLaunching = false;
                return;
            }

            SetStatus("分辨率已切换，3秒后启动游戏...", "#5ee7ff");
            await Task.Delay(3000);
        }

        try
        {
            SetStatus("游戏启动中...", "#5ee7ff");
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{_startBatPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_startBatPath),
            };

            Process.Start(psi);
        }
        catch
        {
            SetStatus("启动失败", "#ff5a6a");
            _isLaunching = false;
            return;
        }

        if (_launchMode == "smart")
        {
            SetStatus("等待游戏窗口...", "#5ee7ff");
            var windowFound = await WaitForWindowAsync(title, TimeSpan.FromSeconds(120));
            if (windowFound == IntPtr.Zero)
            {
                SetStatus("未检测到游戏窗口", "#ff5a6a");
                await RestoreOriginalAsync();
                _isLaunching = false;
                return;
            }

            SetStatus("游戏运行中...", "#7dffa0");
            await WaitForGameExitAsync(windowFound, title);
            await RestoreOriginalAsync();
        }

        _isLaunching = false;
    }

    private async Task TestSwitchAsync()
    {
        var deviceName = _primaryDisplayId ?? WinForms.Screen.PrimaryScreen?.DeviceName;
        if (deviceName is null)
        {
            SetStatus("未找到显示器", "#ff5a6a");
            SetTestSwitchState(false);
            return;
        }

        if (!DisplayMode.TryParse(_targetMode, out var target))
        {
            SetStatus("目标分辨率格式错误", "#ff5a6a");
            SetTestSwitchState(false);
            return;
        }

        _ = DisplayModeHelper.TryGetCurrentMode(deviceName, out _, out var currentStruct);
        _lastKnownOriginalMode = currentStruct;

        SetStatus("测试切换中（15秒后自动恢复）...", "#5ee7ff");
        if (!DisplayModeHelper.TrySetMode(deviceName, target))
        {
            SetStatus("切换失败", "#ff5a6a");
            SetTestSwitchState(false);
            return;
        }

        SetTestSwitchState(true, 15);
        _ = StartTestSwitchAutoRestoreAsync(15);
    }

    private async Task RestoreOriginalAsync()
    {
        var deviceName = _primaryDisplayId ?? WinForms.Screen.PrimaryScreen?.DeviceName;
        if (deviceName is null)
        {
            SetStatus("未找到显示器", "#ff5a6a");
            return;
        }

        DisplayMode restore;
        if (!DisplayMode.TryParse(_originalMode ?? string.Empty, out restore))
        {
            if (_lastKnownOriginalMode.HasValue)
            {
                restore = _lastKnownOriginalMode.Value;
            }
            else
            {
                restore = new DisplayMode(1920, 1080, 60);
            }
        }

        SetStatus("正在恢复分辨率...", "#5ee7ff");
        await Task.Delay(100);
        if (!DisplayModeHelper.TrySetMode(deviceName, restore))
        {
            SetStatus("恢复失败", "#ff5a6a");
            return;
        }

        _testSwitchCts?.Cancel();
        SetTestSwitchState(false);
        SetStatus("已恢复分辨率", "#7dffa0");
    }

    private void SafeRestoreOnExit()
    {
        try
        {
            _testSwitchCts?.Cancel();
            if (_launchMode == "smart" && _lastKnownOriginalMode.HasValue)
            {
                var deviceName = _primaryDisplayId ?? WinForms.Screen.PrimaryScreen?.DeviceName;
                if (deviceName is not null)
                {
                    DisplayModeHelper.TrySetMode(deviceName, _lastKnownOriginalMode.Value);
                }
            }
        }
        catch
        {
            // ignore
        }
    }

    private async Task StartTestSwitchAutoRestoreAsync(int timeoutSeconds)
    {
        _testSwitchCts?.Cancel();
        _testSwitchCts = new CancellationTokenSource();
        var token = _testSwitchCts.Token;

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(timeoutSeconds), token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested || !_testSwitchActive)
        {
            return;
        }

        await RestoreOriginalAsync();
    }

    private void SetTestSwitchState(bool active, int timeoutSeconds = 15)
    {
        _testSwitchActive = active;
        PostMessage("test-switch-state", new { active, timeoutSeconds });
    }

    private static async Task<IntPtr> WaitForWindowAsync(string title, TimeSpan timeout)
    {
        var end = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < end)
        {
            var handle = FindWindow(null, title);
            if (handle != IntPtr.Zero)
            {
                return handle;
            }

            await Task.Delay(500);
        }

        return IntPtr.Zero;
    }

    private static async Task WaitForWindowCloseAsync(string title, TimeSpan missingGracePeriod)
    {
        DateTime? missingSince = null;

        while (true)
        {
            var handle = FindWindow(null, title);
            if (handle != IntPtr.Zero)
            {
                missingSince = null;
                await Task.Delay(1000);
                continue;
            }

            missingSince ??= DateTime.UtcNow;
            if (DateTime.UtcNow - missingSince.Value >= missingGracePeriod)
            {
                return;
            }

            await Task.Delay(1000);
        }
    }

    private static async Task WaitForGameExitAsync(IntPtr windowHandle, string title)
    {
        if (TryGetProcessIdFromWindow(windowHandle, out var processId))
        {
            try
            {
                using var process = Process.GetProcessById(processId);
                while (!process.HasExited)
                {
                    await Task.Delay(1000);
                }

                return;
            }
            catch
            {
                // fallback to window-title based detection
            }
        }

        await WaitForWindowCloseAsync(title, TimeSpan.FromSeconds(8));
    }

    private static bool TryGetProcessIdFromWindow(IntPtr windowHandle, out int processId)
    {
        processId = 0;
        if (windowHandle == IntPtr.Zero)
        {
            return false;
        }

        _ = GetWindowThreadProcessId(windowHandle, out var pid);
        if (pid == 0)
        {
            return false;
        }

        processId = (int)pid;
        return true;
    }

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    private void SendInit()
    {
        var payload = new
        {
            startBatPath = _startBatPath ?? string.Empty,
            originalMode = _originalMode ?? string.Empty,
            targetMode = _targetMode,
            primaryDisplayName = _primaryDisplayName ?? "未选择",
            themeColor = _themeColor,
            backgroundImagePath = _backgroundImagePath ?? string.Empty,
            version = GetAppVersion(),
            displays = _displays.Select(d => new { id = d.Id, name = d.Name, selected = d.Selected }).ToArray(),
        };

        PostMessage("init", payload);
    }

    private void SetStatus(string text, string color)
    {
        PostMessage("status", new { text, color });
    }

    private void PostMessage(string type, object payload)
    {
        if (WebView.CoreWebView2 is null)
        {
            return;
        }

        var json = JsonSerializer.Serialize(new { type, payload }, JsonOptions);
        WebView.CoreWebView2.PostWebMessageAsJson(json);
    }

    private string ResolveUiIndexPath()
    {
        var output = Path.Combine(AppContext.BaseDirectory, "ui", "index.html");
        if (File.Exists(output))
        {
            return output;
        }

        var fallback = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ui", "index.html"));
        return fallback;
    }

    private static string GetAppVersion()
    {
        var version = typeof(MainWindow).Assembly.GetName().Version;
        if (version is null)
        {
            return "0.0.0";
        }

        return version.Revision > 0
            ? $"{version.Major}.{version.Minor}.{version.Build}+{version.Revision}"
            : $"{version.Major}.{version.Minor}.{version.Build}";
    }

    private void ApplyWindowBackdrop()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd == IntPtr.Zero)
            {
                return;
            }

            const int DwmwaUseImmersiveDarkMode = 20;
            const int DwmwaSystemBackdropType = 38;
            const int DwmsbtMainWindow = 2;
            const int DwmsbtTransientWindow = 3;

            var dark = 1;
            _ = DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));

            var backdropType = DwmsbtTransientWindow;
            var hr = DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdropType, sizeof(int));
            if (hr != 0)
            {
                backdropType = DwmsbtMainWindow;
                var fallbackHr = DwmSetWindowAttribute(hwnd, DwmwaSystemBackdropType, ref backdropType, sizeof(int));
                if (fallbackHr != 0)
                {
                    ApplyLegacyAcrylic(hwnd);
                }
            }
        }
        catch
        {
            try
            {
                var hwnd = new WindowInteropHelper(this).Handle;
                if (hwnd != IntPtr.Zero)
                {
                    ApplyLegacyAcrylic(hwnd);
                }
            }
            catch
            {
                // best effort on unsupported systems
            }
        }
    }

    private static void ApplyLegacyAcrylic(IntPtr hwnd)
    {
        const int WcaAccentPolicy = 19;
        const int AccentEnableAcrylicBlurBehind = 4;
        const int DrawAllBorders = 0x20 | 0x40 | 0x80 | 0x100;

        // ARGB in AABBGGRR, tuned for dark acrylic.
        var accent = new AccentPolicy
        {
            AccentState = AccentEnableAcrylicBlurBehind,
            AccentFlags = DrawAllBorders,
            GradientColor = unchecked((int)0x88363A30),
            AnimationId = 0,
        };

        var accentSize = Marshal.SizeOf(accent);
        var accentPtr = Marshal.AllocHGlobal(accentSize);
        try
        {
            Marshal.StructureToPtr(accent, accentPtr, false);
            var data = new WindowCompositionAttributeData
            {
                Attribute = WcaAccentPolicy,
                Data = accentPtr,
                SizeOfData = accentSize,
            };
            _ = SetWindowCompositionAttribute(hwnd, ref data);
        }
        finally
        {
            Marshal.FreeHGlobal(accentPtr);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public int AccentState;
        public int AccentFlags;
        public int GradientColor;
        public int AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public int Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    private static string GetConfigPath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(root, "ChunithmLauncher", "config.json");
    }

    private sealed record DisplayInfo(string Id, string Name, bool Selected);

    private sealed record WebMessage(string? Type, JsonElement Payload);

    private sealed class Config
    {
        public string? StartBatPath { get; set; }
        public string? PrimaryDisplayId { get; set; }
        public string? OriginalMode { get; set; }
        public string? TargetMode { get; set; }
        public string? LaunchMode { get; set; }
        public string? ThemeColor { get; set; }
        public string? GameWindowTitle { get; set; }
        public string? BackgroundImagePath { get; set; }
    }

    private readonly record struct DisplayMode(int Width, int Height, int Frequency)
    {
        private static readonly Regex ModeRegex = new(@"(\d{3,4})\s*[x×]\s*(\d{3,4})(?:\s*@\s*(\d{2,3}))?", RegexOptions.IgnoreCase);

        public static bool TryParse(string input, out DisplayMode mode)
        {
            mode = default;
            if (string.IsNullOrWhiteSpace(input)) return false;

            var match = ModeRegex.Match(input.Replace("Hz", string.Empty, StringComparison.OrdinalIgnoreCase));
            if (!match.Success) return false;

            if (!int.TryParse(match.Groups[1].Value, out var width)) return false;
            if (!int.TryParse(match.Groups[2].Value, out var height)) return false;
            var freq = 60;
            if (match.Groups[3].Success && int.TryParse(match.Groups[3].Value, out var parsed))
            {
                freq = parsed;
            }

            mode = new DisplayMode(width, height, freq);
            return true;
        }

        public override string ToString() => $"{Width}×{Height} @ {Frequency}Hz";
    }

    private static class DisplayModeHelper
    {
        private const int EnumCurrentSettings = -1;
        private const int DispChangeSuccessful = 0;
        private const int DmPelsWidth = 0x80000;
        private const int DmPelsHeight = 0x100000;
        private const int DmDisplayFrequency = 0x400000;
        private const int CdsUpdateRegistry = 0x00000001;
        private const int CdsFullscreen = 0x00000004;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int ChangeDisplaySettingsEx(
            string lpszDeviceName,
            ref DEVMODE lpDevMode,
            IntPtr hwnd,
            int dwflags,
            IntPtr lParam);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct DEVMODE
        {
            private const int CchDeviceName = 32;
            private const int CchFormName = 32;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchDeviceName)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchFormName)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        public static bool TryGetCurrentMode(string deviceName, out string mode, out DisplayMode modeStruct)
        {
            var devMode = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
            if (EnumDisplaySettings(deviceName, EnumCurrentSettings, ref devMode))
            {
                var hz = devMode.dmDisplayFrequency > 0 ? devMode.dmDisplayFrequency : 60;
                modeStruct = new DisplayMode(devMode.dmPelsWidth, devMode.dmPelsHeight, hz);
                mode = modeStruct.ToString();
                return true;
            }

            modeStruct = default;
            mode = string.Empty;
            return false;
        }

        public static bool TrySetMode(string deviceName, DisplayMode mode)
        {
            var devMode = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
            if (!EnumDisplaySettings(deviceName, EnumCurrentSettings, ref devMode))
            {
                return false;
            }

            devMode.dmPelsWidth = mode.Width;
            devMode.dmPelsHeight = mode.Height;
            devMode.dmDisplayFrequency = mode.Frequency;
            devMode.dmFields = DmPelsWidth | DmPelsHeight | DmDisplayFrequency;

            // Persist resolution changes so they survive app focus switches (e.g. Alt+Tab / Win+D).
            var persistentFlags = CdsUpdateRegistry | CdsFullscreen;
            var result = ChangeDisplaySettingsEx(deviceName, ref devMode, IntPtr.Zero, persistentFlags, IntPtr.Zero);
            if (result == DispChangeSuccessful)
            {
                return true;
            }

            // Fallback to temporary apply if persisting fails on some environments.
            result = ChangeDisplaySettingsEx(deviceName, ref devMode, IntPtr.Zero, CdsFullscreen, IntPtr.Zero);
            return result == DispChangeSuccessful;
        }
    }
}






