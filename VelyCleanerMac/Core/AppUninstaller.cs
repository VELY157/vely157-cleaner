namespace VelyCleanerMac.Core;

public sealed record InstalledApp(string Name, string Path, string? BundleId);
public sealed record LeftoverItem(string Path, long Size);

// Desinstalador real: encuentra la app + los restos que deja en ~/Library cuando
// alguien la arrastra a la Papelera a mano. Todo se mueve a la Papelera, nunca se
// borra directo — igual que hace Finder cuando "eliminás" algo.
public static class AppUninstaller
{
    static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static List<InstalledApp> ListApps()
    {
        var apps = new List<InstalledApp>();
        foreach (var dir in new[] { "/Applications", Path.Combine(Home, "Applications") })
        {
            if (!Directory.Exists(dir)) continue;
            IEnumerable<string> entries;
            try { entries = Directory.EnumerateDirectories(dir, "*.app"); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
            foreach (var appPath in entries)
            {
                var name = Path.GetFileNameWithoutExtension(appPath);
                if (name.Equals("VELY157 Cleaner", StringComparison.OrdinalIgnoreCase)) continue;
                apps.Add(new InstalledApp(name, appPath, ReadBundleId(appPath)));
            }
        }
        return apps.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    static string? ReadBundleId(string appPath)
    {
        try
        {
            var plist = Path.Combine(appPath, "Contents", "Info.plist");
            if (!File.Exists(plist)) return null;
            var text = File.ReadAllText(plist);
            const string marker = "<key>CFBundleIdentifier</key>";
            var idx = text.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return null;
            var after = text[(idx + marker.Length)..];
            var start = after.IndexOf("<string>", StringComparison.Ordinal);
            var end = after.IndexOf("</string>", StringComparison.Ordinal);
            if (start < 0 || end < 0 || end <= start) return null;
            return after[(start + 8)..end].Trim();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }

    public static List<LeftoverItem> FindLeftovers(InstalledApp app)
    {
        var simpleName = Normalize(app.Name);
        var bundleId = app.BundleId?.ToLowerInvariant();

        // Nivel de arriba (Application Support/Caches directo): exigimos que el nombre
        // de la carpeta contenga el nombre completo de la app, para no confundir una
        // carpeta "vendedor" genérica (p. ej. "Google", que también tiene Drive, Earth…).
        bool MatchesStrict(string entryName)
        {
            if (bundleId is not null && Path.GetFileNameWithoutExtension(entryName).ToLowerInvariant().Contains(bundleId)) return true;
            var lower = Normalize(Path.GetFileNameWithoutExtension(entryName));
            return simpleName.Length > 3 && lower.Contains(simpleName);
        }

        // Un nivel más adentro de una carpeta "vendedor" (p. ej. Google/Chrome): acá
        // el nombre real del producto puede ser más corto que el de la app, así que
        // comparamos en las dos direcciones.
        bool MatchesLoose(string entryName)
        {
            if (bundleId is not null && Path.GetFileNameWithoutExtension(entryName).ToLowerInvariant().Contains(bundleId)) return true;
            var lower = Normalize(Path.GetFileNameWithoutExtension(entryName));
            if (lower.Length < 4) return false;
            return lower.Contains(simpleName) || (simpleName.Length > 3 && simpleName.Contains(lower));
        }

        var dirs = new[]
        {
            Path.Combine(Home, "Library", "Application Support"),
            Path.Combine(Home, "Library", "Caches"),
            Path.Combine(Home, "Library", "Saved Application State"),
            Path.Combine(Home, "Library", "WebKit"),
            Path.Combine(Home, "Library", "HTTPStorages"),
            Path.Combine(Home, "Library", "Containers"),
            Path.Combine(Home, "Library", "Group Containers"),
            Path.Combine(Home, "Library", "Logs"),
        };

        var results = new List<LeftoverItem>();
        foreach (var dir in dirs)
        {
            if (!Directory.Exists(dir)) continue;
            IEnumerable<string> entries;
            try { entries = Directory.EnumerateFileSystemEntries(dir); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
            foreach (var entry in entries)
            {
                try
                {
                    if (MatchesStrict(entry))
                    {
                        results.Add(new LeftoverItem(entry, MacCleaner.SizeOf(entry, contentsOnly: false)));
                        continue;
                    }
                    // Muchas apps guardan sus cosas bajo una carpeta "vendedor"
                    // (p. ej. Application Support/Google/Chrome). Miramos un nivel más.
                    if (!Directory.Exists(entry)) continue;
                    foreach (var inner in Directory.EnumerateFileSystemEntries(entry))
                    {
                        if (!MatchesLoose(inner)) continue;
                        results.Add(new LeftoverItem(inner, MacCleaner.SizeOf(inner, contentsOnly: false)));
                    }
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
        }

        var prefs = Path.Combine(Home, "Library", "Preferences");
        if (Directory.Exists(prefs))
        {
            try
            {
                foreach (var f in Directory.EnumerateFiles(prefs, "*.plist"))
                    if (MatchesStrict(f)) results.Add(new LeftoverItem(f, new FileInfo(f).Length));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }

        return results.DistinctBy(r => r.Path).OrderByDescending(r => r.Size).ToList();
    }

    static string Normalize(string s) => s.Replace(" ", "").Replace("-", "").ToLowerInvariant();

    // Mueve algo a la Papelera en vez de borrarlo directo (recuperable hasta que el
    // usuario la vacíe). Necesita "Acceso total al disco" igual que el ítem Papelera.
    public static bool MoveToTrash(string path)
    {
        try
        {
            var trash = Path.Combine(Home, ".Trash");
            var dest = UniqueDestination(Path.Combine(trash, Path.GetFileName(path)));
            if (Directory.Exists(path)) Directory.Move(path, dest);
            else if (File.Exists(path)) File.Move(path, dest);
            else return false;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return false; }
    }

    static string UniqueDestination(string dest)
    {
        if (!File.Exists(dest) && !Directory.Exists(dest)) return dest;
        var dir = Path.GetDirectoryName(dest)!;
        var name = Path.GetFileNameWithoutExtension(dest);
        var ext = Path.GetExtension(dest);
        for (var i = 1; ; i++)
        {
            var candidate = Path.Combine(dir, $"{name} {i}{ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
        }
    }
}
