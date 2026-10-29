using System.Diagnostics;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace VelyCleaner;

public sealed record UpdateInfo(string Version, string Url, string Sha256);

public sealed class UpdateService
{
    const string VersionUrl = "https://api.velymediallc.com/api/v1/version";

    // La misma clave pública del servidor: una actualización falsa no tiene firma válida.
    const string PublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEMgY4tV7yf+wqY/tzMDgb+J4PxUcc
        W2SkyUDQ5I9IaxK+dMfRrTVVuOddyfAagtNLW7sAzzpC7TibF1cLcybdRg==
        -----END PUBLIC KEY-----
        """;

    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static Version Current { get; } = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(1, 0, 0);

    readonly string? _versionUrl;
    public UpdateService(string? versionUrl = null) => _versionUrl = versionUrl ?? VersionUrl;

    // Devuelve la actualización solo si hay una versión más nueva y la firma es nuestra.
    public async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var r = await http.GetFromJsonAsync<VersionResponse>(_versionUrl, Json);
            if (r is null || !Version.TryParse(r.Version, out var remote) || remote <= Current) return null;
            if (!VerifySignature(r.Version, r.Sha256, r.Signature)) return null;
            return new UpdateInfo(r.Version, r.Url, r.Sha256);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    // Descarga el nuevo .exe, comprueba su firma por SHA-256 y lanza un script que
    // reemplaza el programa actual y lo vuelve a abrir.
    public async Task<string?> DownloadAndApplyAsync(UpdateInfo update, IProgress<int> progress)
    {
        var target = Environment.ProcessPath;
        if (target is null) return "No se pudo ubicar el programa para actualizarlo.";

        var temp = Path.Combine(Path.GetTempPath(), $"VELY157_update_{Guid.NewGuid():N}.exe");
        try
        {
            using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            using (var response = await http.GetAsync(update.Url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? 0;
                await using var http_stream = await response.Content.ReadAsStreamAsync();
                await using var file = File.Create(temp);
                var buffer = new byte[81920];
                long read = 0;
                int n;
                while ((n = await http_stream.ReadAsync(buffer)) > 0)
                {
                    await file.WriteAsync(buffer.AsMemory(0, n));
                    read += n;
                    if (total > 0) progress.Report((int)(read * 100 / total));
                }
            }

            if (!HashMatches(temp, update.Sha256))
            {
                File.Delete(temp);
                return "La actualización descargada no es válida. Inténtalo de nuevo.";
            }

            LaunchUpdater(temp, target);
            return null; // ok: el programa se cerrará para completarla
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or IOException or UnauthorizedAccessException)
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch (IOException) { }
            return "No se pudo descargar la actualización. Revisa tu internet.";
        }
    }

    static void LaunchUpdater(string newExe, string target)
    {
        var pid = Environment.ProcessId;
        var script = Path.Combine(Path.GetTempPath(), $"VELY157_update_{Guid.NewGuid():N}.cmd");
        File.WriteAllText(script, $"""
            @echo off
            :wait
            tasklist /fi "PID eq {pid}" | find "{pid}" >nul && (ping -n 2 127.0.0.1 >nul & goto wait)
            move /y "{newExe}" "{target}" >nul
            start "" "{target}"
            del "%~f0"
            """, Encoding.ASCII);
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{script}\"") { CreateNoWindow = true, UseShellExecute = false });
    }

    static bool HashMatches(string path, string expectedHex)
    {
        using var fs = File.OpenRead(path);
        return string.Equals(Convert.ToHexString(SHA256.HashData(fs)), expectedHex, StringComparison.OrdinalIgnoreCase);
    }

    static bool VerifySignature(string version, string sha256, string signature)
    {
        try
        {
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(PublicKeyPem);
            var data = Encoding.UTF8.GetBytes($"VELY157-UPDATE|{version}|{sha256}");
            var sig = Convert.FromBase64String(signature.Replace('-', '+').Replace('_', '/').PadRight(signature.Length + (4 - signature.Length % 4) % 4, '='));
            return ecdsa.VerifyData(data, sig, HashAlgorithmName.SHA256);
        }
        catch (Exception e) when (e is FormatException or CryptographicException)
        {
            return false;
        }
    }

    sealed record VersionResponse(string Version, string Url, string Sha256, string Signature);
}
