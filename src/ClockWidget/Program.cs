using System.Diagnostics;
using System.Globalization;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using ClockWidget.Core;
using ClockWidget.Media;
using Microsoft.Win32;

namespace ClockWidget;

public sealed class Program : Application
{
    private Mutex? instance;
    private WidgetWindow? widget;
    private SettingsWindow? editor;
    private TrayController? tray;
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private JsonStore store = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClockWidget"));
    private readonly List<WidgetWindow> mirrors = new();
    private bool storageReady;
    private WeatherController? weather;
    private bool exiting;
    private MediaController? media;
    private MediaStripWindow? mediaStrip;
    public string MediaStatus => media?.Status ?? "Media controls are off.";
    public bool MediaRunning => media != null;
    private EventWaitHandle? showRequest;
    private RegisteredWaitHandle? showRegistration;
    public Settings Settings { get; private set; } = new();
    public List<ThemePreset> Themes { get; private set; } = new();
    public IWeatherProvider Provider { get; private set; } = null!;
    public string StorageStatus { get; private set; } = "";
    public bool Preview { get; private set; }
    public bool DesktopProbe { get; private set; }
    public bool IsExiting => exiting;
    public string SettingsDirectory => store.DirectoryPath;
    public event Action? SettingsChanged;
    public IEnumerable<WidgetWindow> ClockWindows => widget == null ? mirrors : new[] { widget }.Concat(mirrors);

    [STAThread]
    public static int Main(string[] args)
    {
        var app = new Program();
        try { return app.Run(); }
        catch (Exception ex)
        {
            if (args.Contains("--preview")) { WritePreviewError(args, ex); return 1; }
            MessageBox.Show(ex.Message, "Widgets could not start", MessageBoxButton.OK, MessageBoxImage.Error);
            return 1;
        }
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        Preview = e.Args.Contains("--preview");
        DesktopProbe = Preview && e.Args.Contains("--desktop-probe");
        string? sessionDirectory = ArgumentValue(e.Args, "--session-dir");
        if (sessionDirectory != null) store = new JsonStore(Path.GetFullPath(sessionDirectory));
        if (!Preview)
        {
            string showEventName = sessionDirectory == null ? "Local\\ClockWidget.Show" : "Local\\ClockWidget.TestSession.Show";
            instance = new Mutex(true, sessionDirectory == null ? "Local\\ClockWidget.SaqibHussain" : "Local\\ClockWidget.TestSession", out bool first);
            if (!first)
            {
                try { using var existing = EventWaitHandle.OpenExisting(showEventName); existing.Set(); } catch (WaitHandleCannotBeOpenedException) { }
                Shutdown(); return;
            }
            showRequest = new EventWaitHandle(false, EventResetMode.AutoReset, showEventName);
            showRegistration = ThreadPool.RegisterWaitForSingleObject(showRequest, (_, _) => Dispatcher.BeginInvoke(() => { RevealClock(); OpenSettings(); }), null, Timeout.Infinite, false);
        }
        DispatcherUnhandledException += (_, ev) =>
        {
            if (Preview) { WritePreviewError(e.Args, ev.Exception); ev.Handled = true; Shutdown(1); return; }
            MessageBox.Show("Widgets encountered an error. Your saved settings are retained.\n\n" + ev.Exception.Message, "Widgets", MessageBoxButton.OK, MessageBoxImage.Error);
            ev.Handled = true; Shutdown(1);
        };
        var saved = Preview ? null : store.Load<Settings>("settings.json");
        bool firstRun = saved == null;
        Settings = Preview ? new Settings { TimeFormat = ArgumentValue(e.Args, "--time-format") == "12" ? "12" : "24" } : (saved ?? new()).Normalize();
        storageReady = !Preview;
        Themes = (Preview ? null : store.Load<List<ThemePreset>>("themes.json")) ?? new() { ThemePreset.Capture("Default", Settings) };
        Themes = Themes.Where(t => t != null && t.Appearance != null && !string.IsNullOrWhiteSpace(t.Name)).Select(t => ThemePreset.Capture(t.Name, t.Appearance)).ToList();
        if (!Themes.Any(t => t.Name == "Default")) Themes.Insert(0, ThemePreset.Capture("Default", Settings));
        if (storageReady && Settings.DefaultProfileVersion < 1)
        {
            if (MakeCurrentDefault()) Settings = Settings with { DefaultProfileVersion = 1 };
        }
        if (storageReady) SaveThemes();
        if (Preview) Settings = Settings with { Font = ArgumentValue(e.Args, "--font") == "Inter" ? "Inter" : "Nunito", Stacked = e.Args.Contains("--stacked") };
        Provider = new OpenMeteoProvider(http);
        weather = new WeatherController(Provider, new LocationService(), store);
        widget = new WidgetWindow(this, weather);
        widget.Closed += OnWidgetClosed;
        MainWindow = widget;
        widget.Show();
        if (Preview)
        {
            if (e.Args.Contains("--media-preview"))
            {
                media = new MediaController(new PreviewMediaSession(), new PreviewAudioOutput(), Dispatcher);
                mediaStrip = new MediaStripWindow(this, media); mediaStrip.Show();
                _ = media.StartAsync();
            }
            if (e.Args.Contains("--settings-preview"))
            {
                editor = new SettingsWindow(this, widget, weather); editor.Show();
            }
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
            {
                string? path = ArgumentValue(e.Args, "--output");
                if (path != null)
                {
                    if (editor != null) WidgetWindow.SaveVisual(editor, path); else if (mediaStrip != null) WidgetWindow.SaveVisual(mediaStrip, path); else widget.SavePreview(path);
                    if (DesktopProbe) widget.ProbeDesktop(path + ".desktop.json");
                }
                Shutdown();
            });
            return;
        }
        SynchronizeDisplays();
        SynchronizeMedia();
        tray = new TrayController(this, widget);
        SystemEvents.PowerModeChanged += OnPowerChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaysChanged;
        SessionEnding += (_, _) => { Save(); exiting = true; };
        OpenSettings();
        Save();
        if (e.Args.Contains("--lifecycle-probe"))
        {
            if (sessionDirectory == null) throw new InvalidOperationException("Lifecycle probe requires an isolated --session-dir.");
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, async () =>
            {
                try { await LifecycleProbe.RunAsync(this, ArgumentValue(e.Args, "--output") ?? Path.Combine(store.DirectoryPath, "report.json")); Shutdown(); }
                catch (Exception error) { WritePreviewError(e.Args, error); Shutdown(1); }
            });
        }
        if (e.Args.Contains("--media-probe"))
        {
            if (sessionDirectory == null) throw new InvalidOperationException("Media probe requires an isolated --session-dir.");
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, async () =>
            {
                try { await MediaProbe.RunAsync(this, ArgumentValue(e.Args, "--output") ?? Path.Combine(store.DirectoryPath, "media-report.json")); Shutdown(); }
                catch (Exception error) { WritePreviewError(e.Args, error); Shutdown(1); }
            });
        }
        if (!firstRun) _ = weather.RefreshAsync(Settings, true, true);
    }
    private static string? ArgumentValue(string[] args, string name)
    {
        int index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }
    private static void WritePreviewError(string[] args, Exception error)
    {
        string path = (ArgumentValue(args, "--output") ?? Path.Combine(Environment.CurrentDirectory, "preview")) + ".error.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, error.ToString());
    }
    public void UpdateSettings(Settings settings)
    {
        bool startupChanged = Settings.LaunchAtSignIn != settings.LaunchAtSignIn;
        bool displaysChanged = Settings.DisplayId != settings.DisplayId || Settings.AllDisplays != settings.AllDisplays;
        Settings = settings.Normalize();
        if (startupChanged)
        {
            try { StartupRegistration.Set(Settings.LaunchAtSignIn); }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
            {
                Settings = Settings with { LaunchAtSignIn = !Settings.LaunchAtSignIn };
                MessageBox.Show("Windows did not allow changing launch at sign-in.\n" + ex.Message, "Widgets");
            }
        }
        foreach (var clock in ClockWindows.ToArray()) clock.ApplySettings();
        if (displaysChanged) SynchronizeDisplays();
        foreach (var clock in ClockWindows.ToArray()) { if (Settings.ClockVisible) { if (!clock.IsVisible) clock.Show(); } else clock.Hide(); }
        if (storageReady) Save();
        if (storageReady) SynchronizeMedia();
        tray?.Refresh(); SettingsChanged?.Invoke();
        if (weather != null) _ = weather.RefreshAsync(Settings);
    }
    private void SynchronizeMedia()
    {
        if (Preview) return;
        if (!Settings.MediaEnabled)
        {
            mediaStrip?.CloseForExit(); mediaStrip = null;
            if (media != null) { media.Changed -= OnMediaChanged; media.Dispose(); media = null; }
            return;
        }
        if (media == null)
        {
            media = new MediaController(new WindowsMediaSessionService(Dispatcher), new AudioEndpointService(), Dispatcher);
            media.Changed += OnMediaChanged;
            mediaStrip = new MediaStripWindow(this, media); mediaStrip.Show();
            _ = media.StartAsync();
        }
        mediaStrip?.Align(Settings.MediaDisplayId);
    }
    private void OnMediaChanged() => SettingsChanged?.Invoke();
    public void RealignMedia() => mediaStrip?.Align(Settings.MediaDisplayId);
    public bool MakeCurrentDefault()
    {
        var previous = Themes.FirstOrDefault(t => t.Name == "Default");
        if (previous != null && !Themes.Any(t => t.Name == "Previous Default")) Themes.Add(previous with { Name = "Previous Default" });
        Themes.RemoveAll(t => t.Name == "Default");
        Themes.Insert(0, ThemePreset.Capture("Default", Settings));
        return SaveThemes();
    }
    public bool SaveTheme(string name)
    {
        name = name.Trim();
        if (name.Length == 0 || name.Length > 60 || name.Equals("Default", StringComparison.OrdinalIgnoreCase)) return false;
        var theme = ThemePreset.Capture(name, Settings);
        int index = Themes.FindIndex(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) Themes[index] = theme; else Themes.Add(theme);
        return SaveThemes();
    }
    private bool SaveThemes()
    {
        if (!storageReady) return true;
        try { store.Save("themes.json", Themes); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StorageStatus = "Themes could not be saved: " + ex.Message; return false; }
    }
    public void OpenSettings()
    {
        if (editor?.IsVisible == true) { editor.Activate(); return; }
        editor = new SettingsWindow(this, widget!, weather!);
        editor.Closed += (_, _) => { editor = null; Save(); if (!exiting && Settings.ClockVisible) foreach (var clock in ClockWindows.ToArray()) clock.Reveal(); };
        editor.Show(); editor.Activate();
    }
    public void CloseSettings() => editor?.Close();
    private void OnWidgetClosed(object? sender, EventArgs e)
    {
        if (exiting || Preview || weather == null) return;
        widget?.Stop();
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (exiting) return;
            bool reopen = editor?.IsVisible == true;
            editor?.Close(); tray?.Dispose();
            widget = new WidgetWindow(this, weather); widget.Closed += OnWidgetClosed;
            MainWindow = widget; widget.Show(); tray = new TrayController(this, widget);
            if (reopen) OpenSettings();
        });
    }
    public new void Shutdown() { exiting = true; base.Shutdown(); }
    public new void Shutdown(int exitCode) { exiting = true; base.Shutdown(exitCode); }
    public void ResetPosition(bool center = false) { foreach (var clock in ClockWindows.ToArray()) clock.ResetPosition(center); }
    public void RevealClock() { if (!Settings.ClockVisible) UpdateSettings(Settings with { ClockVisible = true }); foreach (var clock in ClockWindows.ToArray()) clock.Reveal(); }
    public void ToggleClockVisibility() { bool visible = !Settings.ClockVisible; UpdateSettings(Settings with { ClockVisible = visible }); if (visible) RevealClock(); }
    public void Unlock() => UpdateSettings(Settings with { Locked = false, ClickThrough = false });
    public void SavePosition(string displayId, Point pixels)
    {
        var positions = new Dictionary<string, WidgetPosition>(Settings.DisplayPositions) { [displayId] = new(pixels.X, pixels.Y) };
        bool primary = widget?.TargetDisplayId == displayId;
        UpdateSettings(Settings with { DisplayPositions = positions, DisplayId = primary ? displayId : Settings.DisplayId, X = primary ? pixels.X : Settings.X, Y = primary ? pixels.Y : Settings.Y });
    }
    public void SelectDisplay(string displayId) => UpdateSettings(Settings with { DisplayId = displayId });
    private void SynchronizeDisplays()
    {
        if (widget == null || weather == null || Preview) return;
        string selected = DisplayService.Resolve(Settings.DisplayId).DeviceName;
        widget.MoveToDisplay(selected);
        var desired = Settings.AllDisplays ? DisplayService.Available().Select(d => d.Id).Where(d => d != selected).ToArray() : Array.Empty<string>();
        foreach (var mirror in mirrors.Where(m => !desired.Contains(m.TargetDisplayId)).ToArray()) { mirrors.Remove(mirror); mirror.CloseForRemoval(); }
        foreach (string id in desired.Where(id => !mirrors.Any(m => m.TargetDisplayId == id)))
        {
            var mirror = new WidgetWindow(this, weather, id); mirrors.Add(mirror); mirror.Show(); if (!Settings.ClockVisible) mirror.Hide();
        }
    }
    private void Save()
    {
        if (!storageReady || Preview) return;
        try { store.Save("settings.json", Settings); StorageStatus = ""; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { StorageStatus = "Settings could not be saved: " + ex.Message; tray?.Refresh(); }
    }
    private void OnPowerChanged(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume) Dispatcher.BeginInvoke(() => { widget?.RefreshNow(); RealignMedia(); if (weather != null) _ = weather.RefreshAsync(Settings, true, true); });
    }
    private void OnDisplaysChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() => { SynchronizeDisplays(); foreach (var clock in ClockWindows.ToArray()) clock.RecoverDisplay(); RealignMedia(); SettingsChanged?.Invoke(); });
    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.PowerModeChanged -= OnPowerChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaysChanged;
        if (!Preview && widget != null) Save();
        mediaStrip?.CloseForExit(); mediaStrip = null;
        if (media != null) { media.Changed -= OnMediaChanged; media.Dispose(); media = null; }
        weather?.Dispose(); tray?.Dispose(); foreach (var clock in ClockWindows.ToArray()) clock.Stop();
        showRegistration?.Unregister(null); showRequest?.Dispose();
        http.Dispose(); instance?.Dispose(); base.OnExit(e);
    }
    public static void OpenLink(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (System.ComponentModel.Win32Exception)
        {
            if (File.Exists(url))
            {
                try { var info = new ProcessStartInfo("notepad.exe") { UseShellExecute = false }; info.ArgumentList.Add(url); Process.Start(info); return; }
                catch (System.ComponentModel.Win32Exception) { }
            }
            MessageBox.Show("Windows could not open this link or file:\n" + url, "Widgets", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

internal static class StartupRegistration
{
    public static void Set(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) key.SetValue("ClockWidget", $"\"{Environment.ProcessPath}\"");
        else key.DeleteValue("ClockWidget", false);
    }
}
