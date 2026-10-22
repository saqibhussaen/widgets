using System.Globalization;
using System.Text.Json;

namespace ClockWidget.Core;

public sealed record Settings
{
    public int Version { get; init; } = 4;
    public bool ShowTime { get; init; } = true;
    public bool ShowDate { get; init; } = true;
    public bool ShowWeather { get; init; } = true;
    public bool ShowCustomText { get; init; } = true;
    public string CustomText { get; init; } = "";
    public int DefaultProfileVersion { get; init; }
    public bool MediaEnabled { get; init; }
    public string MediaDisplayId { get; init; } = "";
    // Legacy value retained for migration; the UI now uses ShowCustomText.
    public bool ShowCity { get; init; } = true;
    public bool ShowPeriod { get; init; } = true;
    public double PeriodSize { get; init; } = 32;
    public string Font { get; init; } = "Nunito";
    public double Weight { get; init; } = 864;
    public double Size { get; init; } = 163;
    public double DateSize { get; init; } = 29;
    public double DateWeight { get; init; } = 745;
    public double WeatherSize { get; init; } = 29;
    public double WeatherWeight { get; init; } = 745;
    public string Color { get; init; } = "#FFFFFF";
    public double Opacity { get; init; } = 1;
    public bool Shadow { get; init; } = true;
    public double Spacing { get; init; } = 8;
    public bool Stacked { get; init; }
    public string TimeFormat { get; init; } = "System";
    public bool Seconds { get; init; } = true;
    public string DatePosition { get; init; } = "Above";
    public string WeatherPosition { get; init; } = "Below";
    public bool Locked { get; init; }
    public bool ClickThrough { get; init; }
    public bool AlwaysOnTop { get; init; }
    public bool LaunchAtSignIn { get; init; }
    public bool ClockVisible { get; init; } = true;
    public string DisplayId { get; init; } = "";
    public bool AllDisplays { get; init; }
    public Dictionary<string, WidgetPosition> DisplayPositions { get; init; } = new();
    public double? X { get; init; }
    public double? Y { get; init; }
    public string LocationMode { get; init; } = "Off";
    public bool WeatherConsent { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public string City { get; init; } = "";
    public bool Fahrenheit { get; init; }

    public Settings Normalize() => this with
    {
        Version = 4,
        ShowCustomText = Version < 4 ? ShowCity : ShowCustomText,
        CustomText = CleanText(Version < 4 ? City : CustomText),
        MediaDisplayId = MediaDisplayId ?? "",
        ShowDate = ShowDate && (Version >= 3 || DatePosition != "Hidden"),
        ShowWeather = ShowWeather && (Version >= 3 || WeatherPosition != "Hidden"),
        PeriodSize = FiniteClamp(PeriodSize, 10, 100, 32),
        Font = Font == "Inter" ? "Inter" : "Nunito",
        Weight = FiniteClamp(Weight, 200, 900, 864),
        Size = FiniteClamp(Size, 48, 240, 163),
        DateSize = FiniteClamp(DateSize, 10, 80, 29), DateWeight = FiniteClamp(DateWeight, 200, 900, 745),
        WeatherSize = FiniteClamp(WeatherSize, 10, 80, 29), WeatherWeight = FiniteClamp(WeatherWeight, 200, 900, 745),
        Opacity = FiniteClamp(Opacity, .15, 1, 1),
        Spacing = FiniteClamp(Spacing, 0, 40, 8),
        Color = System.Text.RegularExpressions.Regex.IsMatch(Color ?? "", "^#[0-9a-fA-F]{6}$") ? Color! : "#FFFFFF",
        TimeFormat = TimeFormat is "12" or "24" ? TimeFormat : "System",
        DatePosition = Row(DatePosition) == "Hidden" ? "Above" : Row(DatePosition), WeatherPosition = Row(WeatherPosition) == "Hidden" ? "Below" : Row(WeatherPosition),
        LocationMode = LocationMode is "Automatic" or "Manual" ? LocationMode : "Off",
        Latitude = Latitude is >= -90 and <= 90 ? Latitude : null,
        Longitude = Longitude is >= -180 and <= 180 ? Longitude : null,
        X = X.HasValue && double.IsFinite(X.Value) ? X : null,
        Y = Y.HasValue && double.IsFinite(Y.Value) ? Y : null,
        City = string.IsNullOrWhiteSpace(City) ? "" : City.Trim()[..Math.Min(City.Trim().Length, 120)], DisplayId = DisplayId ?? "",
        DisplayPositions = (DisplayPositions ?? new()).Where(p => p.Value != null && double.IsFinite(p.Value.X) && double.IsFinite(p.Value.Y)).ToDictionary(p => p.Key, p => p.Value)
    };
    private static string CleanText(string? text) => string.IsNullOrWhiteSpace(text) ? "" : new string(text.Where(c => !char.IsControl(c)).Take(120).ToArray()).Trim();
    private static string Row(string s) => s is "Above" or "Below" ? s : "Hidden";
    private static double FiniteClamp(double n, double min, double max, double fallback) => double.IsFinite(n) ? Math.Clamp(n, min, max) : fallback;
}
public sealed record WidgetPosition(double X, double Y);

public sealed class JsonStore(string directory)
{
    private readonly object gate = new();
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string DirectoryPath => directory;
    public T? Load<T>(string name)
    {
        lock (gate)
        {
            foreach (string suffix in new[] { "", ".bak" })
                try { var value = JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(directory, name + suffix)), Options); if (value != null) return value; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException) { }
            return default;
        }
    }
    public void Save<T>(string name, T value)
    {
        lock (gate)
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, name);
            var temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) { JsonSerializer.Serialize(stream, value, Options); stream.Flush(true); }
            if (File.Exists(path))
            {
                try { using var valid = JsonDocument.Parse(File.ReadAllText(path)); File.Copy(path, path + ".bak", true); }
                catch (JsonException) { /* Preserve the previous valid backup when primary is corrupt. */ }
            }
            File.Move(temp, path, true);
        }
    }
    public void Delete(string name)
    {
        lock (gate) foreach (string suffix in new[] { "", ".bak", ".tmp" }) { var path = Path.Combine(directory, name + suffix); if (File.Exists(path)) File.Delete(path); }
    }
}

public static class ClockText
{
    public static string Format(DateTime time, Settings settings, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        bool twelve = settings.TimeFormat == "12" || (settings.TimeFormat == "System" && !culture.DateTimeFormat.ShortTimePattern.Contains('H'));
        var format = twelve ? "hh:mm" : "HH:mm";
        if (settings.Seconds) format += ":ss";
        // Fixed Latin digits are deliberate; date and AM/PM use the user's culture.
        return time.ToString(format, CultureInfo.InvariantCulture);
    }
    public static string Period(DateTime time, Settings settings, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        bool twelve = settings.TimeFormat == "12" || (settings.TimeFormat == "System" && !culture.DateTimeFormat.ShortTimePattern.Contains('H'));
        return settings.ShowPeriod && twelve ? time.ToString("tt", culture) : "";
    }
}
