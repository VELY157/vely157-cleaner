using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using VelyCleanerMac.Core;

namespace VelyCleanerMac.UI;

// Pantalla "Duplicados" (DISENO.md 5.5): grupos por archivo, el original "Se conserva".
public sealed class DuplicatesView : UserControl
{
    readonly StackPanel _groups = new() { Spacing = 16 };
    readonly Button _goBtn;
    readonly List<CheckBox> _checks = new();
    bool _loaded;

    public DuplicatesView()
    {
        var status = AppTheme.Nota("Buscando duplicados… puede tardar hasta 25 segundos.");
        _goBtn = AppTheme.BotonPrincipal("Mover 0 a la Papelera", 220);
        _goBtn.IsVisible = false;
        _goBtn.Click += GoClick;

        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    AppTheme.TituloGrande("Duplicados"),
                    AppTheme.Nota("Busca en Descargas, Escritorio, Documentos y Películas. Dejamos una copia marcada para conservar."),
                    status, _groups, _goBtn,
                },
            },
        };
        Load(status);
    }

    void Load(TextBlock status)
    {
        if (_loaded) return;
        _loaded = true;
        _ = Task.Run(() =>
        {
            var groups = DuplicateFinder.Find();
            Dispatcher.UIThread.Post(() =>
            {
                status.IsVisible = false;
                if (groups.Count == 0) { _groups.Children.Add(AppTheme.Nota("No encontré duplicados en esas carpetas.")); return; }
                foreach (var g in groups) _groups.Children.Add(GroupCard(g));
                _goBtn.IsVisible = true;
            });
        });
    }

    Control GroupCard(DuplicateGroup group)
    {
        var header = AppTheme.NombreFila($"{group.Paths.Count} copias de {MacCleaner.Format(group.Size)} — recuperas {MacCleaner.Format(group.Size * (group.Paths.Count - 1))}");
        var rows = new StackPanel { Spacing = 4, Margin = new Thickness(0, 8, 0, 0) };
        for (var i = 0; i < group.Paths.Count; i++)
        {
            var path = group.Paths[i];
            var keep = i == 0;
            var check = new CheckBox { IsChecked = !keep, Tag = path };
            Control label = keep
                ? new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { AppTheme.Texto13(path, AppTheme.Gris), Pill("Se conserva", AppTheme.Verde) } }
                : AppTheme.Texto13(path);
            check.Content = label;
            _checks.Add(check);
            check.IsCheckedChanged += (_, _) => UpdateGoLabel();
            rows.Children.Add(check);
        }
        return AppTheme.Tarjeta_(new StackPanel { Children = { header, rows } });
    }

    static Control Pill(string text, IBrush color) => new Border
    {
        Background = color, CornerRadius = new CornerRadius(999), Padding = new Thickness(8, 2),
        Child = new TextBlock { Text = text, FontSize = 10.5, FontWeight = FontWeight.Bold, Foreground = Brushes.Black },
    };

    void UpdateGoLabel()
    {
        var n = _checks.Count(c => c.IsChecked == true);
        _goBtn.Content = $"Mover {n} a la Papelera";
    }

    async void GoClick(object? sender, RoutedEventArgs e)
    {
        var toRemove = _checks.Where(c => c.IsChecked == true).Select(c => (string)c.Tag!).ToList();
        if (toRemove.Count == 0) return;
        var owner = (Window)VisualRoot!;
        var ok = await Confirm.AskAsync(owner, "¿Mover duplicados a la Papelera?", $"Se van a mover {toRemove.Count} archivos. Se pueden recuperar desde la Papelera.", "Mover a la Papelera");
        if (!ok) return;
        _goBtn.IsEnabled = false;
        _goBtn.Content = "Moviendo…";
        var (ok2, fail) = await Task.Run(() =>
        {
            var okc = 0; var failc = 0;
            foreach (var path in toRemove) if (AppUninstaller.MoveToTrash(path)) okc++; else failc++;
            return (okc, failc);
        });
        _goBtn.Content = fail == 0 ? $"✓ {ok2} movidos a la Papelera" : $"Movidos {ok2}, {fail} fallaron";
    }
}
