using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using VelyCleanerMac.Core;

namespace VelyCleanerMac;

public sealed class AppUninstallerWindow : Window
{
    static readonly IBrush Bg = Brush("#0A1430");
    static readonly IBrush Card = Brush("#13224A");
    static readonly IBrush Line = Brush("#26407A");
    static readonly IBrush Blue = Brush("#1E88FF");
    static readonly IBrush Text = Brush("#EAF2FF");
    static readonly IBrush Muted = Brush("#96A8CA");
    static readonly IBrush Green = Brush("#22C55E");
    static readonly IBrush Red = Brush("#F87171");

    readonly StackPanel _body = new() { Spacing = 10, Margin = new Thickness(18) };
    readonly TextBlock _title;

    public AppUninstallerWindow()
    {
        Title = "Desinstalar apps";
        Width = 600;
        Height = 640;
        Background = Bg;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _title = new TextBlock { Text = "Desinstalar apps", Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 16, Margin = new Thickness(18, 16, 18, 0) };
        var layout = new DockPanel();
        DockPanel.SetDock(_title, Dock.Top);
        layout.Children.Add(_title);
        layout.Children.Add(new ScrollViewer { Content = _body });
        Content = layout;

        Opened += (_, _) => ShowAppList();
    }

    void ShowAppList()
    {
        _title.Text = "Desinstalar apps";
        _body.Children.Clear();
        _body.Children.Add(new TextBlock
        {
            Text = "Elegí una app. Te voy a mostrar qué restos deja en tu Mac antes de borrar nada.",
            Foreground = Muted, FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6),
        });
        var loading = new TextBlock { Text = "Buscando apps instaladas…", Foreground = Muted };
        _body.Children.Add(loading);

        _ = Task.Run(() =>
        {
            var apps = AppUninstaller.ListApps();
            Dispatcher.UIThread.Post(() =>
            {
                _body.Children.Remove(loading);
                foreach (var app in apps)
                {
                    var row = new Button
                    {
                        Content = new TextBlock { Text = app.Name, Foreground = Text, FontWeight = FontWeight.SemiBold },
                        Background = Card, BorderBrush = Line, BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(9), Padding = new Thickness(12, 10),
                        HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left,
                    };
                    row.Click += (_, _) => ShowLeftovers(app);
                    _body.Children.Add(row);
                }
            });
        });
    }

    void ShowLeftovers(InstalledApp app)
    {
        _title.Text = $"Desinstalar {app.Name}";
        _body.Children.Clear();
        var back = new TextBlock { Text = "‹ Volver a la lista", Foreground = Blue, FontSize = 12, Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand) };
        back.PointerPressed += (_, _) => ShowAppList();
        _body.Children.Add(back);

        var loading = new TextBlock { Text = "Buscando restos en tu Library…", Foreground = Muted, Margin = new Thickness(0, 10, 0, 0) };
        _body.Children.Add(loading);

        _ = Task.Run(() =>
        {
            var leftovers = AppUninstaller.FindLeftovers(app);
            Dispatcher.UIThread.Post(() =>
            {
                _body.Children.Remove(loading);
                var checks = new List<(LeftoverItem Item, CheckBox Check)>();

                var appRow = new Border
                {
                    Background = Card, CornerRadius = new CornerRadius(9), BorderBrush = Blue, BorderThickness = new Thickness(1),
                    Padding = new Thickness(12, 10),
                    Child = new StackPanel
                    {
                        Children =
                        {
                            new TextBlock { Text = $"📦 {app.Name}.app", Foreground = Text, FontWeight = FontWeight.SemiBold },
                            new TextBlock { Text = app.Path, Foreground = Muted, FontSize = 10 },
                        },
                    },
                };
                _body.Children.Add(appRow);

                if (leftovers.Count == 0)
                {
                    _body.Children.Add(new TextBlock { Text = "No encontré restos en ~/Library para esta app.", Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 8, 0, 0) });
                }
                else
                {
                    _body.Children.Add(new TextBlock { Text = $"Restos encontrados ({leftovers.Count}):", Foreground = Text, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 10, 0, 0) });
                    foreach (var item in leftovers)
                    {
                        var check = new CheckBox
                        {
                            IsChecked = true,
                            Content = new StackPanel
                            {
                                Children =
                                {
                                    new TextBlock { Text = Path.GetFileName(item.Path), Foreground = Text, FontSize = 12 },
                                    new TextBlock { Text = $"{MacCleaner.Format(item.Size)} · {item.Path}", Foreground = Muted, FontSize = 10, TextWrapping = TextWrapping.Wrap, MaxWidth = 440 },
                                },
                            },
                        };
                        checks.Add((item, check));
                        _body.Children.Add(new Border { Background = Brush("#162A58"), CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6), Child = check });
                    }
                }

                var status = new TextBlock { Foreground = Muted, FontSize = 12, Margin = new Thickness(0, 10, 0, 0), TextWrapping = TextWrapping.Wrap };
                var go = new Button
                {
                    Content = "Mover a la Papelera", Width = 220, Height = 42, Background = Red, Foreground = Brushes.White,
                    FontWeight = FontWeight.SemiBold, CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 10, 0, 0),
                    HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                };
                go.Click += (_, _) =>
                {
                    go.IsEnabled = false;
                    go.Content = "Moviendo…";
                    var toRemove = new List<string> { app.Path };
                    toRemove.AddRange(checks.Where(c => c.Check.IsChecked == true).Select(c => c.Item.Path));
                    _ = Task.Run(() =>
                    {
                        var ok = 0; var fail = 0;
                        foreach (var path in toRemove)
                        {
                            if (AppUninstaller.MoveToTrash(path)) ok++; else fail++;
                        }
                        Dispatcher.UIThread.Post(() =>
                        {
                            status.Text = fail == 0
                                ? $"✓ Listo: {ok} elemento(s) movidos a la Papelera."
                                : $"Movidos {ok}, {fail} no se pudieron mover (¿falta Acceso total al disco?).";
                            status.Foreground = fail == 0 ? Green : Red;
                            go.Content = "Mover a la Papelera";
                            go.IsEnabled = false;
                        });
                    });
                };
                _body.Children.Add(go);
                _body.Children.Add(status);
            });
        });
    }

    static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));
}
