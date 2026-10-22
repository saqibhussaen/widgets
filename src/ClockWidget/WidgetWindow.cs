using System.Globalization;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClockWidget.Core;
using Forms = System.Windows.Forms;

namespace ClockWidget;

public sealed class WidgetWindow : Window
{
    private readonly Program app;
    private readonly WeatherController weather;
    private readonly StackPanel rows = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly StackPanel clock = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly StackPanel clockBlock = new() { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly ClockGlyph first = new();
    private readonly ClockGlyph second = new();
    private readonly ClockGlyph third = new();
    private readonly ClockGlyph date = new() { ShapedText = true, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly ClockGlyph period = new() { ShapedText = true, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(12, 0, 0, 6) };
    private readonly ClockGlyph weatherText = new() { ShapedText = true, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly ClockGlyph city = new() { ShapedText = true, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly WeatherIcon icon = new() { Width = 24, Height = 24, Margin = new Thickness(0, 0, 8, 0) };
    private readonly StackPanel weatherRow = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock attribution = Label(10);
    private readonly DispatcherTimer tick = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly DispatcherTimer recovery = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private DesktopHost? host;
    private Point? dragCursor;
    private Point dragOrigin;
    private readonly DateTime previewTime = new(2026, 10, 22, 10, 8, 32);
    private bool ready;
    private bool allowClose;
    public string TargetDisplayId { get; private set; }
    public Point ScreenPosition => host?.PositionPixels() ?? new Point();
    public string TimeText => first.Text;
    public string DesktopStatus => host?.Status ?? "Starting";
    private static TextBlock Label(double size) => new() { FontFamily = new FontFamily("Segoe UI"), FontSize = size, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center };

    public WidgetWindow(Program app, WeatherController weather, string? displayId = null)
    {
        this.app = app; this.weather = weather;
        TargetDisplayId = DisplayService.Resolve(displayId ?? app.Settings.DisplayId).DeviceName;
        Title = "Widgets"; WindowStyle = WindowStyle.None; AllowsTransparency = true;
        Background = Brushes.Transparent; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
        ShowActivated = false; SizeToContent = SizeToContent.WidthAndHeight;
        Content = new Border { Padding = new Thickness(26), Background = Brushes.Transparent, Child = rows };
        clockBlock.Children.Add(clock); clockBlock.Children.Add(period);
        var info = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        info.Children.Add(icon); info.Children.Add(weatherText);
        weatherRow.Children.Add(city); weatherRow.Children.Add(info);
        attribution.Text = "Weather by Open-Meteo";
        attribution.Opacity = .7; attribution.Cursor = Cursors.Hand;
        attribution.Margin = new Thickness(0, 5, 0, 0);
        attribution.MouseLeftButtonUp += (_, e) => { Program.OpenLink("https://open-meteo.com/"); e.Handled = true; };
        weatherRow.Children.Add(attribution);
        SourceInitialized += (_, _) => { if (!app.Preview || app.DesktopProbe) { host ??= new DesktopHost(this, () => app.Settings.Locked); host.Initialize(); } };
        Loaded += (_, _) =>
        {
            if (ready) return;
            ApplySettings(); UpdateLayout();
            if (!app.Preview || app.DesktopProbe)
            {
                host ??= new DesktopHost(this, () => app.Settings.Locked);
                host.Initialize();
                host.Configure(app.Settings.AlwaysOnTop, app.Settings.ClickThrough);
                RestorePosition();
            }
            ready = true;
        };
        MouseLeftButtonDown += (_, e) =>
        {
            if (app.Settings.Locked || app.Settings.ClickThrough || host == null) { CancelDrag(); return; }
            dragCursor = DesktopHost.CursorPixels(); dragOrigin = host.PositionPixels(); CaptureMouse(); e.Handled = true;
        };
        MouseMove += (_, _) =>
        {
            if (app.Settings.Locked || app.Settings.ClickThrough) { CancelDrag(); return; }
            if (dragCursor == null || host == null || Mouse.LeftButton != MouseButtonState.Pressed) return;
            var cursor = DesktopHost.CursorPixels(); host.MovePixels(dragOrigin.X + cursor.X - dragCursor.Value.X, dragOrigin.Y + cursor.Y - dragCursor.Value.Y);
        };
        MouseLeftButtonUp += (_, _) =>
        {
            if (dragCursor == null) return;
            dragCursor = null; ReleaseMouseCapture(); host!.EnsureVisible(DisplayService.Resolve(TargetDisplayId));
            TargetDisplayId = Forms.Screen.FromPoint(new System.Drawing.Point((int)host.PositionPixels().X, (int)host.PositionPixels().Y)).DeviceName;
            app.SavePosition(TargetDisplayId, host.PositionPixels());
        };
        LostMouseCapture += (_, _) => dragCursor = null;
        MouseRightButtonUp += (_, _) => app.OpenSettings();
        tick.Tick += (_, _) => { RefreshNow(); if (!app.Preview) _ = weather.RefreshAsync(app.Settings); };
        recovery.Tick += (_, _) => host?.Recover();
        weather.Changed += OnWeatherChanged;
        tick.Start(); if (!app.Preview) recovery.Start();
        ApplySettings();
    }
    public void ApplySettings()
    {
        var s = app.Settings;
        if (s.Locked || s.ClickThrough) CancelDrag();
        var brush = (Brush)new BrushConverter().ConvertFromString(s.Color)!;
        Foreground = brush; Opacity = s.Opacity;
        attribution.Foreground = brush; period.Foreground = brush;
        date.Foreground = weatherText.Foreground = city.Foreground = brush;
        icon.Width = icon.Height = s.WeatherSize * 1.35;
        first.Foreground = second.Foreground = third.Foreground = icon.Foreground = brush;
        rows.Effect = s.Shadow ? new DropShadowEffect { Color = Colors.Black, BlurRadius = 10, ShadowDepth = 2, Opacity = .55 } : null;
        rows.Children.Clear(); clock.Children.Clear();
        clock.Orientation = s.Stacked ? Orientation.Vertical : Orientation.Horizontal;
        clock.Children.Add(first);
        if (s.Stacked) { clock.Children.Add(second); if (s.Seconds) clock.Children.Add(third); }
        if (s.ShowDate && s.DatePosition == "Above") rows.Children.Add(date);
        if (s.ShowWeather && s.WeatherPosition == "Above") rows.Children.Add(weatherRow);
        if (s.ShowTime) rows.Children.Add(clockBlock);
        if (s.ShowDate && s.DatePosition == "Below") rows.Children.Add(date);
        if (s.ShowWeather && s.WeatherPosition == "Below") rows.Children.Add(weatherRow);
        foreach (FrameworkElement child in rows.Children) child.Margin = new Thickness(0, s.Spacing / 2, 0, s.Spacing / 2);
        host?.Configure(s.AlwaysOnTop, s.ClickThrough);
        RefreshNow(); UpdateLayout();
        if (ready) host?.EnsureVisible(DisplayService.Resolve(TargetDisplayId));
    }
    public void RefreshNow()
    {
        var s = app.Settings; var time = app.Preview ? previewTime : DateTime.Now;
        date.Update(time.ToString("dddd, d MMMM", CultureInfo.CurrentCulture), s.Font, s.DateWeight, s.DateSize);
        string text = ClockText.Format(time, s);
        if (s.Stacked)
        {
            var parts = text.Split(':');
            first.Update(parts[0], s.Font, s.Weight, s.Size);
            second.Update(parts[1], s.Font, s.Weight, s.Size);
            if (s.Seconds) third.Update(parts[2], s.Font, s.Weight, s.Size * .55);
        }
        else first.Update(text, s.Font, s.Weight, s.Size);
        period.Update(ClockText.Period(time, s), s.Font, s.Weight, s.PeriodSize);
        period.Visibility = period.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        var reading = app.Preview ? new WeatherReading(24, 2, true, DateTimeOffset.UtcNow, 0, 0) : weather.Reading;
        if (reading != null && !reading.IsExpired(DateTimeOffset.UtcNow))
        {
            weatherText.Update($"{reading.Temperature(s.Fahrenheit):0}°{(s.Fahrenheit ? "F" : "C")}  ·  {reading.Condition}" + (reading.IsStale(DateTimeOffset.UtcNow) ? " · stale" : ""), s.Font, s.WeatherWeight, s.WeatherSize);
            weatherText.ToolTip = $"Updated {reading.FetchedAt.ToLocalTime():g}\n{weather.Status}";
            icon.Kind = reading.Icon; icon.Visibility = Visibility.Visible; attribution.Visibility = Visibility.Visible;
        }
        else
        {
            weatherText.Update(s.LocationMode == "Off" ? "Set up weather" : "Weather unavailable", s.Font, s.WeatherWeight, s.WeatherSize);
            weatherText.ToolTip = "Right-click or use the tray menu to set up weather.";
            icon.Visibility = Visibility.Collapsed; attribution.Visibility = Visibility.Collapsed;
        }
        icon.InvalidateVisual();
        string name = app.Preview ? "Your text here" : s.CustomText;
        city.Update(name, s.Font, s.WeatherWeight, s.WeatherSize);
        city.Visibility = !s.ShowCustomText || name.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        weatherRow.Visibility = s.ShowWeather ? Visibility.Visible : Visibility.Collapsed;
    }
    private void OnWeatherChanged() { if (Dispatcher.CheckAccess()) RefreshNow(); else Dispatcher.BeginInvoke(RefreshNow); }
    public void ResetPosition(bool center)
    {
        if (host == null) return;
        host.PlaceOn(DisplayService.Resolve(TargetDisplayId), center); app.SavePosition(TargetDisplayId, host.PositionPixels());
    }
    public void RecoverDisplay() { UpdateLayout(); host?.Recover(); host?.EnsureVisible(DisplayService.Resolve(TargetDisplayId)); if (host != null) app.SavePosition(TargetDisplayId, host.PositionPixels()); }
    private void CancelDrag() { dragCursor = null; if (IsMouseCaptured) ReleaseMouseCapture(); }
    private void RestorePosition()
    {
        if (host == null) return;
        var screen = DisplayService.Resolve(TargetDisplayId);
        if (app.Settings.DisplayPositions.TryGetValue(TargetDisplayId, out var saved)) host.MovePixels(saved.X, saved.Y);
        else if (app.Settings.X.HasValue && app.Settings.Y.HasValue && screen.WorkingArea.Contains((int)app.Settings.X.Value, (int)app.Settings.Y.Value)) host.MovePixels(app.Settings.X.Value, app.Settings.Y.Value);
        else host.PlaceOn(screen, false);
        host.EnsureVisible(screen);
    }
    public void MoveToDisplay(string id) { if (TargetDisplayId == id) return; TargetDisplayId = id; RestorePosition(); }
    public void CloseForRemoval() { allowClose = true; Stop(); Close(); }
    protected override void OnClosing(CancelEventArgs e) { if (!allowClose && !app.IsExiting && !app.Preview) e.Cancel = true; base.OnClosing(e); }
    public void Reveal() { if (!IsVisible) Show(); host?.Reveal(); }
    public void SavePreview(string path)
        => SaveVisual(this, path);
    public static void SaveVisual(FrameworkElement visual, string path)
    {
        visual.UpdateLayout();
        var target = new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth * 2), (int)Math.Ceiling(visual.ActualHeight * 2), 192, 192, PixelFormats.Pbgra32);
        target.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(target));
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var stream = File.Create(path); encoder.Save(stream);
    }
    public void ProbeDesktop(string path)
    {
        if (host == null) throw new InvalidOperationException("Desktop probe requires a native host.");
        host.Configure(false, false); host.Recover();
        bool attached = host.Attached;
        host.Configure(true, true);
        bool top = host.IsTopMost, through = host.IsClickThrough;
        host.Configure(false, false); host.Recover();
        bool restored = host.Attached;
        host.Reveal();
        bool uncovered = false;
        var bounds = first.Outline.Bounds;
        for (double x = bounds.Left + 1; x < bounds.Right && !uncovered; x += 2)
            for (double y = bounds.Top + 1; y < bounds.Bottom && !uncovered; y += 2)
                if (first.Outline.FillContains(new Point(x, y)))
                    uncovered = host.IsUncoveredAt(first.PointToScreen(new Point(x + 4, y + 4 - bounds.Top)));
        var report = new { DesktopAttached = attached, TopMostEnabled = top, ClickThroughEnabled = through, DesktopRestored = restored, ClickThroughCleared = !host.IsClickThrough, TopLevel = host.IsTopLevel, WindowVisible = host.IsVisible, GlyphUncoveredAfterReveal = uncovered, Status = host.Status, Diagnostic = host.Diagnostic };
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
    public void Stop() { tick.Stop(); recovery.Stop(); weather.Changed -= OnWeatherChanged; host?.Dispose(); }
}

public sealed class WeatherIcon : FrameworkElement
{
    public string Kind { get; set; } = "Sun";
    public Brush Foreground { get; set; } = Brushes.White;
    protected override void OnRender(DrawingContext dc)
    {
        var pen = new Pen(Foreground, 1.5) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (Kind == "Sun")
        {
            dc.DrawEllipse(null, pen, new Point(12, 12), 4, 4);
            for (int i = 0; i < 8; i++) { double a = i * Math.PI / 4; dc.DrawLine(pen, new Point(12 + Math.Cos(a) * 7, 12 + Math.Sin(a) * 7), new Point(12 + Math.Cos(a) * 10, 12 + Math.Sin(a) * 10)); }
        }
        else
        {
            string data = Kind switch
            {
                "Moon" => "M 17,3 A 9,9 0 1 0 21,17 A 8,8 0 0 1 17,3",
                "Fog" => "M 3,7 L 21,7 M 1,12 L 19,12 M 4,17 L 22,17",
                "Snow" => "M 12,2 L 12,22 M 3,7 L 21,17 M 3,17 L 21,7 M 8,4 L 12,7 L 16,4 M 8,20 L 12,17 L 16,20",
                _ => "M 5,16 C 0,16 0,9 5,9 C 5,1 17,1 18,9 C 24,9 24,16 19,16 Z"
            };
            dc.DrawGeometry(null, pen, Geometry.Parse(data));
            if (Kind == "Rain") { dc.DrawLine(pen, new Point(8, 19), new Point(6, 22)); dc.DrawLine(pen, new Point(16, 19), new Point(14, 22)); }
            if (Kind == "Storm") dc.DrawGeometry(null, pen, Geometry.Parse("M 13,14 L 9,19 L 14,19 L 11,24"));
        }
    }
}
