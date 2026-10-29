using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;

namespace VelyCleanerMac.Core;

// En Mac la actualización abre la página de descarga (reemplazar una .app en uso no es seguro).
public static class Updates
{
    const string VersionUrl = "https://api.velymediallc.com/api/v1/mac-version";
    public const string DownloadPage = "https://velymediallc.com/descargas/VELY157-Cleaner-Mac.zip";

    public static Version Current { get; } = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    public static async Task<string?> NewerVersionAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var r = await http.GetFromJsonAsync<Resp>(VersionUrl, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return r is not null && Version.TryParse(r.Version, out var v) && v > Current ? r.Version : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    public static void Open(string url)
    {
        try
        {
            if (OperatingSystem.IsMacOS()) Process.Start("open", url);
            else Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException) { }
    }

    sealed record Resp(string Version);
}
