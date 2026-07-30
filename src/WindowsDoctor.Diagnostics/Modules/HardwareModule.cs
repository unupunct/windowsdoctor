using Microsoft.Extensions.Logging;
using WindowsDoctor.Common.Enums;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.Diagnostics.Modules;

public class HardwareModule : DiagnosticModuleBase, IDiagnosticModule
{
    private readonly ISystemInfoService _sysInfo;

    public string Name        => "Hardware";
    public string Icon        => "🖥️";
    public string Description => "Checks CPU, RAM, GPU, disk health, and battery status.";

    public HardwareModule(ISystemInfoService sysInfo, ILogger<HardwareModule> logger)
        : base(logger) => _sysInfo = sysInfo;

    public async Task<DiagnosticReport> RunDiagnosticsAsync(IProgress<int> progress, CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        var findings = new List<DiagnosticFinding>();

        progress.Report(10);
        var snap = await _sysInfo.GetSnapshotAsync(ct);
        progress.Report(40);

        // CPU
        findings.Add(Info(Name, "CPU", $"CPU: {snap.CpuName}", $"{snap.CpuCores} cores, {snap.CpuUsagePercent:0}% utilisation"));

        // RAM
        var availGb = snap.AvailableRamBytes / 1_073_741_824.0;
        if (availGb < 0.5)
            findings.Add(MakeFinding(Name, "RAM.Free", HealthStatus.Error, Severity.Critical, "Critically low RAM", $"Only {availGb:0.0} GB RAM available. System may become unstable."));
        else if (availGb < 1.0)
            findings.Add(MakeFinding(Name, "RAM.Free", HealthStatus.Warning, Severity.High, "Very low available RAM", $"{availGb:0.0} GB free — consider closing applications."));
        else if (availGb < 2.0)
            findings.Add(MakeFinding(Name, "RAM.Free", HealthStatus.Warning, Severity.Medium, "Low available RAM", $"{availGb:0.0} GB free."));
        else
            findings.Add(Good(Name, "RAM.Free", "RAM availability OK", $"{availGb:0.0} GB free of {snap.TotalRamBytes / 1_073_741_824.0:0.0} GB total."));

        progress.Report(60);

        // Disk SMART
        foreach (var disk in snap.Disks)
        {
            var pct = disk.TotalBytes > 0 ? (double)disk.FreeBytes / disk.TotalBytes * 100 : 100;
            switch (disk.SmartStatus)
            {
                case SmartStatus.Warning:
                    findings.Add(MakeFinding(Name, $"SMART.{disk.Name}", HealthStatus.Warning, Severity.High,
                        $"Disk {disk.Name} SMART warning", $"Drive {disk.Name} ({disk.Label}) is reporting pre-failure attributes. Back up data immediately.", "run-chkdsk"));
                    break;
                case SmartStatus.Failed:
                    findings.Add(MakeFinding(Name, $"SMART.{disk.Name}", HealthStatus.Error, Severity.Critical,
                        $"Disk {disk.Name} SMART failure", $"Drive {disk.Name} is reporting a failure condition. Replace immediately.", "run-chkdsk"));
                    break;
                default:
                    findings.Add(Good(Name, $"SMART.{disk.Name}", $"Disk {disk.Name} health OK", $"Drive {disk.Name} ({disk.Label}) — {FormatBytes(disk.FreeBytes)} free, SMART: OK"));
                    break;
            }
        }

        progress.Report(80);

        // Battery
        if (snap.BatteryPercent.HasValue)
        {
            var pct = snap.BatteryPercent.Value;
            if (pct < 20)
                findings.Add(MakeFinding(Name, "Battery", HealthStatus.Warning, Severity.High, "Low battery", $"Battery at {pct:0}%. Connect charger."));
            else
                findings.Add(Good(Name, "Battery", "Battery OK", $"Battery at {pct:0}%."));
        }
        else
            findings.Add(Info(Name, "Battery", "No battery detected", "This appears to be a desktop system."));

        // GPU info
        findings.Add(Info(Name, "GPU", $"GPU: {snap.GpuName}", snap.GpuName));

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
}
