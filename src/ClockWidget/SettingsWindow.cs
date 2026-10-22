using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Rectangle = System.Windows.Shapes.Rectangle;
using System.Windows.Shell;
using System.Windows.Input;
using ClockWidget.Core;

namespace ClockWidget;

public sealed class SettingsWindow : Window
{
    private readonly Program app;
    private readonly WidgetWindow widget;
    private readonly WeatherController weather;
    private readonly CancellationTokenSource lifetime = new();
    private readonly TabControl tabs = new();
    private readonly StackPanel appearance = Panel();
    private TextBlock weatherStatus = Text("");
    private TextBlock locationSummary = Text("");
    private bool rebuilding;
    private bool synchronizing;
    private readonly List<(CheckBox Control, Func<Settings, bool> Value)> checks = new();
    private readonly Button visibilityButton = new();
    private readonly TextBlock saveStatus = Text("All changes saved on this device.", 11);
    private TextBox? customText;
    private TextBlock? mediaStatus;
    private Rectangle? previewImage;
    private static readonly Brush Ink = new SolidColorBrush(Color.FromRgb(241, 238, 249));
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(160, 158, 179));
    private static StackPanel Panel() => new() { Margin = new Thickness(16, 0, 24, 24) };
    private static TextBlock Text(string text, double size = 13) => new() { Text = text, FontSize = size, Foreground = size >= 17 ? Ink : Muted, FontWeight = size >= 17 ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 10), LineHeight = size * 1.45 };

    public SettingsWindow(Program app, WidgetWindow widget, WeatherController weather)
    {
        this.app = app; this.widget = widget; this.weather = weather;
        Resources = SettingsTheme.Create();
        Title = "Widgets · Settings"; Width = Math.Min(1080, SystemParameters.WorkArea.Width - 40); Height = Math.Min(760, SystemParameters.WorkArea.Height - 40); MinWidth = 850; MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = new SolidColorBrush(Color.FromRgb(20, 21, 29));
        Foreground = Ink; FontFamily = new FontFamily("Segoe UI Variable, Segoe UI"); FontSize = 13;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResize;
        WindowChrome.SetWindowChrome(this, new WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(6), GlassFrameThickness = new Thickness(0), CornerRadius = new CornerRadius(14), UseAeroCaptionButtons = false });
        var root = new DockPanel { Margin = new Thickness(22, 14, 22, 14) };
        var heading = new Grid { Margin = new Thickness(4, 0, 0, 22) };
        heading.ColumnDefinitions.Add(new ColumnDefinition()); heading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var brand = new StackPanel(); brand.Children.Add(Text("Widgets", 22)); brand.Children.Add(Text("Your desktop, your way.", 12));
        brand.MouseLeftButtonDown += (_, e) => { if (e.ClickCount == 2) WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; else DragMove(); };
        heading.Children.Add(brand);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
        visibilityButton.Content = app.Settings.ClockVisible ? "Hide clock" : "Show clock";
        visibilityButton.Margin = new Thickness(0, 4, 18, 0);
        visibilityButton.Click += (_, _) => app.ToggleClockVisibility(); actions.Children.Add(visibilityButton);
        var minimize = new Button { Content = "−", Width = 32, Height = 32, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Margin = new Thickness(0, 4, 4, 0), ToolTip = "Minimize settings" };
        minimize.Click += (_, _) => WindowState = WindowState.Minimized; actions.Children.Add(minimize);
        var close = new Button { Content = "×", Width = 32, Height = 32, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Margin = new Thickness(0, 4, 0, 0), ToolTip = "Close settings; keep clock running" };
        close.Click += (_, _) => Close(); actions.Children.Add(close);
        Grid.SetColumn(actions, 1); heading.Children.Add(actions);
        DockPanel.SetDock(heading, Dock.Top); root.Children.Add(heading);
        saveStatus.Margin = new Thickness(4, 14, 0, 0); DockPanel.SetDock(saveStatus, Dock.Bottom); root.Children.Add(saveStatus);
        tabs.BorderThickness = new Thickness(0); tabs.TabStripPlacement = Dock.Left;
        RebuildSections();
        var body = new Grid(); body.ColumnDefinitions.Add(new ColumnDefinition()); body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });
        body.Children.Add(tabs);
        var preview = BuildPreview(); Grid.SetColumn(preview, 1); body.Children.Add(preview);
        root.Children.Add(body); Content = root;
        weather.Changed += UpdateWeatherStatus;
        app.SettingsChanged += SynchronizeControls;
        Closed += (_, _) => { lifetime.Cancel(); lifetime.Dispose(); weather.Changed -= UpdateWeatherStatus; app.SettingsChanged -= SynchronizeControls; };
        UpdateWeatherStatus();
        SynchronizeControls();
    }
    private FrameworkElement BuildPreview()
    {
        var sidebar = new DockPanel { Margin = new Thickness(12, 8, 0, 0) };
        var topCenter = new Button
        {
            Content = "Top center", Padding = new Thickness(12, 8, 12, 8),
            Margin = new Thickness(0, 14, 0, 0),
            ToolTip = "Realign the widget to the top center of its display"
        };
        topCenter.Click += (_, _) => app.ResetPosition();
        DockPanel.SetDock(topCenter, Dock.Bottom); sidebar.Children.Add(topCenter);
        var panel = new StackPanel { Margin = new Thickness(12, 8, 0, 0) };
        panel.Children.Add(Text("YOUR CLOCK", 11));
        var image = new Rectangle { Height = 190, Margin = new Thickness(12), Fill = new VisualBrush((Visual)widget.Content) { Stretch = Stretch.Uniform }, Opacity = app.Settings.Opacity };
        previewImage = image;
        var background = new LinearGradientBrush(); background.StartPoint = new Point(0, 0); background.EndPoint = new Point(1, 1);
        background.GradientStops.Add(new GradientStop(Color.FromRgb(32, 36, 51), 0)); background.GradientStops.Add(new GradientStop(Color.FromRgb(70, 55, 84), .6)); background.GradientStops.Add(new GradientStop(Color.FromRgb(44, 64, 76), 1));
        var card = new Border { CornerRadius = new CornerRadius(16), Background = background, Child = image, ClipToBounds = true, Margin = new Thickness(0, 6, 0, 14) }; panel.Children.Add(card);
        panel.Children.Add(Text("A live preview of your desktop clock.", 12));
        panel.Children.Add(Text("Your widget keeps running when you close settings.", 11));
        sidebar.Children.Add(new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = panel });
        return sidebar;
    }
    private void AddTab(string title, StackPanel body) => tabs.Items.Add(new TabItem { Header = title, Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = body } });
    private void RebuildSections()
    {
        rebuilding = true;
        checks.Clear();
        foreach (var tab in tabs.Items.OfType<TabItem>()) if (tab.Content is ScrollViewer viewer) viewer.Content = null;
        tabs.Items.Clear(); appearance.Children.Clear();
        weatherStatus = Text(""); locationSummary = Text(""); customText = null;
        AddTab("Clock", appearance); BuildClock(); BuildDate(); BuildWeather(); BuildMedia(); BuildDesktop(); BuildAbout();
        rebuilding = false; SynchronizeControls(); UpdateWeatherStatus();
    }
    private void BuildClock()
    {
        var themePanel = Panel();
        appearance.Children.Add(new Expander { Header = Text("Themes · Default and your presets", 14), Foreground = Ink, Content = themePanel, Margin = new Thickness(0, 8, 0, 16) });
        themePanel.Children.Add(Text("Themes", 19));
        themePanel.Children.Add(Text("Default preserves your appearance from this upgrade. Presets contain appearance only; your city, permissions and desktop position stay yours."));
        var themes = new ComboBox { ItemsSource = app.Themes.Select(t => t.Name).ToArray(), SelectedIndex = 0, Margin = new Thickness(0, 4, 0, 8), Padding = new Thickness(8), Foreground = Brushes.Black };
        themePanel.Children.Add(themes);
        Button(themePanel, "Apply selected theme", () =>
        {
            if (themes.SelectedItem is string name && app.Themes.FirstOrDefault(t => t.Name == name) is ThemePreset theme)
            { Change(theme.Apply(app.Settings)); RebuildSections(); }
        });
        var nameBox = new TextBox { MaxLength = 60, Padding = new Thickness(9), Margin = new Thickness(0, 8, 0, 6), ToolTip = "Name for your appearance preset" };
        themePanel.Children.Add(Text("Preset name")); themePanel.Children.Add(nameBox);
        var status = Text("");
        Button(themePanel, "Save current appearance as preset", () =>
        {
            string name = nameBox.Text.Trim();
            if (!app.SaveTheme(name)) { status.Text = "Use a name of 1–60 characters other than Default. " + app.StorageStatus; return; }
            themes.ItemsSource = app.Themes.Select(t => t.Name).ToArray(); themes.SelectedItem = name;
            status.Text = "Preset saved. Using the same name updates that preset.";
        });
        Button(themePanel, "Make current appearance Default", () =>
        {
            status.Text = app.MakeCurrentDefault() ? "Current appearance is now Default." : app.StorageStatus;
        });
        themePanel.Children.Add(status);
        appearance.Children.Add(Text("Clock", 19));
        Check(appearance, "Show time", s => s.ShowTime, value => Change(app.Settings with { ShowTime = value }));
        Choice(appearance, "Font", ["Nunito", "Inter"], app.Settings.Font, value => Change(app.Settings with { Font = value }));
        Slider(appearance, "Weight · thin to bold", 200, 900, app.Settings.Weight, 1, value => Change(app.Settings with { Weight = value }));
        Slider(appearance, "Numeral size", 48, 240, app.Settings.Size, 1, value => Change(app.Settings with { Size = value }));
        Choice(appearance, "Clock layout", ["Horizontal", "Stacked"], app.Settings.Stacked ? "Stacked" : "Horizontal", value => Change(app.Settings with { Stacked = value == "Stacked" }));
        Choice(appearance, "Time format", ["System", "12", "24"], app.Settings.TimeFormat, value => Change(app.Settings with { TimeFormat = value }));
        Check(appearance, "Show seconds", s => s.Seconds, value => Change(app.Settings with { Seconds = value }));
        Check(appearance, "Show AM/PM", s => s.ShowPeriod, value => Change(app.Settings with { ShowPeriod = value }));
        Slider(appearance, "AM/PM size", 10, 100, app.Settings.PeriodSize, 1, value => Change(app.Settings with { PeriodSize = value }));
        appearance.Children.Add(Text("AM/PM sits beside the time and appears only in 12-hour mode."));
    }
    private void BuildDate()
    {
        var panel = Panel(); panel.Children.Add(Text("Date", 19));
        Check(panel, "Show date", s => s.ShowDate, value => Change(app.Settings with { ShowDate = value }));
        Slider(panel, "Date size", 10, 80, app.Settings.DateSize, 1, value => Change(app.Settings with { DateSize = value }));
        Slider(panel, "Date weight · thin to bold", 200, 900, app.Settings.DateWeight, 1, value => Change(app.Settings with { DateWeight = value }));
        Choice(panel, "Date position", ["Above", "Below"], app.Settings.DatePosition, value => Change(app.Settings with { DatePosition = value }));
        panel.Children.Add(Text("Turn off time and weather to use the date on its own."));
        AddTab("Date", panel);
    }
    private void BuildFinishing(Panel panel)
    {
        panel.Children.Add(Text("Shared appearance", 19));
        var colorLine = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 10) };
        colorLine.Children.Add(Text("Text color  "));
        var color = new TextBox { Text = app.Settings.Color, Width = 100, Padding = new Thickness(7) };
        color.TextChanged += (_, _) =>
        {
            bool valid = System.Text.RegularExpressions.Regex.IsMatch(color.Text, "^#[0-9a-fA-F]{6}$");
            color.ToolTip = valid ? "RGB hexadecimal color" : "Enter # followed by six hexadecimal digits";
            color.BorderBrush = valid ? Brushes.Gray : Brushes.IndianRed;
            if (valid) Change(app.Settings with { Color = color.Text });
        };
        colorLine.Children.Add(color); panel.Children.Add(colorLine);
        var palette = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        foreach (string hex in new[] { "#FFFFFF", "#D4C4FF", "#FFD3BA", "#B9E8D4", "#181A24" })
        {
            var swatch = new Button { Width = 30, Height = 30, Padding = new Thickness(0), Background = (Brush)new BrushConverter().ConvertFromString(hex)!, Margin = new Thickness(0, 0, 8, 0), ToolTip = hex };
            swatch.Click += (_, _) => color.Text = hex; palette.Children.Add(swatch);
        }
        panel.Children.Add(palette);
        Slider(panel, "Opacity", .15, 1, app.Settings.Opacity, .01, value => Change(app.Settings with { Opacity = value }));
        Check(panel, "Soft text shadow", s => s.Shadow, value => Change(app.Settings with { Shadow = value }));
        Slider(panel, "Space between rows", 0, 40, app.Settings.Spacing, 1, value => Change(app.Settings with { Spacing = value }));
        Button(panel, "Reset appearance to Default theme", () => { Change(app.Themes.First(t => t.Name == "Default").Apply(app.Settings)); RebuildSections(); tabs.SelectedIndex = 4; });
    }
    private void BuildDesktop()
    {
        var panel = Panel(); BuildFinishing(panel); panel.Children.Add(Text("At home on your desktop", 19));
        panel.Children.Add(Text("Drag the clock to move it. Right-click it to open settings. The tray menu stays available when the clock is locked or click-through is on."));
        var displays = DisplayService.Available();
        string selected = DisplayService.Resolve(app.Settings.DisplayId).DeviceName;
        Choice(panel, "Clock display", displays.Select(d => d.Label).ToArray(), displays.First(d => d.Id == selected).Label, value => app.SelectDisplay(displays.First(d => d.Label == value).Id));
        Check(panel, "Show clock on all connected displays", s => s.AllDisplays, value => Change(app.Settings with { AllDisplays = value }));
        panel.Children.Add(Text("Each display remembers its own clock position. Display selection and position presets remain available when dragging is locked."));
        Check(panel, "Lock position", s => s.Locked, value => Change(app.Settings with { Locked = value }));
        Check(panel, "Click-through (use tray to unlock)", s => s.ClickThrough, value => Change(app.Settings with { ClickThrough = value }));
        Check(panel, "Always on top of other apps", s => s.AlwaysOnTop, value => Change(app.Settings with { AlwaysOnTop = value }));
        Check(panel, "Launch at sign-in", s => s.LaunchAtSignIn, value => Change(app.Settings with { LaunchAtSignIn = value }));
        Button(panel, "Top center / reset position", () => app.ResetPosition());
        Button(panel, "Center on current display", () => app.ResetPosition(true));
        Button(panel, "Move / unlock", app.Unlock);
        var status = Text(widget.DesktopStatus); panel.Children.Add(status);
        Button(panel, "Check desktop attachment", () => { widget.RecoverDisplay(); status.Text = widget.DesktopStatus; });
        panel.Children.Add(Text("The clock stays above your wallpaper and behind other apps. Use Show clock whenever you need to find or move it."));
        AddTab("Settings", panel);
    }
    private void BuildWeather()
    {
        var panel = Panel(); panel.Children.Add(Text("Weather", 19));
        Check(panel, "Show weather", s => s.ShowWeather, value => Change(app.Settings with { ShowWeather = value }));
        Check(panel, "Show Custom Text", s => s.ShowCustomText, value => Change(app.Settings with { ShowCustomText = value }));
        panel.Children.Add(Text("Custom Text", 17));
        customText = new TextBox { Text = app.Settings.CustomText, MaxLength = 120, Padding = new Thickness(9), Margin = new Thickness(0, 4, 0, 8), ToolTip = "Your own text above the weather; independent of the forecast location" };
        customText.TextChanged += (_, _) => Change(app.Settings with { CustomText = customText.Text });
        panel.Children.Add(customText);
        panel.Children.Add(Text("Shown above the temperature when Show Custom Text is enabled. Enter any text you like; your selected weather city stays unchanged."));
        Slider(panel, "Weather & text size", 10, 80, app.Settings.WeatherSize, 1, value => Change(app.Settings with { WeatherSize = value }));
        Slider(panel, "Weather & text weight · thin to bold", 200, 900, app.Settings.WeatherWeight, 1, value => Change(app.Settings with { WeatherWeight = value }));
        Choice(panel, "Weather position", ["Above", "Below"], app.Settings.WeatherPosition, value => Change(app.Settings with { WeatherPosition = value }));
        panel.Children.Add(Text("Hiding weather pauses automatic requests and keeps your city for next time. City search below lets you find a location by name."));
        panel.Children.Add(Text("Location & city search", 19));
        panel.Children.Add(Text("Automatic weather requests Windows location access. Rounded coordinates (two decimal places) are sent directly to Open-Meteo; the service also receives your IP address. Manual city search sends your search text to Open-Meteo's geocoding service. No location history or analytics is collected by this app."));
        Link(panel, "Read Open-Meteo service terms and privacy", "https://open-meteo.com/en/terms");
        panel.Children.Add(locationSummary);
        Button(panel, "Enable automatic location & weather", () =>
        {
            app.UpdateSettings(app.Settings with { ShowWeather = true, WeatherConsent = true, LocationMode = "Automatic", Latitude = null, Longitude = null, City = "" });
            _ = weather.RefreshAsync(app.Settings, true, true, requestPermission: true); UpdateWeatherStatus();
        });
        panel.Children.Add(Text("Or choose a city", 17));
        Link(panel, "City data: Open-Meteo / GeoNames", "https://www.geonames.org/");
        var city = new TextBox { Padding = new Thickness(9), Margin = new Thickness(0, 4, 0, 8), ToolTip = "City name (at least two characters)" };
        panel.Children.Add(city);
        var results = new ListBox { Height = 140, Margin = new Thickness(0, 8, 0, 8), Visibility = Visibility.Collapsed };
        var searchStatus = Text("");
        var search = new Button { Content = "Search cities", Padding = new Thickness(12, 8, 12, 8), HorizontalAlignment = HorizontalAlignment.Left };
        search.Click += async (_, _) =>
        {
            if (city.Text.Trim().Length < 2) { searchStatus.Text = "Enter at least two characters."; return; }
            search.IsEnabled = false; searchStatus.Text = "Searching…";
            try
            {
                results.ItemsSource = await app.Provider.SearchAsync(city.Text, lifetime.Token);
                results.Visibility = Visibility.Visible;
                searchStatus.Text = results.Items.Count == 0 ? "No matching cities. Try a nearby city." : "Select a result, then use this city.";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (ex is System.Net.Http.HttpRequestException or System.Text.Json.JsonException or InvalidOperationException or KeyNotFoundException)
            { searchStatus.Text = "City search unavailable. Check your connection and try again."; }
            finally { search.IsEnabled = true; }
        };
        panel.Children.Add(search); panel.Children.Add(results); panel.Children.Add(searchStatus);
        Button(panel, "Use selected city & enable weather", () =>
        {
            if (results.SelectedItem is not Place place) { searchStatus.Text = "Select a city first."; return; }
            app.UpdateSettings(app.Settings with { ShowWeather = true, WeatherConsent = true, LocationMode = "Manual", Latitude = Math.Round(place.Latitude, 2), Longitude = Math.Round(place.Longitude, 2), City = place.ToString() });
            UpdateWeatherStatus();
        });
        Check(panel, "Use Fahrenheit (°F)", s => s.Fahrenheit, value => Change(app.Settings with { Fahrenheit = value }));
        Button(panel, "Refresh weather now", () => _ = weather.RefreshAsync(app.Settings, true, true));
        Button(panel, "Turn off weather & clear saved location", () =>
        {
            app.UpdateSettings(app.Settings with { ShowWeather = false, LocationMode = "Off", WeatherConsent = false, Latitude = null, Longitude = null, City = "" }); UpdateWeatherStatus();
        });
        panel.Children.Add(weatherStatus);
        panel.Children.Add(Text("Weather refreshes every 30 minutes. Cached readings are marked stale after one hour and hidden after 24 hours. The free Open-Meteo hosted service is for noncommercial use; commercial forks must arrange suitable service access."));
        AddTab("Weather", panel);
    }
    private void BuildMedia()
    {
        var panel = Panel(); panel.Children.Add(Text("Media controls · first preview", 19));
        Check(panel, "Enable media controls", s => s.MediaEnabled, value => Change(app.Settings with { MediaEnabled = value }));
        panel.Children.Add(Text("An independent toolbar above the taskbar: Previous, Play/Pause, Next and system output Mute. Works with players that publish Windows media sessions. Clock visibility and movement lock do not affect it."));
        panel.Children.Add(Text("This preview is beside the taskbar, not embedded inside it. Fullscreen apps may cover it. Exact taskbar embedding and the artwork panel remain in development."));
        var displays = DisplayService.Available();
        string selected = DisplayService.Resolve(app.Settings.MediaDisplayId).DeviceName;
        Choice(panel, "Media display", displays.Select(d => d.Label).ToArray(), displays.First(d => d.Id == selected).Label,
            value => Change(app.Settings with { MediaDisplayId = displays.First(d => d.Label == value).Id }));
        Button(panel, "Realign media above taskbar", app.RealignMedia);
        mediaStatus = Text(app.MediaStatus); panel.Children.Add(mediaStatus);
        panel.Children.Add(Text("Track titles stay in memory only. No audio is recorded and no listening history is saved. Mute affects all audio on the default Windows output device. Disabling media stops session monitoring and closes the toolbar."));
        AddTab("Media", panel);
    }
    private void BuildAbout()
    {
        var panel = Panel(); panel.Children.Add(Text("Small widgets for your desktop", 19));
        panel.Children.Add(Text("Widgets · v0.5.0\nCopyright © 2026 Saqib Hussain\nOriginal code and artwork: MIT license."));
        panel.Children.Add(Text("Nunito and Inter are bundled unmodified under SIL Open Font License 1.1. .NET and WPF are open-source Microsoft projects; their redistribution notices are included. DirectWrite and Windows are operating-system prerequisites."));
        Link(panel, "Weather data by Open-Meteo", "https://open-meteo.com/");
        Link(panel, "Weather data license · CC BY 4.0", "https://creativecommons.org/licenses/by/4.0/");
        Link(panel, "City data based on GeoNames", "https://www.geonames.org/");
        Link(panel, "Weather service terms", "https://open-meteo.com/en/terms");
        Button(panel, "Open bundled license notices", () => Program.OpenLink(Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.md")));
        Button(panel, "Open data folder", () =>
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClockWidget");
            try { Directory.CreateDirectory(folder); Program.OpenLink(folder); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { MessageBox.Show("The data folder could not be opened.\n" + ex.Message, "Widgets"); }
        });
        panel.Children.Add(Text("Your settings and most recent weather reading stay in your Windows user profile. No account, ads, analytics, or app update checks. Windows and the weather provider apply their own privacy and service policies."));
        AddTab("About", panel);
    }
    private void UpdateWeatherStatus()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(UpdateWeatherStatus); return; }
        weatherStatus.Text = weather.Status.Length == 0 ? "Weather is up to date." : weather.Status;
        locationSummary.Text = app.Settings.LocationMode switch { "Automatic" => "Current source: automatic Windows location", "Manual" => "Current city: " + app.Settings.City, _ => "Weather is off until you choose an option below." };
    }
    private void Change(Settings settings) { if (!rebuilding && !synchronizing) app.UpdateSettings(settings); }
    private void SynchronizeControls()
    {
        synchronizing = true;
        foreach (var entry in checks) entry.Control.IsChecked = entry.Value(app.Settings);
        visibilityButton.Content = app.Settings.ClockVisible ? "Hide clock" : "Show clock";
        if (customText != null && !customText.IsKeyboardFocusWithin) customText.Text = app.Settings.CustomText;
        if (mediaStatus != null) mediaStatus.Text = app.MediaStatus;
        if (previewImage != null) previewImage.Opacity = app.Settings.Opacity;
        saveStatus.Text = app.StorageStatus.Length == 0 ? "All changes saved on this device.  ·  Automatic backup enabled." : app.StorageStatus;
        synchronizing = false;
    }
    private static void Button(Panel panel, string title, Action action)
    {
        var button = new Button { Content = title, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 5, 0, 5), HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += (_, _) => action(); panel.Children.Add(button);
    }
    private static void Link(Panel panel, string title, string url) => Button(panel, title + " ↗", () => Program.OpenLink(url));
    private void Check(Panel panel, string title, Func<Settings, bool> value, Action<bool> action)
    {
        var check = new CheckBox { Content = title, IsChecked = value(app.Settings), Margin = new Thickness(0, 7, 0, 9) };
        checks.Add((check, value));
        check.Checked += (_, _) => { if (!synchronizing) action(true); };
        check.Unchecked += (_, _) => { if (!synchronizing) action(false); };
        panel.Children.Add(check);
    }
    private static void Choice(Panel panel, string title, string[] choices, string selected, Action<string> action)
    {
        panel.Children.Add(Text(title));
        var segments = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
        string group = Guid.NewGuid().ToString();
        foreach (string choice in choices) { var option = new RadioButton { Content = choice, GroupName = group, IsChecked = choice == selected }; option.Checked += (_, _) => action(choice); segments.Children.Add(option); }
        panel.Children.Add(segments);
    }
    private static void Slider(Panel panel, string title, double min, double max, double value, double step, Action<double> action)
    {
        var label = Text($"{title}   {value:0.##}"); panel.Children.Add(label);
        var slider = new Slider { Minimum = min, Maximum = max, Value = value, TickFrequency = step, IsSnapToTickEnabled = true, Margin = new Thickness(0, 0, 0, 12), ToolTip = title };
        slider.ValueChanged += (_, _) => { label.Text = $"{title}   {slider.Value:0.##}"; action(slider.Value); }; panel.Children.Add(slider);
    }
}
