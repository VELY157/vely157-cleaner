using System.Drawing.Drawing2D;

namespace VelyInstaller;

// Barra lateral azul con el logo, los 4 pasos y una frase abajo.
// Todo se dibuja multiplicado por K (la misma escala que el resto del instalador).
public sealed class Sidebar : PaintedControl
{
    public string[] Steps { get; set; } = [];
    public string Tagline { get; set; } = "";
    public float K { get; set; } = 1f;

    int _current;
    public int Current { get => _current; set { _current = value; Invalidate(); } }

    Font Px(float pt, FontStyle style = FontStyle.Regular, string family = "Segoe UI") => new(family, pt * 96f / 72f * K, style, GraphicsUnit.Pixel);

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        G.Hq(g);
        var rc = ClientRectangle;
        using (var bg = new LinearGradientBrush(rc, Palette.SidebarTop, Palette.SidebarBottom, 90f))
            g.FillRectangle(bg, rc);
        G.DrawWaves(g, rc);

        // Logo arriba
        float w = Width;
        G.DrawShield(g, new RectangleF(w / 2 - 30 * K, 28 * K, 60 * K, 66 * K));
        using (var big = new Font("Segoe UI", 18 * K, FontStyle.Bold, GraphicsUnit.Pixel))
        using (var sub = new Font("Segoe UI", 9.5f * K, FontStyle.Bold, GraphicsUnit.Pixel))
        {
            var top = (int)(102 * K);
            var bigH = TextRenderer.MeasureText(g, "VELY157", big).Height;
            TextRenderer.DrawText(g, "VELY157", big, new Rectangle(0, top, Width, bigH), Palette.Text, TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, "C L E A N E R", sub, new Rectangle(0, top + bigH - (int)(2 * K), Width, (int)(16 * K)), Palette.BlueLight, TextFormatFlags.HorizontalCenter);
        }

        // Pasos
        var y = 170f * K;
        var x = 28f * K;
        var d = 26f * K;
        using var numFont = Px(9.5f);
        using var numBold = Px(9.5f, FontStyle.Regular, "Segoe UI Semibold");
        using var stepFont = Px(10);
        using var stepBold = Px(10, FontStyle.Regular, "Segoe UI Semibold");
        for (var i = 0; i < Steps.Length; i++)
        {
            var state = i < _current ? StepState.Done : i == _current ? StepState.Current : StepState.Pending;
            var circle = new RectangleF(x, y, d, d);

            if (state == StepState.Current)
            {
                using var pill = G.Round(new RectangleF(x - 10 * K, y - 7 * K, Width - x - 6 * K, d + 14 * K), 10 * K);
                using var b = new SolidBrush(Color.FromArgb(70, Palette.Blue));
                g.FillPath(b, pill);
            }

            if (state == StepState.Done)
            {
                using var b = new SolidBrush(Palette.Green);
                g.FillEllipse(b, circle);
                G.DrawCheckMark(g, circle, Color.White, 2.6f * K);
            }
            else if (state == StepState.Current)
            {
                using var b = new LinearGradientBrush(circle, Palette.BlueLight, Palette.Blue, 90f);
                g.FillEllipse(b, circle);
                TextRenderer.DrawText(g, (i + 1).ToString(), numBold, Rectangle.Round(circle), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            else
            {
                using var pen = new Pen(Color.FromArgb(70, Palette.Muted), 2f * K);
                g.DrawEllipse(pen, circle);
                TextRenderer.DrawText(g, (i + 1).ToString(), numFont, Rectangle.Round(circle), Palette.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }

            var color = state == StepState.Pending ? Palette.Muted : Palette.Text;
            var textX = (int)(x + d + 12 * K);
            TextRenderer.DrawText(g, Steps[i], state == StepState.Current ? stepBold : stepFont, new Rectangle(textX, (int)y, Width - textX - (int)(6 * K), (int)d), color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);

            y += 52 * K;
        }

        if (Tagline.Length > 0)
        {
            using var f = Px(9);
            TextRenderer.DrawText(g, Tagline, f, new Rectangle((int)(14 * K), Height - (int)(66 * K), Width - (int)(28 * K), (int)(52 * K)), Palette.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.WordBreak | TextFormatFlags.Bottom);
        }
    }
}
