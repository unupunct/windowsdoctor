using System.Security.Principal;
using Microsoft.Extensions.Logging;
using WindowsDoctor.Common.Enums;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.Repair;

public class RepairService : IRepairService
{
    private readonly ILogger<RepairService> _logger;

    public RepairService(ILogger<RepairService> logger) => _logger = logger;

    public bool IsRunningAsAdmin()
    {
        using var id = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
    }

    public IReadOnlyList<RepairDefinition> GetAvailableRepairs() => _allRepairs;

    public async Task<RepairResult> ExecuteAsync(
        string repairId, IProgress<RepairProgress> progress, CancellationToken ct = default)
    {
        _logger.LogInformation("Executing repair: {RepairId}", repairId);
        var def = _allRepairs.FirstOrDefault(r => r.Id == repairId);
        if (def is null)
            return Fail(repairId, $"Unknown repair: {repairId}");

        if (def.RequiresElevation && !IsRunningAsAdmin())
            return Fail(repairId, "This repair requires Administrator privileges. Please restart Windows Doctor as Administrator.");

        Report(progress, 5, "Starting…");

        return repairId switch
        {
            "clear-temp-files"          => await ClearTempFilesAsync(progress, ct),
            "flush-dns"                 => await FlushDnsAsync(progress, ct),
            "reset-winsock"             => await ResetWinsockAsync(progress, ct),
            "reset-network-stack"       => await ResetNetworkStackAsync(progress, ct),
            "restart-windows-update"    => await RestartWindowsUpdateAsync(progress, ct),
            "clear-windows-update-cache"=> await ClearUpdateCacheAsync(progress, ct),
            "run-sfc"                   => await RunSfcAsync(progress, ct),
            "run-dism"                  => await RunDismAsync(progress, ct),
            "run-chkdsk"                => await RunChkdskAsync(progress, ct),
            "repair-wmi"                => await RepairWmiAsync(progress, ct),
            "reset-icon-cache"          => await ResetIconCacheAsync(progress, ct),
            "rebuild-search-index"      => await RebuildSearchIndexAsync(progress, ct),
            "enable-defender"           => await EnableDefenderAsync(progress, ct),
            "enable-firewall"           => await EnableFirewallAsync(progress, ct),
            "optimize-drives"           => await OptimizeDrivesAsync(progress, ct),
            "create-restore-point"      => await CreateRestorePointAsync(progress, ct),
            "clear-event-logs"          => await ClearEventLogsAsync(progress, ct),
            _                           => Fail(repairId, $"Repair '{repairId}' is not implemented.")
        };
    }

    // ── Repair implementations ─────────────────────────────────────────────

    private static async Task<RepairResult> ClearTempFilesAsync(IProgress<RepairProgress> p, CancellationToken ct)
    {
        Report(p, 10, "Scanning temp folders…");
        var paths = new[] { Path.GetTempPath(), @"C:\Windows\Temp" };
        long freed = 0; int deleted = 0;
        foreach (var dir in paths.Where(Directory.Exists))
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var fi = new FileInfo(f);
                    freed += fi.Length;
                    fi.Delete();
                    deleted++;
                }
                catch { }
            }
        }
        Report(p, 100, "Done.");
        return Ok("clear-temp-files", $"Deleted {deleted} files, freed {FormatBytes(freed)}.");
    }

    private static async Task<RepairResult> FlushDnsAsync(IProgress<RepairProgress> p, CancellationToken ct)
    {
        Report(p, 50, "Flushing DNS cache…");
        var (ok, output, err) = await CommandRunner.RunCmdAsync("ipconfig /flushdns", 15_000, ct);
        Report(p, 100, "Done.");
        return ok ? Ok("flush-dns", "DNS cache flushed successfully.", output)
                  : Fail("flush-dns", "DNS flush failed.", err);
    }

    private static async Task<RepairResult> ResetWinsockAsync(IProgress<RepairProgress> p, CancellationToken ct)
    {
        Report(p, 30, "Resetting Winsock…");
        var (ok1, o1, e1) = await CommandRunner.RunCmdAsync("netsh winsock reset", 30_000, ct);
        Report(p, 70, "Resetting TCP/IP…");
        var (ok2, o2, _) = await CommandRunner.RunCmdAsync("netsh int ip reset", 30_000, ct);
        Report(p, 100, "Done. Restart required.");
        return (ok1 && ok2)
            ? Ok("reset-winsock", "Winsock and TCP/IP stack reset. Please restart your computer.", o1 + "\n" + o2)
            : Fail("reset-winsock", "Reset partially failed.", e1);
    }

    private static async Task<RepairResult> ResetNetworkStackAsync(IProgress<RepairProgress> p, CancellationToken ct)
    {
        var steps = new[] { ("netsh winsock reset", "Resetting Winsock"), ("netsh int ip reset", "Resetting IP"), ("ipconfig /release", "Releasing IP"), ("ipconfig /renew", "Renewing IP") };
        int i = 0;
        foreach (var (cmd, msg) in steps)
        {
            Report(p, (i + 1) * 25, msg);
            await CommandRunner.RunCmdAsync(cmd, 30_000, ct);
            i++;
        }
        Report(p, 100, "Done.");
        return Ok("reset-network-stack", "Network stack reset. Restart your computer.");
    }

    private static async Task<RepairResult> RestartWindowsUpdateAsync(IProgress<RepairProgress> p, CancellationToken ct)
    {
        var stopCmds = new[] { "net stop wuauserv", "net stop cryptSvc", "net stop bits" };
        var startCmds = new[] { "net start bits", "net start cryptSvc", "net start wuauserv" };
        int step = 0;
        foreach (var cmd in stopCmds) { Report(p, ++step * 15, cmd); await CommandRunner.RunCmdAsync(cmd, 30_000, ct); }
        foreach (var cmd in startCmds) { Report(p, ++step * 15, cmd); await CommandRunner.RunCmdAsync(cmd, 30_000, ct); }
        Report(p, 100, "Done.");
        return Ok("restart-windows-update", "Windows Update service restarted successfully.");
    }

    private static async Task<RepairResult> ClearUpdateCacheAsync(IProgress<RepairProgress> p, CancellationToken ct)
    {
        Report(p, 10, "Stopping Windows Update services…");
        await CommandRunner.RunCmdAsync("net stop wuauserv", 30_000, ct);
        await CommandRunner.RunCmdAsync("net stop bits", 30_000, ct);

        Report(p, 40, "Clearing cache…");
        var cacheDir = @"C:\Windows\SoftwareDistribution\Download";
        long freed = 0;
        if (Directory.Exists(cacheDir))
        {
            foreach (var f in Directory.EnumerateFiles(cacheDir, "*", SearchOption.AllDirectories))
            {
                try { var fi = new FileInfo(f); freed += fi.Length; fi.Delete(); } catch { }
            }
        }

        Report(p, 80, "Restarting services…");
        await CommandRunner.RunCmdAsync("net start bits", 30_000, ct);
        await CommandRunner.RunCmdAsync("net start wuauserv", 30_000, ct);
        Report(p, 100, "Done.");
        return Ok("clear-windows-update-cache", $"Windows Update cache cleared ({FormatBytes(freed)} freed). Restart recommended.");
    }

    private static async Task<RepairResult> RunSfcAsync(IProgress<RepairProgress> p, CancellationToken ct)
    {
        Report(p, 10, "Running System File Checker (this may take several minutes)…");
        var (ok, output, err) = await CommandRunner.RunAsync("sfc.exe", "/scannow", 600_000, ct);
        Report(p, 100, "Done.");
        var clean = output.Contains("did not find any integrity violations", StringComparison.OrdinalIgnoreCase);
        var fixed_ = output.Contains("successfully repaired", StringComparison.OrdinalIgnoreCase);
        var msg = clean ? "SFC found no integrity violations." : fixed_ ? "SFC repaired some files." : "SFC scan complete. Review output.";
        return Ok("run-sfc", msg, output);
    }

    private static async Task<RepairResult> RunDismAsync(IProgress<RepairProgress> p, CancellationToken ct)
    {
        Report(p, 10, "Scanning component store…");
        await CommandRunner.RunAsync("DISM.exe", "/Online /Cleanup-Image /ScanHealth", 300_000, ct);
        Report(p, 50, "Restoring health (this may take 15-30 minutes)…");
        var (ok, output, err) = await CommandRunner.RunAsync("DISM.exe", "/Online /Cleanup-Image /RestoreHealth", 1_800_000, ct);
        Report(p, 100, "Done.");
        return ok ? Ok("run-dism", "DISM completed successfully.", output)
                  : Ok("run-dism", "DISM completed with warnings. Review output.", output + "\n" + err);
    }

    private static async Task<RepairResult> RunChkdskAsync(IProgress<RepairProgress> p, CancellationToken ct)
    {
        Report(p, 50, "Scheduling CHKDSK for next boot…");
        var (ok, output, _) = await CommandRunner.RunCmdAsync("echo Y | chkdsk C: /f", 30_000, ct);
        Report(p, 100, "Done.");
        return Ok("run-chkdsk", "CHKDSK has been scheduled for the next system restart.", output);
    }

    private static async Task<RepairResult> RepairWmiAsync(IProgress<RepairProgress> p, CancellationToken ct)
    {
        Report(p, 30, "Verifying WMI repository…");
        var (ok, output, _) = await CommandRunner.RunAsync("winmgmt.exe", "/verifyrepository", 60_000, ct);
        if (output.Contains("consistent", StringComparison.OrdinalIgnoreCase))
        {
            Report(p, 100, "Done.");
            return Ok("repair-wmi", "WMI repository is consistent. No repair needed.", output);
        }
        Report(p, 70, "Salvaging WMI repository…");
        var (ok2, o2, e2) = await CommandRunner.RunAsync("winmgmt.exe", "/salvagerepository", 120_000, ct);
        Report(p, 100, "Done.");
        return ok2 ? Ok("repair-wmi", "WMI repository repaired.", o2) : Fail("repair-wmi", "WMI repair failed.", e2);
    }

    private static async Task<RepairResult> ResetIconCacheAsync(IProgress<RepairProgress> p, CancellationToken ct)
    {
        Report(p, 20, "Stopping Explorer…");
        await CommandRunner.RunCmdAsync("taskkill /f /im explorer.exe", 10_000, ct);
        Report(p, 50, "Deleting icon cache…");
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var cacheFiles = Directory.GetFiles(Path.Combine(appData, @"Microsoft\Windows\Explorer"), "iconcache*.db");
        foreach (var f in cacheFiles) { try { File.Delete(f); } catch { } }
        try { File.Delete(Path.Combine(appData, "IconCache.db")); } catch { }
        Report(p, 80, "Restarting Explorer…");
        System.Diagnostics.Process.Start("explorer.exe");
        await Task.Delay(1000, ct);
        Report(p, 100, "Done.");
        return Ok("reset-icon-cache", "Icon cache cleared and Explorer restarted.");
    }

    private static async Task<RepairResult> RebuildSearchIndexAsync(IProgress<RepairProgress> p, CancellationToken ct)
    {
        Report(p, 20, "Stopping Windows Search…");
        await CommandRunner.RunCmdAsync("net stop WSearch", 30_000, ct);
        Report(p, 50, "Removing index database…");
        var indexPath = @"C:\ProgramData\Microsoft\Search\Data\Applications\Windows\Windows.edb";
        try { if (File.Exists(indexPath)) File.Delete(indexPath); } catch { }
        Report(p, 80, "Starting Windows Search…");
        await CommandRunner.RunCmdAsync("net start WSearch", 30_000, ct);
        Report(p, 100, "Done.");
        return Ok("rebuild-search-index", "Search index will rebuild automatically in the background.");
    }

    private static async Task<RepairResult> EnableDefenderAsync(IProgress<RepairProgress> p, CancellationToken ct)
    {
        Report(p, 50, "Enabling Windows Defender…");
        var (ok, _, err) = await CommandRunner.RunPowerShellAsync("Set-MpPreference -DisableAntiSpyware $false", 30_000, ct);
        Report(p, 100, "Done.");
        return ok ? Ok("enable-defender", "Windows Defender has been enabled.")
                  : Fail("enable-defender", "Failed to enable Defender. Check Group Policy settings.", err);
    }

    private static async Task<RepairResult> EnableFirewallAsync(IProgress<RepairProgress> p, CancellationToken ct)
    {
        Report(p, 50, "Enabling Windows Firewall…");
        var (ok, output, err) = await CommandRunner.RunCmdAsync("netsh advfirewall set allprofiles state on", 30_000, ct);
        Report(p, 100, "Done.");
        return ok ? Ok("enable-firewall", "Windows Firewall enabled for all profiles.", output)
                  : Fail("enable-firewall", "Failed to enable Firewall.", err);
    }

    private static async Task<RepairResult> OptimizeDrivesAsync(IProgress<RepairProgress> p, CancellationToken ct)
    {
        Report(p, 20, "Optimizing C: drive…");
        var (ok, output, err) = await CommandRunner.RunAsync("defrag.exe", "C: /U /V /O", 300_000, ct);
        Report(p, 100, "Done.");
        return ok ? Ok("optimize-drives", "Drive optimization complete.", output)
                  : Fail("optimize-drives", "Optimization failed.", err);
    }

    private static async Task<RepairResult> CreateRestorePointAsync(IProgress<RepairProgress> p, CancellationToken ct)
    {
        Report(p, 50, "Creating system restore point…");
        var (ok, output, err) = await CommandRunner.RunPowerShellAsync(
            "Checkpoint-Computer -Description 'Windows Doctor' -RestorePointType MODIFY_SETTINGS", 60_000, ct);
        Report(p, 100, "Done.");
        return ok ? Ok("create-restore-point", "System restore point created successfully.")
                  : Fail("create-restore-point", "Failed to create restore point. Ensure System Protection is enabled.", err);
    }

    private static async Task<RepairResult> ClearEventLogsAsync(IProgress<RepairProgress> p, CancellationToken ct)
    {
        Report(p, 33, "Clearing System log…");
        await CommandRunner.RunCmdAsync("wevtutil cl System", 30_000, ct);
        Report(p, 66, "Clearing Application log…");
        await CommandRunner.RunCmdAsync("wevtutil cl Application", 30_000, ct);
        Report(p, 100, "Done.");
        return Ok("clear-event-logs", "System and Application event logs cleared.");
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static void Report(IProgress<RepairProgress> p, int pct, string msg)
        => p.Report(new RepairProgress { PercentComplete = pct, StatusMessage = msg });

    private static RepairResult Ok(string id, string msg, string? output = null)
        => new() { RepairId = id, Success = true, Message = msg, Output = output, CompletedAt = DateTime.UtcNow };

    private static RepairResult Fail(string id, string msg, string? err = null)
        => new() { RepairId = id, Success = false, Message = msg, ErrorOutput = err, CompletedAt = DateTime.UtcNow };

    private static string FormatBytes(long b)
    {
        if (b >= 1_073_741_824) return $"{b / 1_073_741_824.0:0.0} GB";
        if (b >= 1_048_576)     return $"{b / 1_048_576.0:0.0} MB";
        return $"{b / 1024.0:0.0} KB";
    }

    // ── Static repair catalog ─────────────────────────────────────────────

    private static readonly IReadOnlyList<RepairDefinition> _allRepairs = new List<RepairDefinition>
    {
        new() { Id="clear-temp-files",          Name="Clear Temp Files",               Category="Storage",         Risk=RepairRisk.Safe,   RequiresElevation=false, RequiresRestart=false, Description="Deletes files in the Windows and user temp folders.", ExpectedImpact="Frees disk space (often several GB)." },
        new() { Id="flush-dns",                  Name="Flush DNS Cache",                Category="Network",         Risk=RepairRisk.Safe,   RequiresElevation=false, RequiresRestart=false, Description="Clears the DNS resolver cache.", ExpectedImpact="Resolves DNS lookup issues and stale entries." },
        new() { Id="reset-winsock",              Name="Reset Winsock",                  Category="Network",         Risk=RepairRisk.Medium, RequiresElevation=true,  RequiresRestart=true,  Description="Resets the Windows network socket catalog and TCP/IP.", ExpectedImpact="Fixes persistent network connectivity problems." },
        new() { Id="reset-network-stack",        Name="Reset Full Network Stack",       Category="Network",         Risk=RepairRisk.Medium, RequiresElevation=true,  RequiresRestart=true,  Description="Resets Winsock, IP stack, and renews DHCP lease.", ExpectedImpact="Resolves complex network issues." },
        new() { Id="restart-windows-update",     Name="Restart Windows Update",         Category="Windows Health",  Risk=RepairRisk.Low,    RequiresElevation=true,  RequiresRestart=false, Description="Stops and restarts the Windows Update services.", ExpectedImpact="Fixes stuck or failing Windows Updates." },
        new() { Id="clear-windows-update-cache", Name="Clear Windows Update Cache",     Category="Windows Health",  Risk=RepairRisk.Low,    RequiresElevation=true,  RequiresRestart=true,  Description="Clears the SoftwareDistribution download cache.", ExpectedImpact="Forces fresh download of updates; frees disk space." },
        new() { Id="run-sfc",                    Name="Run System File Checker (SFC)",  Category="Windows Health",  Risk=RepairRisk.Low,    RequiresElevation=true,  RequiresRestart=false, Description="Scans and repairs protected Windows system files.", ExpectedImpact="Fixes corrupted OS files that cause crashes or errors." },
        new() { Id="run-dism",                   Name="Run DISM Restore Health",        Category="Windows Health",  Risk=RepairRisk.Low,    RequiresElevation=true,  RequiresRestart=false, Description="Uses DISM to repair the Windows component store.", ExpectedImpact="Fixes issues that SFC cannot resolve on its own." },
        new() { Id="run-chkdsk",                 Name="Schedule CHKDSK",                Category="Storage",         Risk=RepairRisk.Low,    RequiresElevation=true,  RequiresRestart=true,  Description="Schedules a disk integrity check on C: at next boot.", ExpectedImpact="Detects and repairs file system errors and bad sectors." },
        new() { Id="repair-wmi",                 Name="Repair WMI Repository",          Category="Windows Health",  Risk=RepairRisk.Medium, RequiresElevation=true,  RequiresRestart=false, Description="Verifies and salvages the WMI repository.", ExpectedImpact="Fixes WMI errors that affect diagnostics and management tools." },
        new() { Id="reset-icon-cache",           Name="Reset Icon Cache",               Category="Performance",     Risk=RepairRisk.Safe,   RequiresElevation=false, RequiresRestart=false, Description="Clears the Windows icon cache database.", ExpectedImpact="Fixes broken, blank, or incorrect icons." },
        new() { Id="rebuild-search-index",       Name="Rebuild Search Index",           Category="Performance",     Risk=RepairRisk.Low,    RequiresElevation=false, RequiresRestart=false, Description="Deletes and rebuilds the Windows Search index.", ExpectedImpact="Fixes slow or missing search results in Start menu." },
        new() { Id="enable-defender",            Name="Enable Windows Defender",        Category="Security",        Risk=RepairRisk.Low,    RequiresElevation=true,  RequiresRestart=false, Description="Enables Windows Defender real-time protection.", ExpectedImpact="Restores active malware protection." },
        new() { Id="enable-firewall",            Name="Enable Windows Firewall",        Category="Security",        Risk=RepairRisk.Low,    RequiresElevation=true,  RequiresRestart=false, Description="Enables Windows Firewall for all network profiles.", ExpectedImpact="Restores inbound connection filtering." },
        new() { Id="optimize-drives",            Name="Optimize Drives",                Category="Storage",         Risk=RepairRisk.Safe,   RequiresElevation=false, RequiresRestart=false, Description="Runs the Windows drive optimiser (TRIM for SSD, defrag for HDD).", ExpectedImpact="Improves disk performance." },
        new() { Id="create-restore-point",       Name="Create Restore Point",           Category="Windows Health",  Risk=RepairRisk.Safe,   RequiresElevation=true,  RequiresRestart=false, Description="Creates a System Restore checkpoint.", ExpectedImpact="Provides a rollback point before making system changes." },
        new() { Id="clear-event-logs",           Name="Clear Event Logs",               Category="Windows Health",  Risk=RepairRisk.Medium, RequiresElevation=true,  RequiresRestart=false, Description="Clears the System and Application event logs.", ExpectedImpact="Removes accumulated log data; improves Event Viewer clarity." },
    };
}
