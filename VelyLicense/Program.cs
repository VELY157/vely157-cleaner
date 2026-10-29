using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using VelyLicense;

// Cuánto tiempo puede usarse el limpiador sin conectarse al servidor (por si tu PC está apagada).
var gracePeriod = TimeSpan.FromDays(3);
// Días de la prueba gratis de la app de Android.
const int trialDays = 7;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});
builder.Host.UseWindowsService(o => o.ServiceName = "VelyLicense");
builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://localhost:5157");

var dataDir = builder.Configuration["DataDir"]
    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VelyLicense");
DataFolder.Ensure(dataDir);

var db = new LicenseDb(Path.Combine(dataDir, "licenses.db"));
db.Init();
builder.Services.AddSingleton(db);
builder.Services.AddSingleton(LicenseSigner.LoadOrCreate(dataDir));

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(dataDir, "dp-keys")))
    .SetApplicationName("VelyLicense");

// cloudflared llega desde localhost y avisa que la visita original era HTTPS.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o =>
{
    o.LoginPath = "/admin/login";
    o.Cookie.Name = "vely_admin";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    o.ExpireTimeSpan = TimeSpan.FromHours(12);
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = ctx =>
    {
        if (ctx.Request.Path.StartsWithSegments("/admin/api")) ctx.Response.StatusCode = 401;
        else ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.OnRejected = (ctx, ct) => new ValueTask(ctx.HttpContext.Response.WriteAsync("Demasiados intentos. Espera unos minutos.", ct));
    o.AddPolicy("check", ctx => RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(ClientIp(ctx),
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(5) }));
});

builder.Services.AddPayments();

var app = builder.Build();

// Primera vez: contraseña del panel al azar, guardada en un archivo solo para ti.
if (db.GetSetting("admin_password") is null)
{
    var initial = KeyGenerator.RandomPassword(16);
    db.SetSetting("admin_password", PasswordHasher.Hash(initial));
    var file = Path.Combine(dataDir, "admin-password-inicial.txt");
    File.WriteAllText(file, initial);
    app.Logger.LogWarning("Contraseña inicial del panel guardada en {File}. Cámbiala desde el panel.", file);
}

app.UseForwardedHeaders();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    ctx.Response.Headers["Cache-Control"] = "no-store";
    await next();
});
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ---------- API pública (la usa el limpiador) ----------

app.MapGet("/", () => Results.Text("VELY157 License API"));
app.MapGet("/api/v1/health", () => Results.Ok(new { ok = true, time = DateTime.UtcNow }));

// Auto-actualización: la última versión del limpiador vive en <dataDir>\cleaner
// (version.txt + "VELY157 Cleaner.exe"); install-velylicense.ps1 la deja ahí.
var cleanerDir = Path.Combine(dataDir, "cleaner");
var cleanerExe = Path.Combine(cleanerDir, "VELY157 Cleaner.exe");
var cleanerVersion = Path.Combine(cleanerDir, "version.txt");

// El limpiador solo instala una actualización si la firma (versión + SHA-256 del exe) es nuestra.
(DateTime Stamp, string Sha256)? cleanerHash = null;
app.MapGet("/api/v1/version", (LicenseSigner signer) =>
{
    if (!File.Exists(cleanerVersion) || !File.Exists(cleanerExe)) return Results.NotFound();
    var stamp = File.GetLastWriteTimeUtc(cleanerExe);
    if (cleanerHash?.Stamp != stamp)
    {
        using var fs = File.OpenRead(cleanerExe);
        cleanerHash = (stamp, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(fs)));
    }
    var version = File.ReadAllText(cleanerVersion).Trim();
    var sha256 = cleanerHash.Value.Sha256;
    return Results.Ok(new
    {
        version,
        url = "https://api.velymediallc.com/download/cleaner",
        sha256,
        signature = signer.SignText($"VELY157-UPDATE|{version}|{sha256}"),
    });
});

// Versión de Mac: el limpiador de Mac solo compara la versión y abre la página de descarga.
app.MapGet("/api/v1/mac-version", () =>
{
    var f = Path.Combine(cleanerDir, "mac-version.txt");
    return File.Exists(f) ? Results.Ok(new { version = File.ReadAllText(f).Trim() }) : Results.NotFound();
});

app.MapGet("/download/cleaner", () =>
    File.Exists(cleanerExe)
        ? Results.File(cleanerExe, "application/octet-stream", "VELY157 Cleaner.exe")
        : Results.NotFound());

app.MapPost("/api/v1/check", (CheckRequest req, HttpContext ctx, LicenseDb db, LicenseSigner signer) =>
{
    var key = KeyGenerator.Normalize(req.Key);
    var hwid = (req.Hwid ?? "").Trim();
    if (key is null || hwid.Length is < 16 or > 128)
        return Fail(400, "bad_request", "Key inválida.");

    var lic = db.FindByKey(key);
    if (lic is null) return Fail(404, "invalid_key", "Esa key no existe.");
    if (lic.Revoked) return Fail(403, "revoked", "Esta key fue cancelada.");

    var now = DateTime.UtcNow;
    if (lic.Hwid is null)
    {
        db.Activate(lic.Id, hwid, Clip(req.Machine, 64), now);
        lic = db.FindById(lic.Id)!;
    }
    if (!string.Equals(lic.Hwid, hwid, StringComparison.Ordinal))
        return Fail(403, "other_machine", "Esta key ya está activada en otra PC.");

    db.Touch(lic.Id, ClientIp(ctx), now);
    var expires = lic.ExpiresAt!.Value;
    if (now >= expires)
        return Fail(403, "expired", $"Tu key venció el {expires.ToLocalTime():dd/MM/yyyy}.");

    var validUntil = expires < now + gracePeriod ? expires : now + gracePeriod;
    var token = signer.Sign(new LicenseToken(lic.Key, hwid, lic.Customer, expires, validUntil, now));
    return Results.Ok(new { ok = true, token, customer = lic.Customer, expiresAt = expires });
}).RequireRateLimiting("check");

// Prueba gratis (app de Android): una por dispositivo. Devuelve una key normal de 7 días
// ya activada en ese dispositivo; reinstalar la app devuelve la misma key, no una nueva.
app.MapPost("/api/v1/trial", (CheckRequest req, LicenseDb db) =>
{
    var hwid = (req.Hwid ?? "").Trim();
    if (hwid.Length is < 16 or > 128) return Fail(400, "bad_request", "Dispositivo inválido.");

    var lic = db.GetOrCreateTrial(hwid, Clip(req.Machine, 64), trialDays, DateTime.UtcNow);
    if (lic.Revoked) return Fail(403, "revoked", "La prueba gratis de este dispositivo fue cancelada.");
    if (DateTime.UtcNow >= lic.ExpiresAt) return Fail(403, "trial_used", "La prueba gratis de este dispositivo ya terminó.");
    return Results.Ok(new { ok = true, key = lic.Key, expiresAt = lic.ExpiresAt });
}).RequireRateLimiting("check");

// Venta de keys con NOWPayments (ver Payments.cs).
app.MapPayments(dataDir, db);

// ---------- Panel de administración ----------

app.MapGet("/admin/login", () => Pages.Html("login.html"));

app.MapPost("/admin/login", async (HttpContext ctx, LicenseDb db) =>
{
    var form = await ctx.Request.ReadFormAsync();
    var stored = db.GetSetting("admin_password");
    if (stored is null || !PasswordHasher.Verify(form["password"].ToString(), stored))
        return Results.Redirect("/admin/login?e=1");
    var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignInAsync(new ClaimsPrincipal(identity));
    return Results.Redirect("/admin");
}).RequireRateLimiting("login");

var admin = app.MapGroup("/admin").RequireAuthorization();
admin.MapGet("", () => Pages.Html("admin.html"));
admin.MapPost("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync();
    return Results.Redirect("/admin/login");
});

var api = admin.MapGroup("/api");

api.MapGet("/licenses", (LicenseDb db) =>
{
    var now = DateTime.UtcNow;
    return db.All().Select(l => new
    {
        l.Id, l.Key, l.Customer, l.Note, l.DurationDays, l.CreatedAt, l.ActivatedAt, l.ExpiresAt,
        l.MachineName, l.LastSeenAt, l.LastIp, l.Checks,
        Status = l.Status(now),
        BoundToPc = l.Hwid is not null,
    });
});

api.MapPost("/licenses", (CreateRequest r, LicenseDb db) =>
{
    var customer = Clip(r.Customer, 80);
    if (customer is null) return Fail(400, "bad_request", "Pon el nombre del cliente.");
    if (r.Days is < 1 or > 3650) return Fail(400, "bad_request", "Los días deben estar entre 1 y 3650.");
    var count = Math.Clamp(r.Count ?? 1, 1, 50);
    var now = DateTime.UtcNow;
    var created = Enumerable.Range(0, count).Select(_ => db.Create(customer, Clip(r.Note, 200), r.Days, now).Key).ToList();
    return Results.Ok(new { ok = true, keys = created });
});

api.MapPost("/licenses/{id:long}/revoke", (long id, LicenseDb db) => Done(db.SetRevoked(id, true)));
api.MapPost("/licenses/{id:long}/restore", (long id, LicenseDb db) => Done(db.SetRevoked(id, false)));
api.MapPost("/licenses/{id:long}/reset-pc", (long id, LicenseDb db) => Done(db.ResetMachine(id)));
api.MapPost("/licenses/{id:long}/extend", (long id, ExtendRequest r, LicenseDb db) =>
    r.Days is < 1 or > 3650 ? Fail(400, "bad_request", "Días inválidos.") : Done(db.Extend(id, r.Days, DateTime.UtcNow)));
api.MapDelete("/licenses/{id:long}", (long id, LicenseDb db) => Done(db.Delete(id)));

api.MapPost("/password", (PasswordRequest r, LicenseDb db) =>
{
    if (!PasswordHasher.Verify(r.Current ?? "", db.GetSetting("admin_password")!))
        return Fail(403, "wrong_password", "La contraseña actual no es correcta.");
    if ((r.New ?? "").Length < 10)
        return Fail(400, "weak_password", "La nueva contraseña debe tener al menos 10 caracteres.");
    db.SetSetting("admin_password", PasswordHasher.Hash(r.New!));
    File.Delete(Path.Combine(dataDir, "admin-password-inicial.txt"));
    return Results.Ok(new { ok = true });
});

app.Run();

static IResult Fail(int status, string error, string message) =>
    Results.Json(new { ok = false, error, message }, statusCode: status);

static IResult Done(bool found) =>
    found ? Results.Ok(new { ok = true }) : Fail(404, "not_found", "No existe.");

static string? Clip(string? s, int max)
{
    s = s?.Trim();
    return string.IsNullOrEmpty(s) ? null : s.Length <= max ? s : s[..max];
}

static string ClientIp(HttpContext ctx) =>
    ctx.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
    ?? ctx.Connection.RemoteIpAddress?.ToString()
    ?? "?";

record CheckRequest(string? Key, string? Hwid, string? Machine);
record CreateRequest(string? Customer, int Days, string? Note, int? Count);
record ExtendRequest(int Days);
record PasswordRequest(string? Current, string? New);
