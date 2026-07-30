using System.Management;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.Services;

public class DiskHealthService : IDiskHealthService
{
    private readonly ILogger<DiskHealthService> _logger;

    public DiskHealthService(ILogger<DiskHealthService> logger) => _logger = logger;

    public async Task<List<DiskHealthInfo>> GetDiskHealthAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var result = new List<DiskHealthInfo>();
            var isAdmin = IsRunningAsAdmin();

            foreach (var drive in EnumeratePhysicalDrives())
            {
                DiskHealthInfo info;
                try
                {
                    info = ReadSmartInfo(drive, isAdmin);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read SMART data for {Device}", drive.DeviceId);
                    info = new DiskHealthInfo
                    {
                        DeviceId = drive.DeviceId,
                        Model = drive.Model,
                        SerialNumber = drive.SerialNumber,
                        InterfaceType = drive.InterfaceType,
                        IsSsd = drive.IsSsd,
                        SizeBytes = drive.SizeBytes,
                        SmartAvailable = false,
                        UnavailableReason = "An unexpected error occurred while reading SMART data."
                    };
                }

                try
                {
                    var (readMbs, writeMbs) = MeasureThroughput(drive.SampleFolderPath);
                    var refSpeed = drive.IsSsd ? (drive.InterfaceType.Contains("NVMe", StringComparison.OrdinalIgnoreCase) ? 2000.0 : 500.0) : 150.0;
                    double? perfPct = readMbs is null ? null : Math.Clamp(readMbs.Value / refSpeed * 100.0, 0, 100);
                    info = info with { ReadSpeedMBs = readMbs, WriteSpeedMBs = writeMbs, PerformancePercent = perfPct };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to measure throughput for {Device}", drive.DeviceId);
                }

                result.Add(info);
            }

            return result;
        }, ct);
    }

    private static bool IsRunningAsAdmin()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    // ── Drive enumeration ────────────────────────────────────────────────

    private sealed record PhysicalDriveRef(string DeviceId, int Index, string Model, string SerialNumber, string InterfaceType, bool IsSsd, long SizeBytes, string? SampleFolderPath);

    private List<PhysicalDriveRef> EnumeratePhysicalDrives()
    {
        var drives = new List<PhysicalDriveRef>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT DeviceID, Index, Model, SerialNumber, InterfaceType, MediaType, Size FROM Win32_DiskDrive");
            foreach (ManagementObject obj in searcher.Get())
            {
                var deviceId = obj["DeviceID"]?.ToString() ?? string.Empty;
                var index = Convert.ToInt32(obj["Index"] ?? 0);
                var model = obj["Model"]?.ToString()?.Trim() ?? "Unknown Disk";
                var serial = obj["SerialNumber"]?.ToString()?.Trim() ?? "Unknown";
                var iface = obj["InterfaceType"]?.ToString() ?? "Unknown";
                var mediaType = obj["MediaType"]?.ToString() ?? string.Empty;
                var size = obj["Size"] is null ? 0L : Convert.ToInt64(obj["Size"]);

                var isSsd = mediaType.Contains("SSD", StringComparison.OrdinalIgnoreCase)
                            || model.Contains("SSD", StringComparison.OrdinalIgnoreCase)
                            || model.Contains("NVMe", StringComparison.OrdinalIgnoreCase)
                            || GetIsSsdFromStorageNamespace(index);

                if (model.Contains("NVMe", StringComparison.OrdinalIgnoreCase)) iface = "NVMe";

                if (!mediaType.Contains("Fixed", StringComparison.OrdinalIgnoreCase)) continue;

                var sampleFolder = GetFirstFixedVolumePath(index);
                drives.Add(new PhysicalDriveRef(deviceId, index, model, serial, iface, isSsd, size, sampleFolder));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate physical drives");
        }
        return drives;
    }

    /// <summary>
    /// Classic Win32_DiskDrive.MediaType rarely distinguishes SSD from HDD for AHCI-attached
    /// drives. The modern Storage namespace's MSFT_PhysicalDisk.MediaType (3=HDD, 4=SSD) is
    /// the reliable, real signal Windows itself uses to label drives in Settings > Storage.
    /// </summary>
    private bool GetIsSsdFromStorageNamespace(int diskIndex)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\Microsoft\Windows\Storage",
                $"SELECT MediaType FROM MSFT_PhysicalDisk WHERE DeviceId='{diskIndex}'");
            foreach (ManagementObject obj in searcher.Get())
            {
                var mediaType = obj["MediaType"] is null ? 0 : Convert.ToInt32(obj["MediaType"]);
                return mediaType == 4; // 4 = SSD
            }
        }
        catch { }
        return false;
    }

    private string? GetFirstFixedVolumePath(int diskIndex)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                $"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='\\\\.\\PHYSICALDRIVE{diskIndex}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");
            foreach (ManagementObject partition in searcher.Get())
            {
                using var logicalSearcher = new ManagementObjectSearcher(
                    $"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");
                foreach (ManagementObject logical in logicalSearcher.Get())
                {
                    var letter = logical["DeviceID"]?.ToString();
                    if (string.IsNullOrEmpty(letter)) continue;

                    var root = letter + @"\";
                    // Writing directly to a drive root is often denied for a non-elevated
                    // process even on an admin account (UAC token filtering) — prefer the
                    // user's Temp folder when it lives on this same volume, since it's
                    // always writable and still measures the same physical disk.
                    var tempPath = Path.GetTempPath();
                    if (string.Equals(Path.GetPathRoot(tempPath), root, StringComparison.OrdinalIgnoreCase))
                        return tempPath;

                    return root;
                }
            }
        }
        catch { }
        return null;
    }

    // ── Live throughput measurement (real, measured — not simulated) ────

    private (double? read, double? write) MeasureThroughput(string? folderPath)
    {
        if (folderPath is null || !Directory.Exists(folderPath)) return (null, null);

        const int sizeMb = 48;
        var buffer = new byte[1024 * 1024];
        new Random().NextBytes(buffer);
        var testFile = Path.Combine(folderPath, $"__wd_bench_{Guid.NewGuid():N}.tmp");

        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using (var fs = new FileStream(testFile, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.WriteThrough))
            {
                for (int i = 0; i < sizeMb; i++) fs.Write(buffer, 0, buffer.Length);
                fs.Flush(true);
            }
            sw.Stop();
            var writeMbs = sizeMb / Math.Max(sw.Elapsed.TotalSeconds, 0.001);

            // A normal buffered read of a file just written is served straight from the
            // OS page cache (multi-GB/s), not the physical disk — bypass the cache with
            // FILE_FLAG_NO_BUFFERING so this measures real media throughput.
            var readMbs = NativeMethods.MeasureUncachedReadMBs(testFile, sizeMb);
            if (readMbs is null)
            {
                sw.Restart();
                using (var fs = new FileStream(testFile, FileMode.Open, FileAccess.Read, FileShare.None, 1024 * 1024, FileOptions.SequentialScan))
                {
                    var readBuf = new byte[1024 * 1024];
                    while (fs.Read(readBuf, 0, readBuf.Length) > 0) { }
                }
                sw.Stop();
                readMbs = sizeMb / Math.Max(sw.Elapsed.TotalSeconds, 0.001);
            }

            return (readMbs, writeMbs);
        }
        finally
        {
            try { File.Delete(testFile); } catch { }
        }
    }

    // ── SMART reading via ATA PASS THROUGH (real device data) ───────────

    private DiskHealthInfo ReadSmartInfo(PhysicalDriveRef drive, bool isAdmin)
    {
        if (!isAdmin)
        {
            return new DiskHealthInfo
            {
                DeviceId = drive.DeviceId,
                Model = drive.Model,
                SerialNumber = drive.SerialNumber,
                InterfaceType = drive.InterfaceType,
                IsSsd = drive.IsSsd,
                SizeBytes = drive.SizeBytes,
                SmartAvailable = false,
                UnavailableReason = "Administrator privileges are required to read SMART data. Restart Windows Doctor as Administrator for full disk health details."
            };
        }

        using var handle = NativeMethods.CreateFile(
            drive.DeviceId,
            NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            0,
            IntPtr.Zero);

        if (handle.IsInvalid)
        {
            var err = Marshal.GetLastWin32Error();
            return new DiskHealthInfo
            {
                DeviceId = drive.DeviceId,
                Model = drive.Model,
                SerialNumber = drive.SerialNumber,
                InterfaceType = drive.InterfaceType,
                IsSsd = drive.IsSsd,
                SizeBytes = drive.SizeBytes,
                SmartAvailable = false,
                UnavailableReason = $"Could not open device (Win32 error {err})."
            };
        }

        var rawData = NativeMethods.ReadAtaSmart(handle, 0xD0); // SMART READ DATA
        if (rawData is null)
        {
            return new DiskHealthInfo
            {
                DeviceId = drive.DeviceId,
                Model = drive.Model,
                SerialNumber = drive.SerialNumber,
                InterfaceType = drive.InterfaceType,
                IsSsd = drive.IsSsd,
                SizeBytes = drive.SizeBytes,
                SmartAvailable = false,
                UnavailableReason = "SMART data is not accessible for this drive (may be a RAID/NVMe controller that does not support ATA pass-through)."
            };
        }

        var rawThresholds = NativeMethods.ReadAtaSmart(handle, 0xD1); // SMART READ THRESHOLDS (may be unsupported on some drives)
        var attributes = SmartAttributeParser.Parse(rawData, rawThresholds);
        if (attributes.Count == 0)
        {
            return new DiskHealthInfo
            {
                DeviceId = drive.DeviceId,
                Model = drive.Model,
                SerialNumber = drive.SerialNumber,
                InterfaceType = drive.InterfaceType,
                IsSsd = drive.IsSsd,
                SizeBytes = drive.SizeBytes,
                SmartAvailable = false,
                UnavailableReason = "SMART data returned no recognizable attributes."
            };
        }

        var (health, temp, poh, cycles, written, read, realloc, pending) = SmartAttributeParser.Analyze(attributes);

        // Windows abstracts AHCI/SATA controllers through the SCSI miniport stack, so
        // Win32_DiskDrive.InterfaceType often reports "SCSI" for real SATA disks. A
        // successful ATA pass-through response is direct proof the device is ATA/SATA.
        var resolvedInterface = drive.InterfaceType.Equals("SCSI", StringComparison.OrdinalIgnoreCase)
            ? "SATA" : drive.InterfaceType;

        return new DiskHealthInfo
        {
            DeviceId = drive.DeviceId,
            Model = drive.Model,
            SerialNumber = drive.SerialNumber,
            InterfaceType = resolvedInterface,
            IsSsd = drive.IsSsd,
            SizeBytes = drive.SizeBytes,
            SmartAvailable = true,
            HealthPercent = health,
            TemperatureCelsius = temp,
            PowerOnHours = poh,
            PowerCycleCount = cycles,
            TotalBytesWritten = written,
            TotalBytesRead = read,
            ReallocatedSectorCount = realloc,
            PendingSectorCount = pending,
            SmartAttributes = attributes
        };
    }

    private static class NativeMethods
    {
        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 0x1;
        public const uint FILE_SHARE_WRITE = 0x2;
        public const uint OPEN_EXISTING = 3;
        private const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
        private const uint FILE_FLAG_SEQUENTIAL_SCAN = 0x08000000;

        /// <summary>Measures real (uncached) sequential read throughput of an existing file.</summary>
        public static double? MeasureUncachedReadMBs(string filePath, int sizeMb)
        {
            using var handle = CreateFile(filePath, GENERIC_READ, FILE_SHARE_READ, IntPtr.Zero,
                OPEN_EXISTING, FILE_FLAG_NO_BUFFERING | FILE_FLAG_SEQUENTIAL_SCAN, IntPtr.Zero);
            if (handle.IsInvalid) return null;

            try
            {
                const int chunk = 1024 * 1024; // 1 MiB — sector-aligned for unbuffered I/O
                var buffer = GC.AllocateArray<byte>(chunk, pinned: true);
                long offset = 0;
                long targetBytes = (long)sizeMb * 1024 * 1024;

                var sw = System.Diagnostics.Stopwatch.StartNew();
                while (offset < targetBytes)
                {
                    int n = RandomAccess.Read(handle, buffer, offset);
                    if (n <= 0) break;
                    offset += n;
                }
                sw.Stop();

                return offset / 1024.0 / 1024.0 / Math.Max(sw.Elapsed.TotalSeconds, 0.001);
            }
            catch
            {
                return null;
            }
        }
        private const uint IOCTL_ATA_PASS_THROUGH = 0x0004D02C;
        private const int HeaderSize = 48; // sizeof(ATA_PASS_THROUGH_EX) on x64

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern SafeFileHandle CreateFile(
            string lpFileName, uint dwDesiredAccess, uint dwShareMode, IntPtr lpSecurityAttributes,
            uint dwCreationDisposition, uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            SafeFileHandle hDevice, uint dwIoControlCode,
            byte[] lpInBuffer, uint nInBufferSize,
            byte[] lpOutBuffer, uint nOutBufferSize,
            out uint lpBytesReturned, IntPtr lpOverlapped);

        /// <summary>Sends an ATA SMART sub-command (0xD0 = READ DATA, 0xD1 = READ THRESHOLDS) via pass-through.</summary>
        public static byte[]? ReadAtaSmart(SafeFileHandle handle, byte featuresCommand)
        {
            const int dataSize = 512;
            var buffer = new byte[HeaderSize + dataSize];

            // ATA_PASS_THROUGH_EX header (x64 layout)
            BitConverter.GetBytes((ushort)HeaderSize).CopyTo(buffer, 0);   // Length
            BitConverter.GetBytes((ushort)0x0003).CopyTo(buffer, 2);      // AtaFlags: DRDY_REQUIRED | DATA_IN
            // PathId/TargetId/Lun/Reserved = 0 (bytes 4-7)
            BitConverter.GetBytes((uint)dataSize).CopyTo(buffer, 8);       // DataTransferLength
            BitConverter.GetBytes((uint)3).CopyTo(buffer, 12);            // TimeOutValue (seconds)
            // ReservedAsUlong = 0 (bytes 16-19), padding (bytes 20-23)
            BitConverter.GetBytes((ulong)HeaderSize).CopyTo(buffer, 24);   // DataBufferOffset

            // CurrentTaskFile starts at offset 40: Features, SectorCount, SectorNum, CylLow, CylHigh, DevHead, Command, Reserved
            buffer[40] = featuresCommand; // Features: 0xD0 READ DATA or 0xD1 READ THRESHOLDS
            buffer[41] = 0x01; // SectorCount
            buffer[42] = 0x00; // SectorNumber
            buffer[43] = 0x4F; // CylinderLow
            buffer[44] = 0xC2; // CylinderHigh
            buffer[45] = 0xA0; // Device/Head
            buffer[46] = 0xB0; // Command: SMART

            try
            {
                var ok = DeviceIoControl(handle, IOCTL_ATA_PASS_THROUGH, buffer, (uint)buffer.Length,
                    buffer, (uint)buffer.Length, out _, IntPtr.Zero);
                if (!ok) return null;

                var data = new byte[dataSize];
                Array.Copy(buffer, HeaderSize, data, 0, dataSize);
                return data;
            }
            catch
            {
                return null;
            }
        }
    }
}
