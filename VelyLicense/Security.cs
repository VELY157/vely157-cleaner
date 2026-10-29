using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace VelyLicense;

// Lo que el limpiador recibe y guarda. Va firmado: si alguien cambia una fecha, la firma deja de valer.
public sealed record LicenseToken(
    string Key,
    string Hwid,
    string Customer,
    DateTime ExpiresAt,
    DateTime ValidUntil,
    DateTime IssuedAt);

public sealed class LicenseSigner
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    readonly ECDsa _key;
    public string PublicKeyPem { get; }

    LicenseSigner(ECDsa key)
    {
        _key = key;
        PublicKeyPem = key.ExportSubjectPublicKeyInfoPem();
    }

    // La clave privada se crea una sola vez y nunca sale de esta PC.
    // public-key.pem es la que va dentro del limpiador.
    public static LicenseSigner LoadOrCreate(string dataDir)
    {
        var privatePath = Path.Combine(dataDir, "signing-key.pem");
        var key = ECDsa.Create();
        if (File.Exists(privatePath))
        {
            key.ImportFromPem(File.ReadAllText(privatePath));
        }
        else
        {
            key.GenerateKey(ECCurve.NamedCurves.nistP256);
            File.WriteAllText(privatePath, key.ExportECPrivateKeyPem());
        }
        var signer = new LicenseSigner(key);
        File.WriteAllText(Path.Combine(dataDir, "public-key.pem"), signer.PublicKeyPem);
        return signer;
    }

    // Formato: v1.<payload base64url>.<firma base64url>
    public string Sign(LicenseToken token)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(token, Json);
        var signature = _key.SignData(payload, HashAlgorithmName.SHA256);
        return $"v1.{B64Url(payload)}.{B64Url(signature)}";
    }

    // Firma de un texto cualquiera (se usa para las actualizaciones del limpiador).
    public string SignText(string text) =>
        B64Url(_key.SignData(Encoding.UTF8.GetBytes(text), HashAlgorithmName.SHA256));

    static string B64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public static class KeyGenerator
{
    // Sin 0/O ni 1/I para que no se confundan al escribirlas.
    const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static string NewKey() =>
        "VELY-" + string.Join('-', Enumerable.Range(0, 4).Select(_ => Random(5)));

    public static string RandomPassword(int length) => Random(length);

    // Acepta la key con o sin guiones, en minúsculas o con espacios.
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var s = new string(input.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (s.Length != 24 || !s.StartsWith("VELY")) return null;
        s = s[4..];
        return $"VELY-{s[..5]}-{s[5..10]}-{s[10..15]}-{s[15..]}";
    }

    static string Random(int length) => string.Create(length, 0, (span, _) =>
    {
        for (var i = 0; i < span.Length; i++)
            span[i] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
    });
}

public static class PasswordHasher
{
    const int Iterations = 210_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('$');
        if (parts.Length != 4 || parts[0] != "pbkdf2" || !int.TryParse(parts[1], out var iterations))
            return false;
        var salt = Convert.FromBase64String(parts[2]);
        var expected = Convert.FromBase64String(parts[3]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}

public static class DataFolder
{
    // Crea la carpeta de datos dejando acceso solo a SYSTEM, Administradores y el usuario actual,
    // porque ahí vive la clave privada que firma las licencias.
    public static void Ensure(string path)
    {
        if (Directory.Exists(path)) return;
        var dir = Directory.CreateDirectory(path);
        if (!OperatingSystem.IsWindows()) return;

        var acl = new DirectorySecurity();
        acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        const InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        foreach (var sid in new[]
        {
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            WindowsIdentity.GetCurrent().User!,
        })
        {
            acl.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        }
        dir.SetAccessControl(acl);
    }
}

public static class Pages
{
    public static IResult Html(string name)
    {
        using var stream = typeof(Pages).Assembly.GetManifestResourceStream($"VelyLicense.Pages.{name}")
            ?? throw new FileNotFoundException(name);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Results.Content(reader.ReadToEnd(), "text/html; charset=utf-8");
    }
}
