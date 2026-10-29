using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace VelyLicense;

// Venta de keys con NOWPayments (cripto).
// 1. La web pide POST /api/v1/orders con el plan → creamos la factura en NOWPayments y devolvemos su enlace.
// 2. El cliente paga en NOWPayments → NOWPayments avisa a POST /api/v1/nowpayments/ipn (firmado con el IPN secret).
// 3. Con el pago "finished" se crea la key; la página de gracias la lee con GET /api/v1/orders/{id}.
// Las claves de NOWPayments viven en <dataDir>\nowpayments.json, que solo edita el dueño.
public static class Payments
{
    public sealed record Plan(string Id, string Name, int Days, decimal PriceUsd);

    // Precios en dólares. Cambiar aquí y en la web (index.html) a la vez.
    public static readonly Plan[] Plans =
    [
        new("1m", "1 mes", 30, 5m),
        new("3m", "3 meses", 90, 12m),
        new("12m", "1 año", 365, 35m),
    ];

    const string WebOrigin = "https://velymediallc.com";
    const string ApiOrigin = "https://api.velymediallc.com";

    sealed record Config(string? ApiKey, string? IpnSecret, bool Sandbox);
    record OrderRequest(string? Plan);

    public static void AddPayments(this IServiceCollection services)
    {
        services.AddHttpClient("nowpayments", c => c.Timeout = TimeSpan.FromSeconds(20));
        services.AddCors(o => o.AddPolicy("web", p => p
            .WithOrigins(WebOrigin, "https://www.velymediallc.com", "http://localhost:8765")
            .WithMethods("GET", "POST")
            .WithHeaders("Content-Type")));
    }

    public static void MapPayments(this WebApplication app, string dataDir, LicenseDb licenses)
    {
        var orders = new OrderDb(Path.Combine(dataDir, "licenses.db"));
        orders.Init();
        var configFile = Path.Combine(dataDir, "nowpayments.json");
        if (!File.Exists(configFile))
            File.WriteAllText(configFile, """
                {
                  "ApiKey": "PEGA-AQUI-TU-API-KEY",
                  "IpnSecret": "PEGA-AQUI-TU-IPN-SECRET",
                  "Sandbox": false
                }
                """);

        Config? LoadConfig()
        {
            try
            {
                var c = JsonSerializer.Deserialize<Config>(File.ReadAllText(configFile));
                return c is { ApiKey.Length: > 10, IpnSecret.Length: > 10 } && !c.ApiKey.StartsWith("PEGA") ? c : null;
            }
            catch { return null; }
        }

        app.UseCors();

        app.MapGet("/api/v1/plans", () => Results.Ok(Plans.Select(p => new { p.Id, p.Name, p.Days, p.PriceUsd })))
            .RequireCors("web");

        // Crea el pedido y la factura de NOWPayments; la web redirige al cliente a invoiceUrl.
        app.MapPost("/api/v1/orders", async (OrderRequest req, IHttpClientFactory http, ILogger<Plan> log) =>
        {
            var plan = Plans.FirstOrDefault(p => p.Id == req.Plan);
            if (plan is null) return Error(400, "Plan inválido.");
            var cfg = LoadConfig();
            if (cfg is null) return Error(503, "Los pagos todavía no están configurados. Escríbenos por WhatsApp.");

            var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
            var body = new
            {
                price_amount = plan.PriceUsd,
                price_currency = "usd",
                order_id = id,
                order_description = $"VELY157 Cleaner · licencia {plan.Name}",
                ipn_callback_url = $"{ApiOrigin}/api/v1/nowpayments/ipn",
                success_url = $"{WebOrigin}/gracias.html?pedido={id}",
                cancel_url = $"{WebOrigin}/#cleaner",
            };
            var client = http.CreateClient("nowpayments");
            using var msg = new HttpRequestMessage(HttpMethod.Post, BaseUrl(cfg) + "/v1/invoice") { Content = JsonContent.Create(body) };
            msg.Headers.Add("x-api-key", cfg.ApiKey);
            try
            {
                using var res = await client.SendAsync(msg);
                var json = await res.Content.ReadAsStringAsync();
                if (!res.IsSuccessStatusCode)
                {
                    log.LogWarning("NOWPayments invoice {Status}: {Body}", (int)res.StatusCode, json);
                    return Error(502, "No pudimos crear el pago. Intenta de nuevo en un momento.");
                }
                var node = JsonNode.Parse(json)!;
                var invoiceUrl = node["invoice_url"]?.GetValue<string>();
                var invoiceId = node["id"]?.ToString();
                if (string.IsNullOrEmpty(invoiceUrl)) return Error(502, "No pudimos crear el pago. Intenta de nuevo en un momento.");
                orders.Create(id, plan, invoiceId, DateTime.UtcNow);
                return Results.Ok(new { ok = true, order = id, invoiceUrl });
            }
            catch (Exception e)
            {
                log.LogWarning(e, "NOWPayments no respondió");
                return Error(502, "No pudimos conectar con el sistema de pago. Intenta de nuevo en un momento.");
            }
        }).RequireCors("web").RequireRateLimiting("check");

        // La página de gracias consulta aquí hasta que el pago se confirma y aparece la key.
        app.MapGet("/api/v1/orders/{id}", (string id, LicenseDb db) =>
        {
            if (id.Length != 32) return Error(404, "Pedido no encontrado.");
            var o = orders.Find(id);
            if (o is null) return Error(404, "Pedido no encontrado.");
            var key = o.LicenseId is long lid ? db.FindById(lid)?.Key : null;
            return Results.Ok(new { ok = true, status = key is null ? o.Status : "paid", plan = o.PlanName, days = o.Days, key });
        }).RequireCors("web").RequireRateLimiting("check");

        // Aviso de NOWPayments. Solo se cree si la firma HMAC-SHA512 coincide con el IPN secret.
        app.MapPost("/api/v1/nowpayments/ipn", async (HttpContext ctx, ILogger<Plan> log) =>
        {
            var cfg = LoadConfig();
            if (cfg is null) return Results.StatusCode(503);
            using var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8);
            var raw = await reader.ReadToEndAsync();
            JsonNode? node;
            try { node = JsonNode.Parse(raw); } catch { return Results.BadRequest(); }
            if (node is not JsonObject) return Results.BadRequest();

            var sig = ctx.Request.Headers["x-nowpayments-sig"].ToString().Trim().ToLowerInvariant();
            var expected = Convert.ToHexString(HMACSHA512.HashData(Encoding.UTF8.GetBytes(cfg.IpnSecret!),
                Encoding.UTF8.GetBytes(SortedJson(node)))).ToLowerInvariant();
            if (sig.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(sig), Encoding.ASCII.GetBytes(expected)))
            {
                log.LogWarning("IPN con firma inválida");
                return Results.Unauthorized();
            }

            var orderId = node["order_id"]?.ToString() ?? "";
            var status = node["payment_status"]?.ToString() ?? "";
            var paymentId = node["payment_id"]?.ToString();
            var o = orders.Find(orderId);
            if (o is null) { log.LogWarning("IPN de pedido desconocido {Order}", orderId); return Results.Ok(); }

            decimal.TryParse(node["price_amount"]?.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var priceAmount);
            var priceCurrency = node["price_currency"]?.ToString()?.ToLowerInvariant();
            orders.SetStatus(orderId, status, paymentId);

            // "finished" = el dinero ya llegó a tu billetera. Recién ahí se entrega la key.
            if (status == "finished" && o.LicenseId is null)
            {
                if (priceCurrency != "usd" || priceAmount < o.PriceUsd)
                {
                    log.LogWarning("IPN con monto distinto al pedido {Order}: {Amount} {Cur}", orderId, priceAmount, priceCurrency);
                    return Results.Ok();
                }
                var lic = licenses.Create("Cliente NOWPayments", $"Pedido {orderId} · {o.PlanName} · pago {paymentId}", o.Days, DateTime.UtcNow);
                if (!orders.AttachLicense(orderId, lic.Id)) licenses.Delete(lic.Id); // otro aviso ganó la carrera
                log.LogInformation("Pedido {Order} pagado: key creada ({Days} días)", orderId, o.Days);
            }
            return Results.Ok();
        });
    }

    static string BaseUrl(Config c) => c.Sandbox ? "https://api-sandbox.nowpayments.io" : "https://api.nowpayments.io";

    static IResult Error(int status, string message) => Results.Json(new { ok = false, message }, statusCode: status);

    // NOWPayments firma el JSON con las claves ordenadas alfabéticamente y sin espacios.
    static readonly JsonSerializerOptions Compact = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    static string SortedJson(JsonNode node) => Sort(node)!.ToJsonString(Compact);

    static JsonNode? Sort(JsonNode? n) => n switch
    {
        JsonObject o => new JsonObject(o.OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => KeyValuePair.Create(p.Key, Sort(p.Value)))),
        JsonArray a => new JsonArray(a.Select(Sort).ToArray()),
        _ => n?.DeepClone(),
    };

    sealed record Order(string Id, string Plan, string PlanName, int Days, decimal PriceUsd, string Status, long? LicenseId);

    sealed class OrderDb(string path)
    {
        readonly string _cs = new SqliteConnectionStringBuilder { DataSource = path }.ToString();

        public void Init() => Exec("""
            CREATE TABLE IF NOT EXISTS orders (
                id          TEXT PRIMARY KEY,
                plan        TEXT NOT NULL,
                plan_name   TEXT NOT NULL,
                days        INTEGER NOT NULL,
                price_usd   TEXT NOT NULL,
                status      TEXT NOT NULL,
                invoice_id  TEXT,
                payment_id  TEXT,
                license_id  INTEGER,
                created_at  TEXT NOT NULL,
                updated_at  TEXT NOT NULL
            );
            """);

        public void Create(string id, Plan p, string? invoiceId, DateTime now) => Exec("""
            INSERT INTO orders (id, plan, plan_name, days, price_usd, status, invoice_id, created_at, updated_at)
            VALUES ($id, $plan, $name, $days, $price, 'waiting', $inv, $now, $now)
            """, ("$id", id), ("$plan", p.Id), ("$name", p.Name), ("$days", p.Days),
            ("$price", p.PriceUsd.ToString(CultureInfo.InvariantCulture)), ("$inv", invoiceId), ("$now", Now()));

        public Order? Find(string id)
        {
            using var c = Open();
            using var cmd = Cmd(c, "SELECT id, plan, plan_name, days, price_usd, status, license_id FROM orders WHERE id = $id", ("$id", id));
            using var r = cmd.ExecuteReader();
            return r.Read()
                ? new Order(r.GetString(0), r.GetString(1), r.GetString(2), r.GetInt32(3),
                    decimal.Parse(r.GetString(4), CultureInfo.InvariantCulture), r.GetString(5), r.IsDBNull(6) ? null : r.GetInt64(6))
                : null;
        }

        public void SetStatus(string id, string status, string? paymentId) => Exec(
            "UPDATE orders SET status = $s, payment_id = COALESCE($p, payment_id), updated_at = $now WHERE id = $id",
            ("$id", id), ("$s", status), ("$p", paymentId), ("$now", Now()));

        public bool AttachLicense(string id, long licenseId) => Exec(
            "UPDATE orders SET license_id = $l, updated_at = $now WHERE id = $id AND license_id IS NULL",
            ("$id", id), ("$l", licenseId), ("$now", Now())) == 1;

        static string Now() => DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        SqliteConnection Open() { var c = new SqliteConnection(_cs); c.Open(); return c; }

        static SqliteCommand Cmd(SqliteConnection c, string sql, params (string, object?)[] ps)
        {
            var cmd = c.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
            return cmd;
        }

        int Exec(string sql, params (string, object?)[] ps)
        {
            using var c = Open();
            using var cmd = Cmd(c, sql, ps);
            return cmd.ExecuteNonQuery();
        }
    }
}
