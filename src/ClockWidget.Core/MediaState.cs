namespace ClockWidget.Core;

public sealed record MediaSource(string Id, string Name);
public sealed record MediaState(string Title, string Artist, bool Playing, bool CanPrevious, bool CanPlayPause, bool CanNext, string Status)
{
    public static MediaState Empty { get; } = new("No active media", "", false, false, false, false, "Open a compatible player to start.");
}
public enum MediaCommand { Previous, PlayPause, Next }

public interface IMediaSessionService : IDisposable
{
    MediaState State { get; }
    IReadOnlyList<MediaSource> Sources { get; }
    string SelectedId { get; }
    event Action? Changed;
    Task StartAsync();
    Task SelectAsync(string? id);
    Task<bool> CommandAsync(MediaCommand command);
}

public interface IAudioOutputService : IDisposable
{
    bool? Muted { get; }
    void Refresh();
    bool ToggleMute();
}
