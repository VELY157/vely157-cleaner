namespace VelyCleaner;

public sealed class MainForm : Form, IMessageFilter
{
    readonly LicenseClient _client;
    readonly LicenseToken _token;
    readonly UpdateService _updates = new();

    readonly List<TargetRow> _rows = [];
    readonly Ring _ring;
    readonly FancyButton _analyze, _clean, _update, _gear, _changeKey;
    readonly SmoothProgress _progress;
    readonly Label _status, _countdown;
    readonly Panel _settings;
    readonly System.Windows.Forms.Timer _clock = new() { Interval = 1000 };
    readonly bool _online;
    bool _checking;
    long _lastAnalyzed;

    public bool ChangeKeyRequested { get; private set; }

    public MainForm(LicenseClient client, LicenseResult license)
    {
        _client = client;
        _token = license.Token!;

        Text = "VELY157 Cleaner";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = Palette.Font(10);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(880, 640);
        try { Icon = Images.AppIcon(); } catch { /* ignora */ }

        var bg = new GradientPanel { Dock = DockStyle.Fill, TopColor = Palette.BgTop, BottomColor = Palette.Bg };
        Controls.Add(bg);

        // ---- Barra superior: logo + nombre + reloj + tuerca ----
        _online = license.Online;
        const int headerH = 76;
        var header = new Panel { Dock = DockStyle.Top, Height = headerH, BackColor = Color.Transparent };
        var headerLine = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Palette.CardBorder };
        var logo = new Logo { Size = new Size(150, 48), Location = new Point(22, 14) };

        var name = new Label { BackColor = Color.Transparent, Text = _token.Customer, ForeColor = Palette.Text, Font = Palette.Semibold(12), AutoSize = false, Location = new Point(190, 15), Size = new Size(ClientSize.Width - 190 - 80, 24), AutoEllipsis = true };
        _countdown = new Label { BackColor = Color.Transparent, ForeColor = Palette.Muted, Font = Palette.Font(10), AutoSize = false, Location = new Point(190, 41), Size = new Size(ClientSize.Width - 190 - 80, 20) };

        _gear = new FancyButton { Text = "⚙", Primary = false, Width = 46, Height = 46, Font = new Font("Segoe UI Symbol", 16), Location = new Point(ClientSize.Width - 68, 15) };
        var tips = new ToolTip();
        tips.SetToolTip(_gear, "Opciones");
        _gear.Click += (_, _) => { _settings.Visible = !_settings.Visible; if (_settings.Visible) _settings.BringToFront(); };

        header.Controls.AddRange([logo, name, _countdown, _gear, headerLine]);

        // ---- Menú de la tuerca (se abre y se cierra) ----
        _settings = new Panel { Visible = false, Size = new Size(234, 120), Location = new Point(ClientSize.Width - 256, headerH - 4), BackColor = Color.Transparent };
        var settingsCard = new Card { Dock = DockStyle.Fill, Fill = Color.FromArgb(250, Palette.CardHover), Border = Palette.Blue, Radius = 10 };
        _update = new FancyButton { Text = "Buscar actualización", Primary = false, Width = 210, Height = 42, Location = new Point(12, 12) };
        _changeKey = new FancyButton { Text = "Cambiar key", Primary = false, Width = 210, Height = 42, Location = new Point(12, 62) };
        _changeKey.Click += (_, _) =>
        {
            _settings.Visible = false;
            if (MessageBox.Show(this, "¿Quitar la key de esta PC para usar otra?", "Cambiar key", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            ChangeKeyRequested = true;
            Close();
        };
        settingsCard.Controls.AddRange([_update, _changeKey]);
        _settings.Controls.Add(settingsCard);

        // ---- Cuerpo: lista (izq) + panel con el aro (der) ----
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(22, 8, 22, 18), BackColor = Color.Transparent };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));

        var listCard = new Card { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 11, 0), Padding = new Padding(10) };
        var listTitle = new Label { BackColor = Color.Transparent, Text = "¿Qué limpiar?", Dock = DockStyle.Top, Height = 30, ForeColor = Palette.Text, Font = Palette.Semibold(11), Padding = new Padding(6, 4, 0, 0) };
        var list = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Color.Transparent };
        foreach (var target in Cleaner.Targets)
        {
            var row = new TargetRow(target);
            row.CheckedChanged += (_, _) => UpdateEstimate();
            _rows.Add(row);
            list.Controls.Add(row);
        }
        list.Resize += (_, _) => { foreach (var r in _rows) r.Width = list.ClientSize.Width - 6; };
        listCard.Controls.Add(list);
        listCard.Controls.Add(listTitle);

        var side = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        _ring = new Ring { Size = new Size(230, 230), Big = "—", Small = "para liberar", Anchor = AnchorStyles.Top };
        _status = new Label { BackColor = Color.Transparent, Dock = DockStyle.Bottom, Height = 40, ForeColor = Palette.Muted, Font = Palette.Font(9.5f), TextAlign = ContentAlignment.MiddleCenter, Text = "Analiza para ver cuánto puedes liberar." };
        _progress = new SmoothProgress { Dock = DockStyle.Bottom, Height = 22, Margin = new Padding(0, 0, 0, 8) };
        var buttons = new Panel { Dock = DockStyle.Bottom, Height = 52, BackColor = Color.Transparent };
        _clean = new FancyButton { Text = "Limpiar", Icon = "✦", Width = 150, Height = 44, Anchor = AnchorStyles.Top | AnchorStyles.Right, Enabled = false };
        _analyze = new FancyButton { Text = "Analizar", Primary = false, Width = 130, Height = 44, Anchor = AnchorStyles.Top | AnchorStyles.Left };
        buttons.Controls.AddRange([_analyze, _clean]);
        side.Controls.Add(buttons);
        side.Controls.Add(new Panel { Dock = DockStyle.Bottom, Height = 4, BackColor = Color.Transparent });
        side.Controls.Add(_progress);
        side.Controls.Add(_status);
        side.Controls.Add(_ring);
        side.Layout += (_, _) =>
        {
            _ring.Location = new Point((side.ClientSize.Width - _ring.Width) / 2, Math.Max(6, (side.ClientSize.Height - 118 - _ring.Height) / 2));
            _analyze.Location = new Point(0, 4);
            _clean.Location = new Point(buttons.ClientSize.Width - _clean.Width, 4);
        };

        body.Controls.Add(listCard, 0, 0);
        body.Controls.Add(side, 1, 0);

        bg.Controls.Add(body);
        bg.Controls.Add(header);
        Controls.Add(_settings); // en el formulario, por encima de todo
        _settings.BringToFront();

        _analyze.Click += async (_, _) => await AnalyzeAsync();
        _clean.Click += async (_, _) => await CleanAsync();
        _update.Click += async (_, _) =>
        {
            if (_update.Tag is UpdateInfo) await ApplyUpdateAsync();
            else await ManualCheckAsync();
        };

        // Reloj corriendo en la barra superior.
        UpdateCountdown();
        _clock.Tick += (_, _) => UpdateCountdown();
        _clock.Start();

        // Cerrar el menú de la tuerca al hacer clic fuera.
        Application.AddMessageFilter(this);
        FormClosed += (_, _) => { _clock.Stop(); Application.RemoveMessageFilter(this); };

        Shown += async (_, _) =>
        {
            await AnalyzeAsync();
            await CheckUpdateAsync();
        };
    }

    void UpdateCountdown()
    {
        var rem = _token.ExpiresAt - DateTime.UtcNow;
        if (rem <= TimeSpan.Zero)
        {
            _countdown.Text = "Licencia vencida";
            _countdown.ForeColor = Palette.Bad;
            return;
        }
        var t = rem.TotalDays >= 1
            ? $"Te quedan {(int)rem.TotalDays}d {rem.Hours:00}h {rem.Minutes:00}m {rem.Seconds:00}s"
            : $"Te quedan {(int)rem.TotalHours:00}:{rem.Minutes:00}:{rem.Seconds:00}";
        _countdown.Text = t + (_online ? "" : "   ·   sin conexión");
        _countdown.ForeColor = rem.TotalDays < 2 ? Palette.Warn : Palette.Muted;
    }

    // Solo para capturas: abre el menú y muestra una actualización disponible.
    public void PreviewSettings(bool withUpdate)
    {
        if (withUpdate) OfferUpdate(new UpdateInfo("1.0.1", "", ""));
        _settings.Visible = true;
        _settings.BringToFront();
    }

    // Cierra el menú de la tuerca si se hace clic fuera de él y del botón.
    public bool PreFilterMessage(ref Message m)
    {
        const int WM_LBUTTONDOWN = 0x201;
        if (m.Msg == WM_LBUTTONDOWN && _settings.Visible)
        {
            var p = Cursor.Position;
            var overMenu = _settings.RectangleToScreen(_settings.ClientRectangle).Contains(p);
            var overGear = _gear.RectangleToScreen(_gear.ClientRectangle).Contains(p);
            if (!overMenu && !overGear) _settings.Visible = false;
        }
        return false;
    }

    List<TargetRow> Selected() => _rows.Where(r => r.Checked).ToList();

    void UpdateEstimate()
    {
        var total = _rows.Where(r => r.Checked).Sum(r => r.Bytes);
        _lastAnalyzed = total;
        _ring.Accent = Palette.Blue;
        _ring.Big = Cleaner.Format(total);
        _ring.Small = "para liberar";
        _ring.Value = 0;
        _clean.Enabled = total > 0 || _rows.Any(r => r.Checked);
    }

    async Task AnalyzeAsync()
    {
        SetBusy(true);
        _status.ForeColor = Palette.Muted;
        _status.Text = "Analizando tu PC…";
        _ring.Small = "analizando…";
        var done = 0;
        foreach (var row in _rows)
        {
            row.SizeText = "…";
            var bytes = await Task.Run(row.Target.Measure);
            row.Bytes = bytes;
            _progress.Value = ++done * 100 / _rows.Count;
        }
        _progress.Value = 0;
        UpdateEstimate();
        _status.Text = $"Puedes liberar unos {Cleaner.Format(_lastAnalyzed)}.";
        SetBusy(false);
    }

    async Task CleanAsync()
    {
        // La licencia se vuelve a comprobar justo antes de limpiar.
        if (!_client.StillValid(_token))
        {
            var again = await _client.CheckAsync(_token.Key);
            if (!again.Valid)
            {
                MessageBox.Show(this, again.Message, "Licencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ChangeKeyRequested = true;
                Close();
                return;
            }
        }

        var selected = Selected();
        if (selected.Count == 0) { _status.Text = "No marcaste nada."; return; }
        if (selected.Any(r => r.Target.Name.StartsWith("Vaciar la papelera")) &&
            MessageBox.Show(this, "La papelera se vaciará y esos archivos no se podrán recuperar. ¿Continuar?", "Papelera", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            return;

        SetBusy(true);
        _ring.Accent = Palette.Green;
        _ring.Small = "liberado";
        long freed = 0;
        var done = 0;
        foreach (var row in selected)
        {
            _status.ForeColor = Palette.Muted;
            _status.Text = $"Limpiando: {row.Target.Name}…";
            var bytes = await Task.Run(row.Target.Clean);
            freed += bytes;
            row.MarkCleaned(bytes);
            done++;
            _ring.Big = Cleaner.Format(freed);
            _ring.Value = done / (float)selected.Count;
            _progress.Value = done * 100 / selected.Count;
        }
        _ring.Value = 1f;
        _progress.Value = 0;
        _status.ForeColor = Palette.Green;
        _status.Text = $"¡Listo! Se liberaron {Cleaner.Format(freed)}.";
        SetBusy(false);
        _clean.Enabled = false;
    }

    // Al abrir: busca en silencio. Si hay versión nueva, resalta la tuerca y el botón.
    async Task CheckUpdateAsync()
    {
        var update = await _updates.CheckAsync();
        if (update is not null && !IsDisposed) OfferUpdate(update);
    }

    void OfferUpdate(UpdateInfo update)
    {
        _update.Tag = update;
        _update.Text = $"Actualizar a {update.Version}";
        _update.Primary = true;
        _gear.Primary = true; // la tuerca se pone azul para avisar
    }

    // Cuando el cliente pulsa "Buscar actualización" sin que haya una pendiente.
    async Task ManualCheckAsync()
    {
        if (_checking) return;
        _checking = true;
        _update.Enabled = false;
        _update.Text = "Buscando…";
        var update = await _updates.CheckAsync();
        _update.Enabled = true;
        _checking = false;
        if (update is not null) { OfferUpdate(update); }
        else { _update.Text = "Ya tienes la última versión"; }
    }

    async Task ApplyUpdateAsync()
    {
        if (_update.Tag is not UpdateInfo update) return;
        _update.Enabled = false;
        var progress = new Progress<int>(p => { _update.Text = $"Descargando… {p}%"; });
        _update.Text = "Descargando… 0%";
        var error = await _updates.DownloadAndApplyAsync(update, progress);
        if (error is not null)
        {
            _update.Enabled = true;
            _update.Text = $"Actualizar a {update.Version}";
            MessageBox.Show(this, error, "Actualización", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        _status.Text = "Actualizando… el programa se reiniciará.";
        Application.Exit();
    }

    void SetBusy(bool busy)
    {
        _analyze.Enabled = !busy;
        _clean.Enabled = !busy && _rows.Any(r => r.Checked);
        foreach (var row in _rows) row.Enabled = !busy;
        UseWaitCursor = busy;
    }
}

// Una fila de la lista: casilla + nombre + descripción + tamaño.
public sealed class TargetRow : Card
{
    readonly FancyCheck _check;
    readonly Label _desc;
    readonly Label _size;
    long _bytes;

    public CleanTarget Target { get; }
    public bool Checked => _check.Checked;
    public long Bytes { get => _bytes; set { _bytes = value; SizeText = Target.Name == "Caché DNS" ? "" : Cleaner.Format(value); } }
    public string SizeText { set { _size.Text = value; _size.ForeColor = Palette.Muted; } }
    public event EventHandler? CheckedChanged;

    public TargetRow(CleanTarget target)
    {
        Target = target;
        Height = 58;
        Margin = new Padding(3, 3, 3, 5);
        Fill = Color.FromArgb(150, 20, 34, 68);
        Radius = 9;

        _check = new FancyCheck { Text = target.Name, Checked = target.DefaultOn, Font = Palette.Semibold(10), Location = new Point(12, 8), AutoSize = false, Size = new Size(260, 24) };
        _check.CheckedChanged += (_, e) => CheckedChanged?.Invoke(this, e);
        _desc = new Label { BackColor = Color.Transparent, Text = target.Description, ForeColor = Palette.Muted, Font = Palette.Font(8.5f), AutoSize = false, Location = new Point(34, 31), Size = new Size(300, 20), AutoEllipsis = true };
        _size = new Label { BackColor = Color.Transparent, Text = "", ForeColor = Palette.Muted, Font = Palette.Semibold(10), TextAlign = ContentAlignment.MiddleRight, AutoSize = false, Anchor = AnchorStyles.Top | AnchorStyles.Right };

        Controls.AddRange([_check, _desc, _size]);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
        base.OnSizeChanged(e);
        if (_size is null) return; // aún en el constructor
        _size.Bounds = new Rectangle(Width - 108, 6, 96, Height - 12);
        _desc.Width = Width - 46 - 100;
        _check.Width = Width - 120;
    }

    public void MarkCleaned(long bytes)
    {
        _size.Text = "✓ " + Cleaner.Format(bytes);
        _size.ForeColor = Palette.Green;
        _check.Enabled = false;
    }
}
