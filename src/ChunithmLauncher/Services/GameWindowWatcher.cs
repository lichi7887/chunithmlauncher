using System.Timers;

namespace ChunithmLauncher.Services;

public sealed class GameWindowWatcher : IDisposable
{
    private readonly Timer _timer;
    private readonly Func<string> _titleAccessor;
    private bool _isWindowPresent;

    public event Action<bool>? WindowPresenceChanged;

    public GameWindowWatcher(Func<string> titleAccessor)
    {
        _titleAccessor = titleAccessor;
        _timer = new Timer(1000);
        _timer.Elapsed += (_, _) => ProbeWindow();
    }

    public void Start() => _timer.Start();

    public void Stop() => _timer.Stop();

    private void ProbeWindow()
    {
        var title = _titleAccessor();
        var handle = NativeDisplayApi.FindWindow(null, title);
        var present = handle != IntPtr.Zero;

        if (present == _isWindowPresent)
        {
            return;
        }

        _isWindowPresent = present;
        WindowPresenceChanged?.Invoke(present);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Dispose();
    }
}
