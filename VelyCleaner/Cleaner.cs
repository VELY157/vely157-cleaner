using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VelyCleaner;

// Una cosa que se puede limpiar. Items devuelve lo que se borra (archivos o carpetas completas);
// las carpetas raíz como %TEMP% nunca se borran, solo su contenido.
public sealed class CleanTarget(string name, string description, bool defaultOn, Func<IEnumerable<FileSystemInfo>>? items = null, Func<long>? specialSize = null, Action? special = null)
{
    public string Name { get; } = name;
    public string Description { get; } = description;
    public bool DefaultOn { get; } = defaultOn;

    public long Measure() => specialSize?.Invoke() ?? (items is null ? 0 : items().Sum(Cleaner.SizeOf));

    // Devuelve los bytes liberados. Lo que está en uso se salta.
    public long Clean()
    {
        if (special is not null)
        {
            var before = specialSize?.Invoke() ?? 0;
            special();
            return before;
        }
        return items is null ? 0 : items().Sum(Cleaner.Delete);
    }
}

public static class Cleaner
{
    static readonly string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    static readonly string Roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    static readonly string Windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    static readonly string ProgramData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

    public static IReadOnlyList<CleanTarget> Targets { get; } =
    [
        new("Archivos temporales del usuario", "Carpeta %TEMP% de tu usuario.", true,
            () => Contents(Path.GetTempPath())),
        new("Archivos temporales de Windows", @"C:\Windows\Temp.", true,
            () => Contents(Path.Combine(Windows, "Temp"))),
        new("Caché de navegadores", "Chrome, Edge, Brave, Opera y Firefox. No borra contraseñas, historial ni sesiones.", true,
            () => BrowserCaches().SelectMany(Contents)),
        new("Caché de Discord", "Imágenes y archivos temporales de Discord.", true,
            () => new[] { "Cache", "Code Cache", "GPUCache" }.SelectMany(d => Contents(Path.Combine(Roaming, "discord", d)))),
        new("Caché de miniaturas", "Miniaturas de imágenes del Explorador. Windows las vuelve a crear.", true,
            () => Files(Path.Combine(Local, @"Microsoft\Windows\Explorer"), "thumbcache_*.db", "iconcache_*.db")),
        new("Reportes de errores de Windows", "Informes de fallos y volcados de memoria antiguos.", true,
            () => Contents(Path.Combine(ProgramData, @"Microsoft\Windows\WER\ReportArchive"))
                .Concat(Contents(Path.Combine(ProgramData, @"Microsoft\Windows\WER\ReportQueue")))
                .Concat(Contents(Path.Combine(Local, "CrashDumps")))),
        new("Descargas de Windows Update", "Instaladores de actualizaciones ya descargados.", true,
            () => Contents(Path.Combine(Windows, @"SoftwareDistribution\Download"))),
        new("Caché de shaders (DirectX / NVIDIA / AMD)", "Se vuelve a crear; los juegos pueden tardar un poco más la primera vez.", false,
            () => Contents(Path.Combine(Local, "D3DSCache"))
                .Concat(Contents(Path.Combine(Local, @"NVIDIA\DXCache")))
                .Concat(Contents(Path.Combine(Local, @"NVIDIA\GLCache")))
                .Concat(Contents(Path.Combine(Local, @"AMD\DxCache")))
                .Concat(Contents(Path.Combine(Local, @"AMD\DxcCache")))),
        new("Caché DNS", "Equivale a ipconfig /flushdns.", true,
            specialSize: () => 0, special: FlushDns),
        new("Vaciar la papelera de reciclaje", "Los archivos de la papelera se borran para siempre.", false,
            specialSize: RecycleBinSize, special: EmptyRecycleBin),
    ];

    public static long SizeOf(FileSystemInfo item)
    {
        try
        {
            if (item is FileInfo f) return f.Length;
            if (item.Attributes.HasFlag(FileAttributes.ReparsePoint)) return 0;
            return ((DirectoryInfo)item).EnumerateFileSystemInfos().Sum(SizeOf);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return 0; }
    }

    // Borra un archivo o una carpeta con todo su contenido.
    // Los accesos directos de carpeta (junctions/symlinks) se quitan sin entrar en ellos.
    public static long Delete(FileSystemInfo item)
    {
        try
        {
            if (item is FileInfo f)
            {
                var size = f.Length;
                if (f.IsReadOnly) f.IsReadOnly = false;
                f.Delete();
                return size;
            }
            var dir = (DirectoryInfo)item;
            if (dir.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                dir.Delete(recursive: false);
                return 0;
            }
            var freed = dir.EnumerateFileSystemInfos().ToList().Sum(Delete);
            try { dir.Delete(recursive: false); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { } // quedó algo en uso dentro
            return freed;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return 0; // en uso o protegido
        }
    }

    static IEnumerable<FileSystemInfo> Contents(string path)
    {
        var dir = new DirectoryInfo(path);
        if (!dir.Exists) return [];
        try { return dir.EnumerateFileSystemInfos().ToList(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return []; }
    }

    static IEnumerable<FileSystemInfo> Files(string path, params string[] patterns)
    {
        var dir = new DirectoryInfo(path);
        if (!dir.Exists) return [];
        return patterns.SelectMany(p => dir.EnumerateFiles(p)).ToList();
    }

    static IEnumerable<string> BrowserCaches()
    {
        string[] chromiumRoots =
        [
            Path.Combine(Local, @"Google\Chrome\User Data"),
            Path.Combine(Local, @"Microsoft\Edge\User Data"),
            Path.Combine(Local, @"BraveSoftware\Brave-Browser\User Data"),
            Path.Combine(Local, @"Opera Software\Opera Stable"),
            Path.Combine(Local, @"Opera Software\Opera GX Stable"),
        ];
        foreach (var root in chromiumRoots.Where(Directory.Exists))
        {
            // Perfiles: Default, Profile 1, ... (Opera guarda la caché en la raíz)
            var profiles = Directory.GetDirectories(root).Where(p => Directory.Exists(Path.Combine(p, "Cache")) || Directory.Exists(Path.Combine(p, "Code Cache"))).Append(root);
            foreach (var p in profiles)
            {
                yield return Path.Combine(p, "Cache");
                yield return Path.Combine(p, "Code Cache");
                yield return Path.Combine(p, "GPUCache");
            }
        }
        var firefox = Path.Combine(Local, @"Mozilla\Firefox\Profiles");
        if (Directory.Exists(firefox))
            foreach (var p in Directory.GetDirectories(firefox))
                yield return Path.Combine(p, "cache2");
    }

    static void FlushDns()
    {
        using var p = Process.Start(new ProcessStartInfo("ipconfig", "/flushdns") { CreateNoWindow = true, UseShellExecute = false });
        p?.WaitForExit(10_000);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SHQUERYRBINFO { public int cbSize; public long i64Size; public long i64NumItems; }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHQueryRecycleBin(string? pszRootPath, ref SHQUERYRBINFO pSHQueryRBInfo);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern int SHEmptyRecycleBin(IntPtr hwnd, string? pszRootPath, uint dwFlags);

    static long RecycleBinSize()
    {
        var info = new SHQUERYRBINFO { cbSize = Marshal.SizeOf<SHQUERYRBINFO>() };
        return SHQueryRecycleBin(null, ref info) == 0 ? info.i64Size : 0;
    }

    static void EmptyRecycleBin()
    {
        const uint noConfirmation = 0x1, noProgress = 0x2, noSound = 0x4;
        SHEmptyRecycleBin(IntPtr.Zero, null, noConfirmation | noProgress | noSound);
    }

    public static string Format(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.00} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0.0} MB",
        >= 1L << 10 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes} B",
    };
}
