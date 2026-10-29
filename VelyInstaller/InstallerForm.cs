using System.Drawing.Drawing2D;

namespace VelyInstaller;

public sealed class InstallerForm : Form
{
    static readonly string[] StepNames = ["Bienvenida", "Ubicación", "Instalación", "Finalizar"];

    // Tamaño de diseño (a 100 %). Todo se multiplica por _k.
    const int DesignW = 880, DesignH = 560;

    readonly Sidebar _sidebar;
    readonly Panel _content;
    readonly float _k;
    int _step;
    bool _previewOnly; // para capturas sin instalar de verdad

    string _targetDir = InstallerCore.DefaultDir;
    bool _desktopIcon = true;
    bool _installed;

    public InstallerForm()
    {
        Text = "VELY157 Cleaner · Instalador";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        // Escalamos nosotros (letra, posiciones y tamaños con el mismo factor) en vez de dejar que
        // Windows escale solo una parte: así el texto nunca queda más grande que su espacio.
        AutoScaleMode = AutoScaleMode.None;
        _k = ComputeScale();
        Font = F(9.5f);
        ClientSize = new Size(S(DesignW), S(DesignH));
        BackColor = Palette.Bg;
        try { Icon = Images.AppIcon(); } catch { /* ignora */ }

        _sidebar = new Sidebar
        {
            Dock = DockStyle.Left,
            Width = S(250),
            K = _k,
            Steps = StepNames,
            Tagline = "Tu sistema, más\nlimpio y rápido",
        };
        _content = new GradientPanel { Dock = DockStyle.Fill, TopColor = Palette.BgTop, BottomColor = Palette.Bg };
        Controls.Add(_content);
        Controls.Add(_sidebar);

        ShowStep(0);
    }

    // La escala de Windows (125 %, 150 %…), pero nunca más grande de lo que cabe en la pantalla.
    float ComputeScale()
    {
        var k = DeviceDpi / 96f;
        if (float.TryParse(Environment.GetEnvironmentVariable("VELY_UI_SCALE"), System.Globalization.CultureInfo.InvariantCulture, out var forced))
            k = forced; // solo para pruebas
        var work = Screen.FromPoint(Cursor.Position).WorkingArea;
        var fit = Math.Min((work.Width - 40f) / (DesignW + 16), (work.Height - 60f) / (DesignH + 40));
        return Math.Clamp(Math.Min(k, fit), 0.7f, 3f);
    }

    int S(float v) => (int)Math.Round(v * _k);
    Point P(float x, float y) => new(S(x), S(y));
    Size Z(float w, float h) => new(S(w), S(h));

    // Letra en píxeles ya escalados (los puntos de Windows crecerían por su cuenta con la escala).
    Font F(float pt, FontStyle style = FontStyle.Regular) => new("Segoe UI", pt * 96f / 72f * _k, style, GraphicsUnit.Pixel);
    Font FSemi(float pt) => new("Segoe UI Semibold", pt * 96f / 72f * _k, GraphicsUnit.Pixel);

    int W => _content.ClientSize.Width;
    int H => _content.ClientSize.Height;

    // Para las capturas de pantalla: muestra un paso sin instalar nada.
    public void Preview(int step) { _previewOnly = true; ShowStep(step); }

    internal void ShowStep(int step)
    {
        _step = step;
        _sidebar.Current = step;
        _content.SuspendLayout();
        _content.Controls.Clear();
        switch (step)
        {
            case 0: BuildWelcome(); break;
            case 1: BuildLocation(); break;
            case 2: BuildInstall(); break;
            case 3: BuildFinish(); break;
        }
        _content.ResumeLayout();
    }

    Label Text_(string text, Font font, Color color, Rectangle bounds, ContentAlignment align = ContentAlignment.TopLeft) => new()
    {
        Text = text, Font = font, ForeColor = color, BackColor = Color.Transparent,
        AutoSize = false, Bounds = bounds, TextAlign = align, AutoEllipsis = true,
    };

    // ---- Encabezado de cada paso ----
    void Heading(string title, string subtitle, string glyph)
    {
        var icon = new PaintedGlyph { Glyph = glyph, Size = Z(50, 50), Location = P(40, 38) };
        var t = Text_(title, F(15, FontStyle.Bold), Palette.Text, new Rectangle(S(104), S(36), W - S(130), S(30)), ContentAlignment.MiddleLeft);
        var s = Text_(subtitle, F(9.5f), Palette.Muted, new Rectangle(S(105), S(68), W - S(130), S(40)));
        _content.Controls.AddRange([icon, t, s]);
    }

    FancyButton Nav(string text, bool primary, float right, EventHandler onClick)
    {
        var b = new FancyButton { Text = text, Primary = primary, Font = FSemi(9.5f), Size = Z(140, 40) };
        b.Location = new Point(W - S(right), H - S(66));
        b.Click += onClick;
        _content.Controls.Add(b);
        return b;
    }

    // ---- Paso 1: Bienvenida ----
    void BuildWelcome()
    {
        var shield = new PaintedGlyph { Glyph = "shield", Size = Z(104, 112), Location = new Point((W - S(104)) / 2, S(44)) };
        var title = Text_("Bienvenido al asistente de instalación", F(14, FontStyle.Bold), Palette.Text,
            new Rectangle(S(30), S(170), W - S(60), S(30)), ContentAlignment.MiddleCenter);
        var body = Text_("Este programa instalará VELY157 Cleaner en tu equipo.\nTe ayuda a mantener tu sistema limpio, seguro y funcionando al máximo rendimiento.",
            F(9.5f), Palette.Muted, new Rectangle(S(50), S(206), W - S(100), S(56)), ContentAlignment.TopCenter);

        var chips = new[] { ("rocket", "Más rendimiento"), ("shieldsmall", "Mayor seguridad"), ("spark", "Sistema limpio") };
        var chipW = S(176);
        var gap = S(12);
        var x = (W - (chips.Length * chipW + (chips.Length - 1) * gap)) / 2;
        foreach (var (g, t) in chips)
        {
            _content.Controls.Add(new Feature { Glyph = g, Caption = t, K = _k, Font = FSemi(9), Size = new Size(chipW, S(44)), Location = new Point(x, S(282)) });
            x += chipW + gap;
        }

        _content.Controls.AddRange([shield, title, body]);
        Nav("Siguiente  →", true, 180, (_, _) => ShowStep(1));
    }

    // ---- Paso 2: Ubicación ----
    void BuildLocation()
    {
        Heading("Elegir carpeta de destino", "Selecciona dónde quieres instalar VELY157 Cleaner.", "folder");

        var pathBox = new InputBox { Width = W - S(220), Height = S(40), Location = P(40, 130), Padding = new Padding(S(10), 0, S(10), 0) };
        pathBox.Box.Font = F(9.5f);
        pathBox.Box.Text = _targetDir;
        pathBox.Box.ReadOnly = true;
        var browse = new FancyButton { Text = "Examinar…", Primary = false, Font = FSemi(9.5f), Size = Z(130, 40), Location = new Point(W - S(170), S(130)) };
        browse.Click += (_, _) =>
        {
            using var dlg = new FolderBrowserDialog { Description = "Carpeta de instalación", UseDescriptionForTitle = true, SelectedPath = _targetDir };
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _targetDir = Path.Combine(dlg.SelectedPath, InstallerCore.AppName);
                pathBox.Box.Text = _targetDir;
                RefreshDisk();
            }
        };

        var card = new Card { Size = new Size(W - S(80), S(64)), Location = P(40, 188), Fill = Color.FromArgb(150, Palette.Card) };
        var diskIcon = new PaintedGlyph { Glyph = "disk", Size = Z(30, 30), Location = P(14, 17), Parent = card };
        var reqL = Text_("", F(9), Palette.Muted, new Rectangle(S(56), S(12), card.Width - S(70), S(19)));
        reqL.Name = "req";
        var freeL = Text_("", F(9), Palette.Muted, new Rectangle(S(56), S(33), card.Width - S(70), S(19)));
        freeL.Name = "free";
        card.Controls.AddRange([reqL, freeL]);

        var desktop = new FancyCheck { Text = "Crear acceso directo en el escritorio", Checked = _desktopIcon, BackColor = Color.Transparent, Font = F(9.5f), Location = P(42, 270), Size = new Size(W - S(84), S(26)) };
        desktop.CheckedChanged += (_, _) => _desktopIcon = desktop.Checked;

        _content.Controls.AddRange([pathBox, browse, card, desktop]);
        RefreshDisk();

        Nav("Atrás", false, 330, (_, _) => ShowStep(0));
        Nav("Siguiente  →", true, 180, (_, _) => ShowStep(2));
    }

    void RefreshDisk()
    {
        var (free, drive) = InstallerCore.DiskInfo(_targetDir);
        if (_content.Controls.Find("req", true).FirstOrDefault() is Label req) req.Text = "Espacio requerido:  ~70 MB";
        if (_content.Controls.Find("free", true).FirstOrDefault() is Label f) f.Text = $"Espacio disponible en {drive.TrimEnd('\\')}:  {InstallerCore.Format(free)}";
    }

    // ---- Paso 3: Instalación ----
    void BuildInstall()
    {
        Heading("Instalando VELY157 Cleaner", "Espera mientras se copian los archivos en tu equipo.", "gear");

        var progress = new SmoothProgress { Font = FSemi(9), Size = new Size(W - S(80), S(22)), Location = P(40, 136) };
        var steps = new[] { "Preparando archivos…", "Copiando archivos…", "Configurando componentes…", "Finalizando…" };
        var rows = new List<StepRow>();
        for (var i = 0; i < steps.Length; i++)
        {
            var row = new StepRow { Caption = steps[i], K = _k, Font = F(9.5f), BoldFont = FSemi(9.5f), Size = new Size(W - S(100), S(30)), Location = P(50, 180 + i * 38) };
            rows.Add(row);
            _content.Controls.Add(row);
        }
        _content.Controls.Add(progress);

        var reporter = new Progress<(int Percent, string Step)>(p =>
        {
            progress.Value = p.Percent;
            for (var i = 0; i < rows.Count; i++)
                rows[i].State = p.Percent >= (i + 1) * 24 ? StepState.Done : p.Percent >= i * 24 ? StepState.Current : StepState.Pending;
        });

        BeginInstall(reporter);
    }

    async void BeginInstall(IProgress<(int, string)> reporter)
    {
        try
        {
            if (_previewOnly)
            {
                for (var p = 0; p <= 60; p += 4) { reporter.Report((p, "")); await Task.Delay(40); }
                return; // en vista previa no avanza ni ejecuta nada
            }
            if (InstallerCore.HasPayload)
                await InstallerCore.InstallAsync(_targetDir, _desktopIcon, reporter);
            else
                await Task.Delay(1500);
            _installed = true;
            await Task.Delay(400);
            ShowStep(3);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "No se pudo completar la instalación:\n\n" + ex.Message, "Instalación", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ShowStep(1);
        }
    }

    // ---- Paso 4: Finalizar ----
    void BuildFinish()
    {
        var check = new PaintedGlyph { Glyph = "success", Size = Z(100, 100), Location = new Point((W - S(100)) / 2, S(52)) };
        var title = Text_("¡Instalación completada!", F(15, FontStyle.Bold), Palette.Text, new Rectangle(S(30), S(172), W - S(60), S(32)), ContentAlignment.MiddleCenter);
        var body = Text_("VELY157 Cleaner se instaló correctamente en tu equipo.", F(9.5f), Palette.Muted, new Rectangle(S(30), S(208), W - S(60), S(24)), ContentAlignment.MiddleCenter);

        var runW = S(280);
        var run = new FancyCheck { Text = "Ejecutar VELY157 Cleaner ahora", Checked = true, BackColor = Color.Transparent, Font = F(9.5f), Size = new Size(runW, S(26)), Location = new Point((W - runW) / 2, S(254)) };

        _content.Controls.AddRange([check, title, body, run]);
        Nav("Finalizar  ✓", true, 180, (_, _) =>
        {
            if (run.Checked && _installed) TryRun();
            Close();
        });
    }

    void TryRun()
    {
        try
        {
            var exe = Path.Combine(_targetDir, InstallerCore.ExeName);
            if (File.Exists(exe))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException) { /* ignora */ }
    }
}

// Ícono dibujado (escudo, engranaje, carpeta, check, etc.).
public sealed class PaintedGlyph : PaintedControl
{
    public string Glyph { get; set; } = "shield";

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        G.Hq(g);
        var r = new RectangleF(1, 1, Width - 2, Height - 2);
        switch (Glyph)
        {
            case "shield": G.DrawShield(g, r); break;
            case "success": DrawSuccess(g, r); break;
            default: DrawBadge(g, r, Glyph); break;
        }
    }

    static void DrawSuccess(Graphics g, RectangleF r)
    {
        using (var glow = new GraphicsPath())
        {
            glow.AddEllipse(RectangleF.Inflate(r, 6, 6));
            using var gb = new PathGradientBrush(glow) { CenterColor = Color.FromArgb(120, Palette.Green), SurroundColors = [Color.FromArgb(0, Palette.Green)] };
            g.FillPath(gb, glow);
        }
        using (var b = new LinearGradientBrush(r, Color.FromArgb(74, 222, 128), Palette.Green, 90f))
            g.FillEllipse(b, r);
        G.DrawCheckMark(g, r, Color.White, Math.Max(4f, r.Width * 0.08f));
    }

    static void DrawBadge(Graphics g, RectangleF r, string glyph)
    {
        using (var b = new LinearGradientBrush(r, Palette.BlueLight, Palette.BlueDark, 90f))
        using (var path = G.Round(r, r.Width * 0.26f))
            g.FillPath(b, path);
        var mark = glyph switch { "folder" => "📁", "gear" => "⚙", "disk" => "🖴", "rocket" => "🚀", "spark" => "✦", "shieldsmall" => "🛡", _ => "" };
        if (mark.Length > 0)
        {
            using var f = new Font("Segoe UI Emoji", r.Height * 0.42f, GraphicsUnit.Pixel);
            TextRenderer.DrawText(g, mark, f, Rectangle.Round(r), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}

// Chip de característica: ícono + texto, en la bienvenida.
public sealed class Feature : PaintedControl
{
    public string Glyph { get; set; } = "spark";
    public string Caption { get; set; } = "";
    public float K { get; set; } = 1f;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        G.Hq(g);
        var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using (var path = G.Round(r, 10 * K))
        using (var b = new SolidBrush(Color.FromArgb(150, Palette.Card)))
        {
            g.FillPath(b, path);
            using var pen = new Pen(Palette.CardBorder);
            g.DrawPath(pen, path);
        }
        var s = 24 * K;
        var icon = new RectangleF(11 * K, (Height - s) / 2f, s, s);
        using (var b = new LinearGradientBrush(icon, Palette.BlueLight, Palette.BlueDark, 90f))
        using (var ip = G.Round(icon, 7 * K))
            g.FillPath(b, ip);
        var mark = Glyph switch { "rocket" => "🚀", "shieldsmall" => "🛡", _ => "✦" };
        using (var ef = new Font("Segoe UI Emoji", 12 * K, GraphicsUnit.Pixel))
            TextRenderer.DrawText(g, mark, ef, Rectangle.Round(icon), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        var textX = (int)(icon.Right + 9 * K);
        TextRenderer.DrawText(g, Caption, Font, new Rectangle(textX, 0, Width - textX - (int)(6 * K), Height), Palette.Text,
            TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}

// Línea de progreso en el paso de instalación: círculo de estado + texto.
public sealed class StepRow : PaintedControl
{
    public string Caption { get; set; } = "";
    public float K { get; set; } = 1f;
    public Font? BoldFont { get; set; }
    StepState _state = StepState.Pending;
    public StepState State { get => _state; set { _state = value; Invalidate(); } }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        G.Hq(g);
        var d = 20 * K;
        var circle = new RectangleF(2, (Height - d) / 2f, d, d);
        G.DrawStatus(g, circle, _state);
        var color = _state == StepState.Pending ? Palette.Muted : Palette.Text;
        var textX = (int)(circle.Right + 12 * K);
        TextRenderer.DrawText(g, Caption, _state == StepState.Current && BoldFont != null ? BoldFont : Font,
            new Rectangle(textX, 0, Width - textX, Height), color, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
    }
}
