using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;

namespace VelyCleaner;

public sealed record LicenseToken(string Key, string Hwid, string Customer, DateTime ExpiresAt, DateTime ValidUntil, DateTime IssuedAt);

public sealed record LicenseResult(bool Valid, string Message, LicenseToken? Token = null, bool Online = false)
{
    public static LicenseResult Fail(string message) => new(false, message);
}

public sealed class LicenseClient
{
    const string DefaultCheckUrl = "https://api.velymediallc.com/api/v1/check";

    // Clave pública del servidor de licencias. Solo sirve para comprobar firmas, no para crearlas.
    const string PublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEMgY4tV7yf+wqY/tzMDgb+J4PxUcc
        W2SkyUDQ5I9IaxK+dMfRrTVVuOddyfAagtNLW7sAzzpC7TibF1cLcybdRg==
        -----END PUBLIC KEY-----
        """;

    // Margen para relojes un poco desfasados.
    static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(10);
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    static readonly byte[] Entropy = Encoding.UTF8.GetBytes("VELY157-cleaner-license-v1");

    readonly string _checkUrl;

    // checkUrl solo se cambia en pruebas; un servidor falso no puede firmar licencias.
    public LicenseClient(string? checkUrl = null) => _checkUrl = checkUrl ?? DefaultCheckUrl;

    readonly string _storePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VELY157", "license.dat");

    public string Hwid { get; } = ComputeHwid();

    public string? SavedKey => Load()?.Key;

    public async Task<LicenseResult> CheckAsync(string key)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            using var response = await http.PostAsJsonAsync(_checkUrl, new { key, hwid = Hwid, machine = Environment.MachineName });
            var body = await response.Content.ReadFromJsonAsync<CheckResponse>(Json);

            if (response.IsSuccessStatusCode && body is { Ok: true, Token: not null })
            {
                var token = Verify(body.Token);
                if (token is null || token.Hwid != Hwid)
                    return LicenseResult.Fail("Respuesta del servidor no válida.");
                Save(new StoredLicense(token.Key, body.Token, DateTime.UtcNow));
                return new LicenseResult(true, "", token, Online: true);
            }

            // 4xx = el servidor dijo que no (no existe, vencida, cancelada, otra PC): no hay modo sin conexión.
            var status = (int)response.StatusCode;
            if (status is >= 400 and < 500 && status != 429)
            {
                if (Load()?.Key == key) Clear();
                return LicenseResult.Fail(body?.Message ?? "Key no válida.");
            }
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
        }

        // Sin conexión o servidor caído: vale la última autorización firmada mientras no caduque.
        return CheckOffline(key);
    }

    // Prueba gratis de 7 días: el servidor da una sola por PC (reinstalar no da otra).
    public async Task<LicenseResult> StartTrialAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            using var response = await http.PostAsJsonAsync(_checkUrl.Replace("/check", "/trial"), new { hwid = Hwid, machine = Environment.MachineName });
            var body = await response.Content.ReadFromJsonAsync<TrialResponse>(Json);
            if (response.IsSuccessStatusCode && body is { Ok: true, Key: not null })
                return await CheckAsync(body.Key);
            return LicenseResult.Fail((body?.Message ?? "No se pudo iniciar la prueba gratis.").Replace("dispositivo", "PC"));
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            return LicenseResult.Fail("No se pudo conectar con el servidor. Revisa tu internet.");
        }
    }

    // Comprobación rápida antes de limpiar, sin internet.
    public bool StillValid(LicenseToken token) => DateTime.UtcNow < token.ValidUntil && DateTime.UtcNow < token.ExpiresAt;

    public void Clear()
    {
        try { File.Delete(_storePath); } catch (IOException) { }
    }

    LicenseResult CheckOffline(string key)
    {
        var stored = Load();
        if (stored is null || stored.Key != key)
            return LicenseResult.Fail("No se pudo conectar con el servidor de licencias. Revisa tu internet.");

        var token = Verify(stored.Token);
        var now = DateTime.UtcNow;
        if (token is null || token.Hwid != Hwid || token.Key != key)
            return LicenseResult.Fail("La licencia guardada no es válida. Conéctate a internet.");
        if (now < stored.LastSeenUtc - ClockSkew || now < token.IssuedAt - ClockSkew)
            return LicenseResult.Fail("La fecha de tu PC no es correcta. Corrígela y conéctate a internet.");
        if (now >= token.ExpiresAt)
            return LicenseResult.Fail("Tu key venció.");
        if (now >= token.ValidUntil)
            return LicenseResult.Fail("Necesitas conectarte a internet para verificar tu licencia.");

        Save(stored with { LastSeenUtc = now > stored.LastSeenUtc ? now : stored.LastSeenUtc });
        return new LicenseResult(true, "", token, Online: false);
    }

    static LicenseToken? Verify(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3 || parts[0] != "v1") return null;
            var payload = FromB64Url(parts[1]);
            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(PublicKeyPem);
            if (!ecdsa.VerifyData(payload, FromB64Url(parts[2]), HashAlgorithmName.SHA256)) return null;
            return JsonSerializer.Deserialize<LicenseToken>(payload, Json);
        }
        catch (Exception e) when (e is FormatException or JsonException or CryptographicException)
        {
            return null;
        }
    }

    // Identificador de esta instalación de Windows (no cambia si renombran la PC).
    static string ComputeHwid()
    {
        using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        var machineGuid = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography")?.GetValue("MachineGuid") as string
            ?? Environment.MachineName;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("VELY157|" + machineGuid));
        return Convert.ToHexString(hash);
    }

    StoredLicense? Load()
    {
        try
        {
            var data = ProtectedData.Unprotect(File.ReadAllBytes(_storePath), Entropy, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<StoredLicense>(data, Json);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or CryptographicException or JsonException)
        {
            return null;
        }
    }

    void Save(StoredLicense license)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_storePath)!);
        var data = ProtectedData.Protect(JsonSerializer.SerializeToUtf8Bytes(license, Json), Entropy, DataProtectionScope.CurrentUser);
        File.WriteAllBytes(_storePath, data);
    }

    static byte[] FromB64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }

    sealed record StoredLicense(string Key, string Token, DateTime LastSeenUtc);
    sealed record CheckResponse(bool Ok, string? Token, string? Message, string? Error);
    sealed record TrialResponse(bool Ok, string? Key, string? Message);
}
