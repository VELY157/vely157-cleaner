using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace VelyCleanerMac.UI;

// Carga imagenes empacadas como AvaloniaResource (Assets/**) sin tumbar la app si falta el archivo.
public static class Assets
{
    public static Image? TryImage(string fileName, double width)
    {
        try
        {
            var uri = new Uri($"avares://VelyCleanerMac/Assets/{fileName}");
            if (!AssetLoader.Exists(uri)) return null;
            using var stream = AssetLoader.Open(uri);
            return new Image { Source = new Bitmap(stream), Width = width, Stretch = Stretch.Uniform };
        }
        catch (Exception e) when (e is IOException or NotSupportedException or FileNotFoundException) { return null; }
    }
}
