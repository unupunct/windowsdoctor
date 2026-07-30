using WindowsDoctor.Common.Enums;

namespace WindowsDoctor.Common.Models;

/// <summary>
/// Represents a single check result produced by a diagnostic module.
/// Each finding describes one discrete issue (or confirmation of health) discovered during a scan.
/// </summary>
public sealed record DiagnosticFinding
{
    /// <summary>
    /// Stable unique identifier for this finding instance.
    /// Typically formatted as "{Module}:{Check}:{sequence}" (e.g. "Storage:FreeSpace:C").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>The name of the diagnostic module that produced this finding.</summary>
    public required string Module { get; init; }

    /// <summary>A short, machine-readable key for the specific check performed (e.g. "FreeSpacePercent").</summary>
    public required string Check { get; init; }

    /// <summary>The health outcome of this check.</summary>
    public required HealthStatus Status { get; init; }

    /// <summary>The severity associated with this finding when <see cref="Status"/> is not <see cref="HealthStatus.Good"/>.</summary>
    public required Severity Severity { get; init; }

    /// <summary>A short, human-readable title (one line) suitable for display in a list or summary.</summary>
    public required string Title { get; init; }

    /// <summary>A full descriptive message explaining what was found.</summary>
    public required string Message { get; init; }

    /// <summary>Optional extended technical details, stack traces, or raw output for advanced users.</summary>
    public string? Details { get; init; }

    /// <summary>
    /// The <see cref="RepairDefinition.Id"/> of a repair action that can address this finding,
    /// or <see langword="null"/> if no automated repair is available.
    /// </summary>
    public string? RecommendedRepairId { get; init; }

    /// <summary>
    /// Arbitrary structured data captured during the check (e.g. metric values, file paths).
    /// Keys are camelCase strings; values are any JSON-serialisable type.
    /// </summary>
    public Dictionary<string, object>? Data { get; init; }

    /// <summary>The UTC timestamp at which this finding was recorded.</summary>
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// The complete output of one diagnostic module run, including all findings and a computed health score.
/// </summary>
public sealed record DiagnosticReport
{
    /// <summary>The name of the module that produced this report (matches <see cref="Core.Interfaces.IDiagnosticModule.Name"/>).</summary>
    public required string ModuleName { get; init; }

    /// <summary>UTC time at which the module began executing.</summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>UTC time at which the module finished executing.</summary>
    public required DateTime CompletedAt { get; init; }

    /// <summary>
    /// A normalised health score for this module in the range [0, 100].
    /// 100 indicates all checks passed; lower values reflect the number and severity of findings.
    /// </summary>
    public required double HealthScore { get; init; }

    /// <summary>All individual check results produced during this module run.</summary>
    public required List<DiagnosticFinding> Findings { get; init; }
}
