namespace ClockWidget.Core;

public sealed record ThemePreset(string Name, Settings Appearance)
{
    public static ThemePreset Capture(string name, Settings settings) => new(name.Trim(), Copy(new Settings(), settings.Normalize()));
    public Settings Apply(Settings target) => Copy(target, Appearance.Normalize()).Normalize();
    private static Settings Copy(Settings target, Settings source) => target with
    {
        Font = source.Font,
        Weight = source.Weight,
        Size = source.Size,
        DateSize = source.DateSize,
        DateWeight = source.DateWeight,
        WeatherSize = source.WeatherSize,
        WeatherWeight = source.WeatherWeight,
        Color = source.Color,
        Opacity = source.Opacity,
        Shadow = source.Shadow,
        Spacing = source.Spacing,
        Stacked = source.Stacked,
        TimeFormat = source.TimeFormat,
        Seconds = source.Seconds,
        DatePosition = source.DatePosition,
        WeatherPosition = source.WeatherPosition,
        ShowTime = source.ShowTime,
        ShowDate = source.ShowDate,
        ShowWeather = source.ShowWeather,
        ShowCity = source.ShowCity,
        ShowCustomText = source.ShowCustomText,
        ShowPeriod = source.ShowPeriod,
        PeriodSize = source.PeriodSize,
        Fahrenheit = source.Fahrenheit
    };
}
