using System.Management;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using WindowsDoctor.Common.Enums;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.Services;

public class SystemInfoService : ISystemInfoService
{
    private readonly ILogger<SystemInfoService> _logger;

    public SystemInfoService(ILogger<SystemInfoService> logger) => _logger = logger;

    public async Task<SystemSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var computerName = Environment.MachineName;
            var (edition, build, version) = GetWindowsInfo();
            var uptime = GetUptime();
            var (cpuName, cpuCores) = GetCpuInfo();
            var cpuUsage = GetCpuUsage();
            var (totalRam, availableRam) = GetRamInfo();
            var gpu = GetGpuName();
            var (motherboard, bios) = GetBoardInfo();
            var disks = GetDisks();
            var internet = CheckInternet();
            var defender = GetDefenderStatus();
            var firewall = GetFirewallStatus();
            var bitlocker = GetBitLockerStatus();
            var secureBoot = GetSecureBootStatus();
            var tpm = GetTpmPresent();
            var updatePending = GetWindowsUpdatePending();
            var battery = GetBatteryPercent();

            return new SystemSnapshot
            {
                ComputerName = computerName,
                WindowsEdition = edition,
                WindowsBuild = build,
                WindowsVersion = version,
                Uptime = uptime,
                CpuName = cpuName,
                CpuCores = cpuCores,
                CpuUsagePercent = cpuUsage,
                TotalRamBytes = totalRam,
                AvailableRamBytes = availableRam,
                GpuName = gpu,
                Motherboard = motherboard,
                BiosVersion = bios,
                Disks = disks,
                InternetConnected = internet,
                DefenderEnabled = defender,
                FirewallEnabled = firewall,
                BitLockerEnabled = bitlocker,
                SecureBootEnabled = secureBoot,
                TpmPresent = tpm,
                WindowsUpdatePending = updatePending,
                BatteryPercent = battery,
                CapturedAt = DateTime.UtcNow
            };
        }, ct);
    }

    public async Task<double> GetCpuUsageAsync(CancellationToken ct = default)
        => await Task.Run(GetCpuUsage, ct);

    public async Task<double> GetRamUsagePercentAsync(CancellationToken ct = default)
    {
        var (total, available) = GetRamInfo();
        if (total == 0) return 0;
        return await Task.FromResult((total - available) / (double)total * 100.0);
    }

    // ── Private helpers ────────────────────────────────────────────────────

    private (string edition, string build, string version) GetWindowsInfo()
    {
        try
        {
            var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
            if (key is null) return ("Windows", "Unknown", "Unknown");
            var edition = key.GetValue("ProductName")?.ToString() ?? "Windows";
            var build = key.GetValue("CurrentBuildNumber")?.ToString() ?? "Unknown";
            var ubr = key.GetValue("UBR")?.ToString();
            var fullBuild = ubr is not null ? $"{build}.{ubr}" : build;
            var displayVersion = key.GetValue("DisplayVersion")?.ToString() ?? string.Empty;

            // Registry ProductName still says "Windows 10" on Windows 11 builds (>= 22000).
            if (int.TryParse(build, out var buildNum) && buildNum >= 22000)
                edition = edition.Replace("Windows 10", "Windows 11");

            return (edition, fullBuild, displayVersion);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read Windows version info");
            return ("Windows", "Unknown", "Unknown");
        }
    }

    private static TimeSpan GetUptime()
    {
        try { return TimeSpan.FromMilliseconds(Environment.TickCount64); }
        catch { return TimeSpan.Zero; }
    }

    private (string name, int cores) GetCpuInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString()?.Trim() ?? "Unknown CPU";
                var cores = Convert.ToInt32(obj["NumberOfCores"] ?? 1);
                return (name, cores);
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read CPU info"); }
        return ("Unknown CPU", Environment.ProcessorCount);
    }

    private double GetCpuUsage()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
                return Convert.ToDouble(obj["LoadPercentage"] ?? 0);
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read CPU usage"); }
        return 0;
    }

    private (long total, long available) GetRamInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                var totalKb = Convert.ToInt64(obj["TotalVisibleMemorySize"] ?? 0);
                var freeKb = Convert.ToInt64(obj["FreePhysicalMemory"] ?? 0);
                return (totalKb * 1024, freeKb * 1024);
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read RAM info"); }
        return (0, 0);
    }

    private string GetGpuName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
            string? best = null;
            long bestRam = -1;
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString()?.Trim();
                if (string.IsNullOrEmpty(name)) continue;
                var ram = obj["AdapterRAM"] is null ? 0 : Convert.ToInt64(obj["AdapterRAM"]);
                if (ram > bestRam)
                {
                    bestRam = ram;
                    best = name;
                }
            }
            return best ?? "Unknown GPU";
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read GPU info"); }
        return "Unknown GPU";
    }

    private (string board, string bios) GetBoardInfo()
    {
        string board = "Unknown", bios = "Unknown";
        try
        {
            using var bs = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
            foreach (ManagementObject obj in bs.Get())
                board = $"{obj["Manufacturer"]} {obj["Product"]}".Trim();

            using var biosS = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion FROM Win32_BIOS");
            foreach (ManagementObject obj in biosS.Get())
                bios = obj["SMBIOSBIOSVersion"]?.ToString() ?? "Unknown";
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to read board/BIOS info"); }
        return (board, bios);
    }

    private List<DiskInfo> GetDisks()
    {
        var result = new List<DiskInfo>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                var isSsd = IsSsd(drive.Name);
                var smart = GetSmartStatus(drive.Name);
                result.Add(new DiskInfo
                {
                    Name = drive.Name,
                    Label = drive.VolumeLabel,
                    DriveType = drive.DriveType.ToString(),
                    TotalBytes = drive.TotalSize,
                    FreeBytes = drive.AvailableFreeSpace,
                    IsSsd = isSsd,
                    SmartStatus = smart
                });
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to enumerate disks"); }
        return result;
    }

    private bool IsSsd(string driveLetter)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT MediaType FROM Win32_DiskDrive WHERE Index=0");
            foreach (ManagementObject obj in searcher.Get())
            {
                var media = obj["MediaType"]?.ToString() ?? string.Empty;
                return media.Contains("SSD", StringComparison.OrdinalIgnoreCase) ||
                       media.Contains("Solid", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch { }
        return false;
    }

    private SmartStatus GetSmartStatus(string driveLetter)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Status FROM Win32_DiskDrive");
            foreach (ManagementObject obj in searcher.Get())
            {
                var status = obj["Status"]?.ToString() ?? string.Empty;
                if (status.Equals("OK", StringComparison.OrdinalIgnoreCase)) return SmartStatus.Good;
                if (status.Contains("Pred", StringComparison.OrdinalIgnoreCase)) return SmartStatus.Warning;
                return SmartStatus.Unknown;
            }
        }
        catch { }
        return SmartStatus.Unknown;
    }

    private bool CheckInternet()
    {
        try
        {
            using var ping = new Ping();
            var reply = ping.Send("8.8.8.8", 1500);
            return reply.Status == IPStatus.Success;
        }
        catch { return false; }
    }

    private bool GetDefenderStatus()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows Defender");
            if (key is null) return true;
            var val = key.GetValue("DisableAntiSpyware");
            return val is null || Convert.ToInt32(val) == 0;
        }
        catch { return true; }
    }

    private bool GetFirewallStatus()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\StandardProfile");
            if (key is null) return true;
            return Convert.ToInt32(key.GetValue("EnableFirewall") ?? 1) == 1;
        }
        catch { return true; }
    }

    private bool GetBitLockerStatus()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\CIMV2\Security\MicrosoftVolumeEncryption",
                "SELECT ProtectionStatus FROM Win32_EncryptableVolume WHERE DriveLetter='C:'");
            foreach (ManagementObject obj in searcher.Get())
                return Convert.ToInt32(obj["ProtectionStatus"] ?? 0) == 1;
        }
        catch { }
        return false;
    }

    private bool GetSecureBootStatus()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\SecureBoot\State");
            if (key is null) return false;
            return Convert.ToInt32(key.GetValue("UEFISecureBootEnabled") ?? 0) == 1;
        }
        catch { return false; }
    }

    private bool GetTpmPresent()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\CIMv2\Security\MicrosoftTpm",
                "SELECT IsEnabled_InitialValue FROM Win32_Tpm");
            foreach (ManagementObject obj in searcher.Get())
                return Convert.ToBoolean(obj["IsEnabled_InitialValue"] ?? false);
        }
        catch { }
        return false;
    }

    private bool GetWindowsUpdatePending()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
            return key is not null;
        }
        catch { return false; }
    }

    private double? GetBatteryPercent()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT EstimatedChargeRemaining FROM Win32_Battery");
            foreach (ManagementObject obj in searcher.Get())
                return Convert.ToDouble(obj["EstimatedChargeRemaining"] ?? -1);
        }
        catch { }
        return null;
    }
}
