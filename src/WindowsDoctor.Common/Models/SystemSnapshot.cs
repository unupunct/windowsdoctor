using WindowsDoctor.Common.Enums;

namespace WindowsDoctor.Common.Models;

/// <summary>
/// A point-in-time snapshot of the system's hardware, OS, and security configuration.
/// Captured at the start of each diagnostic scan.
/// </summary>
public sealed record SystemSnapshot
{
    /// <summary>The NetBIOS or DNS name of the computer.</summary>
    public required string ComputerName { get; init; }

    /// <summary>The Windows edition (e.g. "Windows 11 Pro").</summary>
    public required string WindowsEdition { get; init; }

    /// <summary>The full Windows build string (e.g. "22631.3296").</summary>
    public required string WindowsBuild { get; init; }

    /// <summary>The marketing version of Windows (e.g. "23H2").</summary>
    public required string WindowsVersion { get; init; }

    /// <summary>How long the machine has been running since the last boot.</summary>
    public required TimeSpan Uptime { get; init; }

    /// <summary>The display name of the primary CPU (e.g. "Intel Core i7-13700K").</summary>
    public required string CpuName { get; init; }

    /// <summary>Total logical processor count.</summary>
    public required int CpuCores { get; init; }

    /// <summary>CPU utilisation at the time of capture, expressed as 0–100.</summary>
    public required double CpuUsagePercent { get; init; }

    /// <summary>Total installed physical RAM in bytes.</summary>
    public required long TotalRamBytes { get; init; }

    /// <summary>Available (free) physical RAM in bytes at the time of capture.</summary>
    public required long AvailableRamBytes { get; init; }

    /// <summary>Display name of the primary GPU (e.g. "NVIDIA GeForce RTX 4080").</summary>
    public required string GpuName { get; init; }

    /// <summary>Motherboard manufacturer and model string.</summary>
    public required string Motherboard { get; init; }

    /// <summary>BIOS/UEFI version string.</summary>
    public required string BiosVersion { get; init; }

    /// <summary>Enumeration of all detected fixed and removable disks.</summary>
    public required List<DiskInfo> Disks { get; init; }

    /// <summary>Whether the machine had internet connectivity at the time of capture.</summary>
    public required bool InternetConnected { get; init; }

    /// <summary>Whether Windows Defender real-time protection was enabled.</summary>
    public required bool DefenderEnabled { get; init; }

    /// <summary>Whether the Windows Firewall was enabled for at least one active profile.</summary>
    public required bool FirewallEnabled { get; init; }

    /// <summary>Whether BitLocker Drive Encryption is active on the OS volume.</summary>
    public required bool BitLockerEnabled { get; init; }

    /// <summary>Whether Secure Boot is enabled in UEFI firmware.</summary>
    public required bool SecureBootEnabled { get; init; }

    /// <summary>Whether a Trusted Platform Module (TPM) chip is present and enabled.</summary>
    public required bool TpmPresent { get; init; }

    /// <summary>Whether one or more Windows Updates are pending installation.</summary>
    public required bool WindowsUpdatePending { get; init; }

    /// <summary>
    /// Battery charge level as a percentage (0–100), or <see langword="null"/> on desktops
    /// or when battery status is unavailable.
    /// </summary>
    public double? BatteryPercent { get; init; }

    /// <summary>The UTC date and time at which this snapshot was taken.</summary>
    public required DateTime CapturedAt { get; init; }
}

/// <summary>
/// Describes a single physical or logical disk drive detected on the system.
/// </summary>
public sealed record DiskInfo
{
    /// <summary>The drive letter and path (e.g. "C:\").</summary>
    public required string Name { get; init; }

    /// <summary>The user-assigned volume label, if any.</summary>
    public required string Label { get; init; }

    /// <summary>The drive type descriptor (e.g. "Fixed", "Removable", "Network").</summary>
    public required string DriveType { get; init; }

    /// <summary>Total capacity of the volume in bytes.</summary>
    public required long TotalBytes { get; init; }

    /// <summary>Available free space on the volume in bytes.</summary>
    public required long FreeBytes { get; init; }

    /// <summary>
    /// <see langword="true"/> if the underlying physical disk is a solid-state device;
    /// <see langword="false"/> for spinning media or when indeterminate.
    /// </summary>
    public required bool IsSsd { get; init; }

    /// <summary>The S.M.A.R.T. health status reported by the drive firmware.</summary>
    public required SmartStatus SmartStatus { get; init; }
}
