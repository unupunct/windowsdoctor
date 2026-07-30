using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using WindowsDoctor.Common.Enums;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.Diagnostics.Modules;

public class WindowsHealthModule : DiagnosticModuleBase, IDiagnosticModule
{
    private readonly ISystemInfoService _sysInfo;

    public string Name        => "Windows Health";
    public string Icon        => "🪟";
    public string Description => "Verifies Windows Update, system file integrity, and critical services.";

    public WindowsHealthModule(ISystemInfoService sysInfo, ILogger<WindowsHealthModule> logger)
        : base(logger) => _sysInfo = sysInfo;

    public async Task<DiagnosticReport> RunDiagnosticsAsync(IProgress<int> progress, CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        var findings = new List<DiagnosticFinding>();

        progress.Report(5);
        var snap = await _sysInfo.GetSnapshotAsync(ct);
        progress.Report(20);

        // Windows Update pending reboot
        if (snap.WindowsUpdatePending)
            findings.Add(MakeFinding(Name, "Update.RebootPending", HealthStatus.Warning, Severity.Medium,
                "Windows Update pending restart", "A system restart is required to finish installing Windows Updates.", "restart-windows-update"));
        else
            findings.Add(Good(Name, "Update.RebootPending", "No pending reboot", "No Windows Update restart is pending."));

        progress.Report(35);

        // Check for pending reboot from other sources
        var pendingReboot = await Task.Run(CheckOtherPendingReboot, ct);
        if (pendingReboot)
            findings.Add(MakeFinding(Name, "PendingReboot", HealthStatus.Warning, Severity.Low,
                "Pending reboot detected", "A reboot is pending from a software installation or component update."));

        // Windows Update last check time
        var (lastCheck, updateOk) = await Task.Run(CheckWindowsUpdateLastRun, ct);
        if (!updateOk)
            findings.Add(MakeFinding(Name, "Update.LastCheck", HealthStatus.Warning, Severity.High,
                "Windows Update not checked recently", $"Last check: {lastCheck}. Enable automatic updates.", "restart-windows-update"));
        else
            findings.Add(Good(Name, "Update.LastCheck", "Windows Update OK", $"Last checked: {lastCheck}."));

        progress.Report(55);

        // CBS log check for corruption
        var corruptionFound = await Task.Run(CheckCbsLog, ct);
        if (corruptionFound)
            findings.Add(MakeFinding(Name, "SystemFiles.CBS", HealthStatus.Warning, Severity.High,
                "System file corruption detected", "CBS.log indicates system file issues. Run SFC and DISM to repair.", "run-sfc"));
        else
            findings.Add(Good(Name, "SystemFiles.CBS", "No system file corruption detected", "CBS.log shows no recent integrity issues."));

        progress.Report(75);

        // Event log critical errors
        var (critCount, errors) = await Task.Run(GetCriticalErrors, ct);
        if (critCount > 10)
            findings.Add(MakeFinding(Name, "EventLog.Critical", HealthStatus.Warning, Severity.High,
                $"{critCount} critical errors in event log", $"High number of critical events in the last 24 hours: {errors}", null,
                $"Top sources: {errors}"));
        else if (critCount > 2)
            findings.Add(MakeFinding(Name, "EventLog.Critical", HealthStatus.Warning, Severity.Medium,
                $"{critCount} critical events (24h)", $"Recent critical events: {errors}"));
        else
            findings.Add(Good(Name, "EventLog.Critical", "Event log looks healthy", $"Only {critCount} critical events in last 24h."));

        // Defender status
        if (!snap.DefenderEnabled)
            findings.Add(MakeFinding(Name, "Defender", HealthStatus.Error, Severity.Critical,
                "Windows Defender disabled", "Real-time protection is off. Your system is at risk.", "enable-defender"));

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

    private static bool CheckOtherPendingReboot()
    {
        try
        {
            var keys = new[]
            {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending",
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired",
                @"SYSTEM\CurrentControlSet\Control\Session Manager\PendingFileRenameOperations"
            };
            foreach (var k in keys)
            {
                using var key = Registry.LocalMachine.OpenSubKey(k);
                if (key is not null) return true;
            }
        }
        catch { }
        return false;
    }

    private static (string lastCheck, bool ok) CheckWindowsUpdateLastRun()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update");
            var lastSuccess = key?.GetValue("LastSuccessTime")?.ToString();
            if (lastSuccess is not null && DateTime.TryParse(lastSuccess, out var dt))
            {
                var age = DateTime.Now - dt;
                return (dt.ToString("yyyy-MM-dd"), age.TotalDays < 30);
            }
        }
        catch { }
        return ("Unknown", false);
    }

    private static bool CheckCbsLog()
    {
        var cbsPath = @"C:\Windows\Logs\CBS\CBS.log";
        try
        {
            if (!File.Exists(cbsPath)) return false;
            using var fs = new FileStream(cbsPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var length = fs.Length;
            var readBytes = Math.Min(2000, length);
            fs.Seek(-readBytes, SeekOrigin.End);
            var buf = new byte[readBytes];
            fs.Read(buf, 0, (int)readBytes);
            var text = System.Text.Encoding.UTF8.GetString(buf);
            return text.Contains("corruption", StringComparison.OrdinalIgnoreCase) ||
                   text.Contains("repair", StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    }

    private static (int count, string topSources) GetCriticalErrors()
    {
        try
        {
            var log = new EventLog("System");
            var cutoff = DateTime.Now.AddHours(-24);
            var errors = log.Entries
                .Cast<EventLogEntry>()
                .Where(e => e.TimeGenerated >= cutoff &&
                            e.EntryType is EventLogEntryType.Error or EventLogEntryType.FailureAudit)
                .ToList();
            var top = errors.GroupBy(e => e.Source)
                .OrderByDescending(g => g.Count())
                .Take(3)
                .Select(g => $"{g.Key}({g.Count()})")
                .ToList();
            return (errors.Count, string.Join(", ", top));
        }
        catch { return (0, "Access denied"); }
    }
}
