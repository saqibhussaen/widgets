using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using ClockWidget;
using ClockWidget.Core;
using ClockWidget.Media;
using System.Windows.Threading;

internal static class TestProgram
{
    private static int passed, failed;
    [STAThread]
    public static int Main()
    {
        Run("default appearance", () => { var s = new Settings(); Equal("Nunito", s.Font); Equal(864d, s.Weight); Equal(163d, s.Size); True(s.Seconds); Equal(29d, s.DateSize); Equal(745d, s.DateWeight); Equal(29d, s.WeatherSize); Equal(745d, s.WeatherWeight); Equal("Above", s.DatePosition); Equal("Below", s.WeatherPosition); });
        Run("settings normalize malformed values", () =>
        {
            var s = new Settings { Size = -5, Weight = 2000, Opacity = double.NaN, Color = "bad", Latitude = 100, X = double.PositiveInfinity }.Normalize();
            Equal(48d, s.Size); Equal(900d, s.Weight); Equal(1d, s.Opacity); Equal("#FFFFFF", s.Color); True(s.Latitude == null && s.X == null);
        });
        Run("atomic settings round-trip", () => WithStore(store => { var s = new Settings { Weight = 553, Font = "Inter", X = -1200, Locked = true, DisplayPositions = new() { ["secondary"] = new(-1200, 80) } }; store.Save("settings.json", s); Equal(JsonSerializer.Serialize(s), JsonSerializer.Serialize(store.Load<Settings>("settings.json")!)); True(!File.Exists(Path.Combine(store.DirectoryPath, "settings.json.tmp"))); }));
        Run("corrupt settings recover", () => WithStore(store => { Directory.CreateDirectory(store.DirectoryPath); File.WriteAllText(Path.Combine(store.DirectoryPath, "settings.json"), "{invalid"); True(store.Load<Settings>("settings.json") == null); }));
        Run("midnight 12 hour", () => Equal("12:00", ClockText.Format(new(2026, 1, 1), new() { TimeFormat = "12", Seconds = false })));
        Run("noon and period", () => { var t = new DateTime(2026, 1, 1, 12, 0, 0); Equal("12:00", ClockText.Format(t, new() { TimeFormat = "12", Seconds = false })); Equal("PM", ClockText.Period(t, new() { TimeFormat = "12" }, CultureInfo.GetCultureInfo("en-US"))); });
        Run("system 24 hour with seconds", () => Equal("23:59:58", ClockText.Format(new(2026, 1, 1, 23, 59, 58), new() { Seconds = true }, CultureInfo.GetCultureInfo("en-GB"))));
        Run("system 12 hour", () => Equal("01:08", ClockText.Format(new(2026, 1, 1, 13, 8, 0), new() { Seconds = false }, CultureInfo.GetCultureInfo("en-US"))));
        Run("clock has no accumulated timer drift", () => { var s = new Settings { TimeFormat = "24", Seconds = false }; Equal("03:00", ClockText.Format(new(2026, 3, 8, 3, 0, 0), s)); Equal("01:59", ClockText.Format(new(2026, 3, 8, 1, 59, 0), s)); });
        Run("backup restores the previous complete settings", () => WithStore(store => { store.Save("settings.json", new Settings { Size = 163, Seconds = true }); store.Save("settings.json", new Settings { Size = 180 }); File.WriteAllText(Path.Combine(store.DirectoryPath, "settings.json"), "{truncated"); var restored = store.Load<Settings>("settings.json")!; Equal(163d, restored.Size); True(restored.Seconds); }));
        Run("date/weather limits and hidden state persist", () => WithStore(store => { var s = new Settings { DateSize = 500, DateWeight = 151, WeatherSize = 1, WeatherWeight = 1000, ClockVisible = false, AllDisplays = true, DisplayId = "secondary" }.Normalize(); Equal(80d, s.DateSize); Equal(200d, s.DateWeight); Equal(10d, s.WeatherSize); Equal(900d, s.WeatherWeight); store.Save("settings.json", s); var read = store.Load<Settings>("settings.json")!; True(!read.ClockVisible && read.AllDisplays); Equal("secondary", read.DisplayId); }));
        Run("legacy hidden rows migrate without losing placement", () => { var s = new Settings { Version = 2, DatePosition = "Hidden", WeatherPosition = "Hidden" }.Normalize(); True(!s.ShowDate && !s.ShowWeather); Equal("Above", s.DatePosition); Equal("Below", s.WeatherPosition); True(!s.Normalize().ShowDate); });
        Run("period can be hidden and resized safely", () => { Equal("", ClockText.Period(DateTime.Now, new Settings { TimeFormat = "12", ShowPeriod = false })); Equal(100d, new Settings { PeriodSize = 999 }.Normalize().PeriodSize); });
        Run("themes exclude private settings and preserve desktop behavior", () => WithStore(store => {
            var source = new Settings { ShowTime = false, ShowCity = false, PeriodSize = 44, Size = 190, City = "Private city", Latitude = 20, Longitude = 30, WeatherConsent = true, LocationMode = "Automatic", DisplayId = "private-monitor", LaunchAtSignIn = true, X = 99 };
            var theme = ThemePreset.Capture("Date only", source); var json = JsonSerializer.Serialize(theme); True(!json.Contains("Private city") && !json.Contains("private-monitor")); True(theme.Appearance.Latitude == null && !theme.Appearance.WeatherConsent && !theme.Appearance.LaunchAtSignIn);
            store.Save("themes.json", new[] { theme }); var restored = store.Load<ThemePreset[]>("themes.json")![0];
            var target = new Settings { City = "Keep city", Latitude = 1, Longitude = 2, WeatherConsent = false, Locked = true, X = 7, AllDisplays = true };
            var applied = restored.Apply(target); True(!applied.ShowTime && !applied.ShowCity && applied.Locked && applied.AllDisplays && !applied.WeatherConsent); Equal(190d, applied.Size); Equal(7d, applied.X!.Value); Equal("Keep city", applied.City);
        }));
        Run("custom text migrates without changing forecast city", () => { var s = new Settings { Version = 3, ShowCity = false, City = "Forecast city" }.Normalize(); Equal("Forecast city", s.CustomText); Equal("Forecast city", s.City); True(!s.ShowCustomText); var edited = (s with { CustomText = "  My\nmessage  ", ShowCustomText = true }).Normalize(); Equal("Mymessage", edited.CustomText); Equal("Forecast city", edited.City); Equal(120, (edited with { CustomText = new string('a', 200) }).Normalize().CustomText.Length); });
        Run("themes exclude custom text and media enablement", () => { var source = new Settings { CustomText = "Personal message", ShowCustomText = false, MediaEnabled = true, MediaDisplayId = "private-monitor" }; var theme = ThemePreset.Capture("Private", source); True(!theme.Appearance.MediaEnabled && theme.Appearance.CustomText.Length == 0 && theme.Appearance.MediaDisplayId.Length == 0); var target = new Settings { CustomText = "Keep this", MediaEnabled = false }; var applied = theme.Apply(target); Equal("Keep this", applied.CustomText); True(!applied.ShowCustomText && !applied.MediaEnabled); });
        foreach (var font in new[] { "Nunito", "Inter" })
        {
            Run(font + " genuine intermediate axis outlines", () =>
            {
                var glyph = new ClockGlyph(); glyph.Update("10:08", font, 550, 112); string a = glyph.Outline.ToString(CultureInfo.InvariantCulture);
                glyph.Update("10:08", font, 551, 112); string b = glyph.Outline.ToString(CultureInfo.InvariantCulture);
                glyph.Update("10:08", font, 600, 112); string c = glyph.Outline.ToString(CultureInfo.InvariantCulture);
                True(a != b && b != c && a != c); True(!glyph.Outline.IsEmpty());
            });
            Run(font + " shaped date and city weights interpolate", () =>
            {
                var glyph = new ClockGlyph { ShapedText = true };
                foreach (string text in new[] { "Thursday, 22 October", "Karachi, Pakistan", "24°C · Partly cloudy" })
                { glyph.Update(text, font, 550, 24); string a = glyph.Outline.ToString(CultureInfo.InvariantCulture); glyph.Update(text, font, 551, 24); True(a != glyph.Outline.ToString(CultureInfo.InvariantCulture)); True(glyph.Outline.Bounds.Width > 0); }
            });
            Run(font + " all sizes, endpoints and stacked parts", () =>
            {
                var glyph = new ClockGlyph();
                foreach (double size in new[] { 48d, 112d, 240d }) foreach (double weight in new[] { 200d, 550d, 900d }) foreach (var text in new[] { "00", "59", "23:59:59" })
                { glyph.Update(text, font, weight, size); True(!glyph.Outline.IsEmpty()); True(glyph.Outline.Bounds.Width > 0); }
            });
        }
        Run("weather fixture parse and units", () => { using var j = JsonDocument.Parse(Fixture("current")); var w = OpenMeteoProvider.Parse(j.RootElement, DateTimeOffset.UtcNow, 1, 2); Equal(24.5, w.Celsius); Equal(76.1, w.Temperature(true), .001); Equal("Partly cloudy", w.Condition); Equal("Cloud", w.Icon); });
        Run("weather freshness boundaries", () => { var t = DateTimeOffset.UtcNow; var w = new WeatherReading(20, 0, true, t, 1, 2); True(!w.IsStale(t.AddMinutes(59))); True(w.IsStale(t.AddHours(1))); True(!w.IsExpired(t.AddHours(23))); True(w.IsExpired(t.AddHours(24))); });
        Run("weather malformed and missing fields", () => { foreach (string json in new[] { "{}", "{\"current\":{\"temperature_2m\":999,\"weather_code\":0,\"is_day\":1}}", "{\"current\":null}" }) { bool threw = false; try { using var j = JsonDocument.Parse(json); OpenMeteoProvider.Parse(j.RootElement, DateTimeOffset.UtcNow, 0, 0); } catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { threw = true; } True(threw); } });
        Run("day/night and condition icons", () => { Equal("Moon", new WeatherReading(0, 0, false, default, 0, 0).Icon); Equal("Snow", new WeatherReading(0, 85, true, default, 0, 0).Icon); Equal("Thunderstorm", new WeatherReading(0, 95, true, default, 0, 0).Condition); });
        Run("repeated appearance changes preserve WPF logical tree", () => WithStore(store =>
        {
            var app = new ClockWidget.Program(); using var controller = new WeatherController(new FakeWeather(), new FakeLocation(), store);
            var window = new WidgetWindow(app, controller);
            foreach (bool stacked in new[] { false, true, false }) foreach (var position in new[] { "Above", "Below", "Hidden" })
            { app.UpdateSettings(app.Settings with { Stacked = stacked, DatePosition = position, WeatherPosition = position, Seconds = true }); window.ApplySettings(); var content = (System.Windows.FrameworkElement)window.Content; content.Measure(new System.Windows.Size(2000, 2000)); True(content.DesiredSize.Width > 0); }
            foreach (var state in new[] { (false, true, false), (false, false, true), (true, false, false), (false, false, false) })
            {
                app.UpdateSettings(app.Settings with { ShowTime = state.Item1, ShowDate = state.Item2, ShowWeather = state.Item3, ShowCity = false, TimeFormat = "12", PeriodSize = 44 }); window.ApplySettings();
                var rows = (System.Windows.Controls.StackPanel)((System.Windows.Controls.Border)window.Content).Child;
                Equal((state.Item1 ? 1 : 0) + (state.Item2 ? 1 : 0) + (state.Item3 ? 1 : 0), rows.Children.Count);
            }
            app.MakeCurrentDefault(); app.UpdateSettings(app.Settings with { DateSize = 33 }); app.MakeCurrentDefault();
            Equal(33d, app.Themes.Single(t => t.Name == "Default").Appearance.DateSize);
            True(app.Themes.Any(t => t.Name == "Previous Default"));
            var settings = new SettingsWindow(app, window, controller);
            settings.ApplyTemplate();
            ((System.Windows.FrameworkElement)settings.Content).Measure(new System.Windows.Size(1080, 700));
            settings.Close();
            window.CloseForRemoval();
        }));
        SynchronizationContext.SetSynchronizationContext(null);
        RunAsync("media startup reads state without sending commands", async () => { var sessions = new FakeMedia(); var audio = new FakeAudio(); using var controller = new MediaController(sessions, audio, Dispatcher.CurrentDispatcher); await controller.StartAsync(); Equal(0, sessions.Calls); Equal(0, audio.Writes); True(audio.Reads > 0); True(controller.Muted == false); });
        RunAsync("media command routing respects rejection", async () => { var sessions = new FakeMedia { Accept = false }; using var controller = new MediaController(sessions, new FakeAudio(), Dispatcher.CurrentDispatcher); True(!await controller.ExecuteAsync(MediaCommand.Next)); Equal(MediaCommand.Next, sessions.Last); Equal(1, sessions.Calls); });
        RunAsync("media serializes rapid clicks", async () => { var sessions = new FakeMedia { Pending = new() }; using var controller = new MediaController(sessions, new FakeAudio(), Dispatcher.CurrentDispatcher); var first = controller.ExecuteAsync(MediaCommand.PlayPause); True(controller.Busy); True(!await controller.ExecuteAsync(MediaCommand.Next)); Equal(1, sessions.Calls); sessions.Pending.SetResult(true); True(await first); True(!controller.Busy); });
        RunAsync("media disposal ignores late command completion", async () => { var sessions = new FakeMedia { Pending = new() }; var audio = new FakeAudio(); var controller = new MediaController(sessions, audio, Dispatcher.CurrentDispatcher); int notifications = 0; controller.Changed += () => notifications++; var pending = controller.ExecuteAsync(MediaCommand.Previous); controller.Dispose(); int before = notifications; sessions.Pending.SetResult(true); await pending; Equal(before, notifications); True(sessions.Disposed && audio.Disposed); True(!await controller.ExecuteAsync(MediaCommand.Next)); });
        Run("media mute is explicit and unavailable output fails", () => { var audio = new FakeAudio(); using var controller = new MediaController(new FakeMedia(), audio, Dispatcher.CurrentDispatcher); Equal(0, audio.Writes); True(controller.ToggleMute()); Equal(1, audio.Writes); True(controller.Muted == true); audio.Available = false; True(!controller.ToggleMute()); True(controller.Status.Contains("No available")); });
        RunAsync("HTTP rounding, parse, and city search", async () =>
        {
            var handler = new FixtureHandler(); using var http = new HttpClient(handler); var provider = new OpenMeteoProvider(http);
            await provider.GetCurrentAsync(24.8608, 67.0104, default); True(handler.Last!.Contains("latitude=24.86") && handler.Last.Contains("longitude=67.01"));
            var cities = await provider.SearchAsync("Karachi", default); Equal("Pakistan", cities[0].Country);
        });
        RunAsync("HTTP errors propagate", async () =>
        {
            using var http = new HttpClient(new FixtureHandler { Failure = true }); bool threw = false;
            try { await new OpenMeteoProvider(http).GetCurrentAsync(1, 2, default); } catch (HttpRequestException) { threw = true; } True(threw);
        });
        RunAsync("weather consent prevents network and location", async () =>
        {
            await WithStoreAsync(async store => { var p = new FakeWeather(); var l = new FakeLocation(); using var c = new WeatherController(p, l, store); await c.RefreshAsync(new Settings { LocationMode = "Automatic" }); Equal(0, p.Calls); Equal(0, l.Calls); });
        });
        RunAsync("hidden weather pauses requests while keeping city and consent", async () => {
            await WithStoreAsync(async store => { var p = new FakeWeather(); var l = new FakeLocation(); using var c = new WeatherController(p, l, store); var s = Manual(); await c.RefreshAsync(s with { ShowWeather = false }, true, true); Equal(0, p.Calls); Equal(0, l.Calls); await c.RefreshAsync(s); Equal(1, p.Calls); await c.RefreshAsync(s with { ShowWeather = false }); True(c.Status.Contains("paused")); });
        });
        RunAsync("denied location offers manual fallback", async () =>
        {
            await WithStoreAsync(async store => { var p = new FakeWeather(); var l = new FakeLocation { Denied = true }; using var c = new WeatherController(p, l, store); await c.RefreshAsync(new Settings { WeatherConsent = true, LocationMode = "Automatic" }); Equal(0, p.Calls); True(c.Status.Contains("choose a city")); await c.RefreshAsync(Manual()); Equal(1, p.Calls); True(c.Reading != null); });
        });
        RunAsync("cache preserved during offline failures", async () =>
        {
            await WithStoreAsync(async store => { var p = new FakeWeather(); using var c = new WeatherController(p, new FakeLocation(), store); await c.RefreshAsync(Manual()); p.Fail = true; await c.RefreshAsync(Manual(), true); True(c.Reading != null); True(c.Status.Contains("unavailable")); });
        });
        RunAsync("old and wrong-location cache excluded", async () =>
        {
            await WithStoreAsync(async store => { store.Save("weather-cache.json", new WeatherReading(10, 0, true, DateTimeOffset.UtcNow.AddHours(-25), 1, 2)); using var c = new WeatherController(new FakeWeather { Fail = true }, new FakeLocation(), store); await c.RefreshAsync(Manual()); True(c.Reading == null); store.Save("weather-cache.json", new WeatherReading(10, 0, true, DateTimeOffset.UtcNow, 9, 9)); await c.RefreshAsync(Manual(), true); True(c.Reading == null); });
        });
        RunAsync("turning off weather clears location cache", async () =>
        {
            await WithStoreAsync(async store => { using var c = new WeatherController(new FakeWeather(), new FakeLocation(), store); await c.RefreshAsync(Manual()); True(store.Load<WeatherReading>("weather-cache.json") != null); await c.RefreshAsync(new Settings()); True(c.Reading == null); True(store.Load<WeatherReading>("weather-cache.json") == null); });
        });
        RunAsync("automatic location reused and refreshed on resume", async () =>
        {
            await WithStoreAsync(async store => { var l = new FakeLocation(); var p = new FakeWeather(); using var c = new WeatherController(p, l, store); var s = new Settings { WeatherConsent = true, LocationMode = "Automatic" }; await c.RefreshAsync(s, true); await c.RefreshAsync(s); Equal(1, p.Calls); await c.RefreshAsync(s, true); Equal(1, l.Calls); await c.RefreshAsync(s, true, true); Equal(2, l.Calls); });
        });
        RunAsync("in-flight result cannot re-enable weather after consent revoked", async () =>
        {
            await WithStoreAsync(async store =>
            {
                var provider = new DelayedWeather(); using var controller = new WeatherController(provider, new FakeLocation(), store);
                var old = controller.RefreshAsync(Manual());
                await controller.RefreshAsync(new Settings());
                provider.First.SetResult(new WeatherReading(99, 0, true, DateTimeOffset.UtcNow, 1, 2));
                await old;
                True(controller.Reading == null); Equal("Set up weather", controller.Status); True(store.Load<WeatherReading>("weather-cache.json") == null);
            });
        });
        RunAsync("superseded location result cannot overwrite newer city", async () =>
        {
            await WithStoreAsync(async store =>
            {
                var provider = new DelayedWeather(); using var controller = new WeatherController(provider, new FakeLocation(), store);
                var old = controller.RefreshAsync(Manual());
                await controller.RefreshAsync(Manual() with { Latitude = 3, Longitude = 4 });
                provider.First.SetResult(new WeatherReading(99, 0, true, DateTimeOffset.UtcNow, 1, 2)); await old;
                Equal(3d, controller.Reading!.Latitude); Equal(24d, controller.Reading.Celsius);
            });
        });
        Console.WriteLine($"\n{passed} passed, {failed} failed."); return failed == 0 ? 0 : 1;
    }
    private static Settings Manual() => new() { WeatherConsent = true, LocationMode = "Manual", Latitude = 1, Longitude = 2 };
    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name + ".json"));
    private static void Run(string name, Action test) { try { test(); passed++; Console.WriteLine("PASS " + name); } catch (Exception ex) { failed++; Console.WriteLine("FAIL " + name + ": " + ex); } }
    private static void RunAsync(string name, Func<Task> test) => Run(name, () => test().GetAwaiter().GetResult());
    private static void True(bool value) { if (!value) throw new Exception("Assertion failed"); }
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }
    private static void Equal(double expected, double actual, double tolerance) => True(Math.Abs(expected - actual) < tolerance);
    private static void WithStore(Action<JsonStore> action) { var path = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString()); try { action(new(path)); } finally { if (Directory.Exists(path)) Directory.Delete(path, true); } }
    private static async Task WithStoreAsync(Func<JsonStore, Task> action) { var path = Path.Combine(AppContext.BaseDirectory, "test-data", Guid.NewGuid().ToString()); try { await action(new(path)); } finally { if (Directory.Exists(path)) Directory.Delete(path, true); } }
    private sealed class FakeMedia : IMediaSessionService
    {
        public bool Accept = true, Disposed; public int Calls; public MediaCommand Last; public TaskCompletionSource<bool>? Pending;
        public MediaState State => new("Fixture title", "Fixture artist", false, true, true, true, "");
        public IReadOnlyList<MediaSource> Sources => Array.Empty<MediaSource>(); public string SelectedId => "";
        public event Action? Changed { add { } remove { } }
        public Task StartAsync() => Task.CompletedTask;
        public Task SelectAsync(string? id) => Task.CompletedTask;
        public Task<bool> CommandAsync(MediaCommand command) { Calls++; Last = command; return Pending?.Task ?? Task.FromResult(Accept); }
        public void Dispose() => Disposed = true;
    }
    private sealed class FakeAudio : IAudioOutputService
    {
        public bool Available = true, Disposed; public int Reads, Writes; private bool muted;
        public bool? Muted => Available ? muted : null;
        public void Refresh() => Reads++;
        public bool ToggleMute() { if (!Available) return false; Writes++; muted = !muted; return true; }
        public void Dispose() => Disposed = true;
    }
    private sealed class FixtureHandler : HttpMessageHandler
    {
        public string? Last; public bool Failure;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Last = request.RequestUri!.ToString(); return Task.FromResult(new HttpResponseMessage(Failure ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK) { Content = new StringContent(Fixture(Last.Contains("geocoding") ? "cities" : "current")) }); }
    }
    private sealed class FakeWeather : IWeatherProvider
    {
        public int Calls; public bool Fail;
        public Task<WeatherReading> GetCurrentAsync(double latitude, double longitude, CancellationToken token) { Calls++; if (Fail) throw new HttpRequestException("Offline"); return Task.FromResult(new WeatherReading(24, 2, true, DateTimeOffset.UtcNow, latitude, longitude)); }
        public Task<IReadOnlyList<Place>> SearchAsync(string city, CancellationToken token) => Task.FromResult<IReadOnlyList<Place>>([]);
    }
    private sealed class FakeLocation : ILocationService
    {
        public int Calls; public bool Denied;
        public Task<(double Latitude, double Longitude)?> LocateAsync(CancellationToken token, bool requestPermission = false) { Calls++; return Task.FromResult<(double, double)?>(Denied ? null : (1, 2)); }
    }
    private sealed class DelayedWeather : IWeatherProvider
    {
        public readonly TaskCompletionSource<WeatherReading> First = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int calls;
        public Task<WeatherReading> GetCurrentAsync(double latitude, double longitude, CancellationToken token) => ++calls == 1 ? First.Task : Task.FromResult(new WeatherReading(24, 0, true, DateTimeOffset.UtcNow, latitude, longitude));
        public Task<IReadOnlyList<Place>> SearchAsync(string city, CancellationToken token) => Task.FromResult<IReadOnlyList<Place>>([]);
    }
}
