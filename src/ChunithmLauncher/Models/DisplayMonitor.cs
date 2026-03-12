namespace ChunithmLauncher.Models;

public sealed class DisplayMonitor
{
    public required string Id { get; init; }
    public required string DeviceName { get; init; }
    public required string Description { get; init; }
    public bool IsPrimary { get; init; }
    public DisplayMode CurrentMode { get; init; } = new(1920, 1080, 60);
}
