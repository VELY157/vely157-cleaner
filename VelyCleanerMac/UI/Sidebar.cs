using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace VelyCleanerMac.UI;

public enum Section { Home, Cleaning, Apps, LargeFiles, Duplicates, Uninstaller, License, Settings }

public sealed class Sidebar : UserControl
{
    const double RowHeight = 40;

    readonly Dictionary<Section, Button> _buttons = new();
    readonly Dictionary<Section, Border> _badges = new();
    readonly Border _pill;
    readonly Canvas _navCanvas;
    readonly TextBlock _footName;
    readonly TextBlock _footClock;
    readonly Border _dot;

    public Action<Section>? SectionChanged;
    Section _current = Section.Home;

    static readonly (Section Section, string Icon, string Label)[] Items =
    [
        (Section.Home, "home", "Inicio"),
        (Section.Cleaning, "broom", "Limpieza"),
        (Section.Apps, "grid", "Aplicaciones"),
        (Section.LargeFiles, "files", "Archivos grandes"),
        (Section.Duplicates, "copy", "Duplicados"),
        (Section.Uninstaller, "box", "Desinstalador"),
    ];
    static readonly (Section Section, string Icon, string Label)[] ItemsBottom =
    [
        (Section.License, "key", "Licencia"),
        (Section.Settings, "gear", "Configuración"),
    ];

    public Sidebar()
    {
        Width = 232;

        var logo = new StackPanel
        {
            Margin = new Thickness(20, 50, 0, 18),
            Children =
            {
                new TextBlock { Text = "VELY157", Foreground = AppTheme.Texto, FontSize = 17, FontWeight = FontWeight.ExtraBold },
                new TextBlock { Text = "C L E A N E R", Foreground = AppTheme.Cian, FontSize = 10, FontWeight = FontWeight.Bold },
            },
        };

        _pill = new Border
        {
            Background = AppTheme.Azul, CornerRadius = new CornerRadius(9), Height = RowHeight,
            Width = 200, HorizontalAlignment = HorizontalAlignment.Left,
        };
        if (!AppTheme.ReduceMotion)
            _pill.Transitions = new Transitions
            {
                new DoubleTransition { Property = Canvas.TopProperty, Duration = TimeSpan.FromMilliseconds(320), Easing = AppTheme.SalidaFuerte },
            };
        Canvas.SetLeft(_pill, 0);
        Canvas.SetTop(_pill, 0);

        _navCanvas = new Canvas { Width = 200 };
        _navCanvas.Children.Add(_pill);

        var navStack = new StackPanel { Spacing = 2 };
        double y = 0;
        foreach (var it in Items) { navStack.Children.Add(MakeRow(it.Section, it.Icon, it.Label)); y += RowHeight + 2; }
        navStack.Children.Add(new Border { Height = 1, Background = AppTheme.Linea, Margin = new Thickness(4, 10) });
        foreach (var it in ItemsBottom) navStack.Children.Add(MakeRow(it.Section, it.Icon, it.Label));

        var navHost = new Panel { Children = { _navCanvas, navStack } };

        _dot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = AppTheme.Verde, VerticalAlignment = VerticalAlignment.Center };
        _footName = new TextBlock { Text = "Cliente", Foreground = AppTheme.Texto, FontSize = 13, FontWeight = FontWeight.SemiBold };
        _footClock = new TextBlock { Text = "", Foreground = AppTheme.Gris, FontSize = 11 };
        var foot = new Border
        {
            BorderBrush = AppTheme.Linea, BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(20, 14),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal, Spacing = 8,
                Children = { _dot, new StackPanel { Children = { _footName, _footClock } } },
            },
        };

        var layout = new DockPanel();
        DockPanel.SetDock(logo, Dock.Top);
        DockPanel.SetDock(foot, Dock.Bottom);
        layout.Children.Add(logo);
        layout.Children.Add(foot);
        layout.Children.Add(new Border { Margin = new Thickness(16, 0), Child = navHost });

        Content = new Border { Background = AppTheme.Lateral, Child = layout };
        Select(Section.Home, animate: false);
    }

    Button MakeRow(Section section, string icon, string label)
    {
        var text = new TextBlock { Text = label, Foreground = AppTheme.Texto, FontSize = 13.5, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        var badge = new Border
        {
            Background = AppTheme.Ambar, Padding = new Thickness(6, 2), CornerRadius = new CornerRadius(999),
            IsVisible = false, VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock { Text = "Vence pronto", Foreground = Brushes.Black, FontSize = 9.5, FontWeight = FontWeight.Bold },
        };
        _badges[section] = badge;
        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(12, 0),
            Children = { Icons.Make(icon, 16, AppTheme.Texto), text, badge },
        };
        var btn = new Button
        {
            Content = row, Height = RowHeight, Background = Brushes.Transparent, BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand), Margin = new Thickness(0, 0, 0, 2),
        };
        btn.Click += (_, _) => Select(section, animate: true);
        _buttons[section] = btn;
        return btn;
    }

    public void Select(Section section, bool animate = true)
    {
        _current = section;
        var idx = Array.FindIndex(Items, i => i.Section == section);
        double top;
        if (idx >= 0) top = idx * (RowHeight + 2);
        else
        {
            var idx2 = Array.FindIndex(ItemsBottom, i => i.Section == section);
            top = Items.Length * (RowHeight + 2) + 10 + 1 + idx2 * (RowHeight + 2);
        }
        if (!animate) { var t = _pill.Transitions; _pill.Transitions = null; Canvas.SetTop(_pill, top); _pill.Transitions = t; }
        else Canvas.SetTop(_pill, top);
        SectionChanged?.Invoke(section);
    }

    public void SetBadge(Section section, bool show) { if (_badges.TryGetValue(section, out var b)) b.IsVisible = show; }

    public void SetFooter(string name, string clockText, bool online, bool expiring)
    {
        _footName.Text = name;
        _footClock.Text = clockText;
        _dot.Background = expiring ? AppTheme.Ambar : AppTheme.Verde;
        SetBadge(Section.License, expiring);
    }
}
