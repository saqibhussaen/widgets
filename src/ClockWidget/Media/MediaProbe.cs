using System.Text.Json;
using System.Windows;
using ClockWidget.Core;

namespace ClockWidget.Media;

// Explicit read-only integration check. It never sends playback or audio changes.
internal static class MediaProbe
{
    public static async Task RunAsync(Program app, string output)
    {
        using var sessions = new WindowsMediaSessionService(app.Dispatcher);
        using var audio = new AudioEndpointService();
        await sessions.StartAsync(); audio.Refresh();
        app.UpdateSettings(app.Settings with { MediaEnabled = true });
        await Task.Delay(500);
        var strip = Application.Current.Windows.OfType<MediaStripWindow>().Single();
        bool visible = strip.IsVisible;
        app.ToggleClockVisibility();
        bool independent = strip.IsVisible;
        app.CloseSettings(); await Task.Delay(300);
        bool survives = strip.IsVisible;
        app.UpdateSettings(app.Settings with { MediaEnabled = false });
        bool cleanedUp = !app.MediaRunning && !Application.Current.Windows.OfType<MediaStripWindow>().Any();
        var report = new
        {
            SessionAccessAvailable = !sessions.State.Status.Contains("access is unavailable"),
            SessionCount = sessions.Sources.Count,
            AudioOutputAvailable = audio.Muted.HasValue,
            ToolbarVisible = visible, IndependentFromClockVisibility = independent,
            SurvivesSettingsClose = survives, DisableReleasesControllerAndWindow = cleanedUp,
            PlaybackCommandsSent = 0, AudioChangesSent = 0
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        if (!(visible && independent && survives && cleanedUp)) throw new InvalidOperationException("Media lifecycle check failed.");
    }
}

// Deterministic offline screenshot source; no player, endpoint or network access.
internal sealed class PreviewMediaSession : IMediaSessionService
{
    public MediaState State => new("An afternoon playlist", "Example artist", true, true, true, true, "Illustrative preview");
    public IReadOnlyList<MediaSource> Sources => Array.Empty<MediaSource>();
    public string SelectedId => "";
    public event Action? Changed { add { } remove { } }
    public Task StartAsync() => Task.CompletedTask;
    public Task SelectAsync(string? id) => Task.CompletedTask;
    public Task<bool> CommandAsync(MediaCommand command) => Task.FromResult(false);
    public void Dispose() { }
}
internal sealed class PreviewAudioOutput : IAudioOutputService
{
    public bool? Muted => false;
    public void Refresh() { }
    public bool ToggleMute() => false;
    public void Dispose() { }
}
