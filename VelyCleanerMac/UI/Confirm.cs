using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace VelyCleanerMac.UI;

// Ventana de confirmar (DISENO.md 5): fondo oscurecido, titulo como pregunta,
// explicacion en una linea, "Cancelar" + accion en rojo.
public static class Confirm
{
    public static async Task<bool> AskAsync(Window owner, string question, string explanation, string actionLabel)
    {
        var tcs = new TaskCompletionSource<bool>();
        var dialog = new Window
        {
            SystemDecorations = SystemDecorations.None,
            Background = Brushes.Transparent,
            TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
            CanResize = false,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        var title = new TextBlock { Text = question, Foreground = AppTheme.Texto, FontSize = 16.5, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap, Width = 360 };
        var body = new TextBlock { Text = explanation, Foreground = AppTheme.Gris, FontSize = 13, TextWrapping = TextWrapping.Wrap, Width = 360 };
        var cancel = AppTheme.BotonSecundario("Cancelar", 120);
        var go = new Button
        {
            Content = actionLabel, Height = 34, Background = AppTheme.RojoBoton, Foreground = Brushes.White,
            FontWeight = FontWeight.SemiBold, FontSize = 13.5, CornerRadius = new CornerRadius(9), BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Padding = new Thickness(16, 0), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right, Children = { cancel, go } };

        var box = new Border
        {
            Background = AppTheme.Tarjeta, CornerRadius = new CornerRadius(14), BorderBrush = AppTheme.Linea2, BorderThickness = new Thickness(1),
            Padding = new Thickness(22), Width = 404,
            Child = new StackPanel { Spacing = 14, Children = { title, body, buttons } },
        };

        if (!AppTheme.ReduceMotion)
        {
            box.RenderTransform = new ScaleTransform(0.96, 0.96);
            box.Opacity = 0;
            box.Transitions = new Avalonia.Animation.Transitions
            {
                new Avalonia.Animation.DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(210), Easing = AppTheme.SalidaFuerte },
            };
        }

        var backdrop = new Border { Background = new SolidColorBrush(Colors.Black, 0.8), Child = new Panel { Children = { box }, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
        dialog.Content = backdrop;

        cancel.Click += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };
        go.Click += (_, _) => { tcs.TrySetResult(true); dialog.Close(); };
        dialog.Closed += (_, _) => tcs.TrySetResult(false);

        dialog.Opened += (_, _) =>
        {
            if (AppTheme.ReduceMotion) return;
            box.Opacity = 1;
            box.RenderTransform = new ScaleTransform(1, 1);
        };

        await dialog.ShowDialog(owner);
        return await tcs.Task;
    }
}
