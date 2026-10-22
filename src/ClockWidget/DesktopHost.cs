using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace ClockWidget;

// Keep WPF's layered HWND top-level: a wallpaper WorkerW parent can hide all pixels.
// Only this application's window z-order is changed; Explorer is never modified.
public sealed class DesktopHost(Window window, Func<bool>? isLocked = null) : IDisposable
{
    private nint hwnd;
    private bool top, through;
    private DateTime revealUntil;
    private HwndSource? source;
    public bool Attached { get; private set; }
    public string Diagnostic { get; private set; } = "";
    public bool IsTopMost => ((long)GetWindowLongPtr(hwnd, -20) & 8) != 0;
    public bool IsClickThrough => ((long)GetWindowLongPtr(hwnd, -20) & 0x20) != 0;
    public bool IsVisible => hwnd != 0 && IsWindowVisible(hwnd) && !IsIconic(hwnd);
    public bool IsTopLevel => GetParent(hwnd) == 0;
    public string Status => top ? "Always on top" : DateTime.UtcNow < revealUntil ? "Clock brought into view" : Attached ? "On your desktop" : "Desktop unavailable · clock is floating";
    private delegate bool EnumProc(nint h, nint l);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint FindWindowEx(nint parent, nint after, string? cls, string? name);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, nint l);
    [DllImport("user32.dll")] private static extern nint GetParent(nint h);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint h, uint command);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint h);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint h);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint h, out Rect rect);
    [DllImport("user32.dll")] private static extern nint GetWindowLongPtr(nint h, int index);
    [DllImport("user32.dll")] private static extern nint SetWindowLongPtr(nint h, int index, nint value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(nint h, nint after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint h, int command);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint p);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(NativePoint p);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint h, uint flags);

    public void Initialize() { hwnd = new WindowInteropHelper(window).EnsureHandle(); if (source == null) { source = HwndSource.FromHwnd(hwnd); source?.AddHook(WindowMessage); } ApplyStyles(); }
    private nint WindowMessage(nint h, int message, nint w, nint l, ref bool handled) { if (message == 0x112 && ((long)w & 0xFFF0) == 0xF010 && isLocked?.Invoke() == true) handled = true; return 0; }
    private void ApplyStyles()
    {
        long style = (long)GetWindowLongPtr(hwnd, -20) | 0x80 | 0x08000000;
        SetWindowLongPtr(hwnd, -20, (nint)(through ? style | 0x20 : style & ~0x20));
    }
    public void Configure(bool alwaysOnTop, bool clickThrough)
    {
        top = alwaysOnTop; through = clickThrough;
        if (hwnd == 0) return;
        ApplyStyles(); Recover();
    }
    public void Recover()
    {
        if (hwnd == 0 || !window.IsVisible) return;
        if (IsIconic(hwnd)) ShowWindow(hwnd, 4);
        if (top || DateTime.UtcNow < revealUntil) { SetWindowPos(hwnd, new nint(-1), 0, 0, 0, 0, 0x53); return; }
        if (IsTopMost) SetWindowPos(hwnd, new nint(-2), 0, 0, 0, 0, 0x13);
        nint desktop = 0;
        EnumProc callback = (h, _) =>
        {
            if (IsWindowVisible(h) && FindWindowEx(h, 0, "SHELLDLL_DefView", null) != 0) { desktop = h; return false; }
            return true;
        };
        EnumWindows(callback, 0);
        Attached = desktop != 0;
        if (desktop != 0)
        {
            // Insert after the window immediately ABOVE the desktop: below apps,
            // above wallpaper/icons. Never put the clock at HWND_BOTTOM.
            var aboveDesktop = GetWindow(desktop, 3);
            if (aboveDesktop != hwnd) SetWindowPos(hwnd, aboveDesktop, 0, 0, 0, 0, 0x53);
        }
        else SetWindowPos(hwnd, new nint(-2), 0, 0, 0, 0, 0x53);
        Diagnostic = $"Top-level layered window: {IsTopLevel}; visible: {IsVisible}; desktop anchor: {Attached}";
    }
    public void Reveal() { revealUntil = DateTime.UtcNow.AddSeconds(5); ShowWindow(hwnd, 4); EnsureVisible(); Recover(); }
    public bool IsUncoveredAt(Point screenPoint)
    {
        var found = WindowFromPoint(new NativePoint { X = (int)Math.Round(screenPoint.X), Y = (int)Math.Round(screenPoint.Y) });
        return found == hwnd || GetAncestor(found, 2) == hwnd;
    }
    public Point PositionPixels() { GetWindowRect(hwnd, out var r); return new Point(r.Left, r.Top); }
    public static Point CursorPixels() { GetCursorPos(out var p); return new Point(p.X, p.Y); }
    public void MovePixels(double x, double y) => SetWindowPos(hwnd, 0, (int)Math.Round(x), (int)Math.Round(y), 0, 0, 0x15);
    public void Place(bool center)
    {
        var p = PositionPixels(); PlaceOn(Forms.Screen.FromPoint(new System.Drawing.Point((int)p.X, (int)p.Y)), center);
    }
    public void PlaceOn(Forms.Screen screen, bool center)
    {
        GetWindowRect(hwnd, out var r); var area = screen.WorkingArea;
        MovePixels(area.Left + (area.Width - (r.Right - r.Left)) / 2d, area.Top + (center ? (area.Height - (r.Bottom - r.Top)) / 2d : area.Height * .08));
    }
    public void EnsureVisible(Forms.Screen? preferred = null)
    {
        GetWindowRect(hwnd, out var r);
        if (!Forms.Screen.AllScreens.Any(s => s.WorkingArea.Contains(r.Left, r.Top) && s.WorkingArea.Contains(r.Right - 1, r.Bottom - 1)))
        {
            var screen = preferred ?? Forms.Screen.FromRectangle(new System.Drawing.Rectangle(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top));
            var area = screen.WorkingArea;
            MovePixels(Math.Clamp(r.Left, area.Left, Math.Max(area.Left, area.Right - (r.Right - r.Left))), Math.Clamp(r.Top, area.Top, Math.Max(area.Top, area.Bottom - (r.Bottom - r.Top))));
        }
    }
    public void Dispose() { source?.RemoveHook(WindowMessage); source = null; }
}
