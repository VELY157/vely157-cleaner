using System.Security.Cryptography;

namespace VelyCleanerMac.Core;

public sealed record DuplicateGroup(long Size, List<string> Paths);

// Busca duplicados reales (mismo tamaño + mismo contenido, SHA-256 completo) en las
// carpetas típicas del usuario. Mismos frenos que LargeFiles: evita paquetes de macOS
// y corta por tiempo si hay demasiados archivos (p. ej. iCloud).
public static class DuplicateFinder
{
    const long MinBytes = 1L * 1024 * 1024;
    const int MaxEntriesVisited = 60_000;
    static readonly TimeSpan MaxDuration = TimeSpan.FromSeconds(25);
    static readonly string[] PackageExtensions =
        [".app", ".photoslibrary", ".musiclibrary", ".tvlibrary", ".theater", ".bundle",
         ".framework", ".sparsebundle", ".xcodeproj", ".xcworkspace", ".fcpbundle", ".imovielibrary"];

    public static List<DuplicateGroup> Find()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var bySize = new Dictionary<long, List<string>>();
        var visited = 0;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        foreach (var folder in new[] { "Downloads", "Desktop", "Documents", "Movies" })
        {
            var root = Path.Combine(home, folder);
            if (Directory.Exists(root)) Walk(root, bySize, ref visited, sw);
        }

        var groups = new List<DuplicateGroup>();
        foreach (var (size, paths) in bySize)
        {
            if (paths.Count < 2) continue;
            if (sw.Elapsed > MaxDuration) break;
            var byHash = new Dictionary<string, List<string>>();
            foreach (var path in paths)
            {
                if (sw.Elapsed > MaxDuration) break;
                var hash = HashFile(path);
                if (hash is null) continue;
                if (!byHash.TryGetValue(hash, out var list)) byHash[hash] = list = [];
                list.Add(path);
            }
            foreach (var (_, dupPaths) in byHash)
                if (dupPaths.Count > 1) groups.Add(new DuplicateGroup(size, dupPaths));
        }
        // Primero los que más espacio liberan si borrás todas las copias menos una.
        return groups.OrderByDescending(g => g.Size * (g.Paths.Count - 1)).ToList();
    }

    static void Walk(string dir, Dictionary<long, List<string>> bySize, ref int visited, System.Diagnostics.Stopwatch sw)
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
                    if (PackageExtensions.Contains(Path.GetExtension(entry), StringComparer.OrdinalIgnoreCase)) continue;
                    Walk(entry, bySize, ref visited, sw);
                    continue;
                }
                var size = new FileInfo(entry).Length;
                if (size < MinBytes) continue;
                if (!bySize.TryGetValue(size, out var list)) bySize[size] = list = [];
                list.Add(entry);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    static string? HashFile(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(stream));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }
}
