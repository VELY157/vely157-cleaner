using System.Diagnostics;

namespace VelyCleanerMac.Core;

public sealed record MemoryInfo(long FreeBytes, long TotalBytes);
public sealed record BatteryInfo(int Percent, int CycleCount, string Condition, bool Charging);

public static class Performance
{
    public static MemoryInfo GetMemoryInfo()
    {
        var pages = new Dictionary<string, long>();
        foreach (var line in Run("vm_stat").Split('\n'))
        {
            var idx = line.IndexOf(':');
            if (idx < 0) continue;
            var value = line[(idx + 1)..].Trim().TrimEnd('.');
            if (long.TryParse(value, out var v)) pages[line[..idx].Trim()] = v;
        }
        long Get(string k) => pages.GetValueOrDefault(k);
        // Libre + especulativa: lo que macOS puede entregar de inmediato sin tocar disco.
        var free = (Get("Pages free") + Get("Pages speculative")) * 4096L;
        var total = long.TryParse(Run("sysctl", "-n", "hw.memsize").Trim(), out var t) ? t : 0;
        return new MemoryInfo(free, total);
    }

    public static BatteryInfo GetBatteryInfo()
    {
        var batt = Run("pmset", "-g", "batt");
        var percent = 0;
        var charging = batt.Contains("AC Power") && !batt.Contains("not charging");
        var pctIdx = batt.IndexOf('%');
        if (pctIdx > 0)
        {
            var start = pctIdx;
            while (start > 0 && char.IsDigit(batt[start - 1])) start--;
            _ = int.TryParse(batt[start..pctIdx], out percent);
        }

        var cycles = 0;
        var condition = "Desconocida";
        var profile = Run("system_profiler", "SPPowerDataType");
        foreach (var line in profile.Split('\n'))
        {
            var t = line.Trim();
            if (t.StartsWith("Cycle Count:")) int.TryParse(t["Cycle Count:".Length..].Trim(), out cycles);
            else if (t.StartsWith("Condition:")) condition = t["Condition:".Length..].Trim();
        }
        return new BatteryInfo(percent, cycles, condition, charging);
    }

    // Pide tu contraseña de Mac (cuadro nativo del sistema, VELY157 Cleaner nunca la ve)
    // para liberar memoria inactiva que el sistema todavía no reclamó solo.
    public static bool FreeInactiveMemory()
    {
        try
        {
            var psi = new ProcessStartInfo("osascript") { UseShellExecute = false, RedirectStandardError = true };
            psi.ArgumentList.Add("-e");
            psi.ArgumentList.Add("do shell script \"/usr/sbin/purge\" with administrator privileges");
            using var p = Process.Start(psi);
            if (p is null) return false;
            p.WaitForExit(30000);
            return p.ExitCode == 0;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException) { return false; }
    }

    static string Run(string file, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo(file) { UseShellExecute = false, RedirectStandardOutput = true };
            foreach (var a in args) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi);
            if (p is null) return "";
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(8000);
            return output;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException) { return ""; }
    }
}
