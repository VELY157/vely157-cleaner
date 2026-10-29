using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using VelyCleanerMac.Core;

namespace VelyCleanerMac.UI;

// Pantalla "Licencia" (DISENO.md 5.7).
public sealed class LicenseView : UserControl
{
    readonly TextBlock _big;
    readonly TextBlock _smallNote;
    readonly ProgressBar _bar;
    readonly TextBlock _cliente;
    readonly TextBlock _plan;
    readonly TextBlock _equipo;
    readonly TextBlock _conexion;
    readonly TextBlock _keyText;
    LicenseToken? _token;
    bool _online;

    public Action? OnChangeKey;

    public LicenseView()
    {
        _big = new TextBlock { Text = "—", Foreground = AppTheme.Texto, FontSize = 34, FontWeight = FontWeight.ExtraBold };
        _smallNote = AppTheme.Nota("");
        _bar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 8, CornerRadius = new CornerRadius(4), Background = AppTheme.Linea, Foreground = AppTheme.Verde, Margin = new Thickness(0, 10, 0, 0) };

        _cliente = AppTheme.Texto13("");
        _plan = AppTheme.Texto13("");
        _equipo = AppTheme.Texto13("");
        _conexion = AppTheme.Texto13("");
        var datos = new StackPanel
        {
            Spacing = 8, Margin = new Thickness(0, 16, 0, 0),
            Children = { Fila("Cliente", _cliente), Fila("Plan", _plan), Fila("Equipo", _equipo), Fila("Conexión", _conexion) },
        };

        _keyText = new TextBlock { Foreground = AppTheme.Texto, FontFamily = new FontFamily("Menlo, SF Mono, Consolas, monospace"), FontSize = 15, FontWeight = FontWeight.SemiBold, LetterSpacing = 0.6 };
        var changeKey = AppTheme.BotonSecundario("Cambiar key", 150);
        changeKey.Click += (_, _) => OnChangeKey?.Invoke();
        var keyCard = AppTheme.Tarjeta_(new StackPanel { Spacing = 10, Children = { AppTheme.Etiqueta("Tu key"), _keyText, changeKey } });

        var left = new StackPanel
        {
            Spacing = 0,
            Children = { AppTheme.TituloGrande("Licencia"), AppTheme.Tarjeta_(new StackPanel { Children = { _big, _smallNote, _bar, datos } }, new Thickness(20)).WithMargin(0, 14, 0, 14), keyCard },
        };

        var planes = new StackPanel
        {
            Spacing = 10,
            Children =
            {
                AppTheme.Subtitulo("Planes"),
                PlanRow("1 mes", "$5"), PlanRow("3 meses", "$12"), PlanRow("1 año", "$35"),
            },
        };
        var buyWeb = AppTheme.BotonPrincipal("Comprar en velymediallc.com", 260);
        buyWeb.Click += (_, _) => Updates.Open("https://velymediallc.com");
        var buyWa = AppTheme.BotonSecundario("Comprar por WhatsApp", 260);
        buyWa.Click += (_, _) => Updates.Open("https://wa.me/18093700254?text=" + Uri.EscapeDataString("Hola Vely Media, quiero comprar una licencia de VELY157 Cleaner para Mac."));
        var right = new StackPanel { Width = 280, Margin = new Thickness(14, 40, 0, 0), Spacing = 12, Children = { AppTheme.Tarjeta_(planes), buyWeb, buyWa } };

        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        body.Children.Add(left);
        Grid.SetColumn(right, 1); body.Children.Add(right);
        Content = new ScrollViewer { Content = body };
    }

    static Control Fila(string label, TextBlock value)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        grid.Children.Add(AppTheme.Nota(label));
        Grid.SetColumn(value, 1); value.HorizontalAlignment = HorizontalAlignment.Right; grid.Children.Add(value);
        return grid;
    }

    static Control PlanRow(string label, string price)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        grid.Children.Add(AppTheme.Texto13(label));
        var p = new TextBlock { Text = price, Foreground = AppTheme.Azul2, FontWeight = FontWeight.Bold, FontSize = 14 };
        Grid.SetColumn(p, 1); grid.Children.Add(p);
        return new Border { Background = AppTheme.Tarjeta2, CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 8), Child = grid };
    }

    public void SetToken(LicenseToken token, bool online)
    {
        _token = token; _online = online;
        _cliente.Text = token.Customer;
        _plan.Text = $"{(token.ExpiresAt - token.IssuedAt).Days} días";
        _equipo.Text = MacId.Compute()[..12] + "…";
        _conexion.Text = online ? "En línea" : "Sin conexión (modo de gracia)";
        var key = token.Key;
        _keyText.Text = key.Length > 10 ? key[..5] + "-•••••-•••••-" + key[^5..] : key;
        Tick();
    }

    public void Tick()
    {
        if (_token is null) return;
        var rem = _token.ExpiresAt - DateTime.UtcNow;
        if (rem <= TimeSpan.Zero) { _big.Text = "Vencida"; _big.Foreground = AppTheme.Rojo; _smallNote.Text = "Renueva para seguir limpiando."; _bar.Value = 0; return; }
        _big.Text = rem.TotalDays >= 1 ? $"{(int)rem.TotalDays}d {rem.Hours:00}h {rem.Minutes:00}m" : $"{(int)rem.TotalHours:00}:{rem.Minutes:00}:{rem.Seconds:00}";
        _big.Foreground = rem.TotalDays < 3 ? AppTheme.Ambar : AppTheme.Texto;
        _smallNote.Text = (rem.TotalDays < 3 ? "Vence pronto · " : "") + (_online ? "licencia verificada en línea" : "sin conexión · gracia de 3 días");
        var total = Math.Max((_token.ExpiresAt - _token.IssuedAt).TotalSeconds, 1);
        _bar.Value = Math.Clamp(rem.TotalSeconds / total * 100, 0, 100);
        _bar.Foreground = rem.TotalDays < 3 ? AppTheme.Ambar : AppTheme.Verde;
    }
}

static class ControlMarginExt
{
    public static Control WithMargin(this Control c, double l, double t, double r, double b) { c.Margin = new Thickness(l, t, r, b); return c; }
}
