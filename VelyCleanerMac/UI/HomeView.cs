using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using VelyCleanerMac.Core;

namespace VelyCleanerMac.UI;

public sealed class HomeView : UserControl
{
    readonly List<(CleanItem Item, CheckBox Check, TextBlock Size)> _rows = new();
    readonly TextBlock _ringVal;
    readonly TextBlock _ringLbl;
    readonly Arc _ringFg;
    readonly TextBlock _status;
    readonly Button _analyze;
    readonly Button _clean;
    readonly ProgressBar _progress;
    readonly TextBlock _greet;
    readonly TextBlock _heroMsg;
    readonly TextBlock _lastClean;
    readonly TextBlock _licState;
    readonly TextBlock _diskInfo;
    readonly ProgressBar _diskBar;
    readonly Border _fdaBanner;
    readonly TextBlock _qaUpdateTitle;
    readonly TextBlock _ramInfo;
    readonly TextBlock _sysInfo;

    public Action? GoToLicense, GoToLargeFiles, GoToDuplicates, GoToUninstaller;
    public Func<Task>? OnFreeRam;
    public Func<Task<string?>>? OnCheckUpdate;

    public HomeView(string customerName, AppSettings settings)
    {
        // ---- Tarjeta de saludo ----
        _greet = AppTheme.TituloGrande(Saludo() + ", " + customerName);
        _heroMsg = AppTheme.Texto13("Tu Mac está en buen estado. Analiza para ver cuánto espacio puedes recuperar.", AppTheme.Gris);
        _heroMsg.TextWrapping = TextWrapping.Wrap;
        var heroArt = TryHero();
        var greetGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        var greetText = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Children = { _greet, _heroMsg } };
        greetGrid.Children.Add(greetText);
        if (heroArt is not null) { Grid.SetColumn(heroArt, 1); greetGrid.Children.Add(heroArt); }
        var greetCard = new Border
        {
            CornerRadius = new CornerRadius(14), BorderBrush = AppTheme.Alpha("#5AB0FF", 0.25), BorderThickness = new Thickness(1),
            Padding = new Thickness(20), Child = greetGrid,
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Color.Parse("#15306A"), 0), new GradientStop(Color.Parse("#0F1C44"), 1) },
            },
        };

        // ---- Tarjeta de estado: anillo + 3 datos ----
        _ringVal = new TextBlock { Text = "0 KB", Foreground = AppTheme.Texto, FontSize = 30, FontWeight = FontWeight.ExtraBold, HorizontalAlignment = HorizontalAlignment.Center };
        _ringLbl = AppTheme.Nota("listo para analizar"); _ringLbl.HorizontalAlignment = HorizontalAlignment.Center;
        _ringFg = new Arc { Width = 150, Height = 150, StartAngle = -90, SweepAngle = 0, Stroke = RingBrush(), StrokeThickness = 12, StrokeLineCap = PenLineCap.Round };
        var ringBg = new Ellipse { Width = 150, Height = 150, Stroke = AppTheme.Linea2, StrokeThickness = 12 };
        var ring = new Panel
        {
            Width = 150, Height = 150, HorizontalAlignment = HorizontalAlignment.Center,
            Children = { ringBg, _ringFg, new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Spacing = 2, Children = { _ringVal, _ringLbl } } },
        };

        _diskInfo = AppTheme.Nota("");
        _diskBar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 4, Foreground = AppTheme.Cian, Background = AppTheme.Linea, CornerRadius = new CornerRadius(2) };
        _lastClean = AppTheme.Nota("Todavía no limpiaste con esta app.");
        _licState = AppTheme.Nota("", AppTheme.Verde);

        var metrics = new StackPanel
        {
            Spacing = 12, Margin = new Thickness(0, 14, 0, 0),
            Children =
            {
                Metrica("disk", "Espacio en disco", new StackPanel { Spacing = 4, Children = { _diskInfo, _diskBar } }),
                Metrica("clock", "Última limpieza", _lastClean),
                Metrica("shield", "Licencia", _licState),
            },
        };
        var statusCard = AppTheme.Tarjeta_(new StackPanel { Children = { ring, metrics } });

        // ---- ¿Qué limpiar? ----
        _fdaBanner = BuildFdaBanner();
        var listHeader = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        listHeader.Children.Add(AppTheme.Subtitulo("¿Qué limpiar?"));
        var selAll = AppTheme.BotonSecundario("Seleccionar todo");
        selAll.Height = 28; selAll.FontSize = 12;
        Grid.SetColumn(selAll, 1); listHeader.Children.Add(selAll);

        var list = new StackPanel { Spacing = 8, Margin = new Thickness(0, 10, 0, 0) };
        foreach (var item in MacCleaner.Items)
        {
            var size = new TextBlock { Foreground = AppTheme.Gris, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center, Text = "—" };
            var isDefaultOn = item.Name == "Papelera" ? settings.TrashCheckedByDefault : item.DefaultOn;
            var check = new CheckBox
            {
                IsChecked = isDefaultOn, VerticalAlignment = VerticalAlignment.Center,
                Content = new StackPanel
                {
                    Children =
                    {
                        AppTheme.NombreFila(item.NeedsFullDiskAccess ? item.Name + "  🔒" : item.Name),
                        AppTheme.Nota(item.Description),
                    },
                },
            };
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            row.Children.Add(check);
            Grid.SetColumn(size, 1); row.Children.Add(size);
            list.Children.Add(new Border { Background = AppTheme.Fila, CornerRadius = new CornerRadius(9), Padding = new Thickness(12, 10), Child = row });
            _rows.Add((item, check, size));
        }
        selAll.Click += (_, _) =>
        {
            var all = _rows.All(r => r.Check.IsChecked == true);
            foreach (var r in _rows) r.Check.IsChecked = !all;
        };

        _status = AppTheme.Nota("Pulsa Analizar para empezar.");
        _analyze = AppTheme.BotonSecundario("Analizar", 110);
        _clean = AppTheme.BotonPrincipal("Limpiar ✦", 130);
        _clean.IsEnabled = false;
        var buttonsRow = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(0, 12, 0, 0) };
        buttonsRow.Children.Add(_status);
        Grid.SetColumn(_analyze, 1); buttonsRow.Children.Add(_analyze);
        Grid.SetColumn(_clean, 2); _clean.Margin = new Thickness(8, 0, 0, 0); buttonsRow.Children.Add(_clean);

        _progress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 4, Foreground = AppTheme.Cian, Background = AppTheme.Linea, Margin = new Thickness(0, 10, 0, 0) };

        var listCard = AppTheme.Tarjeta_(new StackPanel { Children = { listHeader, _fdaBanner, list, buttonsRow, _progress } });

        // ---- Accesos rapidos + sistema ----
        _qaUpdateTitle = AppTheme.NombreFila("Buscando actualización…");
        var qaUpdate = QuickAction("check", _qaUpdateTitle, "Comprobando versión…", out var qaUpdateSub);
        qaUpdate.PointerPressed += async (_, _) =>
        {
            if (OnCheckUpdate is null) return;
            var v = await OnCheckUpdate();
            _qaUpdateTitle.Text = v is null ? "Ya tienes la última versión" : $"Descargar versión {v}";
            qaUpdateSub.Text = v is null ? $"Versión {Updates.Current}" : "Haz clic para abrir la descarga";
            if (v is not null) qaUpdate.PointerPressed += (_, _2) => Updates.Open(Updates.DownloadPage);
        };

        var qaRam = QuickAction("chip", "Liberar RAM inactiva", "Calculando…", out var qaRamSub);
        _ramInfo = qaRamSub;
        qaRam.PointerPressed += async (_, _) =>
        {
            if (OnFreeRam is null) return;
            qaRamSub.Text = "Liberando…";
            await OnFreeRam();
            RefreshSystemInfo();
        };

        var qaKey = QuickAction("key", "Cambiar key", "Ver tu licencia", out _);
        qaKey.PointerPressed += (_, _) => GoToLicense?.Invoke();
        var qaBig = QuickAction("files", "Archivos grandes", "Ver qué ocupa espacio", out _);
        qaBig.PointerPressed += (_, _) => GoToLargeFiles?.Invoke();
        var qaDup = QuickAction("copy", "Archivos duplicados", "Buscar copias repetidas", out _);
        qaDup.PointerPressed += (_, _) => GoToDuplicates?.Invoke();
        var qaUnin = QuickAction("box", "Desinstalar apps", "Quitar apps que no usas", out _);
        qaUnin.PointerPressed += (_, _) => GoToUninstaller?.Invoke();

        var quickPanel = new StackPanel
        {
            Spacing = 10,
            Children = { AppTheme.Subtitulo("Accesos rápidos"), qaUpdate, qaRam, qaKey, qaBig, qaDup, qaUnin },
        };

        _sysInfo = AppTheme.Nota("Cargando…");
        var sysCard = AppTheme.Tarjeta_(new StackPanel { Spacing = 8, Children = { AppTheme.Subtitulo("Este Mac"), _sysInfo } });

        var right = new StackPanel { Spacing = 14, Width = 272, Children = { statusCard, AppTheme.Tarjeta_(quickPanel), sysCard } };
        var left = new StackPanel { Spacing = 14, Children = { greetCard, listCard } };

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        body.Children.Add(left);
        Grid.SetColumn(right, 1); right.Margin = new Thickness(14, 0, 0, 0); body.Children.Add(right);

        Content = new ScrollViewer { Content = body, Padding = new Thickness(0, 0, 4, 0) };
        RefreshSystemInfo();
        foreach (var r in _rows) r.Check.IsCheckedChanged += (_, _) => Estimate();

        _analyze.Click += async (_, _) => await Analyze();
        _clean.Click += async (_, _) => await CleanAsync();
    }

    public event Func<Task<bool>>? BeforeClean; // chequea licencia antes de limpiar

    static string Saludo()
    {
        var h = DateTime.Now.Hour;
        return h < 12 ? "Buenos días" : h < 19 ? "Buenas tardes" : "Buenas noches";
    }

    static IBrush RingBrush() => new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
        GradientStops = { new GradientStop(Color.Parse("#38BDF8"), 0), new GradientStop(Color.Parse("#2F6BFF"), 1) },
    };

    static Control? TryHero() => Assets.TryImage("hero-3d.png", 150);

    static Control Metrica(string icon, string label, Control value)
    {
        var box = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(8), Background = AppTheme.Tarjeta2, Child = Icons.Make(icon, 15, AppTheme.Azul2), VerticalAlignment = VerticalAlignment.Top };
        var text = new StackPanel { Margin = new Thickness(10, 0, 0, 0), Spacing = 2, Children = { AppTheme.NombreFila(label), value } };
        Grid.SetColumn(text, 1);
        return new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Children = { box, text } };
    }

    static Border QuickAction(string icon, string title, string sub, out TextBlock subText) => QuickAction(icon, new TextBlock { Text = title, Foreground = AppTheme.Texto, FontWeight = FontWeight.SemiBold, FontSize = 13 }, sub, out subText);

    static Border QuickAction(string icon, TextBlock titleBlock, string sub, out TextBlock subText)
    {
        subText = AppTheme.Nota(sub);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
        grid.Children.Add(new Border { Width = 32, Height = 32, CornerRadius = new CornerRadius(9), Background = AppTheme.Fila, Child = Icons.Make(icon, 15, AppTheme.Azul2) });
        var text = new StackPanel { Margin = new Thickness(10, 0), VerticalAlignment = VerticalAlignment.Center, Children = { titleBlock, subText } };
        Grid.SetColumn(text, 1); grid.Children.Add(text);
        var chev = Icons.Make("chev", 13, AppTheme.Gris2);
        Grid.SetColumn(chev, 2); grid.Children.Add(chev);
        return AppTheme.AccesoRapido(grid);
    }

    Border BuildFdaBanner()
    {
        var open = AppTheme.BotonSecundario("Abrir Configuración"); open.Height = 30; open.FontSize = 12;
        var recheck = AppTheme.BotonSecundario("Ya lo activé, revisar"); recheck.Height = 30; recheck.FontSize = 12;
        open.Click += (_, _) => MacCleaner.OpenFullDiskAccessSettings();
        var banner = new Border
        {
            Background = AppTheme.Alpha("#D9A441", 0.14), BorderBrush = AppTheme.Ambar, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 10), Margin = new Thickness(0, 10, 0, 0),
            IsVisible = !MacCleaner.HasFullDiskAccess(),
            Child = new StackPanel
            {
                Spacing = 8,
                Children =
                {
                    AppTheme.Texto13("🔒 Para vaciar la Papelera y limpiar Mail, dale \"Acceso total al disco\" a VELY157 Cleaner."),
                    new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { open, recheck } },
                },
            },
        };
        recheck.Click += async (_, _) =>
        {
            banner.IsVisible = !MacCleaner.HasFullDiskAccess();
            if (!banner.IsVisible) await Analyze();
        };
        return banner;
    }

    void RefreshSystemInfo()
    {
        _ = Task.Run(() =>
        {
            var mem = Performance.GetMemoryInfo();
            var bat = Performance.GetBatteryInfo();
            var ramLine = $"RAM libre: {MacCleaner.Format(mem.FreeBytes)} de {MacCleaner.Format(mem.TotalBytes)}";
            var sysText = $"{ramLine}\nBatería: {bat.Percent}% · {bat.Condition} ({bat.CycleCount} ciclos)";
            try
            {
                var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.RootDirectory.FullName == "/");
                if (drive is not null)
                {
                    var used = drive.TotalSize - drive.AvailableFreeSpace;
                    var pct = drive.TotalSize == 0 ? 0 : used * 100.0 / drive.TotalSize;
                    Dispatcher.UIThread.Post(() =>
                    {
                        _diskInfo.Text = $"{MacCleaner.Format(used)} usados de {MacCleaner.Format(drive.TotalSize)}";
                        _diskBar.Value = pct;
                    });
                }
            }
            catch (IOException) { }
            Dispatcher.UIThread.Post(() => { _ramInfo.Text = ramLine; _sysInfo.Text = sysText; });
        });
    }

    void Estimate()
    {
        var total = _rows.Where(r => r.Check.IsChecked == true).Sum(r => r.Size.Tag is long b ? b : 0);
        _ringVal.Text = MacCleaner.Format(total);
        _ringLbl.Text = "para liberar";
        _status.Text = $"Puedes liberar unos {MacCleaner.Format(total)}.";
        AnimateRing(total == 0 ? 0 : Math.Min(1.0, total / (10.0 * (1L << 30))));
    }

    void AnimateRing(double fraction) => _ringFg.SweepAngle = 360 * Math.Clamp(fraction, 0, 1);

    async Task Analyze()
    {
        _analyze.IsEnabled = _clean.IsEnabled = false;
        _status.Text = "Analizando tu Mac…";
        var done = 0;
        foreach (var r in _rows)
        {
            r.Size.Text = "…";
            var bytes = await Task.Run(r.Item.Measure);
            r.Size.Tag = bytes;
            r.Size.Text = MacCleaner.Format(bytes);
            r.Size.Foreground = AppTheme.Gris;
            _progress.Value = ++done * 100.0 / _rows.Count;
        }
        _progress.Value = 0;
        Estimate();
        _analyze.IsEnabled = _clean.IsEnabled = true;
    }

    async Task CleanAsync()
    {
        if (BeforeClean is not null && !await BeforeClean()) return;
        var selected = _rows.Where(r => r.Check.IsChecked == true).ToList();
        if (selected.Count == 0) { _status.Text = "No marcaste nada."; return; }
        var ok = await Confirm.AskAsync((Window)VisualRoot!, "¿Limpiar lo seleccionado?", $"Se van a borrar {selected.Count} categorías. Lo que vaya a la Papelera se puede recuperar; lo demás no.", "Limpiar");
        if (!ok) return;

        _analyze.IsEnabled = _clean.IsEnabled = false;
        _ringLbl.Text = "liberado";
        long freed = 0;
        var done = 0;
        foreach (var r in selected)
        {
            _status.Text = $"Limpiando: {r.Item.Name}…";
            var bytes = await Task.Run(r.Item.Clean);
            freed += bytes;
            r.Size.Text = "✓ " + MacCleaner.Format(bytes);
            r.Size.Foreground = AppTheme.Verde;
            _ringVal.Text = MacCleaner.Format(freed);
            _progress.Value = ++done * 100.0 / selected.Count;
        }
        _progress.Value = 0;
        _status.Text = $"¡Listo! Se liberaron {MacCleaner.Format(freed)}.";
        _lastClean.Text = $"Hoy · {MacCleaner.Format(freed)} liberados";
        _analyze.IsEnabled = true;
        RefreshSystemInfo();
    }

    public void Kickoff() => _ = Analyze();

    public void SetLicenseState(bool active, string note) { _licState.Text = note; _licState.Foreground = active ? AppTheme.Verde : AppTheme.Ambar; }
}
