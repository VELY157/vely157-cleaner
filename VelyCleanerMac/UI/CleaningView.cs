using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using VelyCleanerMac.Core;

namespace VelyCleanerMac.UI;

// Pantalla "Limpieza" (DISENO.md 5.2): tarjetas por categoria con tamaño y carpetas exactas.
public sealed class CleaningView : UserControl
{
    public CleaningView()
    {
        var cards = new StackPanel { Spacing = 14 };
        var all = new List<Control>();
        foreach (var item in MacCleaner.Items)
        {
            var size = AppTheme.NombreFila("…", AppTheme.Gris);
            var clean = AppTheme.BotonSecundario("Limpiar esta categoría", 180);
            var paths = new StackPanel { Spacing = 2, Margin = new Thickness(0, 8, 0, 0) };
            foreach (var p in item.Paths())
                paths.Children.Add(AppTheme.Nota("· " + p.Replace(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "~")));

            var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
            header.Children.Add(new StackPanel { Children = { AppTheme.NombreFila(item.Name), AppTheme.Nota(item.Description) } });
            Grid.SetColumn(size, 1); size.VerticalAlignment = VerticalAlignment.Center; size.Margin = new Thickness(0, 0, 12, 0); header.Children.Add(size);
            Grid.SetColumn(clean, 2); header.Children.Add(clean);

            var card = AppTheme.Tarjeta_(new StackPanel { Children = { header, paths } });
            cards.Children.Add(card);
            all.Add(card);

            _ = Task.Run(item.Measure).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                size.Text = MacCleaner.Format(t.Result);
                size.Tag = t.Result;
            }));

            clean.Click += async (_, _) =>
            {
                var owner = (Window)VisualRoot!;
                var ok = await Confirm.AskAsync(owner, $"¿Limpiar {item.Name}?", "Esto borra lo que haya en esa categoría ahora mismo.", "Limpiar");
                if (!ok) return;
                clean.IsEnabled = false;
                var freed = await Task.Run(item.Clean);
                size.Text = "✓ " + MacCleaner.Format(freed);
                size.Foreground = AppTheme.Verde;
                clean.IsEnabled = true;
            };
        }

        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 14,
                Children = { AppTheme.TituloGrande("Limpieza"), AppTheme.Nota("Esto es exactamente lo que se borra en cada categoría."), cards },
            },
        };
        AppTheme.EntradaEscalonada(all);
    }
}
