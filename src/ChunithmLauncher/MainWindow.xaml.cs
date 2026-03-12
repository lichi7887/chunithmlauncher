using System.IO;
using System.Text.Json;
using System.Windows;
using ChunithmLauncher.Services;
using Microsoft.Web.WebView2.Core;

namespace ChunithmLauncher;

public partial class MainWindow : Window
{
    private readonly ConfigService _configService;
    private readonly LauncherBridge _bridge;
    private readonly LauncherRuntime _runtime;

    public MainWindow()
    {
        InitializeComponent();

        _configService = new ConfigService();
        var config = _configService.Load();
        var displayService = new DisplayService();
        _runtime = new LauncherRuntime(displayService, config);
        _bridge = new LauncherBridge(_configService, displayService, _runtime, config);

        Loaded += OnLoaded;
        Closed += (_, _) => _runtime.Dispose();
        _runtime.StatusChanged += OnRuntimeStatusChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LauncherWebView.EnsureCoreWebView2Async();
        LauncherWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        LauncherWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
        LauncherWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

        var indexPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "index.html");
        LauncherWebView.Source = new Uri(indexPath);
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var raw = e.TryGetWebMessageAsString();
        var request = JsonSerializer.Deserialize<WebRequest>(raw);
        if (request is null || string.IsNullOrWhiteSpace(request.Action))
        {
            return;
        }

        string result;
        switch (request.Action)
        {
            case "bootstrap":
                result = _bridge.GetBootstrapData();
                break;
            case "saveConfig":
                result = _bridge.SaveConfig(request.Payload ?? "{}");
                break;
            case "launch":
                result = _bridge.LaunchGame();
                break;
            case "testSwitch":
                result = _bridge.TestSwitch();
                break;
            case "restore":
                result = _bridge.RestoreResolution();
                break;
            default:
                result = "unknown";
                break;
        }

        var response = JsonSerializer.Serialize(new { action = request.Action, result });
        await LauncherWebView.CoreWebView2.ExecuteScriptAsync($"window.__hostResponse({response});");
    }

    private async void OnRuntimeStatusChanged(string message)
    {
        if (LauncherWebView.CoreWebView2 is null)
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new { message });
        await LauncherWebView.CoreWebView2.ExecuteScriptAsync($"window.__runtimeStatus({payload});");
    }

    private sealed class WebRequest
    {
        public string? Action { get; set; }
        public string? Payload { get; set; }
    }
}
