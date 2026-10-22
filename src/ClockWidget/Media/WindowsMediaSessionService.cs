using System.Runtime.InteropServices;
using System.Windows.Threading;
using ClockWidget.Core;
using Windows.Media.Control;

namespace ClockWidget.Media;

public sealed class WindowsMediaSessionService(Dispatcher dispatcher) : IMediaSessionService
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? session;
    private readonly Dictionary<string, GlobalSystemMediaTransportControlsSession> sessions = new();
    private readonly CancellationTokenSource lifetime = new();
    private bool disposed, busy;
    private int revision;
    private string? preferred;
    public MediaState State { get; private set; } = MediaState.Empty;
    public IReadOnlyList<MediaSource> Sources { get; private set; } = Array.Empty<MediaSource>();
    public string SelectedId { get; private set; } = "";
    public event Action? Changed;

    public async Task StartAsync()
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var op = GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            using var registration = timeout.Token.Register(op.Cancel);
            var found = await op;
            if (disposed) return;
            manager = found;
            manager.SessionsChanged += SessionsChanged;
            manager.CurrentSessionChanged += CurrentChanged;
            await RebuildAsync();
        }
        catch (Exception ex) when (Expected(ex)) { if (!disposed) Publish(MediaState.Empty with { Status = "Windows media access is unavailable. The clock is still running." }); }
    }
    private void SessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args)
        => Dispatch(async () => await RebuildAsync());
    private void CurrentChanged(GlobalSystemMediaTransportControlsSessionManager sender, CurrentSessionChangedEventArgs args)
        => Dispatch(async () => { if (preferred == null) await AttachAsync(manager?.GetCurrentSession()); });
    private async Task RebuildAsync()
    {
        if (disposed || manager == null) return;
        try
        {
            var previous = sessions.ToArray();
            sessions.Clear(); var sources = new List<MediaSource>();
            foreach (var item in manager.GetSessions())
            {
                string id = previous.FirstOrDefault(p => Equals(p.Value, item)).Key ?? Guid.NewGuid().ToString("N");
                sessions[id] = item; sources.Add(new(id, item.SourceAppUserModelId));
            }
            Sources = sources;
            if (preferred != null && !sessions.ContainsKey(preferred)) preferred = null;
            await AttachAsync(preferred != null ? sessions[preferred] : manager.GetCurrentSession());
        }
        catch (Exception ex) when (Expected(ex)) { Publish(MediaState.Empty with { Status = "Media session changed. Choose another player." }); }
    }
    public async Task SelectAsync(string? id)
    {
        if (disposed) return;
        preferred = id != null && sessions.ContainsKey(id) ? id : null;
        try { await AttachAsync(preferred == null ? manager?.GetCurrentSession() : sessions[preferred]); }
        catch (Exception ex) when (Expected(ex)) { Publish(MediaState.Empty); }
    }
    private async Task AttachAsync(GlobalSystemMediaTransportControlsSession? next)
    {
        Detach(); session = next; revision++;
        SelectedId = sessions.FirstOrDefault(p => Equals(p.Value, next)).Key ?? "";
        if (next != null) { next.MediaPropertiesChanged += MetadataChanged; next.PlaybackInfoChanged += PlaybackChanged; }
        await RefreshAsync();
    }
    private void MetadataChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        => Dispatch(async () => { if (Equals(sender, session)) await RefreshAsync(); });
    private void PlaybackChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        => Dispatch(async () => { if (Equals(sender, session)) await RefreshAsync(); });
    private async Task RefreshAsync()
    {
        var active = session; int version = ++revision;
        if (active == null) { Publish(MediaState.Empty); return; }
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var op = active.TryGetMediaPropertiesAsync();
            using var registration = timeout.Token.Register(op.Cancel);
            var metadata = await op;
            if (disposed || version != revision || !Equals(active, session)) return;
            var info = active.GetPlaybackInfo(); var controls = info.Controls;
            Publish(new(string.IsNullOrWhiteSpace(metadata.Title) ? "Untitled media" : metadata.Title,
                metadata.Artist ?? "", info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                controls.IsPreviousEnabled, controls.IsPlayPauseToggleEnabled || (info.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing ? controls.IsPauseEnabled : controls.IsPlayEnabled),
                controls.IsNextEnabled, ""));
        }
        catch (Exception ex) when (Expected(ex)) { if (!disposed && version == revision) Publish(MediaState.Empty with { Status = "Player unavailable. Try another session." }); }
    }
    public async Task<bool> CommandAsync(MediaCommand command)
    {
        var active = session;
        if (disposed || busy || active == null) return false;
        if (!(command switch { MediaCommand.Previous => State.CanPrevious, MediaCommand.Next => State.CanNext, _ => State.CanPlayPause })) return false;
        busy = true;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            var op = command switch
            {
                MediaCommand.Previous => active.TrySkipPreviousAsync(),
                MediaCommand.Next => active.TrySkipNextAsync(),
                _ => active.GetPlaybackInfo().Controls.IsPlayPauseToggleEnabled ? active.TryTogglePlayPauseAsync()
                    : State.Playing ? active.TryPauseAsync() : active.TryPlayAsync()
            };
            using var registration = timeout.Token.Register(op.Cancel);
            bool accepted = await op;
            if (disposed || !Equals(active, session)) return false;
            await RefreshAsync();
            if (!accepted) Publish(State with { Status = "The player did not accept that command." });
            return accepted;
        }
        catch (Exception ex) when (Expected(ex)) { if (!disposed) Publish(State with { Status = "Playback control unavailable." }); return false; }
        finally { busy = false; }
    }
    private void Dispatch(Func<Task> action)
    {
        if (!disposed) dispatcher.BeginInvoke(async () =>
        {
            if (disposed) return;
            try { await action(); }
            catch (Exception ex) when (Expected(ex)) { if (!disposed) Publish(MediaState.Empty with { Status = "Media session unavailable." }); }
        });
    }
    private void Publish(MediaState value) { if (!disposed) { State = value; Changed?.Invoke(); } }
    private static bool Expected(Exception ex) => ex is COMException or UnauthorizedAccessException or InvalidOperationException or OperationCanceledException or System.NotSupportedException;
    private void Detach() { if (session != null) { session.MediaPropertiesChanged -= MetadataChanged; session.PlaybackInfoChanged -= PlaybackChanged; session = null; } }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; revision++; lifetime.Cancel(); Detach();
        if (manager != null) { manager.SessionsChanged -= SessionsChanged; manager.CurrentSessionChanged -= CurrentChanged; }
        manager = null; sessions.Clear(); Sources = Array.Empty<MediaSource>(); State = MediaState.Empty;
        lifetime.Dispose();
    }
}
