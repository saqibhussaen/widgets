using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace ClockWidget;

public sealed class ClockGlyph : FrameworkElement
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void OutlineCallback(int kind, float x, float y, float x2, float y2, float x3, float y3);
    [DllImport("ClockFont.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private static extern int ClockOutline(string path, float weight, string text, float size, OutlineCallback callback, out float width);
    [DllImport("ClockFont.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
    private static extern int TextOutline(string path, string family, float weight, string text, float size, OutlineCallback callback, out float width);
    private Geometry geometry = Geometry.Empty;
    private double advance;
    public Brush Foreground { get; set; } = Brushes.White;
    public string Signature { get; private set; } = "";
    public bool ShapedText { get; init; }
    public string Text { get; private set; } = "";

    public void Update(string text, string font, double weight, double size)
    {
        Text = text;
        if (text.Length == 0) { geometry = Geometry.Empty; advance = 0; Signature = ""; InvalidateMeasure(); InvalidateVisual(); return; }
        string signature = $"{text}|{font}|{weight:R}|{size:R}";
        if (signature == Signature) { InvalidateVisual(); return; }
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", font.ToLowerInvariant(), "font.ttf");
        var result = new StreamGeometry();
        using (var context = result.Open())
        {
            // StreamGeometry closes figures when the next begins; filled glyph figures are closed.
            OutlineCallback callback = (kind, x, y, x2, y2, x3, y3) =>
            {
                switch (kind)
                {
                    case 0: context.BeginFigure(new Point(x, y), true, true); break;
                    case 1: context.LineTo(new Point(x, y), true, false); break;
                    case 2: context.BezierTo(new Point(x, y), new Point(x2, y2), new Point(x3, y3), true, false); break;
                    case 4: result.FillRule = x == 0 ? FillRule.EvenOdd : FillRule.Nonzero; break;
                }
            };
            float width;
            int status = ShapedText ? TextOutline(path, font, (float)weight, text, (float)size, callback, out width) : ClockOutline(path, (float)weight, text, (float)size, callback, out width);
            Marshal.ThrowExceptionForHR(status);
            advance = width;
            GC.KeepAlive(callback);
        }
        result.Freeze(); geometry = result; Signature = signature;
        InvalidateMeasure(); InvalidateVisual();
    }
    public Geometry Outline => geometry;
    protected override Size MeasureOverride(Size availableSize) => new(Math.Ceiling(Math.Max(advance, geometry.Bounds.Width)) + 8, Math.Ceiling(geometry.Bounds.Height) + 8);
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (geometry.IsEmpty()) return;
        dc.PushTransform(new TranslateTransform(4, 4 - geometry.Bounds.Top));
        dc.DrawGeometry(Foreground, null, geometry);
        dc.Pop();
    }
}
