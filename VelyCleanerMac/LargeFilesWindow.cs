using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using VelyCleanerMac.Core;

namespace VelyCleanerMac;

public sealed class LargeFilesWindow : Window
{
    static readonly IBrush Bg = new SolidColorBrush(Color.Parse("#0A1430"));
    static readonly IBrush Card = new SolidColorBrush(Color.Parse("#13224A"));
    static readonly IBrush Line = new SolidColorBrush(Color.Parse("#26407A"));
    static readonly IBrush Blue = new SolidColorBrush(Color.Parse("#1E88FF"));
    static readonly IBrush Text = new SolidColorBrush(Color.Parse("#EAF2FF"));
    static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#96A8CA"));

    public LargeFilesWindow()
    {
        Title = "Archivos grandes";
        Width = 560;
        Height = 620;
        Background = Bg;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        var status = new TextBlock { Text = "Buscando archivos grandes en Descargas, Escritorio, Documentos…", Foreground = Muted, Margin = new Thickness(18, 14), TextWrapping = TextWrapping.Wrap };
        var list = new StackPanel { Spacing = 8, Margin = new Thickness(18, 0, 18, 18) };

        var layout = new DockPanel();
        var title = new TextBlock { Text = "Archivos grandes (más de 100 MB)", Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 16, Margin = new Thickness(18, 16, 18, 0) };
        var note = new TextBlock { Text = "No se borra nada automático: elegís vos qué sacar.", Foreground = Muted, FontSize = 11, Margin = new Thickness(18, 2, 18, 10) };
        DockPanel.SetDock(title, Dock.Top);
        DockPanel.SetDock(note, Dock.Top);
        layout.Children.Add(title);
        layout.Children.Add(note);
        layout.Children.Add(new ScrollViewer { Content = new StackPanel { Children = { status, list } } });
        Content = layout;

        Opened += (_, _) => _ = Task.Run(() =>
        {
            var files = LargeFiles.Find();
            Dispatcher.UIThread.Post(() =>
            {
                status.IsVisible = false;
                if (files.Count == 0)
                {
                    list.Children.Add(new TextBlock { Text = "No encontré archivos de más de 100 MB en tus carpetas comunes.", Foreground = Muted, TextWrapping = TextWrapping.Wrap });
                    return;
                }
                foreach (var (path, size) in files)
                {
                    var name = new TextBlock { Text = Path.GetFileName(path), Foreground = Text, FontWeight = FontWeight.SemiBold, FontSize = 13 };
                    var dir = new TextBlock { Text = Path.GetDirectoryName(path), Foreground = Muted, FontSize = 10, TextWrapping = TextWrapping.Wrap };
                    var sizeText = new TextBlock { Text = MacCleaner.Format(size), Foreground = Muted, FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
                    var reveal = new Button
                    {
                        Content = "Mostrar", Width = 90, Height = 32, Background = Brush2("#18264A"), Foreground = Text,
                        BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(7),
                        HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                    };
                    reveal.Click += (_, _) => { try { System.Diagnostics.Process.Start("open", ["-R", path]); } catch { } };

                    var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), Margin = new Thickness(0, 0, 0, 2) };
                    row.Children.Add(new StackPanel { Children = { name, dir } });
                    Grid.SetColumn(sizeText, 1); row.Children.Add(sizeText);
                    Grid.SetColumn(reveal, 2); reveal.Margin = new Thickness(10, 0, 0, 0); row.Children.Add(reveal);

                    list.Children.Add(new Border { Background = Card, CornerRadius = new CornerRadius(9), BorderBrush = Line, BorderThickness = new Thickness(1), Padding = new Thickness(12, 8), Child = row });
                }
            });
        });
    }

    static IBrush Brush2(string hex) => new SolidColorBrush(Color.Parse(hex));
}
