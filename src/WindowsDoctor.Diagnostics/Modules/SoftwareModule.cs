using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using WindowsDoctor.Common.Enums;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.Diagnostics.Modules;

public class SoftwareModule : DiagnosticModuleBase, IDiagnosticModule
{
    public string Name        => "Software";
    public string Icon        => "📦";
    public string Description => "Inventories installed software, finds outdated or suspicious apps.";

    public SoftwareModule(ILogger<SoftwareModule> logger) : base(logger) { }

    public async Task<DiagnosticReport> RunDiagnosticsAsync(IProgress<int> progress, CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        var findings = new List<DiagnosticFinding>();

        progress.Report(10);
        var apps = await Task.Run(GetInstalledApps, ct);
        progress.Report(60);

        findings.Add(Info(Name, "Total", $"{apps.Count} applications installed", $"Found {apps.Count} installed programs."));

        var noPublisher = apps.Where(a => string.IsNullOrWhiteSpace(a.Publisher)).ToList();
        if (noPublisher.Count > 10)
            findings.Add(MakeFinding(Name, "NoPublisher", HealthStatus.Warning, Severity.Low,
                $"{noPublisher.Count} apps with no publisher", "Apps without a publisher may be low-quality or suspicious."));

        var cutoff = DateTime.Now.AddYears(-2);
        var old = apps.Where(a => a.InstallDate.HasValue && a.InstallDate < cutoff).ToList();
        if (old.Count > 15)
            findings.Add(MakeFinding(Name, "OldApps", HealthStatus.Warning, Severity.Low,
                $"{old.Count} apps older than 2 years", "Consider reviewing and updating older software."));
        else
            findings.Add(Good(Name, "OldApps", "Software age OK", $"Only {old.Count} apps older than 2 years."));

        // Check winget
        var wingetOk = await Task.Run(CheckWinget, ct);
        if (wingetOk)
            findings.Add(Good(Name, "Winget", "winget available", "Windows Package Manager (winget) is installed."));
        else
            findings.Add(Info(Name, "Winget", "winget not found", "winget is not available. Install from the Microsoft Store."));

        progress.Report(100);

        return new DiagnosticReport
        {
            ModuleName = Name,
            StartedAt = started,
            CompletedAt = DateTime.UtcNow,
            HealthScore = CalculateScore(findings.Where(f => f.Status != HealthStatus.Good).ToList()),
            Findings = findings
        };
    }

    private record AppInfo(string Name, string? Publisher, DateTime? InstallDate);

    private static List<AppInfo> GetInstalledApps()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<AppInfo>();
        var keys = new[]
        {
            (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"),
            (Registry.CurrentUser,  @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"),
        };
        foreach (var (hive, path) in keys)
        {
            try
            {
                using var parent = hive.OpenSubKey(path);
                if (parent is null) continue;
                foreach (var subName in parent.GetSubKeyNames())
                {
                    using var sub = parent.OpenSubKey(subName);
                    if (sub is null) continue;
                    var name = sub.GetValue("DisplayName")?.ToString();
                    if (string.IsNullOrWhiteSpace(name) || !seen.Add(name)) continue;
                    var pub = sub.GetValue("Publisher")?.ToString();
                    var dateStr = sub.GetValue("InstallDate")?.ToString();
                    DateTime? date = null;
                    if (dateStr?.Length == 8 &&
                        DateTime.TryParseExact(dateStr, "yyyyMMdd", null,
                            System.Globalization.DateTimeStyles.None, out var d))
                        date = d;
                    result.Add(new AppInfo(name, pub, date));
                }
            }
            catch { }
        }
        return result;
    }

    private static bool CheckWinget()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("winget", "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = System.Diagnostics.Process.Start(psi)!;
            p.WaitForExit(3000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }
}
