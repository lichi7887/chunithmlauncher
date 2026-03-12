namespace ChunithmLauncher.Models;

public sealed class LauncherConfig
{
    public string? StartBatPath { get; set; }
    public string? PrimaryMonitorId { get; set; }
    public DisplayMode? OriginalDisplayMode { get; set; }
    public DisplayMode TargetDisplayMode { get; set; } = new(1920, 1080, 120);
    public string ThemeColorHex { get; set; } = "#fdd500";
    public string WindowTitle { get; set; } = "teaGfx DirectX Release";
    public string? BackgroundSource { get; set; }
    public bool IsFirstRunCompleted { get; set; }
}
