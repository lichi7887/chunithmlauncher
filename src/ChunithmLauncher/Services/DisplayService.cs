using ChunithmLauncher.Models;

namespace ChunithmLauncher.Services;

public sealed class DisplayService
{
    public IReadOnlyList<DisplayMonitor> GetMonitors()
    {
        var monitors = new List<DisplayMonitor>();
        var index = 0u;

        while (true)
        {
            var device = new NativeDisplayApi.DISPLAY_DEVICE { cb = System.Runtime.InteropServices.Marshal.SizeOf<NativeDisplayApi.DISPLAY_DEVICE>() };
            if (!NativeDisplayApi.EnumDisplayDevices(null, index, ref device, 0))
            {
                break;
            }

            const int activeFlag = 0x1;
            const int primaryFlag = 0x4;
            if ((device.StateFlags & activeFlag) == 0)
            {
                index++;
                continue;
            }

            var mode = GetCurrentMode(device.DeviceName) ?? new DisplayMode(1920, 1080, 60);
            monitors.Add(new DisplayMonitor
            {
                Id = string.IsNullOrWhiteSpace(device.DeviceID) ? device.DeviceName : device.DeviceID,
                DeviceName = device.DeviceName,
                Description = string.IsNullOrWhiteSpace(device.DeviceString) ? device.DeviceName : device.DeviceString,
                IsPrimary = (device.StateFlags & primaryFlag) != 0,
                CurrentMode = mode
            });

            index++;
        }

        return monitors;
    }

    public DisplayMode? GetCurrentMode(string deviceName)
    {
        var devMode = CreateDevMode();
        var ok = NativeDisplayApi.EnumDisplaySettings(deviceName, NativeDisplayApi.ENUM_CURRENT_SETTINGS, ref devMode);
        return ok ? new DisplayMode(devMode.dmPelsWidth, devMode.dmPelsHeight, devMode.dmDisplayFrequency) : null;
    }

    public bool TryChangeResolution(string deviceName, DisplayMode mode)
    {
        var devMode = CreateDevMode();
        if (!NativeDisplayApi.EnumDisplaySettings(deviceName, NativeDisplayApi.ENUM_CURRENT_SETTINGS, ref devMode))
        {
            return false;
        }

        devMode.dmPelsWidth = mode.Width;
        devMode.dmPelsHeight = mode.Height;
        devMode.dmDisplayFrequency = mode.RefreshRate;
        devMode.dmFields = NativeDisplayApi.DM_PELSWIDTH | NativeDisplayApi.DM_PELSHEIGHT | NativeDisplayApi.DM_DISPLAYFREQUENCY;

        var result = NativeDisplayApi.ChangeDisplaySettingsEx(deviceName, ref devMode, IntPtr.Zero, NativeDisplayApi.CDS_UPDATEREGISTRY, IntPtr.Zero);
        return result == NativeDisplayApi.DISP_CHANGE_SUCCESSFUL;
    }

    private static NativeDisplayApi.DEVMODE CreateDevMode()
    {
        return new NativeDisplayApi.DEVMODE
        {
            dmDeviceName = new string('\0', 32),
            dmFormName = new string('\0', 32),
            dmSize = (short)System.Runtime.InteropServices.Marshal.SizeOf<NativeDisplayApi.DEVMODE>()
        };
    }
}
