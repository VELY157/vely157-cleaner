using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using VelyCleanerMac.Core;

namespace VelyCleanerMac.UI;

// Pantalla "Activacion" (DISENO.md 5.9): mitad escudo + ventajas, mitad formulario.
public sealed class ActivationView : UserControl
{
    readonly TextBox _keyBox;
    readonly TextBlock _error;
    readonly Button _trial;
    readonly Button _activate;

    public Func<Task>? OnTrial;
    public Func<string, Task>? OnActivate;

    public ActivationView(string? message)
    {
        var shield = new Border
        {
            Width = 120, Height = 120, CornerRadius = new CornerRadius(60), Background = AppTheme.Alpha("#2F8CFF", 0.14),
            Child = Icons.Make("shield", 56, AppTheme.Azul2), HorizontalAlignment = HorizontalAlignment.Center,
        };
        var advantages = new StackPanel
        {
            Spacing = 14, Margin = new Thickness(0, 24, 0, 0),
            Children =
            {
                Advantage("spark", "Limpieza real, no inventada", "Vemos exactamente qué carpetas se van a borrar."),
                Advantage("shield", "Nunca borra sin avisar", "Todo lo sensible va a la Papelera, no se pierde."),
                Advantage("clock", "Funciona 3 días sin internet", "Tu licencia se guarda firmada en el equipo."),
            },
        };
        var left = new StackPanel
        {
            Width = 420, Margin = new Thickness(40), VerticalAlignment = VerticalAlignment.Center,
            Children = { shield, advantages },
        };

        var title = AppTheme.TituloGrande("Activa tu Mac");
        var sub = AppTheme.Nota("Necesitas una licencia para limpiar. Puedes probarla gratis 7 días.");
        _trial = AppTheme.BotonPrincipal("Empezar 7 días gratis", 340);
        _trial.HorizontalAlignment = HorizontalAlignment.Stretch;

        var orLine = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,*"), Margin = new Thickness(0, 18) };
        orLine.Children.Add(new Border { Height = 1, Background = AppTheme.Linea, VerticalAlignment = VerticalAlignment.Center });
        var orText = AppTheme.Nota("o"); orText.Margin = new Thickness(10, 0); Grid.SetColumn(orText, 1); orLine.Children.Add(orText);
        var orRight = new Border { Height = 1, Background = AppTheme.Linea, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(orRight, 2); orLine.Children.Add(orRight);

        _keyBox = new TextBox
        {
            Watermark = "VELY-XXXXX-XXXXX-XXXXX-XXXXX", FontFamily = new FontFamily("Menlo, SF Mono, Consolas, monospace"),
            FontSize = 15, Width = 340, HorizontalContentAlignment = HorizontalAlignment.Center, Height = 36,
            Background = AppTheme.Tarjeta2, Foreground = AppTheme.Texto, BorderBrush = AppTheme.Linea2, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
        };
        _keyBox.TextChanged += (_, _) => FormatKey();

        _activate = AppTheme.BotonSecundario("Activar key", 340);
        _activate.HorizontalAlignment = HorizontalAlignment.Stretch;
        _error = new TextBlock { Text = message ?? "", Foreground = AppTheme.Rojo, TextWrapping = TextWrapping.Wrap, Width = 340, FontSize = 12.5 };

        var buy = new TextBlock { Text = "Comprar licencia", Foreground = AppTheme.Azul2, FontSize = 12.5, Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) };
        buy.PointerPressed += (_, _) => Updates.Open("https://velymediallc.com");

        var form = new StackPanel
        {
            Width = 340, Spacing = 12, VerticalAlignment = VerticalAlignment.Center,
            Children = { title, sub, _trial, orLine, _keyBox, _activate, _error, buy },
        };
        var right = new Border { Child = form, Margin = new Thickness(40), HorizontalAlignment = HorizontalAlignment.Center };

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,*") };
        body.Children.Add(left);
        var sep = new Border { Width = 1, Background = AppTheme.Linea }; Grid.SetColumn(sep, 1); body.Children.Add(sep);
        Grid.SetColumn(right, 2); body.Children.Add(right);
        Content = body;

        _trial.Click += async (_, _) =>
        {
            _trial.IsEnabled = false; _trial.Content = "Activando prueba…"; _error.Text = "";
            if (OnTrial is not null) await OnTrial();
            _trial.IsEnabled = true; _trial.Content = "Empezar 7 días gratis";
        };
        _activate.Click += async (_, _) =>
        {
            var key = _keyBox.Text?.Trim().ToUpperInvariant() ?? "";
            if (key.Length == 0) { _error.Text = "Escribe tu key."; return; }
            _activate.IsEnabled = false; _activate.Content = "Verificando…"; _error.Text = "";
            if (OnActivate is not null) await OnActivate(key);
            _activate.IsEnabled = true; _activate.Content = "Activar key";
        };
    }

    public void SetError(string message) { _error.Text = message; }

    void FormatKey()
    {
        var raw = new string((_keyBox.Text ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
        var groups = new List<string>();
        var i = 0;
        if (raw.StartsWith("VELY")) { groups.Add("VELY"); i = 4; }
        while (i < raw.Length) { groups.Add(raw.Substring(i, Math.Min(5, raw.Length - i))); i += 5; }
        var formatted = string.Join("-", groups);
        if (formatted != _keyBox.Text)
        {
            var caret = _keyBox.CaretIndex;
            _keyBox.Text = formatted;
            _keyBox.CaretIndex = Math.Min(formatted.Length, caret);
        }
    }

    static Control Advantage(string icon, string title, string note)
    {
        var box = new Border { Width = 34, Height = 34, CornerRadius = new CornerRadius(9), Background = AppTheme.Tarjeta2, Child = Icons.Make(icon, 16, AppTheme.Azul2) };
        var text = new StackPanel { Margin = new Thickness(10, 0, 0, 0), Spacing = 2, Children = { AppTheme.NombreFila(title), AppTheme.Nota(note) } };
        return new StackPanel { Orientation = Orientation.Horizontal, Children = { box, text } };
    }
}
