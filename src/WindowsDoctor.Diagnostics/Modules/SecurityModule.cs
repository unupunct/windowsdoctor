using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using WindowsDoctor.Common.Enums;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.Diagnostics.Modules;

public class SecurityModule : DiagnosticModuleBase, IDiagnosticModule
{
    private readonly ISystemInfoService _sysInfo;

    public string Name        => "Security";
    public string Icon        => "🛡️";
    public string Description => "Audits Windows security settings, Defender, firewall, and account policies.";

    public SecurityModule(ISystemInfoService sysInfo, ILogger<SecurityModule> logger)
        : base(logger) => _sysInfo = sysInfo;

    public async Task<DiagnosticReport> RunDiagnosticsAsync(IProgress<int> progress, CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        var findings = new List<DiagnosticFinding>();

        progress.Report(5);
        var snap = await _sysInfo.GetSnapshotAsync(ct);
        progress.Report(25);

        // Defender
        if (!snap.DefenderEnabled)
            findings.Add(MakeFinding(Name, "Defender", HealthStatus.Error, Severity.Critical,
                "Windows Defender disabled", "Real-time protection is OFF. Enable it immediately.", "enable-defender"));
        else
            findings.Add(Good(Name, "Defender", "Windows Defender enabled", "Real-time protection is active."));

        // Firewall
        if (!snap.FirewallEnabled)
            findings.Add(MakeFinding(Name, "Firewall", HealthStatus.Error, Severity.High,
                "Windows Firewall disabled", "The firewall is off — network traffic is unfiltered.", "enable-firewall"));
        else
            findings.Add(Good(Name, "Firewall", "Windows Firewall enabled", "Firewall is active."));

        // BitLocker
        if (!snap.BitLockerEnabled)
            findings.Add(MakeFinding(Name, "BitLocker", HealthStatus.Warning, Severity.Medium,
                "BitLocker not enabled", "Drive encryption is off. Enable BitLocker to protect data at rest."));
        else
            findings.Add(Good(Name, "BitLocker", "BitLocker enabled", "Drive encryption is active."));

        // Secure Boot
        if (!snap.SecureBootEnabled)
            findings.Add(MakeFinding(Name, "SecureBoot", HealthStatus.Warning, Severity.Medium,
                "Secure Boot disabled", "Secure Boot is disabled. This may allow unsigned boot code to run."));
        else
            findings.Add(Good(Name, "SecureBoot", "Secure Boot enabled", "UEFI Secure Boot is active."));

        progress.Report(50);

        // UAC
        var uacEnabled = await Task.Run(CheckUac, ct);
        if (!uacEnabled)
            findings.Add(MakeFinding(Name, "UAC", HealthStatus.Error, Severity.High,
                "UAC is disabled", "User Account Control is off. All programs run with admin privileges."));
        else
            findings.Add(Good(Name, "UAC", "UAC enabled", "User Account Control is active."));

        // SmartScreen
        var smartScreen = await Task.Run(CheckSmartScreen, ct);
        if (!smartScreen)
            findings.Add(MakeFinding(Name, "SmartScreen", HealthStatus.Warning, Severity.Medium,
                "SmartScreen disabled", "Windows SmartScreen is off. Enable it for phishing protection."));
        else
            findings.Add(Good(Name, "SmartScreen", "SmartScreen enabled", "SmartScreen filter is active."));

        progress.Report(75);

        // RDP
        var rdpEnabled = await Task.Run(CheckRdpEnabled, ct);
        if (rdpEnabled)
            findings.Add(MakeFinding(Name, "RDP", HealthStatus.Warning, Severity.Medium,
                "Remote Desktop enabled", "RDP is enabled. Ensure it is properly secured with strong passwords and NLA."));
        else
            findings.Add(Good(Name, "RDP", "Remote Desktop disabled", "RDP is not enabled on this machine."));

        // Windows version check
        var (buildNum, buildOk) = await Task.Run(CheckWindowsBuild, ct);
        if (!buildOk)
            findings.Add(MakeFinding(Name, "WinBuild", HealthStatus.Warning, Severity.High,
                "Windows build may be outdated", $"Build {buildNum} detected. Ensure Windows is fully updated."));
        else
            findings.Add(Good(Name, "WinBuild", "Windows build is current", $"Build {buildNum} is up to date."));

        // TPM
        if (!snap.TpmPresent)
            findings.Add(MakeFinding(Name, "TPM", HealthStatus.Warning, Severity.Low,
                "TPM not detected", "Trusted Platform Module not found. Some security features require TPM 2.0."));
        else
            findings.Add(Good(Name, "TPM", "TPM present", "TPM chip is detected and enabled."));

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

    private static bool CheckUac()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System");
            return Convert.ToInt32(key?.GetValue("EnableLUA") ?? 1) == 1;
        }
        catch { return true; }
    }

    private static bool CheckSmartScreen()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer");
            var val = key?.GetValue("SmartScreenEnabled")?.ToString() ?? "On";
            return !val.Equals("Off", StringComparison.OrdinalIgnoreCase);
        }
        catch { return true; }
    }

    private static bool CheckRdpEnabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Terminal Server");
            return Convert.ToInt32(key?.GetValue("fDenyTSConnections") ?? 1) == 0;
        }
        catch { return false; }
    }

    private static (string build, bool ok) CheckWindowsBuild()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            var build = key?.GetValue("CurrentBuildNumber")?.ToString() ?? "0";
            return (build, int.Parse(build) >= 19041);
        }
        catch { return ("Unknown", true); }
    }
}
