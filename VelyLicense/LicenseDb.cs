using System.Globalization;
using Microsoft.Data.Sqlite;

namespace VelyLicense;

public sealed record License(
    long Id,
    string Key,
    string Customer,
    string? Note,
    int DurationDays,
    DateTime CreatedAt,
    DateTime? ActivatedAt,
    DateTime? ExpiresAt,
    bool Revoked,
    string? Hwid,
    string? MachineName,
    DateTime? LastSeenAt,
    string? LastIp,
    long Checks)
{
    public string Status(DateTime now) =>
        Revoked ? "cancelada"
        : ExpiresAt is null ? "sin_activar"
        : now >= ExpiresAt ? "vencida"
        : "activa";
}

public sealed class LicenseDb(string path)
{
    const string Columns =
        "id, key, customer, note, duration_days, created_at, activated_at, expires_at, revoked, hwid, machine_name, last_seen_at, last_ip, checks";

    readonly string _connectionString = new SqliteConnectionStringBuilder { DataSource = path }.ToString();

    public void Init()
    {
        using var c = Open();
        Exec(c, """
            PRAGMA journal_mode=WAL;
            CREATE TABLE IF NOT EXISTS licenses (
                id            INTEGER PRIMARY KEY AUTOINCREMENT,
                key           TEXT NOT NULL UNIQUE,
                customer      TEXT NOT NULL,
                note          TEXT,
                duration_days INTEGER NOT NULL,
                created_at    TEXT NOT NULL,
                activated_at  TEXT,
                expires_at    TEXT,
                revoked       INTEGER NOT NULL DEFAULT 0,
                hwid          TEXT,
                machine_name  TEXT,
                last_seen_at  TEXT,
                last_ip       TEXT,
                checks        INTEGER NOT NULL DEFAULT 0
            );
            CREATE TABLE IF NOT EXISTS settings (
                k TEXT PRIMARY KEY,
                v TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS trials (
                hwid       TEXT PRIMARY KEY,
                license_id INTEGER NOT NULL,
                created_at TEXT NOT NULL
            );
            """);
    }

    // Una sola prueba gratis por dispositivo: si ya la pidió (aunque haya reinstalado), devuelve la misma.
    public License GetOrCreateTrial(string hwid, string? machineName, int days, DateTime now)
    {
        using (var c = Open())
        {
            using var cmd = Command(c, "SELECT license_id FROM trials WHERE hwid = $hwid", ("$hwid", hwid));
            if (cmd.ExecuteScalar() is long existing && FindById(existing) is { } old) return old;
        }

        var lic = Create("Prueba gratis", $"Prueba {days} días · {machineName ?? "?"}", days, now);
        Activate(lic.Id, hwid, machineName, now);
        using (var c = Open())
        {
            // Si otra petición del mismo dispositivo ganó la carrera, se queda la suya.
            if (Exec(c, "INSERT OR IGNORE INTO trials (hwid, license_id, created_at) VALUES ($hwid, $id, $now)",
                    ("$hwid", hwid), ("$id", lic.Id), ("$now", T(now))) == 0)
            {
                Delete(lic.Id);
                return GetOrCreateTrial(hwid, machineName, days, now);
            }
        }
        return FindById(lic.Id)!;
    }

    public License? FindByKey(string key)
    {
        using var c = Open();
        return Query(c, $"SELECT {Columns} FROM licenses WHERE key = $key", ("$key", key)).FirstOrDefault();
    }

    public License? FindById(long id)
    {
        using var c = Open();
        return Query(c, $"SELECT {Columns} FROM licenses WHERE id = $id", ("$id", id)).FirstOrDefault();
    }

    public List<License> All()
    {
        using var c = Open();
        return Query(c, $"SELECT {Columns} FROM licenses ORDER BY id DESC");
    }

    public License Create(string customer, string? note, int days, DateTime now)
    {
        using var c = Open();
        while (true)
        {
            var key = KeyGenerator.NewKey();
            try
            {
                Exec(c, """
                    INSERT INTO licenses (key, customer, note, duration_days, created_at)
                    VALUES ($key, $customer, $note, $days, $now)
                    """,
                    ("$key", key), ("$customer", customer), ("$note", note), ("$days", days), ("$now", T(now)));
                return FindByKey(key)!;
            }
            catch (SqliteException e) when (e.SqliteErrorCode == 19) // UNIQUE: key repetida, se genera otra
            {
            }
        }
    }

    // Los días empiezan a contar la primera vez que se activa.
    // Si se hizo "Reset PC", conserva la fecha de vencimiento que ya tenía.
    // Devuelve false si otra PC la activó un instante antes.
    public bool Activate(long id, string hwid, string? machineName, DateTime now)
    {
        using var c = Open();
        var lic = Query(c, $"SELECT {Columns} FROM licenses WHERE id = $id", ("$id", id)).Single();
        var expires = lic.ExpiresAt ?? now.AddDays(lic.DurationDays);
        return Exec(c, """
            UPDATE licenses
            SET hwid = $hwid, machine_name = $machine,
                activated_at = COALESCE(activated_at, $now), expires_at = $expires
            WHERE id = $id AND hwid IS NULL
            """,
            ("$id", id), ("$hwid", hwid), ("$machine", machineName), ("$now", T(now)), ("$expires", T(expires))) == 1;
    }

    public void Touch(long id, string ip, DateTime now)
    {
        using var c = Open();
        Exec(c, "UPDATE licenses SET last_seen_at = $now, last_ip = $ip, checks = checks + 1 WHERE id = $id",
            ("$id", id), ("$now", T(now)), ("$ip", ip));
    }

    public bool SetRevoked(long id, bool revoked)
    {
        using var c = Open();
        return Exec(c, "UPDATE licenses SET revoked = $r WHERE id = $id", ("$id", id), ("$r", revoked ? 1 : 0)) == 1;
    }

    public bool ResetMachine(long id)
    {
        using var c = Open();
        return Exec(c, "UPDATE licenses SET hwid = NULL, machine_name = NULL WHERE id = $id", ("$id", id)) == 1;
    }

    // Sin activar: suma días a la duración. Activada: suma días desde el vencimiento,
    // o desde hoy si ya venció.
    public bool Extend(long id, int days, DateTime now)
    {
        using var c = Open();
        var lic = Query(c, $"SELECT {Columns} FROM licenses WHERE id = $id", ("$id", id)).FirstOrDefault();
        if (lic is null) return false;
        if (lic.ExpiresAt is null)
            return Exec(c, "UPDATE licenses SET duration_days = duration_days + $d WHERE id = $id", ("$id", id), ("$d", days)) == 1;
        var from = lic.ExpiresAt.Value > now ? lic.ExpiresAt.Value : now;
        return Exec(c, "UPDATE licenses SET expires_at = $e WHERE id = $id", ("$id", id), ("$e", T(from.AddDays(days)))) == 1;
    }

    public bool Delete(long id)
    {
        using var c = Open();
        return Exec(c, "DELETE FROM licenses WHERE id = $id", ("$id", id)) == 1;
    }

    public string? GetSetting(string key)
    {
        using var c = Open();
        using var cmd = Command(c, "SELECT v FROM settings WHERE k = $k", ("$k", key));
        return cmd.ExecuteScalar() as string;
    }

    public void SetSetting(string key, string value)
    {
        using var c = Open();
        Exec(c, "INSERT INTO settings (k, v) VALUES ($k, $v) ON CONFLICT(k) DO UPDATE SET v = excluded.v",
            ("$k", key), ("$v", value));
    }

    SqliteConnection Open()
    {
        var c = new SqliteConnection(_connectionString);
        c.Open();
        return c;
    }

    static SqliteCommand Command(SqliteConnection c, string sql, params (string Name, object? Value)[] ps)
    {
        var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in ps)
            cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    static int Exec(SqliteConnection c, string sql, params (string, object?)[] ps)
    {
        using var cmd = Command(c, sql, ps);
        return cmd.ExecuteNonQuery();
    }

    static List<License> Query(SqliteConnection c, string sql, params (string, object?)[] ps)
    {
        using var cmd = Command(c, sql, ps);
        using var r = cmd.ExecuteReader();
        var list = new List<License>();
        while (r.Read())
        {
            list.Add(new License(
                r.GetInt64(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3),
                r.GetInt32(4), P(r, 5)!.Value, P(r, 6), P(r, 7), r.GetInt32(8) == 1,
                r.IsDBNull(9) ? null : r.GetString(9), r.IsDBNull(10) ? null : r.GetString(10),
                P(r, 11), r.IsDBNull(12) ? null : r.GetString(12), r.GetInt64(13)));
        }
        return list;
    }

    static string T(DateTime d) => d.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

    static DateTime? P(SqliteDataReader r, int i) =>
        r.IsDBNull(i) ? null : DateTime.Parse(r.GetString(i), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}
