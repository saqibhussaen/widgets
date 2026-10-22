using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace ClockWidget.Core;

public sealed record Place(string Name, string Country, double Latitude, double Longitude)
{
    public override string ToString() => $"{Name}, {Country}";
}
public sealed record WeatherReading(double Celsius, int Code, bool IsDay, DateTimeOffset FetchedAt, double Latitude, double Longitude)
{
    public bool IsStale(DateTimeOffset now) => now - FetchedAt >= TimeSpan.FromHours(1);
    public bool IsExpired(DateTimeOffset now) => now - FetchedAt >= TimeSpan.FromHours(24);
    public double Temperature(bool fahrenheit) => fahrenheit ? Celsius * 9 / 5 + 32 : Celsius;
    public string Condition => Code switch
    {
        0 => "Clear", 1 => "Mostly clear", 2 => "Partly cloudy", 3 => "Overcast",
        45 or 48 => "Fog", >= 51 and <= 57 => "Drizzle", >= 61 and <= 67 => "Rain",
        >= 71 and <= 77 => "Snow", >= 80 and <= 82 => "Showers", 85 or 86 => "Snow showers",
        >= 95 and <= 99 => "Thunderstorm", _ => "Unknown conditions"
    };
    public string Icon => Code switch
    {
        0 or 1 => IsDay ? "Sun" : "Moon", 2 or 3 => "Cloud", 45 or 48 => "Fog",
        >= 71 and <= 77 or 85 or 86 => "Snow", >= 95 => "Storm", _ => "Rain"
    };
}
public interface IWeatherProvider
{
    Task<WeatherReading> GetCurrentAsync(double latitude, double longitude, CancellationToken token);
    Task<IReadOnlyList<Place>> SearchAsync(string city, CancellationToken token);
}
public sealed class OpenMeteoProvider(HttpClient http) : IWeatherProvider
{
    public async Task<WeatherReading> GetCurrentAsync(double latitude, double longitude, CancellationToken token)
    {
        latitude = Math.Round(latitude, 2); longitude = Math.Round(longitude, 2);
        var query = FormattableString.Invariant($"https://api.open-meteo.com/v1/forecast?latitude={latitude:F2}&longitude={longitude:F2}&current=temperature_2m,weather_code,is_day&temperature_unit=celsius");
        using var response = await http.GetAsync(query, token);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        return Parse(json.RootElement, DateTimeOffset.UtcNow, latitude, longitude);
    }
    public static WeatherReading Parse(JsonElement root, DateTimeOffset now, double lat, double lon)
    {
        var c = root.GetProperty("current");
        var temp = c.GetProperty("temperature_2m").GetDouble();
        int code = c.GetProperty("weather_code").GetInt32();
        int day = c.GetProperty("is_day").GetInt32();
        if (!double.IsFinite(temp) || temp is < -120 or > 80 || day is < 0 or > 1 || code is < 0 or > 99)
            throw new JsonException("Invalid weather reading.");
        return new(temp, code, day == 1, now, lat, lon);
    }
    public async Task<IReadOnlyList<Place>> SearchAsync(string city, CancellationToken token)
    {
        using var response = await http.GetAsync($"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(city.Trim())}&count=8&language=en&format=json", token);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        if (!json.RootElement.TryGetProperty("results", out var results)) return [];
        return results.EnumerateArray().Select(r => new Place(
            r.GetProperty("name").GetString() ?? city,
            r.TryGetProperty("country", out var country) ? country.GetString() ?? "" : "",
            r.GetProperty("latitude").GetDouble(), r.GetProperty("longitude").GetDouble())).ToArray();
    }
}
