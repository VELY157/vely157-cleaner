using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Path = Avalonia.Controls.Shapes.Path;

namespace VelyCleanerMac.UI;

// Iconos de assets/iconos/*.svg (y los del prototipo), pasados a geometria para Avalonia.
// Cada icono es una lista de trazos: (d, relleno?). viewBox original 24x24.
public static class Icons
{
    sealed record Stroke(string Data, bool Fill = false, double Width = 2);

    static readonly Dictionary<string, Stroke[]> Defs = new()
    {
        ["home"] = [new("M4 11l8-7 8 7v9a1 1 0 01-1 1h-5v-6h-4v6H5a1 1 0 01-1-1z", Fill: true)],
        ["broom"] = [new("M14 3l-3.5 8M7 12h8l2 9H5z"), new("M9 16v5M12 16v5M15 16v5", Width: 1.6)],
        ["grid"] = [
            new("M4 4h7v7H4z", Fill: true), new("M13 4h7v7h-7z", Fill: true),
            new("M4 13h7v7H4z", Fill: true), new("M13 13h7v7h-7z"),
        ],
        ["files"] = [new("M6 3h8l4 4v14H6z"), new("M14 3v4h4M9 13h6M9 17h6")],
        ["copy"] = [new("M8 8h12v12H8z"), new("M16 8V5a1 1 0 00-1-1H5a1 1 0 00-1 1v10a1 1 0 001 1h3")],
        ["box"] = [new("M3 7l9-4 9 4-9 4zM3 7v10l9 4 9-4V7M12 11v10")],
        ["key"] = [new("M8 15m-4 0a4 4 0 108 0 4 4 0 10-8 0"), new("M11 12l8-8M16 7l2 2M14 9l2 2")],
        ["gear"] = [new("M12 12m-3 0a3 3 0 106 0 3 3 0 10-6 0"), new("M12 2v3M12 19v3M2 12h3M19 12h3M4.9 4.9l2.1 2.1M17 17l2.1 2.1M4.9 19.1L7 17M17 7l2.1-2.1")],
        ["shield"] = [new("M12 3l7 3v6c0 4.5-3 7.5-7 9-4-1.5-7-4.5-7-9V6z"), new("M9 12l2 2 4-4")],
        ["disk"] = [new("M3 6h18v12H3z"), new("M7 14h.01M3 12h18")],
        ["clock"] = [new("M12 12m-9 0a9 9 0 1018 0 9 9 0 10-18 0"), new("M12 7v5l3 2")],
        ["chip"] = [new("M6 6h12v12H6z"), new("M9.5 9.5h5v5h-5z", Fill: true), new("M9 2v3M15 2v3M9 19v3M15 19v3M2 9h3M2 15h3M19 9h3M19 15h3")],
        ["battery"] = [new("M2 7h18v10H2z"), new("M4.5 9.5h11v5h-11z", Fill: true), new("M22 10.5v3")],
        ["check"] = [new("M5 12.5l4.5 4.5L19 7.5", Width: 3)],
        ["chev"] = [new("M9 6l6 6-6 6")],
        ["refresh"] = [new("M20 11a8 8 0 10-2.3 5.7M20 5v6h-6")],
        ["search"] = [new("M11 11m-7 0a7 7 0 1014 0 7 7 0 10-14 0"), new("M20 20l-4-4")],
        ["spark"] = [new("M12 3l1.8 5.2L19 10l-5.2 1.8L12 17l-1.8-5.2L5 10l5.2-1.8z", Fill: true)],
        ["cart"] = [new("M3 4h2l2.4 11h10.2L20 8H7"), new("M9 19m-1.5 0a1.5 1.5 0 103 0 1.5 1.5 0 10-3 0", Fill: true), new("M17 19m-1.5 0a1.5 1.5 0 103 0 1.5 1.5 0 10-3 0", Fill: true)],
        ["finder"] = [new("M3 4h18v16H3z"), new("M12 4v16M8 9v1M16 9v1M8 15c2.5 2 5.5 2 8 0")],
        ["trash"] = [new("M4 7h16M9 7V4h6v3M6 7l1 13h10l1-13")],
        ["wa"] = [new("M4 20l1.3-3.9A8 8 0 1112 20a8 8 0 01-3.9-1L4 20z")],
        ["globe"] = [new("M12 12m-9 0a9 9 0 1018 0 9 9 0 10-18 0"), new("M3 12h18M12 3c2.5 2.7 2.5 15.3 0 18M12 3c-2.5 2.7-2.5 15.3 0 18")],
        ["bolt"] = [new("M13 2L4 14h7l-1 8 9-12h-7z", Fill: true)],
        ["c-apps"] = [new("M7 17L12 4l5 13M8.6 13h6.8M4 20h16")],
        ["c-discord"] = [
            new("M6 7c4-2 8-2 12 0l2 9c-2 2-4 3-5 3l-1-2c-1.4.3-2.6.3-4 0l-1 2c-1 0-3-1-5-3z", Width: 1.8),
            new("M9.5 12.5m-1.3 0a1.3 1.3 0 102.6 0 1.3 1.3 0 10-2.6 0", Fill: true),
            new("M14.5 12.5m-1.3 0a1.3 1.3 0 102.6 0 1.3 1.3 0 10-2.6 0", Fill: true),
        ],
        ["c-store"] = [new("M12 4v11M7 10l5 5 5-5M5 20h14")],
        ["c-log"] = [new("M6 3h8l4 4v14H6z"), new("M9 11h6M9 15h6")],
    };

    public static Control Make(string name, double size, IBrush? color = null)
    {
        color ??= AppTheme.Texto;
        var canvas = new Canvas { Width = 24, Height = 24 };
        if (Defs.TryGetValue(name, out var strokes))
            foreach (var s in strokes)
            {
                canvas.Children.Add(new Path
                {
                    Data = Geometry.Parse(s.Data),
                    Stroke = s.Fill ? null : color,
                    StrokeThickness = s.Width,
                    StrokeLineCap = PenLineCap.Round,
                    StrokeJoin = PenLineJoin.Round,
                    Fill = s.Fill ? color : null,
                });
            }
        return new Viewbox { Width = size, Height = size, Stretch = Stretch.Uniform, Child = canvas };
    }
}
