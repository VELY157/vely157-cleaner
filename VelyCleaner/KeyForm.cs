namespace VelyCleaner;

public sealed class KeyForm : Form
{
    readonly LicenseClient _client;
    readonly InputBox _key;
    readonly FancyButton _activate, _trial;
    readonly Label _message;

    public LicenseResult? Result { get; private set; }

    public KeyForm(LicenseClient client, string? message)
    {
        _client = client;
        Text = "VELY157 Cleaner";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        Font = Palette.Font(10);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(480, 600);
        try { Icon = Images.AppIcon(); } catch { /* ignora */ }

        var bg = new GradientPanel { Dock = DockStyle.Fill, TopColor = Palette.BgTop, BottomColor = Palette.Bg, Waves = true };
        Controls.Add(bg);

        var logo = new Logo { Size = new Size(260, 150), Location = new Point((ClientSize.Width - 260) / 2, 34) };

        var subtitle = new Label
        { BackColor = Color.Transparent,
            Text = "Necesitas una licencia. Pruébalo 7 días gratis, sin tarjeta.",
            ForeColor = Palette.Muted, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(ClientSize.Width - 72, 24), Location = new Point(36, 196),
        };

        _trial = new FancyButton { Text = "Empezar 7 días gratis", Icon = "✦", Width = ClientSize.Width - 72, Height = 46, Location = new Point(36, 232) };
        _trial.Click += async (_, _) => await TrialAsync();

        var or = new Label
        { BackColor = Color.Transparent,
            Text = "¿Ya tienes una key? Escríbela aquí:",
            ForeColor = Palette.Muted, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(ClientSize.Width - 72, 24), Location = new Point(36, 296),
        };

        _key = new InputBox { Width = ClientSize.Width - 72, Location = new Point(36, 326) };
        _key.Box.CharacterCasing = CharacterCasing.Upper;
        _key.Box.Font = new Font("Consolas", 14);
        _key.Box.MaxLength = 40;
        _key.Box.PlaceholderText = "VELY-XXXXX-XXXXX-XXXXX-XXXXX";
        _key.Box.TextAlign = HorizontalAlignment.Center;

        _activate = new FancyButton { Text = "Activar", Primary = false, Width = ClientSize.Width - 72, Height = 46, Location = new Point(36, 386) };
        _activate.Click += async (_, _) => await ActivateAsync();

        _message = new Label
        { BackColor = Color.Transparent,
            Text = message ?? "", ForeColor = Palette.Bad, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(ClientSize.Width - 72, 48), Location = new Point(36, 444),
        };

        var buy = new Label
        { BackColor = Color.Transparent,
            Text = "Comprar licencia por WhatsApp",
            ForeColor = Palette.BlueLight, Font = Palette.Semibold(10), Cursor = Cursors.Hand,
            AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(ClientSize.Width - 72, 24), Location = new Point(36, ClientSize.Height - 58),
        };
        buy.Click += (_, _) => OpenBuyLink();

        bg.Controls.AddRange([logo, subtitle, _trial, or, _key, _activate, _message, buy]);
        AcceptButton = null;
        _key.Box.KeyDown += async (_, e) => { if (e.KeyCode == Keys.Enter) await ActivateAsync(); };
        Shown += (_, _) => _key.Box.Focus();
    }

    async Task TrialAsync()
    {
        _trial.Enabled = false;
        _trial.Text = "Activando prueba…";
        _message.Text = "";
        var result = await _client.StartTrialAsync();
        _trial.Enabled = true;
        _trial.Text = "Empezar 7 días gratis";

        if (!result.Valid) { _message.ForeColor = Palette.Bad; _message.Text = result.Message; return; }
        Result = result;
        DialogResult = DialogResult.OK;
    }

    // Mismo WhatsApp que la página web (velymediallc-web/index.html → CONTACTO.whatsapp).
    static void OpenBuyLink()
    {
        var text = Uri.EscapeDataString($"Hola Vely Media, quiero comprar una licencia de VELY157 Cleaner para Windows ({Environment.MachineName}).");
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo($"https://wa.me/18093700254?text={text}") { UseShellExecute = true }); }
        catch { /* sin navegador */ }
    }

    async Task ActivateAsync()
    {
        var key = _key.Box.Text.Trim();
        if (key.Length == 0) { _message.ForeColor = Palette.Bad; _message.Text = "Escribe tu key."; return; }

        _activate.Enabled = false;
        _activate.Text = "Verificando…";
        _message.Text = "";
        var result = await _client.CheckAsync(key);
        _activate.Enabled = true;
        _activate.Text = "Activar";

        if (!result.Valid) { _message.ForeColor = Palette.Bad; _message.Text = result.Message; return; }
        Result = result;
        DialogResult = DialogResult.OK;
    }
}
