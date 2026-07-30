using Microsoft.Extensions.Logging;
using WindowsDoctor.Common.Enums;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.Diagnostics.Modules;

public class StorageModule : DiagnosticModuleBase, IDiagnosticModule
{
    private readonly ISystemInfoService _sysInfo;

    public string Name        => "Storage";
    public string Icon        => "💾";
    public string Description => "Analyses disk usage, temp files, and large folders.";

    public StorageModule(ISystemInfoService sysInfo, ILogger<StorageModule> logger)
        : base(logger) => _sysInfo = sysInfo;

    public async Task<DiagnosticReport> RunDiagnosticsAsync(IProgress<int> progress, CancellationToken ct = default)
    {
        var started = DateTime.UtcNow;
        var findings = new List<DiagnosticFinding>();

        progress.Report(5);
        var snap = await _sysInfo.GetSnapshotAsync(ct);
        progress.Report(20);

        // Disk usage per drive
        foreach (var disk in snap.Disks)
        {
            if (disk.TotalBytes == 0) continue;
            var pct = (double)disk.FreeBytes / disk.TotalBytes * 100;
            if (pct < 5)
                findings.Add(MakeFinding(Name, $"DiskFree.{disk.Name}", HealthStatus.Error, Severity.Critical,
                    $"Disk {disk.Name} critically full", $"Only {pct:0.0}% free ({FormatBytes(disk.FreeBytes)}). Immediate cleanup required.", "clear-temp-files"));
            else if (pct < 10)
                findings.Add(MakeFinding(Name, $"DiskFree.{disk.Name}", HealthStatus.Warning, Severity.High,
                    $"Disk {disk.Name} almost full", $"{pct:0.0}% free ({FormatBytes(disk.FreeBytes)}) on {disk.Label}.", "clear-temp-files"));
            else if (pct < 20)
                findings.Add(MakeFinding(Name, $"DiskFree.{disk.Name}", HealthStatus.Warning, Severity.Medium,
                    $"Disk {disk.Name} low on space", $"{pct:0.0}% free ({FormatBytes(disk.FreeBytes)})."));
            else
                findings.Add(Good(Name, $"DiskFree.{disk.Name}", $"Disk {disk.Name} space OK", $"{pct:0.0}% free ({FormatBytes(disk.FreeBytes)})."));
        }

        progress.Report(40);

        // Temp files
        var (tempSize, tempCount) = await Task.Run(() => GetFolderSize(Path.GetTempPath()), ct);
        var winTemp = @"C:\Windows\Temp";
        var (winTempSize, winTempCount) = await Task.Run(() => GetFolderSize(winTemp), ct);
        var totalTemp = tempSize + winTempSize;

        if (totalTemp > 1_073_741_824) // >1 GB
            findings.Add(MakeFinding(Name, "TempFiles", HealthStatus.Warning, Severity.Medium,
                "Large temp folder", $"{FormatBytes(totalTemp)} in temp folders ({tempCount + winTempCount} files). Clearing them may free space.", "clear-temp-files"));
        else
            findings.Add(Good(Name, "TempFiles", "Temp files under control", $"{FormatBytes(totalTemp)} in temp folders."));

        progress.Report(65);

        // Recycle Bin
        var recycleBinSize = await Task.Run(() => GetRecycleBinSize(), ct);
        if (recycleBinSize > 536_870_912) // >512 MB
            findings.Add(MakeFinding(Name, "RecycleBin", HealthStatus.Warning, Severity.Low,
                "Large Recycle Bin", $"Recycle Bin contains {FormatBytes(recycleBinSize)}. Consider emptying it."));
        else
            findings.Add(Good(Name, "RecycleBin", "Recycle Bin OK", $"{FormatBytes(recycleBinSize)} in Recycle Bin."));

        progress.Report(85);

        // Old log files
        var (logCount, logSize) = await Task.Run(() => GetOldLogs(), ct);
        if (logCount > 50)
            findings.Add(MakeFinding(Name, "OldLogs", HealthStatus.Warning, Severity.Low,
                "Many old log files", $"{logCount} log files older than 30 days ({FormatBytes(logSize)}) in Windows\\Logs."));
        else
            findings.Add(Good(Name, "OldLogs", "Log files OK", $"{logCount} old log files found."));

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

    private static (long size, int count) GetFolderSize(string path)
    {
        if (!Directory.Exists(path)) return (0, 0);
        long size = 0; int count = 0;
        try
        {
            foreach (var f in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { size += new FileInfo(f).Length; count++; }
                catch { }
            }
        }
        catch { }
        return (size, count);
    }

    private static long GetRecycleBinSize()
    {
        long total = 0;
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                var bin = Path.Combine(drive.RootDirectory.FullName, "$Recycle.Bin");
                if (Directory.Exists(bin))
                    total += GetFolderSize(bin).size;
            }
        }
        catch { }
        return total;
    }

    private static (int count, long size) GetOldLogs()
    {
        int count = 0; long size = 0;
        var cutoff = DateTime.Now.AddDays(-30);
        try
        {
            var logsPath = @"C:\Windows\Logs";
            foreach (var f in Directory.EnumerateFiles(logsPath, "*.log", SearchOption.AllDirectories))
            {
                try
                {
                    var fi = new FileInfo(f);
                    if (fi.LastWriteTime < cutoff) { count++; size += fi.Length; }
                }
                catch { }
            }
        }
        catch { }
        return (count, size);
    }
}
