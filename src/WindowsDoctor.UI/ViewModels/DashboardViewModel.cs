using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using WindowsDoctor.Common.Enums;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.UI.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly ISystemInfoService _sysInfo;
    private readonly IEnumerable<IDiagnosticModule> _modules;
    private readonly IHealthScoreService _healthScore;
    private readonly IScanHistoryRepository _history;

    [ObservableProperty] private string _computerName  = "–";
    [ObservableProperty] private string _windowsEdition = "–";
    [ObservableProperty] private string _windowsBuild  = "–";
    [ObservableProperty] private string _uptime        = "–";
    [ObservableProperty] private string _cpuName       = "–";
    [ObservableProperty] private string _cpuUsage      = "–";
    [ObservableProperty] private string _ramUsage      = "–";
    [ObservableProperty] private string _gpuName       = "–";
    [ObservableProperty] private string _motherboard   = "–";
    [ObservableProperty] private string _biosVersion   = "–";
    [ObservableProperty] private string _networkStatus = "–";
    [ObservableProperty] private string _internetStatus = "–";
    [ObservableProperty] private string _defenderStatus = "–";
    [ObservableProperty] private string _firewallStatus = "–";
    [ObservableProperty] private string _bitLockerStatus = "–";
    [ObservableProperty] private string _secureBootStatus = "–";
    [ObservableProperty] private string _tpmStatus     = "–";
    [ObservableProperty] private string _windowsUpdateStatus = "–";
    [ObservableProperty] private string _batteryStatus = "–";
    [ObservableProperty] private double _overallHealthScore = 0;
    [ObservableProperty] private string _healthScoreColor  = "#CDD6F4";
    [ObservableProperty] private string _healthScoreLabel  = "Not scanned";
    [ObservableProperty] private bool   _isScanning        = false;
    [ObservableProperty] private int    _scanProgress      = 0;
    [ObservableProperty] private string _scanStatus        = "Press 'Run Scan' to start";
    [ObservableProperty] private string _lastScanTime      = "Never";

    public ObservableCollection<DiskCardItem>      Disks       { get; } = [];
    public ObservableCollection<FindingSummaryItem> TopFindings { get; } = [];
    public ObservableCollection<CategoryScoreItem>  CategoryScores { get; } = [];

    public DashboardViewModel(
        ISystemInfoService sysInfo,
        IEnumerable<IDiagnosticModule> modules,
        IHealthScoreService healthScore,
        IScanHistoryRepository history)
    {
        _sysInfo     = sysInfo;
        _modules     = modules;
        _healthScore = healthScore;
        _history     = history;
    }

    [RelayCommand]
    private async Task RunScanAsync(CancellationToken ct = default)
    {
        if (IsScanning) return;
        IsScanning   = true;
        ScanProgress = 0;
        TopFindings.Clear();
        CategoryScores.Clear();

        try
        {
            ScanStatus = "Reading system information…";
            var snap = await _sysInfo.GetSnapshotAsync(ct);
            PopulateFromSnapshot(snap);
            ScanProgress = 10;

            var reports = new List<DiagnosticReport>();
            var moduleList = _modules.ToList();
            int completed = 0;

            await Task.WhenAll(moduleList.Select(async m =>
            {
                try
                {
                    ScanStatus = $"Running {m.Name} module…";
                    var progress = new Progress<int>();
                    var report = await m.RunDiagnosticsAsync(progress, ct);
                    lock (reports) reports.Add(report);
                }
                catch { }
                finally
                {
                    Interlocked.Increment(ref completed);
                    ScanProgress = 10 + (int)(completed / (double)moduleList.Count * 80);
                }
            }));

            ScanStatus = "Calculating health score…";
            ScanProgress = 92;
            var healthReport = await _healthScore.CalculateAsync(reports, ct);

            OverallHealthScore = healthReport.OverallScore;
            HealthScoreColor   = ScoreToColor(healthReport.OverallScore);
            HealthScoreLabel   = healthReport.OverallStatus.ToString();

            foreach (var (cat, score) in healthReport.CategoryScores)
                CategoryScores.Add(new CategoryScoreItem(cat, score, ScoreToColor(score)));

            foreach (var finding in reports.SelectMany(r => r.Findings)
                .Where(f => f.Status != HealthStatus.Good)
                .OrderByDescending(f => f.Severity)
                .Take(8))
            {
                TopFindings.Add(new FindingSummaryItem(finding));
            }

            ScanStatus = "Saving scan history…";
            ScanProgress = 96;

            using var scope = App.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IScanHistoryRepository>();
            await repo.SaveScanAsync(new FullScanRecord
            {
                Id             = Guid.NewGuid(),
                StartedAt      = DateTime.UtcNow.AddSeconds(-10),
                CompletedAt    = DateTime.UtcNow,
                SystemSnapshot = snap,
                Reports        = reports,
                HealthReport   = healthReport,
                RepairsApplied = []
            }, ct);

            LastScanTime = $"Last scan: {DateTime.Now:HH:mm:ss}";
            ScanStatus   = "Scan complete.";
            ScanProgress = 100;
        }
        catch (OperationCanceledException)
        {
            ScanStatus = "Scan cancelled.";
        }
        catch (Exception ex)
        {
            ScanStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    private void PopulateFromSnapshot(SystemSnapshot s)
    {
        ComputerName    = s.ComputerName;
        WindowsEdition  = s.WindowsEdition;
        WindowsBuild    = s.WindowsBuild;
        Uptime          = $"{s.Uptime.Days}d {s.Uptime.Hours}h {s.Uptime.Minutes}m";
        CpuName         = s.CpuName;
        CpuUsage        = $"{s.CpuUsagePercent:0}%";
        RamUsage        = $"{FormatBytes(s.TotalRamBytes - s.AvailableRamBytes)} / {FormatBytes(s.TotalRamBytes)}";
        GpuName         = s.GpuName;
        Motherboard     = s.Motherboard;
        BiosVersion     = s.BiosVersion;
        NetworkStatus   = s.Disks.Count > 0 ? "Connected" : "Unknown";
        InternetStatus  = s.InternetConnected ? "✅ Internet: Connected" : "❌ Internet: No internet";
        DefenderStatus  = s.DefenderEnabled  ? "✅ Defender: Enabled"  : "❌ Defender: Disabled";
        FirewallStatus  = s.FirewallEnabled  ? "✅ Firewall: Enabled"  : "❌ Firewall: Disabled";
        BitLockerStatus = s.BitLockerEnabled ? "✅ BitLocker: Enabled"  : "⚠️ BitLocker: Disabled";
        SecureBootStatus= s.SecureBootEnabled? "✅ Secure Boot: Enabled"  : "⚠️ Secure Boot: Disabled";
        TpmStatus       = s.TpmPresent       ? "✅ TPM: Present"  : "⚠️ TPM: Not found";
        WindowsUpdateStatus = s.WindowsUpdatePending ? "⚠️ Windows Update: Restart pending" : "✅ Windows Update: Up to date";
        BatteryStatus   = s.BatteryPercent.HasValue ? $"🔋 Battery: {s.BatteryPercent:0}%" : "🔌 Battery: Desktop (no battery)";

        Disks.Clear();
        foreach (var d in s.Disks)
        {
            var pct = d.TotalBytes > 0 ? (d.TotalBytes - d.FreeBytes) / (double)d.TotalBytes * 100 : 0;
            Disks.Add(new DiskCardItem(d.Name, d.Label, pct, FormatBytes(d.FreeBytes), FormatBytes(d.TotalBytes), d.IsSsd));
        }
    }

    private static string ScoreToColor(double score) => score switch
    {
        >= 80 => "#A6E3A1",
        >= 60 => "#F9E2AF",
        >= 40 => "#FAB387",
        _     => "#F38BA8"
    };

    private static string FormatBytes(long b)
    {
        if (b >= 1_073_741_824) return $"{b / 1_073_741_824.0:0.0} GB";
        if (b >= 1_048_576)     return $"{b / 1_048_576.0:0.0} MB";
        return $"{b / 1024.0:0.0} KB";
    }
}

public record DiskCardItem(string Name, string Label, double UsedPercent, string FreeSpace, string TotalSpace, bool IsSsd)
{
    public string StatusColor => UsedPercent > 90 ? "#F38BA8" : UsedPercent > 75 ? "#F9E2AF" : "#A6E3A1";
    public string TypeLabel   => IsSsd ? "SSD" : "HDD";
}

public record FindingSummaryItem(DiagnosticFinding Finding)
{
    public string Icon    => Finding.Status == HealthStatus.Error ? "❌" : Finding.Severity == Severity.Critical ? "🔴" : "⚠️";
    public string Color   => Finding.Severity switch { Severity.Critical => "#F38BA8", Severity.High => "#FAB387", Severity.Medium => "#F9E2AF", _ => "#CDD6F4" };
    public string Module  => Finding.Module;
    public string Title   => Finding.Title;
    public string Message => Finding.Message;
}

public record CategoryScoreItem(string Category, double Score, string Color);
