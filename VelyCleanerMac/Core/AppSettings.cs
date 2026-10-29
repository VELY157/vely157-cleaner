using System.Text.Json;

namespace VelyCleanerMac.Core;

// Preferencias de la pantalla Configuracion. Antes no existian: se guardan aparte,
// nunca tocan la licencia ni las categorias de limpieza.
public sealed class AppSettings
{
    public bool CheckUpdatesOnOpen { get; set; } = true;
    public bool AnalyzeOnOpen { get; set; } = true;
    public bool TrashCheckedByDefault { get; set; }

    static string StorePath
    {
        get
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VELY157");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "settings.json");
        }
    }

    public static AppSettings Load()
    {
        try { return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(StorePath)) ?? new AppSettings(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException) { return new AppSettings(); }
    }

    public void Save()
    {
        try { File.WriteAllText(StorePath, JsonSerializer.Serialize(this)); } catch (IOException) { }
    }
}
