using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using VelyCleanerMac.Core;

namespace VelyCleanerMac;

public sealed class MainWindow : Window
{
    static readonly IBrush Bg = Brush("#0A1430");
    static readonly IBrush Card = Brush("#13224A");
    static readonly IBrush Line = Brush("#26407A");
    static readonly IBrush Blue = Brush("#1E88FF");
    static readonly IBrush Text = Brush("#EAF2FF");
    static readonly IBrush Muted = Brush("#96A8CA");
    static readonly IBrush Green = Brush("#22C55E");
    static readonly IBrush Red = Brush("#F87171");

    readonly LicenseClient _client = new();
    LicenseToken? _token;
    bool _online;
    DispatcherTimer? _clock;

    public MainWindow()
    {
        Title = "VELY157 Cleaner";
        Width = 880;
        Height = 640;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = Bg;
        Content = Center(new TextBlock { Text = "Verificando licencia…", Foreground = Muted, FontSize = 16 });
        Opened += async (_, _) =>
        {
            if (_client.SavedKey is { } key)
            {
                var r = await _client.CheckAsync(key);
                if (r.Valid) { ShowMain(r); return; }
                ShowActivation(r.Message);
            }
            else ShowActivation(null);
        };
    }

    // ---------- Activación ----------
    void ShowActivation(string? message)
    {
        _clock?.Stop();
        var keyBox = new TextBox
        {
            Watermark = "VELY-XXXXX-XXXXX-XXXXX-XXXXX",
            FontFamily = new FontFamily("Menlo, Consolas, monospace"),
            FontSize = 18,
            Width = 420,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        var msg = new TextBlock { Text = message ?? "", Foreground = Red, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, Width = 420 };
        var trial = PrimaryButton("Empezar 7 días gratis", 420);
        trial.Click += async (_, _) =>
        {
            trial.IsEnabled = false;
            trial.Content = "Activando prueba…";
            msg.Text = "";
            var r = await _client.StartTrialAsync();
            trial.IsEnabled = true;
            trial.Content = "Empezar 7 días gratis";
            if (r.Valid) ShowMain(r); else msg.Text = r.Message;
        };
        var buy = SecondaryButton("Comprar licencia por WhatsApp", 420);
        // Mismo WhatsApp que la página web (velymediallc-web/index.html → CONTACTO.whatsapp).
        buy.Click += (_, _) => Updates.Open("https://wa.me/18093700254?text=" + Uri.EscapeDataString(
            $"Hola Vely Media, quiero comprar una licencia de VELY157 Cleaner para Mac ({Environment.MachineName})."));
        var activate = SecondaryButton("Activar", 420);
        activate.Click += async (_, _) =>
        {
            var key = (keyBox.Text ?? "").Trim().ToUpperInvariant();
            if (key.Length == 0) { msg.Text = "Escribe tu key."; return; }
            activate.IsEnabled = false;
            activate.Content = "Verificando…";
            msg.Text = "";
            var r = await _client.CheckAsync(key);
            activate.IsEnabled = true;
            activate.Content = "Activar";
            if (r.Valid) ShowMain(r); else msg.Text = r.Message;
        };

        Content = Center(new StackPanel
        {
            Spacing = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            Children =
            {
                Logo(44),
                new TextBlock { Text = "Necesitas una licencia. Pruébalo 7 días gratis, sin tarjeta.", Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center },
                trial,
                new TextBlock { Text = "¿Ya tienes una key? Escríbela aquí:", Foreground = Muted, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center },
                keyBox,
                activate,
                msg,
                buy,
            },
        });
    }

    // ---------- Panel principal ----------
    void ShowMain(LicenseResult license)
    {
        _token = license.Token!;
        _online = license.Online;

        var name = new TextBlock { Text = _token.Customer, Foreground = Text, FontSize = 17, FontWeight = FontWeight.SemiBold };
        var countdown = new TextBlock { Foreground = Muted, FontSize = 13 };
        var updateBtn = SecondaryButton("Buscar actualización", 210);
        var changeKey = SecondaryButton("Cambiar key", 210);
        var menu = new Border
        {
            Background = Card, BorderBrush = Blue, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10), IsVisible = false, HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 70, 22, 0), ZIndex = 10,
            Child = new StackPanel { Spacing = 8, Children = { updateBtn, changeKey } },
        };
        var gear = SecondaryButton("⚙", 46);
        gear.FontSize = 20;
        gear.Click += (_, _) => menu.IsVisible = !menu.IsVisible;

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            Margin = new Thickness(22, 14, 22, 12),
        };
        var who = new StackPanel { Spacing = 2, Margin = new Thickness(24, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Children = { name, countdown } };
        header.Children.Add(Logo(22));
        Grid.SetColumn(who, 1); header.Children.Add(who);
        Grid.SetColumn(gear, 2); header.Children.Add(gear);

        // Lista de cosas a limpiar
        var rows = new List<(CleanItem Item, CheckBox Check, TextBlock Size)>();
        var list = new StackPanel { Spacing = 8 };
        foreach (var item in MacCleaner.Items)
        {
            var check = new CheckBox { IsChecked = item.DefaultOn, Content = new StackPanel
            {
                Children =
                {
                    new TextBlock { Text = item.Name, Foreground = Text, FontWeight = FontWeight.SemiBold },
                    new TextBlock { Text = item.Description, Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, MaxWidth = 330 },
                },
            } };
            var size = new TextBlock { Foreground = Muted, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            row.Children.Add(check);
            Grid.SetColumn(size, 1); row.Children.Add(size);
            list.Children.Add(new Border { Background = Brush("#162A58"), CornerRadius = new CornerRadius(9), Padding = new Thickness(12, 8), Child = row });
            rows.Add((item, check, size));
        }

        var big = new TextBlock { Text = "—", Foreground = Text, FontSize = 40, FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center };
        var small = new TextBlock { Text = "para liberar", Foreground = Muted, FontSize = 14, HorizontalAlignment = HorizontalAlignment.Center };
        var ring = new Border
        {
            Width = 230, Height = 230, CornerRadius = new CornerRadius(115), BorderBrush = Blue, BorderThickness = new Thickness(10),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { big, small } },
        };
        var progress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 10, Foreground = Blue };
        var status = new TextBlock { Text = "Analizando tu Mac…", Foreground = Muted, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap };
        var analyze = SecondaryButton("Analizar", 130);
        var clean = PrimaryButton("Limpiar  ✦", 150);
        var buttons = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        buttons.Children.Add(analyze);
        Grid.SetColumn(clean, 2); buttons.Children.Add(clean);

        var side = new DockPanel { Margin = new Thickness(12, 0, 0, 0) };
        var bottom = new StackPanel { Spacing = 10, Children = { buttons, progress, status } };
        DockPanel.SetDock(bottom, Dock.Bottom);
        side.Children.Add(bottom);
        side.Children.Add(new Panel { Children = { ring } , VerticalAlignment = VerticalAlignment.Center });

        var listCard = new Border
        {
            Background = Card, CornerRadius = new CornerRadius(12), BorderBrush = Line, BorderThickness = new Thickness(1), Padding = new Thickness(10),
            Child = new DockPanel
            {
                Children =
                {
                    Docked(new TextBlock { Text = "¿Qué limpiar?", Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 15, Margin = new Thickness(4, 2, 0, 10) }, Dock.Top),
                    new ScrollViewer { Content = list },
                },
            },
        };

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("56*,44*"), Margin = new Thickness(22, 4, 22, 18) };
        body.Children.Add(listCard);
        Grid.SetColumn(side, 1); body.Children.Add(side);

        var layout = new DockPanel();
        var headerWrap = new Border { BorderBrush = Line, BorderThickness = new Thickness(0, 0, 0, 1), Child = header };
        DockPanel.SetDock(headerWrap, Dock.Top);
        layout.Children.Add(headerWrap);
        layout.Children.Add(body);

        Content = new Panel { Children = { layout, menu } };

        // ---- Lógica ----
        void Estimate()
        {
            var total = rows.Where(r => r.Check.IsChecked == true).Sum(r => r.Size.Tag is long b ? b : 0);
            big.Text = MacCleaner.Format(total);
            small.Text = "para liberar";
            ring.BorderBrush = Blue;
            status.Text = $"Puedes liberar unos {MacCleaner.Format(total)}.";
        }
        foreach (var r in rows) r.Check.IsCheckedChanged += (_, _) => Estimate();

        async Task Analyze()
        {
            analyze.IsEnabled = clean.IsEnabled = false;
            status.Text = "Analizando tu Mac…";
            var done = 0;
            foreach (var r in rows)
            {
                r.Size.Text = "…";
                var bytes = await Task.Run(r.Item.Measure);
                r.Size.Tag = bytes;
                r.Size.Text = MacCleaner.Format(bytes);
                r.Size.Foreground = Muted;
                progress.Value = ++done * 100.0 / rows.Count;
            }
            progress.Value = 0;
            Estimate();
            analyze.IsEnabled = clean.IsEnabled = true;
        }

        async Task Clean()
        {
            if (_token is null) return;
            if (!_client.StillValid(_token))
            {
                var again = await _client.CheckAsync(_token.Key);
                if (!again.Valid) { ShowActivation(again.Message); return; }
            }
            var selected = rows.Where(r => r.Check.IsChecked == true).ToList();
            if (selected.Count == 0) { status.Text = "No marcaste nada."; return; }
            analyze.IsEnabled = clean.IsEnabled = false;
            ring.BorderBrush = Green;
            small.Text = "liberado";
            long freed = 0;
            var done = 0;
            foreach (var r in selected)
            {
                status.Text = $"Limpiando: {r.Item.Name}…";
                var bytes = await Task.Run(r.Item.Clean);
                freed += bytes;
                r.Size.Text = "✓ " + MacCleaner.Format(bytes);
                r.Size.Foreground = Green;
                big.Text = MacCleaner.Format(freed);
                progress.Value = ++done * 100.0 / selected.Count;
            }
            progress.Value = 0;
            status.Text = $"¡Listo! Se liberaron {MacCleaner.Format(freed)}.";
            analyze.IsEnabled = true;
        }

        analyze.Click += async (_, _) => await Analyze();
        clean.Click += async (_, _) => await Clean();

        string? pendingVersion = null;
        updateBtn.Click += async (_, _) =>
        {
            if (pendingVersion is not null) { Updates.Open(Updates.DownloadPage); menu.IsVisible = false; return; }
            updateBtn.Content = "Buscando…";
            pendingVersion = await Updates.NewerVersionAsync();
            updateBtn.Content = pendingVersion is null ? "Ya tienes la última versión" : $"Descargar versión {pendingVersion}";
        };
        changeKey.Click += (_, _) =>
        {
            menu.IsVisible = false;
            _client.Clear();
            ShowActivation(null);
        };

        // Reloj corriendo
        void Tick()
        {
            var rem = _token!.ExpiresAt - DateTime.UtcNow;
            if (rem <= TimeSpan.Zero) { countdown.Text = "Licencia vencida"; countdown.Foreground = Red; return; }
            countdown.Text = (rem.TotalDays >= 1
                ? $"Te quedan {(int)rem.TotalDays}d {rem.Hours:00}h {rem.Minutes:00}m {rem.Seconds:00}s"
                : $"Te quedan {(int)rem.TotalHours:00}:{rem.Minutes:00}:{rem.Seconds:00}") + (_online ? "" : "   ·   sin conexión");
        }
        Tick();
        _clock?.Stop();
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => Tick();
        _clock.Start();

        _ = Analyze();
        _ = Task.Run(async () =>
        {
            var v = await Updates.NewerVersionAsync();
            if (v is null) return;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                pendingVersion = v;
                updateBtn.Content = $"Descargar versión {v}";
                gear.Background = Blue;
            });
        });
    }

    // ---------- Piezas ----------
    static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));

    static Control Center(Control c) => new Panel { Children = { c }, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

    static Control Docked(Control c, Dock d) { DockPanel.SetDock(c, d); return c; }

    static Control Logo(double size) => new StackPanel
    {
        Spacing = 0,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Children =
        {
            new TextBlock { Text = "VELY157", Foreground = Text, FontSize = size, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center },
            new TextBlock { Text = "C L E A N E R", Foreground = Brush("#38BDF8"), FontSize = size * 0.38, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center },
        },
    };

    static Button PrimaryButton(string text, double width) => new()
    {
        Content = text, Width = width, Height = 44, Background = Blue, Foreground = Brushes.White,
        FontWeight = FontWeight.SemiBold, CornerRadius = new CornerRadius(8),
        HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
    };

    static Button SecondaryButton(string text, double width) => new()
    {
        Content = text, Width = width, Height = 44, Background = Brush("#18264A"), Foreground = Text,
        BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8),
        HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
    };
}
