using Avalonia;
using Avalonia.Media;

namespace VelyCleanerMac.UI;

// Arco del anillo de progreso (Avalonia no trae un shape "Arc" nativo).
public sealed class Arc : Avalonia.Controls.Shapes.Path
{
    double _sweep;
    public double StartAngle { get; set; } = -90;

    public double SweepAngle
    {
        get => _sweep;
        set { _sweep = Math.Clamp(value, 0, 359.999); Redraw(); }
    }

    public Arc() { Redraw(); }

    void Redraw()
    {
        var w = Width > 0 ? Width : 150;
        var h = Height > 0 ? Height : 150;
        var r = Math.Min(w, h) / 2 - StrokeThickness / 2;
        var cx = w / 2; var cy = h / 2;
        if (_sweep <= 0.01) { Data = null; return; }
        var startRad = StartAngle * Math.PI / 180;
        var endRad = (StartAngle + _sweep) * Math.PI / 180;
        var p0 = new Point(cx + r * Math.Cos(startRad), cy + r * Math.Sin(startRad));
        var p1 = new Point(cx + r * Math.Cos(endRad), cy + r * Math.Sin(endRad));
        var large = _sweep > 180;
        var figure = new PathFigure { StartPoint = p0, IsClosed = false };
        figure.Segments!.Add(new ArcSegment { Point = p1, Size = new Size(r, r), IsLargeArc = large, SweepDirection = SweepDirection.Clockwise });
        Data = new PathGeometry { Figures = { figure } };
    }
}
