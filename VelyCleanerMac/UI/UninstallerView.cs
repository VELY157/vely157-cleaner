using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using VelyCleanerMac.Core;

namespace VelyCleanerMac.UI;

// Pantalla "Desinstalador" (DISENO.md 5.6).
public sealed class UninstallerView : UserControl
{
    readonly StackPanel _body = new() { Spacing = 8 };
    bool _loaded;

    public UninstallerView()
    {
        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 14,
                Children = { AppTheme.TituloGrande("Desinstalador"), AppTheme.Nota("Elige una app. Te muestro qué restos deja antes de borrar nada."), _body },
            },
        };
        ShowList();
    }

    void ShowList()
    {
        if (_loaded) { Reload(); return; }
        _loaded = true;
        Reload();
    }

    void Reload()
    {
        _body.Children.Clear();
        var loading = AppTheme.Nota("Buscando apps instaladas…");
        _body.Children.Add(loading);
        _ = Task.Run(() =>
        {
            var apps = AppUninstaller.ListApps();
            Dispatcher.UIThread.Post(() =>
            {
                _body.Children.Remove(loading);
                foreach (var app in apps) _body.Children.Add(AppRow(app));
            });
        });
    }

    Control AppRow(InstalledApp app)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto") };
        row.Children.Add(new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(9), Background = AppTheme.Tarjeta2, Child = Icons.Make("box", 16, AppTheme.Azul2) });
        var used = AppTheme.Nota("Usada hace tiempo");
        var text = new StackPanel { Margin = new Thickness(10, 0), VerticalAlignment = VerticalAlignment.Center, Children = { AppTheme.NombreFila(app.Name), used } };
        Grid.SetColumn(text, 1); row.Children.Add(text);
        var uninstall = AppTheme.BotonSecundario("Desinstalar", 110);
        Grid.SetColumn(uninstall, 3); row.Children.Add(uninstall);

        var card = AppTheme.Tarjeta_(row, new Thickness(12, 10));
        uninstall.Click += (_, _) => { _body.Children.Clear(); ShowDetail(app); };
        return card;
    }

    void ShowDetail(InstalledApp app)
    {
        var back = new TextBlock { Text = "‹ Volver a la lista", Foreground = AppTheme.Azul2, FontSize = 12.5, Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand) };
        back.PointerPressed += (_, _) => Reload();
        _body.Children.Add(back);

        var appCard = AppTheme.Tarjeta_(new StackPanel
        {
            Orientation = Orientation.Horizontal, Spacing = 10,
            Children =
            {
                new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(9), Background = AppTheme.Tarjeta2, Child = Icons.Make("box", 16, AppTheme.Azul2) },
                new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { AppTheme.NombreFila(app.Name + ".app"), AppTheme.Nota(app.Path) } },
            },
        });
        _body.Children.Add(appCard);

        var loading = AppTheme.Nota("Buscando restos en tu Library…");
        _body.Children.Add(loading);

        _ = Task.Run(() =>
        {
            var leftovers = AppUninstaller.FindLeftovers(app);
            Dispatcher.UIThread.Post(() =>
            {
                _body.Children.Remove(loading);
                var checks = new List<(LeftoverItem Item, CheckBox Check)>();
                if (leftovers.Count == 0)
                {
                    _body.Children.Add(AppTheme.Nota("No encontré restos en ~/Library para esta app."));
                }
                else
                {
                    _body.Children.Add(new Border
                    {
                        Background = AppTheme.Alpha("#FBBF24", 0.12), BorderBrush = AppTheme.Ambar, BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 8),
                        Child = AppTheme.Texto13($"+ {leftovers.Count} de restos", AppTheme.Ambar),
                    });
                    foreach (var item in leftovers)
                    {
                        var check = new CheckBox
                        {
                            IsChecked = true,
                            Content = new StackPanel { Children = { AppTheme.Texto13(Path.GetFileName(item.Path)), AppTheme.Nota($"{MacCleaner.Format(item.Size)} · {item.Path}") } },
                        };
                        checks.Add((item, check));
                        _body.Children.Add(new Border { Background = AppTheme.Fila, CornerRadius = new CornerRadius(9), Padding = new Thickness(12, 8), Child = check });
                    }
                }

                var status = AppTheme.Nota("");
                var go = new Button
                {
                    Content = "Desinstalar", Height = 34, Background = AppTheme.RojoBoton, Foreground = Avalonia.Media.Brushes.White,
                    FontWeight = Avalonia.Media.FontWeight.SemiBold, CornerRadius = new CornerRadius(9), Padding = new Thickness(16, 0),
                    Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                };
                go.Click += async (_, _) =>
                {
                    var owner = (Window)VisualRoot!;
                    var ok = await Confirm.AskAsync(owner, $"¿Desinstalar {app.Name}?", "La app y sus restos marcados se mueven a la Papelera.", "Desinstalar");
                    if (!ok) return;
                    go.IsEnabled = false;
                    go.Content = "Moviendo…";
                    var toRemove = new List<string> { app.Path };
                    toRemove.AddRange(checks.Where(c => c.Check.IsChecked == true).Select(c => c.Item.Path));
                    var (ok2, fail) = await Task.Run(() =>
                    {
                        var okc = 0; var failc = 0;
                        foreach (var path in toRemove) if (AppUninstaller.MoveToTrash(path)) okc++; else failc++;
                        return (okc, failc);
                    });
                    status.Text = fail == 0 ? $"✓ Listo: {ok2} elemento(s) movidos a la Papelera." : $"Movidos {ok2}, {fail} no se pudieron mover (¿falta Acceso total al disco?).";
                    status.Foreground = fail == 0 ? AppTheme.Verde : AppTheme.Rojo;
                    go.Content = "Desinstalar";
                };
                _body.Children.Add(go);
                _body.Children.Add(status);
            });
        });
    }
}
