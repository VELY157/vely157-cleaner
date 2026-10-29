using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace VelyCleanerMac.UI;

// Colores, letras y formas de diseno/DISENO.md. Un solo lugar para no repetir hex sueltos.
public static class AppTheme
{
    public static readonly IBrush Fondo = B("#0B1228");
    public static readonly IBrush Lateral = B("#0E1832");
    public static readonly IBrush Tarjeta = B("#111C3A");
    public static readonly IBrush Tarjeta2 = B("#16244A");
    public static readonly IBrush Fila = B("#13203F");
    public static readonly IBrush Linea = new SolidColorBrush(Color.Parse("#80A0E6"), 0.16);
    public static readonly IBrush Linea2 = new SolidColorBrush(Color.Parse("#80A0E6"), 0.28);
    public static readonly IBrush Azul = B("#2F8CFF");
    public static readonly IBrush Azul2 = B("#5AB0FF");
    public static readonly IBrush Cian = B("#38BDF8");
    public static readonly IBrush Texto = B("#EAF1FF");
    public static readonly IBrush Gris = B("#93A3C4");
    public static readonly IBrush Gris2 = B("#63739A");
    public static readonly IBrush Verde = B("#34D399");
    public static readonly IBrush Ambar = B("#FBBF24");
    public static readonly IBrush Rojo = B("#F87171");
    public static readonly IBrush RojoBoton = B("#E5484D");

    public static IBrush B(string hex) => new SolidColorBrush(Color.Parse(hex));
    public static IBrush Alpha(string hex, double a) => new SolidColorBrush(Color.Parse(hex), a);

    public static readonly Easing SalidaFuerte = new SplineEasing(0.23, 1, 0.32, 1);
    public static readonly Easing IdaVuelta = new SplineEasing(0.77, 0, 0.175, 1);

    public static bool ReduceMotion { get; } = Motion.ReadReduceMotion();

    // ---------- Texto ----------
    public static TextBlock TituloGrande(string text) => new()
    {
        Text = text, Foreground = Texto, FontSize = 26, FontWeight = FontWeight.ExtraBold, LetterSpacing = -0.7,
    };

    public static TextBlock Subtitulo(string text) => new()
    {
        Text = text, Foreground = Texto, FontSize = 15.5, FontWeight = FontWeight.Bold,
    };

    public static TextBlock Texto13(string text, IBrush? color = null) => new()
    {
        Text = text, Foreground = color ?? Texto, FontSize = 13.5,
    };

    public static TextBlock NombreFila(string text, IBrush? color = null) => new()
    {
        Text = text, Foreground = color ?? Texto, FontSize = 13.5, FontWeight = FontWeight.SemiBold,
    };

    public static TextBlock Nota(string text, IBrush? color = null) => new()
    {
        Text = text, Foreground = color ?? Gris, FontSize = 11.5, TextWrapping = TextWrapping.Wrap,
    };

    public static TextBlock Etiqueta(string text) => new()
    {
        Text = text.ToUpperInvariant(), Foreground = Gris2, FontSize = 11, FontWeight = FontWeight.SemiBold, LetterSpacing = 0.8,
    };

    // ---------- Formas ----------
    public static Border Tarjeta_(Control content, Thickness? padding = null) => new()
    {
        Background = Tarjeta, CornerRadius = new CornerRadius(14), BorderBrush = Linea, BorderThickness = new Thickness(1),
        Padding = padding ?? new Thickness(16), Child = content,
    };

    public static Button BotonPrincipal(string text, double? width = null) => new()
    {
        Content = text, Height = 34, Width = width ?? double.NaN, Background = Azul, Foreground = Brushes.White,
        FontWeight = FontWeight.SemiBold, FontSize = 13.5, CornerRadius = new CornerRadius(9), BorderThickness = new Thickness(0),
        HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
        Padding = new Thickness(16, 0), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
    };

    public static Button BotonSecundario(string text, double? width = null) => new()
    {
        Content = text, Height = 34, Width = width ?? double.NaN, Background = Tarjeta2, Foreground = Texto,
        FontWeight = FontWeight.SemiBold, FontSize = 13.5, CornerRadius = new CornerRadius(9),
        BorderBrush = Linea2, BorderThickness = new Thickness(1),
        HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
        Padding = new Thickness(14, 0), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
    };

    public static Border AccesoRapido(Control content) => new()
    {
        Background = Tarjeta2, CornerRadius = new CornerRadius(11), BorderBrush = Linea, BorderThickness = new Thickness(1),
        Padding = new Thickness(12, 10), Child = content, Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
    };

    // Entrada escalonada de tarjetas: sube 6px y aparece. 40ms entre cada una.
    public static void EntradaEscalonada(IEnumerable<Control> controles)
    {
        if (ReduceMotion) return;
        var i = 0;
        foreach (var c in controles)
        {
            var delay = i++ * 40;
            c.Opacity = 0;
            c.RenderTransform = new TranslateTransform(0, 6);
            c.Transitions = new Transitions
            {
                new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(260), Easing = SalidaFuerte, Delay = TimeSpan.FromMilliseconds(delay) },
            };
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                c.Opacity = 1;
                c.RenderTransform = new TranslateTransform(0, 0);
            }, Avalonia.Threading.DispatcherPriority.Loaded);
        }
    }
}

// "Reducir movimiento" del sistema: macOS no expone esto a .NET directo, se lee con `defaults`.
public static class Motion
{
    public static bool ReadReduceMotion()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("defaults", "read -g com.apple.universalaccess reduceMotion")
            { RedirectStandardOutput = true, UseShellExecute = false };
            using var p = System.Diagnostics.Process.Start(psi);
            if (p is null) return false;
            var output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(2000);
            return output == "1";
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or System.IO.IOException) { return false; }
    }
}
