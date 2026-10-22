using ClockWidget.Core;
using System.Windows.Threading;

namespace ClockWidget.Media;

public sealed class MediaController : IDisposable
{
    public IMediaSessionService Sessions { get; }
    private readonly IAudioOutputService audio;
    private readonly Dispatcher dispatcher;
    private readonly DispatcherTimer audioTick;
    private bool disposed, busy;
    public bool? Muted => audio.Muted;
    public bool Busy => busy;
    public string Status { get; private set; } = "Connecting to Windows media…";
    public event Action? Changed;
    public MediaController(IMediaSessionService sessions, IAudioOutputService audio, Dispatcher dispatcher)
    {
        Sessions = sessions; this.audio = audio; this.dispatcher = dispatcher;
        audioTick = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        audioTick.Tick += AudioTick;
        sessions.Changed += SessionChanged;
    }
    public async Task StartAsync()
    {
        if (disposed) return;
        audio.Refresh(); audioTick.Start(); await Sessions.StartAsync();
        if (!disposed) SessionChanged();
    }
    private void AudioTick(object? sender, EventArgs args) { if (!disposed) { audio.Refresh(); Changed?.Invoke(); } }
    private void SessionChanged()
    {
        if (disposed) return;
        if (!dispatcher.CheckAccess()) { dispatcher.BeginInvoke(SessionChanged); return; }
        Status = Sessions.State.Status; Changed?.Invoke();
    }
    public async Task<bool> ExecuteAsync(MediaCommand command)
    {
        if (disposed || busy) return false;
        busy = true; Changed?.Invoke();
        try { bool accepted = await Sessions.CommandAsync(command); return !disposed && accepted; }
        finally { busy = false; if (!disposed) { Status = Sessions.State.Status; Changed?.Invoke(); } }
    }
    public bool ToggleMute()
    {
        if (disposed) return false;
        bool success = audio.ToggleMute();
        Status = success ? Sessions.State.Status : "No available Windows audio output.";
        Changed?.Invoke(); return success;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; audioTick.Stop(); audioTick.Tick -= AudioTick;
        Sessions.Changed -= SessionChanged; Sessions.Dispose(); audio.Dispose();
    }
}
