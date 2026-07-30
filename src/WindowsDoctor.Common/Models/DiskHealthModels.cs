namespace WindowsDoctor.Common.Models;

public sealed record SmartAttributeInfo
{
    public required byte Id { get; init; }
    public required string Name { get; init; }
    public required byte Current { get; init; }
    public required byte Worst { get; init; }
    public required byte Threshold { get; init; }
    public required long RawValue { get; init; }
    public required bool IsCritical { get; init; }
    public required string Status { get; init; }
}

public sealed record DiskHealthInfo
{
    public required string DeviceId { get; init; }
    public required string Model { get; init; }
    public required string SerialNumber { get; init; }
    public required string InterfaceType { get; init; }
    public required bool IsSsd { get; init; }
    public required long SizeBytes { get; init; }

    public bool SmartAvailable { get; init; }
    public string? UnavailableReason { get; init; }

    public double HealthPercent { get; init; } = 100;
    public double? PerformancePercent { get; init; }
    public double? ReadSpeedMBs { get; init; }
    public double? WriteSpeedMBs { get; init; }

    public double? TemperatureCelsius { get; init; }
    public long? PowerOnHours { get; init; }
    public long? PowerCycleCount { get; init; }
    public long? TotalBytesWritten { get; init; }
    public long? TotalBytesRead { get; init; }
    public long? ReallocatedSectorCount { get; init; }
    public long? PendingSectorCount { get; init; }

    public List<SmartAttributeInfo> SmartAttributes { get; init; } = [];
}
