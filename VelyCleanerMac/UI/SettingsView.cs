using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using VelyCleanerMac.Core;

namespace VelyCleanerMac.UI;

// Pantalla "Configuracion" (DISENO.md 5.8).
public sealed class SettingsView : UserControl
{
    public Action? OnRemoveKey;
    public Func<Task<string?>>? OnCheckUpdate;

    public SettingsView(AppSettings settings)
    {
        var updatesToggle = Toggle("Buscar actualizaciones al abrir", settings.CheckUpdatesOnOpen, v => { settings.CheckUpdatesOnOpen = v; settings.Save(); });
        var analyzeToggle = Toggle("Analizar al abrir", settings.AnalyzeOnOpen, v => { settings.AnalyzeOnOpen = v; settings.Save(); });
        var trashToggle = Toggle("Papelera marcada por defecto", settings.TrashCheckedByDefault, v => { settings.TrashCheckedByDefault = v; settings.Save(); });
        var togglesCard = AppTheme.Tarjeta_(new StackPanel { Spacing = 14, Children = { AppTheme.Subtitulo("General"), updatesToggle, analyzeToggle, trashToggle } });

        var checkUpdate = AppTheme.BotonSecundario("Buscar actualización", 160);
        var updateStatus = AppTheme.Nota("");
        checkUpdate.Click += async (_, _) =>
        {
            checkUpdate.Content = "Buscando…";
            var v = OnCheckUpdate is null ? null : await OnCheckUpdate();
            checkUpdate.Content = "Buscar actualización";
            updateStatus.Text = v is null ? $"Ya tienes la última versión ({Updates.Current})." : $"Hay una versión nueva: {v}.";
        };

        var support = AppTheme.BotonSecundario("Soporte por WhatsApp", 180);
        support.Click += (_, _) => Updates.Open("https://wa.me/18093700254?text=" + Uri.EscapeDataString("Hola Vely Media, necesito soporte con VELY157 Cleaner para Mac."));

        var removeKey = new Button
        {
            Content = "Quitar la key de este Mac", Height = 34, Background = AppTheme.RojoBoton, Foreground = Avalonia.Media.Brushes.White,
            FontWeight = Avalonia.Media.FontWeight.SemiBold, CornerRadius = new CornerRadius(9), Padding = new Thickness(16, 0),
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
        };
        removeKey.Click += async (_, _) =>
        {
            var owner = (Window)VisualRoot!;
            var ok = await Confirm.AskAsync(owner, "¿Quitar la key de este Mac?", "Vas a tener que activarla de nuevo (o empezar una prueba) para seguir usando la app.", "Quitar key");
            if (ok) OnRemoveKey?.Invoke();
        };

        var rowsCard = AppTheme.Tarjeta_(new StackPanel
        {
            Spacing = 14,
            Children =
            {
                AppTheme.Subtitulo("Cuenta y soporte"),
                Row(checkUpdate, updateStatus),
                Row(support, AppTheme.Nota("+1 (809) 370-0254")),
                Row(removeKey, AppTheme.Nota("")),
            },
        });

        Content = new ScrollViewer
        {
            Content = new StackPanel { Spacing = 14, Children = { AppTheme.TituloGrande("Configuración"), togglesCard, rowsCard } },
        };
    }

    static Control Row(Control button, Control note)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        grid.Children.Add(button);
        Grid.SetColumn(note, 1); note.Margin = new Thickness(12, 0, 0, 0); note.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(note);
        return grid;
    }

    static Control Toggle(string label, bool initial, Action<bool> onChange)
    {
        var sw = new ToggleSwitch { IsChecked = initial, OnContent = null, OffContent = null };
        sw.IsCheckedChanged += (_, _) => onChange(sw.IsChecked == true);
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(AppTheme.Texto13(label));
        Grid.SetColumn(sw, 1); grid.Children.Add(sw);
        return grid;
    }
}
