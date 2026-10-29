using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using VelyCleanerMac.Core;
using VelyCleanerMac.UI;

namespace VelyCleanerMac;

public sealed class MainWindow : Window
{
    readonly LicenseClient _client = new();
    readonly AppSettings _settings = AppSettings.Load();
    LicenseToken? _token;
    bool _online;
    DispatcherTimer? _clock;

    Sidebar? _sidebar;
    HomeView? _home;
    LicenseView? _licenseView;
    readonly Dictionary<Section, Control> _views = new();
    ContentControl? _viewHost;

    public MainWindow()
    {
        Title = "VELY157 Cleaner";
        Width = 1120;
        Height = 720;
        MinWidth = 980;
        MinHeight = 660;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        Background = AppTheme.Fondo;
        ExtendClientAreaToDecorationsHint = true;
        ExtendClientAreaChromeHints = Avalonia.Platform.ExtendClientAreaChromeHints.PreferSystemChrome;
        ExtendClientAreaTitleBarHeightHint = 44;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None };
        TryApplyIcon();

        Content = Center(new TextBlock { Text = "Verificando licencia…", Foreground = AppTheme.Gris, FontSize = 15 });
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

    void TryApplyIcon()
    {
        try
        {
            var path = System.IO.Path.Combine(AppContext.BaseDirectory, "AppIcon.icns");
            // En macOS el icono del Dock lo pone el bundle (.app/Contents/Resources). Esto
            // solo cubre el icono de la ventana cuando Avalonia corre fuera de un bundle.
            var uri = new Uri("avares://VelyCleanerMac/Assets/logomark.png");
            if (AssetLoader.Exists(uri))
                Icon = new WindowIcon(AssetLoader.Open(uri));
        }
        catch (Exception e) when (e is IOException or NotSupportedException) { }
    }

    static Control Center(Control c) => new Panel { Children = { c }, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

    // ---------- Activación ----------
    void ShowActivation(string? message)
    {
        _clock?.Stop();
        var view = new ActivationView(message);
        view.OnTrial = async () =>
        {
            var r = await _client.StartTrialAsync();
            if (r.Valid) ShowMain(r); else view.SetError(r.Message);
        };
        view.OnActivate = async key =>
        {
            var r = await _client.CheckAsync(key);
            if (r.Valid) ShowMain(r); else view.SetError(r.Message);
        };
        Content = view;
    }

    // ---------- App principal ----------
    void ShowMain(LicenseResult license)
    {
        _token = license.Token!;
        _online = license.Online;

        _sidebar = new Sidebar();
        _home = new HomeView(_token.Customer, _settings)
        {
            GoToLicense = () => _sidebar!.Select(Section.License),
            GoToLargeFiles = () => _sidebar!.Select(Section.LargeFiles),
            GoToDuplicates = () => _sidebar!.Select(Section.Duplicates),
            GoToUninstaller = () => _sidebar!.Select(Section.Uninstaller),
            OnFreeRam = () => Task.Run(() => Performance.FreeInactiveMemory()),
            OnCheckUpdate = () => Updates.NewerVersionAsync(),
        };
        _home.BeforeClean += async () =>
        {
            if (_client.StillValid(_token)) return true;
            var again = await _client.CheckAsync(_token.Key);
            if (again.Valid) { _token = again.Token!; _online = again.Online; return true; }
            ShowActivation(again.Message);
            return false;
        };

        var cleaning = new CleaningView();
        var apps = new AppsView();
        var largeFiles = new LargeFilesView();
        var duplicates = new DuplicatesView();
        var uninstaller = new UninstallerView();
        _licenseView = new LicenseView { OnChangeKey = () => { _client.Clear(); ShowActivation(null); } };
        _licenseView.SetToken(_token, _online);
        var settings = new SettingsView(_settings)
        {
            OnRemoveKey = () => { _client.Clear(); ShowActivation(null); },
            OnCheckUpdate = () => Updates.NewerVersionAsync(),
        };

        _views[Section.Home] = _home;
        _views[Section.Cleaning] = cleaning;
        _views[Section.Apps] = apps;
        _views[Section.LargeFiles] = largeFiles;
        _views[Section.Duplicates] = duplicates;
        _views[Section.Uninstaller] = uninstaller;
        _views[Section.License] = _licenseView;
        _views[Section.Settings] = settings;

        _viewHost = new ContentControl { Padding = new Thickness(22, 54, 22, 20) };
        _sidebar.SectionChanged = section => Navigate(section);

        var layout = new Grid { ColumnDefinitions = new ColumnDefinitions("232,*") };
        layout.Children.Add(_sidebar);
        Grid.SetColumn(_viewHost, 1); layout.Children.Add(_viewHost);
        Content = layout;

        Navigate(Section.Home);
        if (_settings.AnalyzeOnOpen) _home.Kickoff();

        Tick();
        _clock?.Stop();
        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => Tick();
        _clock.Start();

        if (_settings.CheckUpdatesOnOpen)
            _ = Task.Run(async () =>
            {
                var v = await Updates.NewerVersionAsync();
                if (v is null) return;
                await Dispatcher.UIThread.InvokeAsync(() => { /* el aviso vive en Accesos rapidos / Configuracion */ });
            });
    }

    void Navigate(Section section)
    {
        if (_viewHost is null || !_views.TryGetValue(section, out var view)) return;
        if (!AppTheme.ReduceMotion)
        {
            view.Opacity = 0;
            view.RenderTransform = new TranslateTransform(0, 6);
            view.Transitions = new Avalonia.Animation.Transitions
            {
                new Avalonia.Animation.DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(220), Easing = AppTheme.SalidaFuerte },
            };
        }
        _viewHost.Content = view;
        if (!AppTheme.ReduceMotion)
            Dispatcher.UIThread.Post(() => { view.Opacity = 1; view.RenderTransform = new TranslateTransform(0, 0); }, DispatcherPriority.Loaded);
    }

    void Tick()
    {
        if (_token is null) return;
        var rem = _token.ExpiresAt - DateTime.UtcNow;
        var expiring = rem.TotalDays < 3;
        var text = rem <= TimeSpan.Zero
            ? "Licencia vencida"
            : (rem.TotalDays >= 1
                ? $"Quedan {(int)rem.TotalDays}d {rem.Hours:00}h {rem.Minutes:00}m"
                : $"Quedan {(int)rem.TotalHours:00}:{rem.Minutes:00}:{rem.Seconds:00}") + (_online ? "" : " · sin conexión");
        _sidebar?.SetFooter(_token.Customer, text, _online, expiring);
        _home?.SetLicenseState(rem > TimeSpan.Zero, expiring ? $"● Vence pronto · quedan {(int)rem.TotalDays} días" : $"● Activa · quedan {(int)rem.TotalDays} días");
        _licenseView?.Tick();
        if (rem <= TimeSpan.Zero) { _clock?.Stop(); ShowActivation("Tu licencia venció."); }
    }
}
