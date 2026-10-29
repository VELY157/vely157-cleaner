using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using VelyCleanerMac.Core;

namespace VelyCleanerMac.UI;

// Pantalla "Archivos grandes" (DISENO.md 5.4).
public sealed class LargeFilesView : UserControl
{
    readonly StackPanel _list = new() { Spacing = 8 };
    bool _loaded;

    public LargeFilesView()
    {
        var status = AppTheme.Nota("Buscando archivos de más de 100 MB en Descargas, Escritorio, Documentos y Películas…");
        var note = new Border
        {
            Background = AppTheme.Alpha("#34D399", 0.12), BorderBrush = AppTheme.Verde, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 8),
            Child = AppTheme.Texto13("Mover a la Papelera no borra para siempre.", AppTheme.Verde),
        };
        Content = new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 14,
                Children = { AppTheme.TituloGrande("Archivos grandes"), note, status, _list },
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
            var files = LargeFiles.Find();
            Dispatcher.UIThread.Post(() =>
            {
                status.IsVisible = false;
                if (files.Count == 0) { _list.Children.Add(AppTheme.Nota("No encontré archivos de más de 100 MB en tus carpetas comunes.")); return; }
                foreach (var (path, size) in files) _list.Children.Add(Row(path, size));
            });
        });
    }

    static Control Row(string path, long size)
    {
        DateTime? modified = null;
        try { modified = File.Exists(path) ? File.GetLastWriteTime(path) : Directory.GetLastWriteTime(path); } catch (IOException) { }

        var name = AppTheme.NombreFila(Path.GetFileName(path));
        var dir = AppTheme.Nota(Path.GetDirectoryName(path) ?? "");
        var sizeText = AppTheme.Texto13(MacCleaner.Format(size));
        var modText = AppTheme.Nota(modified?.ToString("dd MMM yyyy") ?? "—");

        var finder = AppTheme.BotonSecundario("Finder", 72); finder.Height = 28; finder.FontSize = 12;
        finder.Click += (_, _) => { try { System.Diagnostics.Process.Start("open", ["-R", path]); } catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException) { } };
        var trash = AppTheme.BotonSecundario("Papelera", 80); trash.Height = 28; trash.FontSize = 12;

        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("2*,1.3*,Auto,Auto,Auto,Auto"), Margin = new Thickness(0) };
        row.Children.Add(new StackPanel { Children = { name, dir } });
        var sizeCol = new Panel { Children = { sizeText }, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(sizeCol, 2); row.Children.Add(sizeCol);
        var modCol = new Panel { Children = { modText }, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0) };
        Grid.SetColumn(modCol, 3); row.Children.Add(modCol);
        Grid.SetColumn(finder, 4); finder.Margin = new Thickness(0, 0, 8, 0); row.Children.Add(finder);
        Grid.SetColumn(trash, 5); row.Children.Add(trash);

        var container = new Border { Background = AppTheme.Fila, CornerRadius = new CornerRadius(9), Padding = new Thickness(12, 10), Child = row };

        trash.Click += async (_, _) =>
        {
            var owner = (Window)container.GetVisualRoot()!;
            var ok = await Confirm.AskAsync(owner, "¿Mover a la Papelera?", $"\"{Path.GetFileName(path)}\" se puede recuperar desde la Papelera.", "Mover a la Papelera");
            if (!ok) return;
            trash.IsEnabled = finder.IsEnabled = false;
            var moved = await Task.Run(() => AppUninstaller.MoveToTrash(path));
            if (moved) { container.Opacity = 0.55; trash.Content = "✓ Movido"; }
            else { trash.Content = "Error"; trash.IsEnabled = true; }
        };

        return container;
    }
}
