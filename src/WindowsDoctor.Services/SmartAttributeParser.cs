using WindowsDoctor.Common.Models;

namespace WindowsDoctor.Services;

/// <summary>Parses raw ATA SMART READ DATA / READ THRESHOLDS (512-byte) structures.</summary>
internal static class SmartAttributeParser
{
    private static readonly Dictionary<byte, string> Names = new()
    {
        [1] = "Read Error Rate",
        [3] = "Spin-Up Time",
        [4] = "Start/Stop Count",
        [5] = "Reallocated Sectors Count",
        [7] = "Seek Error Rate",
        [9] = "Power-On Hours",
        [10] = "Spin Retry Count",
        [12] = "Power Cycle Count",
        [168] = "SATA Phy Error Count",
        [170] = "Reserved Block Count",
        [171] = "Program Fail Count",
        [172] = "Erase Fail Count",
        [173] = "Wear Leveling Count",
        [174] = "Unexpected Power Loss Count",
        [177] = "Wear Range Delta",
        [179] = "Used Reserved Block Count",
        [180] = "Unused Reserved Block Count",
        [181] = "Program Fail Count Total",
        [182] = "Erase Fail Count Total",
        [183] = "Runtime Bad Block Count",
        [184] = "End-to-End Error",
        [187] = "Reported Uncorrectable Errors",
        [188] = "Command Timeout",
        [190] = "Airflow Temperature",
        [194] = "Temperature",
        [195] = "Hardware ECC Recovered",
        [196] = "Reallocation Event Count",
        [197] = "Current Pending Sector Count",
        [198] = "Uncorrectable Sector Count",
        [199] = "UltraDMA CRC Error Count",
        [202] = "Data Address Mark Errors",
        [231] = "SSD Life Left",
        [232] = "Available Reserved Space",
        [233] = "Media Wearout Indicator",
        [241] = "Total LBAs Written",
        [242] = "Total LBAs Read",
    };

    private static readonly HashSet<byte> CriticalIds = [5, 187, 196, 197, 198, 199];

    public static List<SmartAttributeInfo> Parse(byte[] data, byte[]? thresholds)
    {
        var result = new List<SmartAttributeInfo>();
        if (data.Length < 362) return result;

        for (int i = 0; i < 30; i++)
        {
            int offset = 2 + i * 12;
            byte id = data[offset];
            if (id == 0) continue;

            byte current = data[offset + 3];
            byte worst = data[offset + 4];

            long raw = 0;
            for (int b = 0; b < 6; b++)
                raw |= (long)data[offset + 5 + b] << (b * 8);

            byte threshold = 0;
            if (thresholds is { Length: >= 362 })
            {
                for (int j = 0; j < 30; j++)
                {
                    int tOffset = 2 + j * 12;
                    if (thresholds[tOffset] == id) { threshold = thresholds[tOffset + 1]; break; }
                }
            }

            var isCritical = CriticalIds.Contains(id);
            var name = Names.GetValueOrDefault(id, $"Unknown Attribute 0x{id:X2}");

            string status;
            if (threshold > 0 && current <= threshold) status = "Failing";
            else if (isCritical && raw > 0) status = "Warning";
            else status = "Good";

            result.Add(new SmartAttributeInfo
            {
                Id = id,
                Name = name,
                Current = current,
                Worst = worst,
                Threshold = threshold,
                RawValue = raw,
                IsCritical = isCritical,
                Status = status
            });
        }

        return result;
    }

    public static (double health, double? tempC, long? powerOnHours, long? powerCycles, long? bytesWritten,
        long? bytesRead, long? reallocated, long? pending) Analyze(List<SmartAttributeInfo> attrs)
    {
        double health = 100;
        double? temp = null;
        long? poh = null, cycles = null, written = null, read = null, realloc = null, pending = null;

        var ssdLife = attrs.FirstOrDefault(a => a.Id == 231);
        if (ssdLife is not null)
        {
            health = ssdLife.Current;
        }
        else
        {
            var wearout = attrs.FirstOrDefault(a => a.Id == 233);
            if (wearout is not null) health = wearout.Current;
        }

        foreach (var a in attrs)
        {
            switch (a.Id)
            {
                case 194 or 190:
                    temp ??= a.RawValue & 0xFF;
                    break;
                case 9:
                    poh = a.RawValue & 0xFFFFFFFF;
                    break;
                case 12:
                    cycles = a.RawValue;
                    break;
                case 241:
                    written = a.RawValue * 512;
                    break;
                case 242:
                    read = a.RawValue * 512;
                    break;
                case 5:
                    realloc = a.RawValue;
                    if (ssdLife is null) health -= Math.Min(30, a.RawValue * 2);
                    break;
                case 197:
                    pending = a.RawValue;
                    if (ssdLife is null && a.RawValue > 0) health -= Math.Min(30, a.RawValue * 5);
                    break;
                case 198:
                    if (ssdLife is null && a.RawValue > 0) health -= Math.Min(20, a.RawValue * 5);
                    break;
            }

            if (a.Status == "Failing") health = Math.Min(health, 10);
        }

        health = Math.Clamp(health, 0, 100);
        return (health, temp, poh, cycles, written, read, realloc, pending);
    }
}
