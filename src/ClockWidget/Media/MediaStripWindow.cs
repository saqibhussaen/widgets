using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Automation;
using ClockWidget.Core;
using PathShape = System.Windows.Shapes.Path;

namespace ClockWidget.Media;

public sealed class MediaStripWindow : Window
{
    private readonly Program app;
    private readonly MediaController controller;
    private readonly Button previous, play, next, mute;
    private readonly TextBlock title = new() { Width = 210, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Foreground = Brushes.White, Margin = new Thickness(10, 0, 8, 0) };
    private readonly ComboBox sources = new() { Width = 150, DisplayMemberPath = "Name", SelectedValuePath = "Id", Margin = new Thickness(8, 0, 4, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Player · Auto follows the Windows current session" };
    private bool refreshing, allowClose;
    private string displayId = "";
    public MediaStripWindow(Program app, MediaController controller)
    {
        this.app = app; this.controller = controller;
        Title = "Widgets · Media preview"; WindowStyle = WindowStyle.None; AllowsTransparency = true;
        Background = Brushes.Transparent; ShowInTaskbar = false; ShowActivated = false; ResizeMode = ResizeMode.NoResize;
        SizeToContent = SizeToContent.WidthAndHeight; Topmost = true;
        Resources = SettingsTheme.Create();
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        previous = Command(row, "Previous", "M 3,2 L 3,18 M 17,2 L 6,10 L 17,18 Z", () => _ = controller.ExecuteAsync(MediaCommand.Previous));
        play = Command(row, "Play or pause", "M 5,2 L 18,10 L 5,18 Z", () => _ = controller.ExecuteAsync(MediaCommand.PlayPause));
        next = Command(row, "Next", "M 17,2 L 17,18 M 3,2 L 14,10 L 3,18 Z", () => _ = controller.ExecuteAsync(MediaCommand.Next));
        mute = Command(row, "Mute system output", "M 2,7 L 6,7 L 11,3 L 11,17 L 6,13 L 2,13 Z M 14,6 Q 20,10 14,14", () => controller.ToggleMute());
        row.Children.Add(title); row.Children.Add(sources);
        Command(row, "Open settings", "M 10,3 A 7,7 0 1 1 9.99,3 M 10,7 L 10,13 M 7,10 L 13,10", app.OpenSettings);
        Content = new Border { Background = new SolidColorBrush(Color.FromRgb(30, 31, 40)), CornerRadius = new CornerRadius(12), Padding = new Thickness(8), BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromRgb(65, 64, 80)), Child = row };
        sources.SelectionChanged += async (_, _) => { if (!refreshing) await controller.Sessions.SelectAsync(sources.SelectedValue is string id && id.Length > 0 ? id : null); };
        SourceInitialized += (_, _) => { var hwnd = new WindowInteropHelper(this).Handle; SetWindowLongPtr(hwnd, -20, new IntPtr(GetWindowLongPtr(hwnd, -20).ToInt64() | 0x80)); };
        Loaded += (_, _) => Align(app.Settings.MediaDisplayId);
        controller.Changed += Refresh; Refresh();
    }
    private static Button Command(Panel panel, string name, string path, Action action)
    {
        var button = new Button { Width = 38, Height = 36, Padding = new Thickness(9), Margin = new Thickness(2, 0, 2, 0), ToolTip = name, Content = Draw(path) };
        AutomationProperties.SetName(button, name); button.Click += (_, _) => action(); panel.Children.Add(button); return button;
    }
    private static PathShape Draw(string path) => new() { Data = Geometry.Parse(path), Stroke = Brushes.White, Fill = Brushes.White, StrokeThickness = 1.3, Stretch = Stretch.Uniform };
    private void Refresh()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(Refresh); return; }
        var state = controller.Sessions.State;
        title.Text = state.Title; title.ToolTip = string.Join("\n", new[] { state.Title, state.Artist, controller.Status }.Where(s => s.Length > 0));
        previous.IsEnabled = state.CanPrevious && !controller.Busy;
        play.IsEnabled = state.CanPlayPause && !controller.Busy;
        next.IsEnabled = state.CanNext && !controller.Busy;
        play.Content = Draw(state.Playing ? "M 5,2 L 8,2 L 8,18 L 5,18 Z M 13,2 L 16,2 L 16,18 L 13,18 Z" : "M 5,2 L 18,10 L 5,18 Z");
        AutomationProperties.SetName(play, state.Playing ? "Pause" : "Play");
        play.ToolTip = state.Playing ? "Pause" : "Play";
        mute.IsEnabled = controller.Muted.HasValue;
        string muteLabel = controller.Muted == true ? "Unmute system output" : "Mute system output";
        AutomationProperties.SetName(mute, muteLabel); mute.ToolTip = muteLabel;
        mute.Content = Draw(controller.Muted == true ? "M 2,7 L 6,7 L 11,3 L 11,17 L 6,13 L 2,13 Z M 14,6 L 20,14 M 20,6 L 14,14" : "M 2,7 L 6,7 L 11,3 L 11,17 L 6,13 L 2,13 Z M 14,6 Q 20,10 14,14");
        refreshing = true;
        var options = new[] { new MediaSource("", "Auto · current player") }.Concat(controller.Sessions.Sources).ToArray();
        if (sources.ItemsSource is not MediaSource[] old || !old.SequenceEqual(options)) sources.ItemsSource = options;
        // Keep Auto selected until the user explicitly chooses a source.
        if (sources.SelectedIndex < 0) sources.SelectedIndex = 0;
        refreshing = false;
    }
    public void Align(string id)
    {
        displayId = id; UpdateLayout();
        var screen = DisplayService.Resolve(displayId); var area = screen.WorkingArea;
        var source = PresentationSource.FromVisual(this); var scale = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
        int width = Math.Min((int)Math.Ceiling(ActualWidth * scale.M11), area.Width - 16);
        int height = (int)Math.Ceiling(ActualHeight * scale.M22);
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd != IntPtr.Zero) SetWindowPos(hwnd, new IntPtr(-1), area.Left + (area.Width - width) / 2, area.Bottom - height - 8, width, height, 0x10);
    }
    public void CloseForExit() { allowClose = true; controller.Changed -= Refresh; Close(); }
    protected override void OnClosing(CancelEventArgs e) { if (!allowClose && !app.IsExiting) e.Cancel = true; base.OnClosing(e); }
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
}
