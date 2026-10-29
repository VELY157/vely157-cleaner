using System.Drawing.Drawing2D;
using System.Drawing.Text;

namespace VelyUi;

// Estilo azul compartido por el instalador y el limpiador.

public enum StepState { Pending, Current, Done, Error }

public static class Palette
{
    public static readonly Color Bg = Color.FromArgb(7, 12, 28);
    public static readonly Color BgTop = Color.FromArgb(12, 26, 60);
    public static readonly Color SidebarTop = Color.FromArgb(12, 22, 48);
    public static readonly Color SidebarBottom = Color.FromArgb(8, 14, 32);
    public static readonly Color Card = Color.FromArgb(16, 28, 58);
    public static readonly Color CardHover = Color.FromArgb(22, 38, 76);
    public static readonly Color CardBorder = Color.FromArgb(38, 60, 108);
    public static readonly Color Blue = Color.FromArgb(30, 136, 255);
    public static readonly Color BlueLight = Color.FromArgb(56, 189, 248);
    public static readonly Color BlueDark = Color.FromArgb(18, 84, 210);
    public static readonly Color Text = Color.FromArgb(234, 242, 255);
    public static readonly Color Muted = Color.FromArgb(150, 168, 202);
    public static readonly Color Green = Color.FromArgb(34, 197, 94);
    public static readonly Color Warn = Color.FromArgb(245, 158, 11);
    public static readonly Color Bad = Color.FromArgb(248, 113, 113);

    public static Font Font(float size, FontStyle style = FontStyle.Regular) => new("Segoe UI", size, style);
    public static Font Semibold(float size) => new("Segoe UI Semibold", size);
}

public static class G
{
    public static void Hq(Graphics g)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    }

    public static GraphicsPath Round(RectangleF r, float radius)
    {
        var p = new GraphicsPath();
        if (radius <= 0 || r.Width <= 0 || r.Height <= 0) { p.AddRectangle(r); return p; }
        var d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    static GraphicsPath ShieldPath(RectangleF r)
    {
        PointF P(float x, float y) => new(r.X + x * r.Width, r.Y + y * r.Height);
        var path = new GraphicsPath();
        path.AddLine(P(0.5f, 0f), P(1f, 0.15f));
        path.AddBezier(P(1f, 0.15f), P(1f, 0.6f), P(0.8f, 0.86f), P(0.5f, 1f));
        path.AddBezier(P(0.5f, 1f), P(0.2f, 0.86f), P(0f, 0.6f), P(0f, 0.15f));
        path.CloseFigure();
        return path;
    }

    // Logo: escudo azul con una escoba.
    public static void DrawShield(Graphics g, RectangleF r)
    {
        if (r.Width < 4 || r.Height < 4) return;
        PointF P(float x, float y) => new(r.X + x * r.Width, r.Y + y * r.Height);

        using (var glow = ShieldPath(RectangleF.Inflate(r, r.Width * 0.08f, r.Height * 0.08f)))
        using (var glowBrush = new PathGradientBrush(glow) { CenterColor = Color.FromArgb(90, Palette.Blue), SurroundColors = [Color.FromArgb(0, Palette.Blue)] })
            g.FillPath(glowBrush, glow);

        using var outer = ShieldPath(r);
        using (var fill = new LinearGradientBrush(r, Palette.BlueLight, Palette.BlueDark, 90f))
            g.FillPath(fill, outer);
        using (var rim = new Pen(Color.FromArgb(200, 210, 236, 255), Math.Max(1.2f, r.Width * 0.035f)))
            g.DrawPath(rim, outer);

        var innerRect = RectangleF.Inflate(r, -r.Width * 0.13f, -r.Height * 0.12f);
        using (var inner = ShieldPath(innerRect))
        using (var innerPen = new Pen(Color.FromArgb(110, 255, 255, 255), Math.Max(1f, r.Width * 0.022f)))
            g.DrawPath(innerPen, inner);

        // Escoba
        using (var handle = new Pen(Color.White, Math.Max(1.5f, r.Width * 0.07f)) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLine(handle, P(0.66f, 0.2f), P(0.52f, 0.52f));
        using (var bristles = new GraphicsPath())
        {
            bristles.AddPolygon([P(0.42f, 0.5f), P(0.62f, 0.58f), P(0.54f, 0.82f), P(0.3f, 0.73f)]);
            using var white = new SolidBrush(Color.White);
            g.FillPath(white, bristles);
        }
        using (var line = new Pen(Palette.BlueDark, Math.Max(1f, r.Width * 0.022f)))
        {
            for (var i = 1; i <= 3; i++)
            {
                var t = i / 4f;
                g.DrawLine(line, Lerp(P(0.42f, 0.5f), P(0.62f, 0.58f), t), Lerp(P(0.3f, 0.73f), P(0.54f, 0.82f), t));
            }
        }
    }

    static PointF Lerp(PointF a, PointF b, float t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);

    public static void DrawWaves(Graphics g, Rectangle rc)
    {
        float w = rc.Width, h = rc.Height;
        var top = rc.Bottom - Math.Min(130f, h * 0.3f);
        for (var i = 0; i < 2; i++)
        {
            var off = i * 28f;
            using var p = new GraphicsPath();
            p.AddLine(rc.Left, rc.Bottom, rc.Left, top + 46 + off);
            p.AddBezier(rc.Left, top + 46 + off, rc.Left + w * 0.35f, top - 14 + off, rc.Left + w * 0.62f, top + 96 + off, rc.Right, top + 6 + off);
            p.AddLine(rc.Right, top + 6 + off, rc.Right, rc.Bottom);
            p.CloseFigure();
            var area = new RectangleF(rc.Left, top - 20, w, rc.Bottom - top + 21);
            using var b = new LinearGradientBrush(area, Color.FromArgb(i == 0 ? 60 : 120, Palette.Blue), Color.FromArgb(i == 0 ? 10 : 40, Palette.BlueDark), 90f);
            g.FillPath(b, p);
        }
    }

    public static void DrawCheckMark(Graphics g, RectangleF r, Color color, float width)
    {
        using var pen = new Pen(color, width) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        g.DrawLines(pen, [
            new PointF(r.X + r.Width * 0.24f, r.Y + r.Height * 0.52f),
            new PointF(r.X + r.Width * 0.43f, r.Y + r.Height * 0.7f),
            new PointF(r.X + r.Width * 0.77f, r.Y + r.Height * 0.32f),
        ]);
    }

    public static void DrawCheckBox(Graphics g, RectangleF r, bool isChecked, bool enabled = true)
    {
        using var path = Round(r, r.Width * 0.25f);
        if (isChecked)
        {
            using var b = new LinearGradientBrush(r, enabled ? Palette.BlueLight : Palette.Muted, enabled ? Palette.Blue : Palette.CardBorder, 90f);
            g.FillPath(b, path);
            DrawCheckMark(g, r, Color.White, Math.Max(1.6f, r.Width * 0.14f));
        }
        else
        {
            using var b = new SolidBrush(Color.FromArgb(10, 18, 40));
            g.FillPath(b, path);
            using var pen = new Pen(enabled ? Palette.Muted : Palette.CardBorder, 1.4f);
            g.DrawPath(pen, path);
        }
    }

    // Círculo de estado: listo (verde), en curso (azul a medias), pendiente (gris) o error.
    public static void DrawStatus(Graphics g, RectangleF r, StepState state)
    {
        switch (state)
        {
            case StepState.Done:
                using (var b = new SolidBrush(Palette.Green)) g.FillEllipse(b, r);
                DrawCheckMark(g, r, Color.White, Math.Max(1.6f, r.Width * 0.12f));
                break;
            case StepState.Current:
                using (var pen = new Pen(Palette.BlueLight, Math.Max(1.6f, r.Width * 0.1f))) g.DrawEllipse(pen, RectangleF.Inflate(r, -1, -1));
                using (var b = new SolidBrush(Palette.Blue)) g.FillPie(b, RectangleF.Inflate(r, -r.Width * 0.2f, -r.Height * 0.2f), 90, 180);
                break;
            case StepState.Error:
                using (var b = new SolidBrush(Palette.Bad)) g.FillEllipse(b, r);
                TextRenderer.DrawText(g, "!", Palette.Semibold(r.Height * 0.4f), Rectangle.Round(r), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                break;
            default:
                using (var pen = new Pen(Color.FromArgb(90, Palette.Muted), Math.Max(1.4f, r.Width * 0.08f))) g.DrawEllipse(pen, RectangleF.Inflate(r, -1, -1));
                break;
        }
    }
}

// Base para controles dibujados a mano, con fondo transparente y sin parpadeo.
public abstract class PaintedControl : Control
{
    protected PaintedControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        ForeColor = Palette.Text;
    }
}

public class GradientPanel : Panel
{
    public Color TopColor { get; set; } = Palette.BgTop;
    public Color BottomColor { get; set; } = Palette.Bg;
    public bool Waves { get; set; }

    public GradientPanel()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var rc = ClientRectangle;
        if (rc.Width <= 0 || rc.Height <= 0) return;
        G.Hq(e.Graphics);
        using (var b = new LinearGradientBrush(rc, TopColor, BottomColor, 90f))
            e.Graphics.FillRectangle(b, rc);
        if (Waves) G.DrawWaves(e.Graphics, rc);
    }
}

// Tarjeta con esquinas redondeadas.
public class Card : Panel
{
    public float Radius { get; set; } = 12;
    public Color Fill { get; set; } = Color.FromArgb(200, Palette.Card);
    public Color Border { get; set; } = Palette.CardBorder;

    public Card()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                 | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        base.OnPaintBackground(e);
        G.Hq(e.Graphics);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = G.Round(r, Radius);
        using (var b = new SolidBrush(Fill)) e.Graphics.FillPath(b, path);
        using (var pen = new Pen(Border)) e.Graphics.DrawPath(pen, path);
    }
}

public class FancyButton : PaintedControl
{
    bool _hover, _down, _primary = true;
    string _icon = "";

    public bool Primary { get => _primary; set { _primary = value; Invalidate(); } }
    public string Icon { get => _icon; set { _icon = value; Invalidate(); } }

    public FancyButton()
    {
        Font = Palette.Semibold(10.5f);
        Cursor = Cursors.Hand;
        Size = new Size(150, 42);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = _down = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
    protected override void OnEnabledChanged(EventArgs e) { Cursor = Enabled ? Cursors.Hand : Cursors.Default; Invalidate(); base.OnEnabledChanged(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        G.Hq(g);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = G.Round(r, 8);
        if (_primary)
        {
            var top = !Enabled ? Color.FromArgb(46, 64, 104) : _hover ? Palette.BlueLight : Palette.Blue;
            var bottom = !Enabled ? Color.FromArgb(36, 50, 84) : _down ? Palette.BlueDark : Color.FromArgb(20, 112, 240);
            using var b = new LinearGradientBrush(r, top, bottom, 90f);
            g.FillPath(b, path);
        }
        else
        {
            using var b = new SolidBrush(_hover && Enabled ? Palette.CardHover : Color.FromArgb(24, 38, 72));
            g.FillPath(b, path);
            using var pen = new Pen(Palette.CardBorder);
            g.DrawPath(pen, path);
        }
        var text = _icon.Length == 0 ? Text : $"{Text}  {_icon}";
        TextRenderer.DrawText(g, text, Font, Rectangle.Round(r), Enabled ? Color.White : Palette.Muted,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}

public class FancyCheck : PaintedControl
{
    bool _checked;
    public bool Checked { get => _checked; set { _checked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); } }
    public event EventHandler? CheckedChanged;

    public FancyCheck()
    {
        Font = Palette.Font(10.5f);
        Cursor = Cursors.Hand;
        Height = 28;
    }

    protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }
    protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        G.Hq(e.Graphics);
        var box = Height * 0.68f;
        G.DrawCheckBox(e.Graphics, new RectangleF(1, (Height - box) / 2, box, box), _checked, Enabled);
        TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle((int)box + 10, 0, Width - (int)box - 10, Height), ForeColor,
            TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}

public class SmoothProgress : PaintedControl
{
    int _value;
    public int Value { get => _value; set { _value = Math.Clamp(value, 0, 100); Invalidate(); } }
    public bool ShowText { get; set; } = true;

    public SmoothProgress()
    {
        Height = 24;
        Font = Palette.Semibold(10.5f);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        G.Hq(g);
        var textW = ShowText ? 56 : 0;
        var bar = new RectangleF(1, Height / 2f - 6, Width - textW - 2, 12);
        using (var track = G.Round(bar, 6))
        using (var tb = new SolidBrush(Color.FromArgb(28, 42, 76)))
            g.FillPath(tb, track);
        if (_value > 0)
        {
            var fill = new RectangleF(bar.X, bar.Y, Math.Max(12, bar.Width * _value / 100f), bar.Height);
            using var fp = G.Round(fill, 6);
            using var fb = new LinearGradientBrush(RectangleF.Inflate(fill, 1, 0), Palette.BlueDark, Palette.BlueLight, 0f);
            g.FillPath(fb, fp);
        }
        if (ShowText)
            TextRenderer.DrawText(g, $"{_value}%", Font, new Rectangle(Width - textW, 0, textW, Height), ForeColor, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
    }
}

// Indicador circular con un texto grande y uno pequeño en el centro.
public class Ring : PaintedControl
{
    float _value;
    string _big = "", _small = "";
    Color _accent = Palette.Blue;

    public float Value { get => _value; set { _value = Math.Clamp(value, 0f, 1f); Invalidate(); } }
    public string Big { get => _big; set { _big = value; Invalidate(); } }
    public string Small { get => _small; set { _small = value; Invalidate(); } }
    public Color Accent { get => _accent; set { _accent = value; Invalidate(); } }
    public float Thickness { get; set; } = 12;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        G.Hq(g);
        var size = Math.Min(Width, Height) - Thickness - 2;
        var r = new RectangleF((Width - size) / 2f, (Height - size) / 2f, size, size);
        using (var track = new Pen(Color.FromArgb(30, 46, 84), Thickness))
            g.DrawEllipse(track, r);
        if (_value > 0)
        {
            using var brush = new LinearGradientBrush(RectangleF.Inflate(r, Thickness, Thickness), Color.FromArgb(Math.Min(255, _accent.R + 60), Math.Min(255, _accent.G + 60), Math.Min(255, _accent.B + 40)), _accent, 45f);
            using var arc = new Pen(brush, Thickness) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            g.DrawArc(arc, r, -90, Math.Max(1f, 360f * _value));
        }
        using var big = new Font("Segoe UI Semibold", Math.Max(9f, size * 0.17f), GraphicsUnit.Pixel);
        using var small = new Font("Segoe UI", Math.Max(8f, size * 0.085f), GraphicsUnit.Pixel);
        var bigH = TextRenderer.MeasureText(g, _big, big).Height;
        var smallH = _small.Length > 0 ? TextRenderer.MeasureText(g, _small, small).Height : 0;
        var y = (Height - bigH - smallH) / 2;
        TextRenderer.DrawText(g, _big, big, new Rectangle(0, y, Width, bigH), Palette.Text, TextFormatFlags.HorizontalCenter);
        if (smallH > 0)
            TextRenderer.DrawText(g, _small, small, new Rectangle(0, y + bigH, Width, smallH), Palette.Muted, TextFormatFlags.HorizontalCenter);
    }
}

// Escudo + "VELY157 / CLEANER" + frase opcional.
public class Logo : PaintedControl
{
    string _tagline = "";
    public string Tagline { get => _tagline; set { _tagline = value; Invalidate(); } }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        G.Hq(g);
        float w = Width;
        float tagH = _tagline.Length > 0 ? Height * 0.2f : 0f;
        float body = Height - tagH;
        float shieldH = body * 0.52f;
        float shieldW = shieldH * 0.86f;
        var shieldRect = new RectangleF((w - shieldW) / 2f, body * 0.04f, shieldW, shieldH);
        G.DrawShield(g, shieldRect);

        using var big = new Font("Segoe UI", Math.Max(10f, body * 0.2f), FontStyle.Bold, GraphicsUnit.Pixel);
        using var sub = new Font("Segoe UI", Math.Max(7f, body * 0.095f), FontStyle.Bold, GraphicsUnit.Pixel);
        var y = (int)(body * 0.58f);
        var bigH = TextRenderer.MeasureText(g, "VELY157", big).Height;
        TextRenderer.DrawText(g, "VELY157", big, new Rectangle(0, y, (int)w, bigH), Palette.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
        TextRenderer.DrawText(g, "C L E A N E R", sub, new Rectangle(0, y + bigH - 2, (int)w, (int)(body * 0.14f)), Palette.BlueLight, TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding);
        if (tagH > 0)
        {
            using var tag = new Font("Segoe UI", Math.Max(8f, tagH * 0.32f), GraphicsUnit.Pixel);
            TextRenderer.DrawText(g, _tagline, tag, new Rectangle(0, (int)body, (int)w, (int)tagH), Palette.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak);
        }
    }
}

// Cuadro de texto oscuro con borde redondeado.
public class InputBox : Card
{
    public TextBox Box { get; } = new()
    {
        BorderStyle = BorderStyle.None,
        BackColor = Color.FromArgb(12, 22, 48),
        ForeColor = Palette.Text,
    };

    public InputBox()
    {
        Fill = Color.FromArgb(12, 22, 48);
        Radius = 8;
        Padding = new Padding(12, 0, 12, 0);
        Height = 44;
        Controls.Add(Box);
        Box.GotFocus += (_, _) => { Border = Palette.Blue; Invalidate(); };
        Box.LostFocus += (_, _) => { Border = Palette.CardBorder; Invalidate(); };
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        Box.Width = Width - Padding.Horizontal;
        Box.Location = new Point(Padding.Left, (Height - Box.Height) / 2);
    }
}

public static class Images
{
    // Ícono de ventana dibujado con el escudo del logo.
    public static Icon AppIcon(int size = 64)
    {
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            G.Hq(g);
            G.DrawShield(g, new RectangleF(size * 0.1f, size * 0.06f, size * 0.8f, size * 0.88f));
        }
        return Icon.FromHandle(bmp.GetHicon());
    }
}
