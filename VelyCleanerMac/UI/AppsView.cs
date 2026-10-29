using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VelyCleanerMac.Core;

namespace VelyCleanerMac.UI;

// Pantalla "Aplicaciones" (DISENO.md 5.3). No existe en Core un medidor de cache por app
// instalada, asi que aproximamos: tamaño total = bundle .app; "Cache" = restos que
// AppUninstaller ya sabe encontrar en ~/Library para esa app (Caches, Application Support...).
// "Limpiar cache" mueve SOLO esos restos a la Papelera, nunca el .app.
public sealed class AppsView : UserControl
{
    readonly WrapPanel _grid = new() { Orientation = Orientation.Horizontal };
    bool _loaded;

    public AppsView()
    {
        var status = AppTheme.Nota("Buscando apps instaladas…");
        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 14,
                Children =
                {
                    AppTheme.TituloGrande("Aplicaciones"),
                    AppTheme.Nota("Azul: tamaño de la app. Ámbar: restos de caché que puede dejar en tu Library."),
                    status,
                    _grid,
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
            var apps = AppUninstaller.ListApps();
            Dispatcher.UIThread.Post(() => status.IsVisible = false);
            foreach (var app in apps)
            {
                var appSize = MacCleaner.SizeOf(app.Path, contentsOnly: false);
                var leftovers = AppUninstaller.FindLeftovers(app);
                var cacheSize = leftovers.Sum(l => l.Size);
                Dispatcher.UIThread.Post(() => _grid.Children.Add(Card(app, appSize, cacheSize, leftovers)));
            }
        });
    }

    static Control Card(InstalledApp app, long appSize, long cacheSize, List<LeftoverItem> leftovers)
    {
        var total = appSize + cacheSize;
        var appFrac = total == 0 ? 0 : appSize / (double)total;
        var name = AppTheme.NombreFila(app.Name);
        var totalText = AppTheme.Nota(MacCleaner.Format(total));
        var appBar = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = AppTheme.Azul, Width = 190 * appFrac, HorizontalAlignment = HorizontalAlignment.Left };
        var cacheBar = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = AppTheme.Ambar, Width = 190 * (1 - appFrac), HorizontalAlignment = HorizontalAlignment.Left };
        var bars = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 0, Children = { appBar, cacheBar } };
        var cacheLabel = AppTheme.Nota(cacheSize == 0 ? "Sin restos de caché encontrados" : $"Caché: {MacCleaner.Format(cacheSize)}");

        var clean = AppTheme.BotonSecundario("Limpiar caché", 190);
        clean.IsEnabled = cacheSize > 0;
        clean.Click += async (_, _) =>
        {
            var owner = (Window)clean.GetVisualRoot()!;
            var ok = await Confirm.AskAsync(owner, $"¿Limpiar la caché de {app.Name}?", "Mueve esos restos a la Papelera. La app no se toca.", "Limpiar caché");
            if (!ok) return;
            clean.IsEnabled = false;
            clean.Content = "Limpiando…";
            await Task.Run(() => { foreach (var l in leftovers) AppUninstaller.MoveToTrash(l.Path); });
            clean.Content = "✓ Caché movida a la Papelera";
        };

        var iconBox = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(10), Background = AppTheme.Tarjeta2, Child = Icons.Make("box", 18, AppTheme.Azul2) };
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Children = { iconBox, new StackPanel { Children = { name, totalText } } } };

        return new Border
        {
            Width = 230, Margin = new Thickness(0, 0, 14, 14),
            Child = AppTheme.Tarjeta_(new StackPanel { Spacing = 8, Children = { header, bars, cacheLabel, clean } }),
        };
    }
}
