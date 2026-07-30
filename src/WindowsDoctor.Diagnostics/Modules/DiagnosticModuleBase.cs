using Microsoft.Extensions.Logging;
using WindowsDoctor.Common.Enums;
using WindowsDoctor.Common.Models;

namespace WindowsDoctor.Diagnostics.Modules;

public abstract class DiagnosticModuleBase
{
    protected ILogger Logger { get; }

    protected DiagnosticModuleBase(ILogger logger) => Logger = logger;

    protected DiagnosticFinding MakeFinding(
        string module, string check, HealthStatus status, Severity severity,
        string title, string message, string? repairId = null, string? details = null,
        Dictionary<string, object>? data = null)
        => new()
        {
            Id = $"{module}:{check}:{Guid.NewGuid().ToString("N")[..8]}",
            Module = module,
            Check = check,
            Status = status,
            Severity = severity,
            Title = title,
            Message = message,
            Details = details,
            RecommendedRepairId = repairId,
            Data = data,
            Timestamp = DateTime.UtcNow
        };

    protected DiagnosticFinding Good(string module, string check, string title, string message)
        => MakeFinding(module, check, HealthStatus.Good, Severity.Info, title, message);

    protected DiagnosticFinding Info(string module, string check, string title, string message, string? details = null)
        => MakeFinding(module, check, HealthStatus.Good, Severity.Info, title, message, details: details);

    protected double CalculateScore(List<DiagnosticFinding> findings)
    {
        if (!findings.Any()) return 100;
        double deductions = findings.Sum(f => f.Severity switch
        {
            Severity.Critical => 40,
            Severity.High     => 25,
            Severity.Medium   => 15,
            Severity.Low      => 8,
            _                 => 0
        });
        return Math.Max(0, Math.Min(100, 100 - deductions));
    }

    protected static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:0.0} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:0.0} MB";
        return $"{bytes / 1024.0:0.0} KB";
    }
}
