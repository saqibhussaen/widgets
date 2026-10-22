using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using ClockWidget.Core;

namespace ClockWidget;

// Explicit integration test for our own WPF windows. Always uses an isolated
// profile; it never requests real weather/location or modifies personal settings.
internal static class LifecycleProbe
{
    public static async Task RunAsync(Program app, string output)
    {
        var checks = new Dictionary<string, bool>();
        void Check(string label, bool passed) { checks[label] = passed; if (!passed) throw new InvalidOperationException("Lifecycle probe failed: " + label); }
        var store = new JsonStore(app.SettingsDirectory);
        var editor = Application.Current.Windows.OfType<SettingsWindow>().Single();
        static IEnumerable<DependencyObject> Walk(DependencyObject root) { yield return root; foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) foreach (var node in Walk(child)) yield return node; }
        var headerToggle = Walk(editor).OfType<Button>().Where(b => b.Content is string text && text is "Show clock" or "Hide clock").ToArray();
        Check("ExactlyOneShowHideButton", headerToggle.Length == 1);
        app.UpdateSettings(app.Settings with { ClockVisible = true, Seconds = true, LocationMode = "Off", WeatherConsent = false });
        Check("ClockInitiallyVisible", app.ClockWindows.All(w => w.IsVisible));
        app.ToggleClockVisibility();
        Check("HideHidesAllClocks", app.ClockWindows.All(w => !w.IsVisible));
        Check("HeaderSaysShowWhenHidden", Equals(headerToggle[0].Content, "Show clock"));
        Check("HiddenStateSavedImmediately", store.Load<Settings>("settings.json")?.ClockVisible == false);
        app.ToggleClockVisibility();
        Check("ShowRestoresAllClocks", app.ClockWindows.All(w => w.IsVisible));
        Check("HeaderSaysHideWhenShown", Equals(headerToggle[0].Content, "Hide clock"));
        Check("ShownStateSavedImmediately", store.Load<Settings>("settings.json")?.ClockVisible == true);
        var displays = DisplayService.Available();
        foreach (var display in displays)
        {
            app.SelectDisplay(display.Id); await Task.Delay(150);
            var clock = app.ClockWindows.First(); var p = clock.ScreenPosition;
            Check(display.Label + " selected", clock.TargetDisplayId == display.Id);
            Check(display.Label + " on screen", DisplayService.Resolve(display.Id).WorkingArea.Contains((int)p.X, (int)p.Y));
        }
        app.UpdateSettings(app.Settings with { AllDisplays = true }); await Task.Delay(150);
        Check("OneClockPerDisplay", app.ClockWindows.Count() == displays.Length && app.ClockWindows.Select(w => w.TargetDisplayId).Distinct().Count() == displays.Length);
        app.UpdateSettings(app.Settings with { Locked = true });
        var lockControl = Walk(editor).OfType<CheckBox>().Single(c => Equals(c.Content, "Lock position"));
        Check("LockControlTracksActualState", lockControl.IsChecked == true);
        Check("LockSavedImmediately", store.Load<Settings>("settings.json")?.Locked == true);
        foreach (var clock in app.ClockWindows)
        {
            var p = clock.ScreenPosition;
            clock.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
            Check("LockedMouseDoesNotCapture " + clock.TargetDisplayId, !clock.IsMouseCaptured && clock.ScreenPosition == p);
        }
        app.Unlock(); Check("UnlockIsConsistent", !app.Settings.Locked && !app.Settings.ClickThrough);
        Check("UnlockUpdatesSwitch", lockControl.IsChecked == false);
        lockControl.IsChecked = true;
        Check("LockSwitchUpdatesModelAndDisk", app.Settings.Locked && store.Load<Settings>("settings.json")?.Locked == true);
        app.UpdateSettings(app.Settings with { DateSize = 25, DateWeight = 650, WeatherSize = 23, WeatherWeight = 720 });
        var saved = store.Load<Settings>("settings.json")!;
        Check("TextPreferencesSaved", saved.DateSize == 25 && saved.DateWeight == 650 && saved.WeatherSize == 23 && saved.WeatherWeight == 720);
        Check("BackupExists", File.Exists(Path.Combine(app.SettingsDirectory, "settings.json.bak")));
        var tabControl = Walk(editor).OfType<TabControl>().Single();
        Check("SectionsInRequestedOrder", string.Join(",", tabControl.Items.OfType<TabItem>().Select(t => t.Header)) == "Clock,Date,Weather,Media,Settings,About");
        var topCenter = Walk(editor).OfType<Button>().Single(b => Equals(b.Content, "Top center"));
        foreach (TabItem section in tabControl.Items)
        {
            tabControl.SelectedItem = section; editor.UpdateLayout();
            Check("TopCenterAvailableFrom " + section.Header, topCenter.IsVisible);
        }
        topCenter.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check("SidebarRealignKeepsLock", app.Settings.Locked);
        Check("SidebarRealignSavesEveryDisplay", app.ClockWindows.All(w => app.Settings.DisplayPositions.ContainsKey(w.TargetDisplayId)));
        app.UpdateSettings(app.Settings with { ShowCustomText = true, CustomText = "A little breathing room", City = "Forecast city" });
        var textControl = Walk(editor).OfType<CheckBox>().Single(c => Equals(c.Content, "Show Custom Text"));
        textControl.IsChecked = false;
        Check("CustomTextToggleSaves", !app.Settings.ShowCustomText && store.Load<Settings>("settings.json")?.ShowCustomText == false);
        Check("CustomTextKeepsForecastLocation", app.Settings.City == "Forecast city" && app.Settings.CustomText == "A little breathing room");
        Check("MakeCurrentDefaultSucceeds", app.MakeCurrentDefault());
        Check("DefaultCapturesCurrentAppearance", store.Load<List<ThemePreset>>("themes.json")!.Single(t => t.Name == "Default").Appearance.DateSize == app.Settings.DateSize);
        app.UpdateSettings(app.Settings with { ShowTime = false, ShowDate = true, ShowWeather = false, ShowCity = false, PeriodSize = 42 });
        Check("DateOnlySaved", store.Load<Settings>("settings.json") is { ShowTime: false, ShowDate: true, ShowWeather: false, ShowCity: false, PeriodSize: 42 });
        Check("PresetSaveSucceeds", app.SaveTheme("Date only test"));
        var preset = store.Load<List<ThemePreset>>("themes.json")!.Single(t => t.Name == "Date only test");
        app.UpdateSettings(preset.Apply(app.Settings with { ShowTime = true, Locked = false }));
        Check("PresetRestoresAppearanceOnly", !app.Settings.ShowTime && !app.Settings.ShowWeather && !app.Settings.Locked);
        var applyButton = Walk(editor).OfType<Button>().Single(b => Equals(b.Content, "Apply selected theme"));
        applyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check("ThemeButtonRebuildsSectionsWithoutParentConflict", Walk(editor).OfType<TabControl>().Single().Items.Count == 6);
        app.UpdateSettings(app.Settings with { ShowTime = true, ShowDate = true, ShowWeather = true });
        string time = app.ClockWindows.First().TimeText;
        app.CloseSettings(); await Task.Delay(6200);
        Check("ClosingSettingsKeepsClocksVisible", app.ClockWindows.Count() == displays.Length && app.ClockWindows.All(w => w.IsVisible));
        Check("ClockStillTicksAfterSettingsClose", app.ClockWindows.First().TimeText != time);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output, JsonSerializer.Serialize(checks, new JsonSerializerOptions { WriteIndented = true }));
    }
}
