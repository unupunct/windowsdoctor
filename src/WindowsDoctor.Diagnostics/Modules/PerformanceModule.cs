using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using WindowsDoctor.Common.Enums;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.Diagnostics.Modules;

public class PerformanceModule : DiagnosticModuleBase, IDiagnosticModule
{
    private readonly ISystemInfoService _sysInfo;

    public string Name        => "Performance";
    public string Icon        => "⚡";
    public string Description => "Measures CPU/RAM usage, startup programs, and power configuration.";

    public PerformanceModule(ISystemInfoService sysInfo, ILogger<PerformanceModule> logger)
        : base(logger) => _sysInfo = sysInfo;

    public async Task<DiagnosticReport> RunDiagnosticsAsync(IProgress<int> progress, CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        var findings = new List<DiagnosticFinding>();

        progress.Report(10);
        var cpu = await _sysInfo.GetCpuUsageAsync(ct);
        var ram = await _sysInfo.GetRamUsagePercentAsync(ct);
        progress.Report(35);

        // CPU
        if (cpu > 90)
            findings.Add(MakeFinding(Name, "CPU.Usage", HealthStatus.Error, Severity.Critical, "Critical CPU usage", $"CPU usage is {cpu:0}%."));
        else if (cpu > 70)
            findings.Add(MakeFinding(Name, "CPU.Usage", HealthStatus.Warning, Severity.High, "High CPU usage", $"CPU is at {cpu:0}%."));
        else if (cpu > 50)
            findings.Add(MakeFinding(Name, "CPU.Usage", HealthStatus.Warning, Severity.Medium, "Elevated CPU usage", $"CPU is at {cpu:0}%."));
        else
            findings.Add(Good(Name, "CPU.Usage", "CPU usage normal", $"CPU at {cpu:0}%."));

        // RAM
        if (ram > 90)
            findings.Add(MakeFinding(Name, "RAM.Usage", HealthStatus.Error, Severity.Critical, "Critical RAM pressure", $"RAM usage: {ram:0}%."));
        else if (ram > 80)
            findings.Add(MakeFinding(Name, "RAM.Usage", HealthStatus.Warning, Severity.High, "High RAM usage", $"RAM usage: {ram:0}%."));
        else if (ram > 70)
            findings.Add(MakeFinding(Name, "RAM.Usage", HealthStatus.Warning, Severity.Medium, "Elevated RAM usage", $"RAM usage: {ram:0}%."));
        else
            findings.Add(Good(Name, "RAM.Usage", "RAM usage normal", $"RAM at {ram:0}%."));

        progress.Report(55);

        // Startup programs
        var startupCount = await Task.Run(CountStartupPrograms, ct);
        if (startupCount > 20)
            findings.Add(MakeFinding(Name, "Startup", HealthStatus.Warning, Severity.Medium,
                "Many startup programs", $"{startupCount} programs run at startup. Too many can slow boot time."));
        else if (startupCount > 15)
            findings.Add(MakeFinding(Name, "Startup", HealthStatus.Warning, Severity.Low,
                "High startup program count", $"{startupCount} startup entries found."));
        else
            findings.Add(Good(Name, "Startup", "Startup programs OK", $"{startupCount} startup programs registered."));

        progress.Report(75);

        // Power plan
        var (powerPlan, isSaver) = await Task.Run(GetPowerPlan, ct);
        if (isSaver)
            findings.Add(MakeFinding(Name, "PowerPlan", HealthStatus.Warning, Severity.Low,
                "Power Saver plan active", $"Current plan: {powerPlan}. This limits performance."));
        else
            findings.Add(Good(Name, "PowerPlan", "Power plan OK", $"Active plan: {powerPlan}."));

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

    private static int CountStartupPrograms()
    {
        var count = 0;
        var paths = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"
        };
        foreach (var p in paths)
        {
            try
            {
                using var k = Registry.LocalMachine.OpenSubKey(p);
                count += k?.ValueCount ?? 0;
            }
            catch { }
            try
            {
                using var k = Registry.CurrentUser.OpenSubKey(p);
                count += k?.ValueCount ?? 0;
            }
            catch { }
        }
        return count;
    }

    private static (string name, bool isSaver) GetPowerPlan()
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("powercfg", "/getactivescheme")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = System.Diagnostics.Process.Start(psi)!;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            var isSaver = output.Contains("Power saver", StringComparison.OrdinalIgnoreCase);
            var isHigh = output.Contains("High performance", StringComparison.OrdinalIgnoreCase);
            var name = isSaver ? "Power Saver" : isHigh ? "High Performance" : "Balanced";
            return (name, isSaver);
        }
        catch { return ("Unknown", false); }
    }
}
