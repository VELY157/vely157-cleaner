using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;

namespace VelyInstaller;

public static class InstallerCore
{
    public const string AppName = "VELY157 Cleaner";
    public const string ExeName = "VELY157 Cleaner.exe";
    public const string Publisher = "Vely Media LLC";
    public const string Version = "1.0.0";
    const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\VELY157 Cleaner";

    public static string DefaultDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppName);

    public static bool HasPayload =>
        Assembly.GetExecutingAssembly().GetManifestResourceNames().Contains("cleaner.exe.gz");

    // Extrae el limpiador (viene comprimido en gzip dentro del instalador).
    public static void ExtractCleaner(string exePath)
    {
        using var res = Assembly.GetExecutingAssembly().GetManifestResourceStream("cleaner.exe.gz")
            ?? throw new InvalidOperationException("El instalador no trae el limpiador.");
        using var gz = new GZipStream(res, CompressionMode.Decompress);
        using var outFile = File.Create(exePath);
        gz.CopyTo(outFile);
    }

    public static async Task InstallAsync(string dir, bool desktopIcon, IProgress<(int Percent, string Step)> progress)
    {
        await Task.Run(() =>
        {
            progress.Report((6, "Preparando archivos…"));
            Directory.CreateDirectory(dir);
            var exePath = Path.Combine(dir, ExeName);

            progress.Report((20, "Copiando archivos…"));
            ExtractCleaner(exePath);

            progress.Report((72, "Configurando componentes…"));
            var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "Programs");
            Directory.CreateDirectory(startMenu);
            var startLnk = Path.Combine(startMenu, AppName + ".lnk");
            CreateShortcut(startLnk, exePath, dir);

            var desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), AppName + ".lnk");
            if (desktopIcon) CreateShortcut(desktopLnk, exePath, dir);
            else if (File.Exists(desktopLnk)) File.Delete(desktopLnk);

            progress.Report((88, "Finalizando…"));
            WriteUninstaller(dir, startLnk, desktopLnk);
            RegisterUninstall(dir, exePath);

            progress.Report((100, "Instalación completada"));
        });
    }

    // Crea un acceso directo .lnk usando Windows Script Host.
    public static void CreateShortcut(string lnkPath, string target, string workingDir)
    {
        var type = Type.GetTypeFromProgID("WScript.Shell");
        if (type is null) return;
        dynamic shell = Activator.CreateInstance(type)!;
        try
        {
            var sc = shell.CreateShortcut(lnkPath);
            sc.TargetPath = target;
            sc.WorkingDirectory = workingDir;
            sc.IconLocation = target + ",0";
            sc.Description = AppName;
            sc.Save();
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
        }
    }

    static void RegisterUninstall(string dir, string exePath)
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(UninstallKey);
            var size = File.Exists(exePath) ? new FileInfo(exePath).Length / 1024 : 0;
            key.SetValue("DisplayName", AppName);
            key.SetValue("DisplayVersion", Version);
            key.SetValue("Publisher", Publisher);
            key.SetValue("DisplayIcon", exePath);
            key.SetValue("InstallLocation", dir);
            key.SetValue("UninstallString", $"\"{Path.Combine(dir, "desinstalar.cmd")}\"");
            key.SetValue("EstimatedSize", (int)size, RegistryValueKind.DWord);
            key.SetValue("NoModify", 1, RegistryValueKind.DWord);
            key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
        }
        catch (UnauthorizedAccessException) { /* sin permiso: se omite el registro */ }
    }

    // Desinstalador: borra los accesos, el registro y, al final, su propia carpeta.
    static void WriteUninstaller(string dir, string startLnk, string desktopLnk)
    {
        var cmd = Path.Combine(dir, "desinstalar.cmd");
        // (goto) 2>nul libera el .cmd y luego rmdir borra la carpeta, incluido este archivo.
        File.WriteAllText(cmd, $"""
            @echo off
            reg delete "HKLM\{UninstallKey}" /f >nul 2>&1
            del /f /q "{startLnk}" >nul 2>&1
            del /f /q "{desktopLnk}" >nul 2>&1
            (goto) 2>nul & rmdir /s /q "{dir}"
            """, System.Text.Encoding.ASCII);
    }

    public static (long FreeBytes, string Drive) DiskInfo(string dir)
    {
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(dir)) ?? "C:\\";
            var d = new DriveInfo(root);
            return (d.AvailableFreeSpace, root);
        }
        catch (Exception e) when (e is ArgumentException or IOException)
        {
            return (0, "C:\\");
        }
    }

    public static string Format(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.0} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0} MB",
        _ => $"{bytes / 1024.0:0} KB",
    };
}
