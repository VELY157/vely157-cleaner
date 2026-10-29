using System.Diagnostics;

namespace VelyCleanerMac.Core;

public sealed class CleanItem(string name, string description, bool defaultOn, Func<IEnumerable<string>> paths, bool contentsOnly = true, bool needsFullDiskAccess = false)
{
    public string Name { get; } = name;
    public string Description { get; } = description;
    public bool DefaultOn { get; } = defaultOn;
    public bool NeedsFullDiskAccess { get; } = needsFullDiskAccess;

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
            var appSupport = Path.Combine(Lib, "Application Support", b);
            // Cachés de GPU que viven sueltas en la carpeta del navegador, no por perfil.
            foreach (var shared in new[] { "GrShaderCache", "GraphiteDawnCache", "ShaderCache", "GPUPersistentCache" })
                yield return Path.Combine(appSupport, shared);
            if (Directory.Exists(appSupport))
                foreach (var prof in SafeDirs(appSupport))
                {
                    yield return Path.Combine(prof, "Cache");
                    yield return Path.Combine(prof, "Code Cache");
                    yield return Path.Combine(prof, "GPUCache");
                    // Chrome/Brave movieron la caché de apps web (Service Workers) acá.
                    yield return Path.Combine(prof, "Service Worker", "CacheStorage");
                    yield return Path.Combine(prof, "Service Worker", "ScriptCache");
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
        new("Caché de Xcode y Simulador de iOS", "DerivedData y cachés del Simulador. Se puede volver a generar al compilar.", true,
            () => [Path.Combine(Lib, "Developer", "Xcode", "DerivedData"), Path.Combine(Lib, "Developer", "CoreSimulator", "Caches")]),
        new("Adjuntos descargados de Mail", "Mail los vuelve a bajar si los necesitás. No borra los mensajes.", false,
            () => One(Path.Combine(Lib, "Containers", "com.apple.mail", "Data", "Library", "Mail Downloads")),
            needsFullDiskAccess: true),
        new("Papelera", "Vacía la Papelera (no se puede recuperar).", false,
            () => One(Path.Combine(Home, ".Trash")), contentsOnly: true, needsFullDiskAccess: true),
    ];

    // Sonda liviana: ~/.Trash está protegido por macOS (TCC) y solo se puede leer
    // con "Acceso total al disco" activado para esta app.
    public static bool HasFullDiskAccess()
    {
        try
        {
            _ = Directory.EnumerateFileSystemEntries(Path.Combine(Home, ".Trash")).Any();
            return true;
        }
        catch (UnauthorizedAccessException) { return false; }
        catch (Exception e) when (e is IOException) { return true; }
    }

    public static void OpenFullDiskAccessSettings()
    {
        try { Process.Start("open", "x-apple.systempreferences:com.apple.preference.security?Privacy_AllFiles"); }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException) { }
    }

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
            if (item is FileInfo f) { var s = f.Length; EnsureWritable(f.FullName); f.Delete(); return s; }
            var d = (DirectoryInfo)item;
            if (IsLink(d)) { d.Delete(false); return 0; }
            EnsureWritable(d.FullName);
            var freed = d.EnumerateFileSystemInfos().ToList().Sum(DeleteInfo);
            try { d.Delete(false); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            return freed;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return 0; }
    }

    // Algunas herramientas (p. ej. dotslash, usado por React Native DevTools) dejan sus
    // carpetas de caché en modo solo-lectura (dr-xr-xr-x) para evitar que se modifiquen
    // los binarios bajados. Sin permiso de escritura no se puede borrar lo de adentro,
    // así que se lo agregamos antes de intentar.
    static void EnsureWritable(string path)
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;
        try
        {
            var mode = File.GetUnixFileMode(path);
            var needed = mode | UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
            if (needed != mode) File.SetUnixFileMode(path, needed);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException) { }
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
