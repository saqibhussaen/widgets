using Forms = System.Windows.Forms;

namespace ClockWidget;

public sealed record DisplayOption(string Id, string Label)
{
    public override string ToString() => Label;
}
public static class DisplayService
{
    public static Forms.Screen Resolve(string id) => Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == id) ?? Forms.Screen.PrimaryScreen!;
    public static DisplayOption[] Available() => Forms.Screen.AllScreens.OrderByDescending(s => s.Primary).Select((s, i) => new DisplayOption(s.DeviceName, $"Display {i + 1}{(s.Primary ? " · Main" : "")} · {s.Bounds.Width} × {s.Bounds.Height}")).ToArray();
}
