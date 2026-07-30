using WindowsDoctor.Common.Enums;

namespace WindowsDoctor.Common.Models;

/// <summary>
/// Aggregated health report computed from the findings of all diagnostic modules in a scan.
/// </summary>
public sealed record HealthReport
{
    /// <summary>
    /// Weighted overall health score for the system in the range [0, 100].
    /// Derived from the individual module scores in <see cref="CategoryScores"/>.
    /// </summary>
    public required double OverallScore { get; init; }

    /// <summary>
    /// Per-module health scores, keyed by module name (matches <see cref="DiagnosticReport.ModuleName"/>).
    /// Each value is in the range [0, 100].
    /// </summary>
    public required Dictionary<string, double> CategoryScores { get; init; }

    /// <summary>
    /// Ordered list of the most important recommended actions, derived from high-severity findings.
    /// Suitable for display as a "Top Issues" panel on the dashboard.
    /// </summary>
    public required List<string> TopRecommendations { get; init; }

    /// <summary>The overall health status derived from <see cref="OverallScore"/> thresholds.</summary>
    public required HealthStatus OverallStatus { get; init; }
}

/// <summary>
/// The complete persisted record for a single full diagnostic scan session,
/// including the system snapshot, all module reports, the computed health report,
/// and any repairs applied immediately after the scan.
/// </summary>
public sealed record FullScanRecord
{
    /// <summary>Unique identifier for this scan record.</summary>
    public required Guid Id { get; init; }

    /// <summary>UTC time at which scanning began.</summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>UTC time at which all modules completed and the health report was computed.</summary>
    public required DateTime CompletedAt { get; init; }

    /// <summary>System state captured at the start of the scan.</summary>
    public required SystemSnapshot SystemSnapshot { get; init; }

    /// <summary>One report per diagnostic module that was executed during this scan.</summary>
    public required List<DiagnosticReport> Reports { get; init; }

    /// <summary>The aggregated health report computed from <see cref="Reports"/>.</summary>
    public required HealthReport HealthReport { get; init; }

    /// <summary>
    /// Repair actions that were executed during or immediately following this scan session.
    /// Empty when no repairs were applied.
    /// </summary>
    public required List<RepairResult> RepairsApplied { get; init; }
}

/// <summary>
/// A lightweight summary of a completed scan, used to populate history lists without loading the full record.
/// </summary>
public sealed record ScanSummary
{
    /// <summary>Matches <see cref="FullScanRecord.Id"/>.</summary>
    public required Guid Id { get; init; }

    /// <summary>The UTC date and time the scan was completed.</summary>
    public required DateTime ScanDate { get; init; }

    /// <summary>The overall health score computed for this scan (0–100).</summary>
    public required double OverallScore { get; init; }

    /// <summary>Total number of findings across all modules.</summary>
    public required int FindingCount { get; init; }

    /// <summary>Number of findings with <see cref="Severity.Critical"/> severity.</summary>
    public required int CriticalCount { get; init; }
}
