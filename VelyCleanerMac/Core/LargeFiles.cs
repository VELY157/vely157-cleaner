namespace VelyCleanerMac.Core;

// Busca archivos grandes en las carpetas típicas del usuario (Descargas, Escritorio,
// Documentos, Películas, Imágenes). No borra nada: solo informa, para que el usuario
// decida qué sacar. A diferencia de la caché, estos son archivos propios del usuario.
public static class LargeFiles
{
    // Paquetes de macOS: por fuera son una carpeta, pero hay que tratarlos como un
    // solo archivo. Si los recorremos adentro (p. ej. una Fototeca con 500 mil fotos)
    // la búsqueda tarda minutos por nada.
    static readonly string[] PackageExtensions =
        [".app", ".photoslibrary", ".musiclibrary", ".tvlibrary", ".theater", ".bundle",
         ".framework", ".sparsebundle", ".xcodeproj", ".xcworkspace", ".fcpbundle", ".imovielibrary"];

    // Frenos de seguridad: si una carpeta tiene muchísimos archivos (node_modules, una
    // librería de código) o está en iCloud (cada archivo "placeholder" tarda por red),
    // no vale la pena seguir para este buscador. El de tiempo manda sobre el de cantidad.
    const int MaxEntriesVisited = 60_000;
    static readonly TimeSpan MaxDuration = TimeSpan.FromSeconds(12);

    public static List<(string Path, long Size)> Find(long minBytes = 100L * 1024 * 1024, int max = 200)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var results = new List<(string, long)>();
        var visited = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var folder in new[] { "Downloads", "Desktop", "Documents", "Movies" })
        {
            var root = Path.Combine(home, folder);
            if (Directory.Exists(root)) Walk(root, minBytes, results, ref visited, sw);
        }
        return results.OrderByDescending(r => r.Item2).Take(max).ToList();
    }

    static void Walk(string dir, long minBytes, List<(string, long)> results, ref int visited, System.Diagnostics.Stopwatch sw)
    {
        if (visited >= MaxEntriesVisited || sw.Elapsed > MaxDuration) return;
        IEnumerable<string> entries;
        try { entries = Directory.EnumerateFileSystemEntries(dir); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return; }

        foreach (var entry in entries)
        {
            if (++visited > MaxEntriesVisited || sw.Elapsed > MaxDuration) return;
            try
            {
                var attr = File.GetAttributes(entry);
                if (attr.HasFlag(FileAttributes.ReparsePoint)) continue;
                if (attr.HasFlag(FileAttributes.Directory))
                {
                    if (PackageExtensions.Contains(Path.GetExtension(entry), StringComparer.OrdinalIgnoreCase))
                    {
                        var size = MacCleaner.SizeOf(entry, contentsOnly: false);
                        if (size >= minBytes) results.Add((entry, size));
                        continue;
                    }
                    Walk(entry, minBytes, results, ref visited, sw);
                    continue;
                }
                var fileSize = new FileInfo(entry).Length;
                if (fileSize >= minBytes) results.Add((entry, fileSize));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }
}
