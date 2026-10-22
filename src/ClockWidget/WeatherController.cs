using ClockWidget.Core;

namespace ClockWidget;

public sealed class WeatherController(IWeatherProvider provider, ILocationService location, JsonStore store) : IDisposable
{
    private CancellationTokenSource? pending;
    private (double Lat, double Lon)? automatic;
    private DateTimeOffset lastLocated;
    private DateTimeOffset nextRefresh;
    private string key = "";
    public WeatherReading? Reading { get; private set; }
    public string Status { get; private set; } = "Set up weather";
    public string LocationName { get; private set; } = "";
    public event Action? Changed;

    public async Task RefreshAsync(Settings settings, bool force = false, bool relocate = false, bool requestPermission = false)
    {
        string newKey = $"{settings.ShowWeather}|{settings.LocationMode}|{settings.WeatherConsent}|{settings.Latitude}|{settings.Longitude}";
        LocationName = settings.City.Length > 0 ? settings.City : settings.LocationMode == "Automatic" ? "Current location" : "";
        if (key != newKey)
        {
            key = newKey; force = true; automatic = null; lastLocated = default;
            Reading = null; CancelActive();
        }
        if (!settings.WeatherConsent || settings.LocationMode == "Off")
        {
            Reading = null; Status = "Set up weather";
            TryDeleteCache(); Changed?.Invoke(); return;
        }
        if (!settings.ShowWeather) { CancelActive(); Status = "Weather is hidden; refresh paused"; Changed?.Invoke(); return; }
        if (pending != null && !pending.IsCancellationRequested && !force && !relocate) return;
        if (!force && !relocate && DateTimeOffset.UtcNow < nextRefresh) return;
        CancelActive();
        var cts = pending = new CancellationTokenSource(TimeSpan.FromSeconds(40));
        try
        {
            (double Lat, double Lon)? coordinates = settings.LocationMode == "Manual" && settings.Latitude.HasValue && settings.Longitude.HasValue
                ? (settings.Latitude.Value, settings.Longitude.Value) : null;
            if (settings.LocationMode == "Automatic")
            {
                if (automatic == null || relocate || DateTimeOffset.UtcNow - lastLocated >= TimeSpan.FromHours(1))
                {
                    Status = "Getting location…"; Changed?.Invoke();
                    var found = await location.LocateAsync(cts.Token, requestPermission);
                    if (found == null) { automatic = null; Reading = null; Status = "Location unavailable — choose a city in Settings"; Changed?.Invoke(); return; }
                    automatic = (found.Value.Latitude, found.Value.Longitude); lastLocated = DateTimeOffset.UtcNow;
                }
                coordinates = automatic;
            }
            cts.Token.ThrowIfCancellationRequested();
            if (coordinates == null) { Reading = null; Status = "Choose a city in Settings"; Changed?.Invoke(); return; }
            var lat = Math.Round(coordinates.Value.Lat, 2); var lon = Math.Round(coordinates.Value.Lon, 2);
            if (Reading?.Latitude != lat || Reading.Longitude != lon) Reading = null;
            var cached = store.Load<WeatherReading>("weather-cache.json");
            if (Reading == null && cached?.Latitude == lat && cached.Longitude == lon && !cached.IsExpired(DateTimeOffset.UtcNow)) Reading = cached;
            Status = "Updating weather…"; Changed?.Invoke();
            var reading = await provider.GetCurrentAsync(lat, lon, cts.Token);
            cts.Token.ThrowIfCancellationRequested();
            Reading = reading; Status = "";
            try { store.Save("weather-cache.json", reading); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Status = "Weather cache could not be saved"; }
        }
        catch (OperationCanceledException) { if (pending == cts) Status = "Weather unavailable — request timed out"; }
        catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        { if (pending == cts) Status = settings.LocationMode == "Automatic" && automatic == null ? "Location unavailable — choose a city in Settings" : "Weather unavailable — retrying later"; }
        finally
        {
            if (pending == cts)
            {
                nextRefresh = DateTimeOffset.UtcNow.AddMinutes(30);
                pending = null; Changed?.Invoke();
            }
            cts.Dispose();
        }
    }
    private void TryDeleteCache() { try { store.Delete("weather-cache.json"); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Status = "Weather off; cache could not be removed"; } }
    private void CancelActive() { var old = pending; pending = null; old?.Cancel(); }
    public void Dispose() => CancelActive();
}
