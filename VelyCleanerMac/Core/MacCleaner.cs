using System.Diagnostics;

namespace VelyCleanerMac.Core;

public sealed class CleanItem(string name, string description, bool defaultOn, Func<IEnumerable<string>> paths, bool contentsOnly = true)
{
    public string Name { get; } = name;
    public string Description { get; } = description;
    public bool DefaultOn { get; } = defaultOn;

    // Rutas a limpiar. contentsOnly = borra lo de adentro pero deja la carpeta.
    public IEnumerable<string> Paths() => paths();
    public bool ContentsOnly { get; } = contentsOnly;

    public long Measure() => Paths().Where(Exists).Sum(p => MacCleaner.SizeOf(p, ContentsOnly));
    public long Clean() => Paths().Where(Exists).Sum(p => MacCleaner.Delete(p, ContentsOnly));

    static bool Exists(string p) => File.Exists(p) || Directory.Exists(p);
}

public static class MacCleaner
{
    static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    static string Lib => Path.Combine(Home, "Library");

    static IEnumerable<string> One(string p) => [p];

    static IEnumerable<string> BrowserCaches()
    {
        // Rutas de caché por navegador en macOS (no toca contraseñas ni historial).
        yield return Path.Combine(Lib, "Caches", "com.apple.Safari");
        foreach (var b in new[] { "Google/Chrome", "BraveSoftware/Brave-Browser", "Microsoft Edge", "com.operasoftware.Opera" })
        {
            yield return Path.Combine(Lib, "Caches", b);
            var profiles = Path.Combine(Lib, "Application Support", b);
            if (Directory.Exists(profiles))
                foreach (var prof in SafeDirs(profiles))
                {
                    yield return Path.Combine(prof, "Cache");
                    yield return Path.Combine(prof, "Code Cache");
                    yield return Path.Combine(prof, "GPUCache");
                }
        }
        var ff = Path.Combine(Lib, "Caches", "Firefox", "Profiles");
        if (Directory.Exists(ff))
            foreach (var prof in SafeDirs(ff))
                yield return Path.Combine(prof, "cache2");
    }

    public static IReadOnlyList<CleanItem> Items { get; } =
    [
        new("Caché de aplicaciones", "Carpeta ~/Library/Caches de tus apps.", true,
            () => One(Path.Combine(Lib, "Caches"))),
        new("Caché de navegadores", "Safari, Chrome, Brave, Edge, Opera y Firefox. No borra contraseñas ni historial.", true,
            BrowserCaches),
        new("Registros (logs)", "~/Library/Logs: diagnósticos viejos de apps.", true,
            () => One(Path.Combine(Lib, "Logs"))),
        new("Caché de Discord", "Imágenes y archivos temporales de Discord.", true,
            () => new[] { "Cache", "Code Cache", "GPUCache" }.Select(d => Path.Combine(Lib, "Application Support", "discord", d))),
        new("Descargas incompletas de la App Store", "Restos de descargas de actualizaciones.", false,
            () => One(Path.Combine(Lib, "Caches", "com.apple.appstore"))),
        new("Papelera", "Vacía la Papelera (no se puede recuperar).", false,
            () => One(Path.Combine(Home, ".Trash")), contentsOnly: true),
    ];

    public static long SizeOf(string path, bool contentsOnly)
    {
        try
        {
            if (File.Exists(path)) return new FileInfo(path).Length;
            if (!Directory.Exists(path)) return 0;
            var di = new DirectoryInfo(path);
            if (IsLink(di)) return 0;
            long total = 0;
            if (contentsOnly)
                foreach (var child in di.EnumerateFileSystemInfos()) total += SizeOfInfo(child);
            else
                total = SizeOfInfo(di);
            return total;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return 0; }
    }

    static long SizeOfInfo(FileSystemInfo item)
    {
        try
        {
            if (item is FileInfo f) return f.Length;
            var d = (DirectoryInfo)item;
            if (IsLink(d)) return 0;
            return d.EnumerateFileSystemInfos().Sum(SizeOfInfo);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return 0; }
    }

    public static long Delete(string path, bool contentsOnly)
    {
        try
        {
            if (File.Exists(path)) { var s = new FileInfo(path).Length; File.Delete(path); return s; }
            if (!Directory.Exists(path)) return 0;
            var di = new DirectoryInfo(path);
            if (IsLink(di)) return 0;
            if (contentsOnly)
                return di.EnumerateFileSystemInfos().ToList().Sum(DeleteInfo);
            return DeleteInfo(di);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return 0; }
    }

    static long DeleteInfo(FileSystemInfo item)
    {
        try
        {
            if (item is FileInfo f) { var s = f.Length; f.Delete(); return s; }
            var d = (DirectoryInfo)item;
            if (IsLink(d)) { d.Delete(false); return 0; }
            var freed = d.EnumerateFileSystemInfos().ToList().Sum(DeleteInfo);
            try { d.Delete(false); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            return freed;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return 0; }
    }

    static bool IsLink(FileSystemInfo i) => i.Attributes.HasFlag(FileAttributes.ReparsePoint);

    static IEnumerable<string> SafeDirs(string path)
    {
        try { return Directory.GetDirectories(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; }
    }

    public static string Format(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.00} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0.0} MB",
        >= 1L << 10 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes} B",
    };
}
