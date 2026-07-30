using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Principal;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;
using WindowsDoctor.Diagnostics.Modules;

namespace WindowsDoctor.UI.ViewModels;

public partial class StorageViewModel : DiagnosticBaseViewModel
{
    private readonly StorageModule _module;
    private readonly IDiskHealthService _diskHealth;
    protected override IDiagnosticModule Module => _module;

    [ObservableProperty] private bool _isLoadingDiskHealth;
    [ObservableProperty] private string _diskHealthStatus = "Press \"Scan Disk Health\" to read SMART data and measure performance.";

    public bool IsRunningAsAdmin { get; } = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    public bool ShowAdminBanner => !IsRunningAsAdmin;

    public ObservableCollection<DiskHealthItemViewModel> Disks { get; } = [];

    public StorageViewModel(StorageModule module, IDiskHealthService diskHealth)
    {
        _module = module;
        _diskHealth = diskHealth;
        SetModuleInfo();
        _ = ScanDiskHealthAsync();
    }

    [RelayCommand]
    private void RestartAsAdmin()
    {
        var exePath = Process.GetCurrentProcess().MainModule?.FileName;
        if (exePath is null) return;
        try
        {
            Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true, Verb = "runas" });
            System.Windows.Application.Current.Shutdown();
        }
        catch (System.ComponentModel.Win32Exception) { /* user cancelled UAC prompt */ }
    }

    [RelayCommand]
    private async Task ScanDiskHealthAsync(CancellationToken ct = default)
    {
        if (IsLoadingDiskHealth) return;
        IsLoadingDiskHealth = true;
        DiskHealthStatus = "Reading SMART data and measuring throughput…";
        Disks.Clear();

        try
        {
            var results = await _diskHealth.GetDiskHealthAsync(ct);
            foreach (var d in results)
                Disks.Add(new DiskHealthItemViewModel(d));

            DiskHealthStatus = $"{results.Count} disk(s) analysed at {DateTime.Now:HH:mm:ss}.";
        }
        catch (Exception ex)
        {
            DiskHealthStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoadingDiskHealth = false;
        }
    }
}

public class DiskHealthItemViewModel
{
    private readonly DiskHealthInfo _d;
    public DiskHealthItemViewModel(DiskHealthInfo d) => _d = d;

    public string Model => _d.Model;
    public string TypeLabel => _d.IsSsd ? "SSD" : "HDD";
    public string InterfaceType => _d.InterfaceType;
    public string SizeLabel => FormatBytes(_d.SizeBytes);
    public bool SmartAvailable => _d.SmartAvailable;
    public string? UnavailableReason => _d.UnavailableReason;

    public double HealthPercent => _d.HealthPercent;
    public string HealthLabel => $"{_d.HealthPercent:0}%";
    public string HealthColor => _d.HealthPercent switch
    {
        >= 80 => "#A6E3A1",
        >= 50 => "#F9E2AF",
        _ => "#F38BA8"
    };
    public string HealthStatusLabel => _d.HealthPercent switch
    {
        >= 80 => "Good",
        >= 50 => "Caution",
        _ => "Poor — back up your data"
    };

    public bool HasPerformance => _d.PerformancePercent is not null;
    public double PerformancePercent => _d.PerformancePercent ?? 0;
    public string PerformanceLabel => _d.PerformancePercent is null ? "N/A" : $"{_d.PerformancePercent:0}%";
    public string PerformanceColor => (_d.PerformancePercent ?? 0) switch
    {
        >= 70 => "#A6E3A1",
        >= 40 => "#F9E2AF",
        _ => "#F38BA8"
    };
    public string ReadSpeedLabel => _d.ReadSpeedMBs is null ? "N/A" : $"{_d.ReadSpeedMBs:0} MB/s read";
    public string WriteSpeedLabel => _d.WriteSpeedMBs is null ? "N/A" : $"{_d.WriteSpeedMBs:0} MB/s write";

    public string TemperatureLabel => _d.TemperatureCelsius is null ? "N/A" : $"{_d.TemperatureCelsius:0} °C";
    public string PowerOnLabel => _d.PowerOnHours is null ? "N/A" : $"{_d.PowerOnHours:N0} h ({_d.PowerOnHours / 24:N0} days)";
    public string PowerCycleLabel => _d.PowerCycleCount is null ? "N/A" : $"{_d.PowerCycleCount:N0}";
    public string TotalWrittenLabel => _d.TotalBytesWritten is null ? "N/A" : FormatBytes(_d.TotalBytesWritten.Value);
    public string TotalReadLabel => _d.TotalBytesRead is null ? "N/A" : FormatBytes(_d.TotalBytesRead.Value);
    public string ReallocatedLabel => _d.ReallocatedSectorCount?.ToString("N0") ?? "N/A";
    public string PendingLabel => _d.PendingSectorCount?.ToString("N0") ?? "N/A";

    public List<SmartAttributeRowViewModel> SmartRows =>
        _d.SmartAttributes.Select(a => new SmartAttributeRowViewModel(a)).ToList();

    private static string FormatBytes(long b)
    {
        if (b >= 1_073_741_824) return $"{b / 1_073_741_824.0:0.0} GB";
        if (b >= 1_048_576) return $"{b / 1_048_576.0:0.0} MB";
        return $"{b / 1024.0:0.0} KB";
    }
}

public class SmartAttributeRowViewModel(SmartAttributeInfo a)
{
    public string IdLabel => $"0x{a.Id:X2} ({a.Id})";
    public string Name => a.Name;
    public byte Current => a.Current;
    public byte Worst => a.Worst;
    public string ThresholdLabel => a.Threshold == 0 ? "—" : a.Threshold.ToString();
    public long RawValue => a.RawValue;
    public string Status => a.Status;
    public string StatusColor => a.Status switch
    {
        "Failing" => "#F38BA8",
        "Warning" => "#F9E2AF",
        _ => "#A6E3A1"
    };
}
