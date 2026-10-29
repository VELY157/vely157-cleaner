using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using VelyCleanerMac.Core;

namespace VelyCleanerMac;

public sealed class DuplicateFinderWindow : Window
{
    static readonly IBrush Bg = Brush("#0A1430");
    static readonly IBrush Card = Brush("#13224A");
    static readonly IBrush Line = Brush("#26407A");
    static readonly IBrush Blue = Brush("#1E88FF");
    static readonly IBrush Text = Brush("#EAF2FF");
    static readonly IBrush Muted = Brush("#96A8CA");
    static readonly IBrush Green = Brush("#22C55E");
    static readonly IBrush Red = Brush("#F87171");

    public DuplicateFinderWindow()
    {
        Title = "Archivos duplicados";
        Width = 620;
        Height = 660;
        Background = Bg;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var title = new TextBlock { Text = "Archivos duplicados", Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 16, Margin = new Thickness(18, 16, 18, 0) };
        var note = new TextBlock { Text = "Busca en Descargas, Escritorio, Documentos y Películas. En cada grupo dejamos una copia marcada para conservar.", Foreground = Muted, FontSize = 11, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(18, 2, 18, 10) };
        var status = new TextBlock { Text = "Buscando duplicados… puede tardar hasta 25 segundos.", Foreground = Muted, Margin = new Thickness(18, 0, 18, 10) };
        var body = new StackPanel { Spacing = 10, Margin = new Thickness(18, 0, 18, 18) };
        var goBtn = new Button
        {
            Content = "Mover seleccionados a la Papelera", Width = 280, Height = 42, Background = Red, Foreground = Brushes.White,
            FontWeight = FontWeight.SemiBold, CornerRadius = new CornerRadius(8), Margin = new Thickness(18, 0, 18, 10),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            IsVisible = false,
        };

        var layout = new DockPanel();
        DockPanel.SetDock(title, Dock.Top);
        DockPanel.SetDock(note, Dock.Top);
        DockPanel.SetDock(goBtn, Dock.Bottom);
        layout.Children.Add(title);
        layout.Children.Add(note);
        layout.Children.Add(goBtn);
        layout.Children.Add(new ScrollViewer { Content = new StackPanel { Children = { status, body } } });
        Content = layout;

        var checks = new List<CheckBox>();

        Opened += (_, _) => _ = Task.Run(() =>
        {
            var groups = DuplicateFinder.Find();
            Dispatcher.UIThread.Post(() =>
            {
                status.IsVisible = false;
                if (groups.Count == 0)
                {
                    body.Children.Add(new TextBlock { Text = "No encontré duplicados en esas carpetas.", Foreground = Muted, TextWrapping = TextWrapping.Wrap });
                    return;
                }
                foreach (var group in groups)
                {
                    body.Children.Add(new TextBlock
                    {
                        Text = $"{group.Paths.Count} copias de {MacCleaner.Format(group.Size)} — recuperás {MacCleaner.Format(group.Size * (group.Paths.Count - 1))}",
                        Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 13, Margin = new Thickness(0, 6, 0, 2),
                    });
                    for (var i = 0; i < group.Paths.Count; i++)
                    {
                        var path = group.Paths[i];
                        var check = new CheckBox
                        {
                            IsChecked = i > 0, // la primera queda destildada (se conserva)
                            Tag = path,
                            Content = new TextBlock { Text = path, Foreground = i == 0 ? Muted : Text, FontSize = 11, TextWrapping = TextWrapping.Wrap, MaxWidth = 500 },
                        };
                        checks.Add(check);
                        body.Children.Add(check);
                    }
                }
                goBtn.IsVisible = true;
            });
        });

        goBtn.Click += (_, _) =>
        {
            var toRemove = checks.Where(c => c.IsChecked == true).Select(c => (string)c.Tag!).ToList();
            if (toRemove.Count == 0) return;
            goBtn.IsEnabled = false;
            goBtn.Content = "Moviendo…";
            _ = Task.Run(() =>
            {
                var ok = 0; var fail = 0;
                foreach (var path in toRemove)
                    if (AppUninstaller.MoveToTrash(path)) ok++; else fail++;
                Dispatcher.UIThread.Post(() =>
                {
                    goBtn.Content = fail == 0 ? $"✓ {ok} movidos a la Papelera" : $"Movidos {ok}, {fail} fallaron";
                    goBtn.Background = fail == 0 ? Green : Red;
                });
            });
        };
    }

    static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));
}
