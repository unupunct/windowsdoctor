using WindowsDoctor.Common.Enums;

namespace WindowsDoctor.Common.Models;

/// <summary>
/// Describes a repair action that Windows Doctor can execute to address one or more diagnostic findings.
/// This is the static definition of the repair; <see cref="RepairResult"/> captures the execution outcome.
/// </summary>
public sealed record RepairDefinition
{
    /// <summary>
    /// Stable unique identifier for this repair action (e.g. "repair.storage.clear-temp-files").
    /// Referenced by <see cref="DiagnosticFinding.RecommendedRepairId"/>.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>Display name shown in the Repair Center (e.g. "Clear Temporary Files").</summary>
    public required string Name { get; init; }

    /// <summary>A paragraph describing what the repair does and why it is recommended.</summary>
    public required string Description { get; init; }

    /// <summary>A brief, user-facing statement of what the user should notice after the repair succeeds (e.g. "Frees up to several GB of disk space").</summary>
    public required string ExpectedImpact { get; init; }

    /// <summary>The risk category for executing this repair.</summary>
    public required RepairRisk Risk { get; init; }

    /// <summary>
    /// <see langword="true"/> if the repair must be executed from an elevated (Administrator) process.
    /// The application will prompt for elevation or display a warning if not already elevated.
    /// </summary>
    public required bool RequiresElevation { get; init; }

    /// <summary>
    /// <see langword="true"/> if a system restart is required to complete the repair.
    /// The application will prompt the user before and/or after execution.
    /// </summary>
    public required bool RequiresRestart { get; init; }

    /// <summary>
    /// The <see cref="DiagnosticFinding.Id"/> this repair is primarily intended to resolve,
    /// or <see langword="null"/> if the repair is a general maintenance action.
    /// </summary>
    public string? TargetFindingId { get; init; }

    /// <summary>
    /// Logical grouping for display in the Repair Center
    /// (e.g. "Storage", "Windows Health", "Performance", "Security").
    /// </summary>
    public required string Category { get; init; }
}

/// <summary>
/// Represents an incremental progress update reported by a repair action during execution.
/// Consumed by <see cref="IProgress{T}"/> handlers in the UI.
/// </summary>
public sealed record RepairProgress
{
    /// <summary>Completion percentage in the range [0, 100].</summary>
    public required int PercentComplete { get; init; }

    /// <summary>A short human-readable description of the current step being performed.</summary>
    public required string StatusMessage { get; init; }
}

/// <summary>
/// The outcome of executing a single repair action.
/// </summary>
public sealed record RepairResult
{
    /// <summary>The <see cref="RepairDefinition.Id"/> of the repair that was executed.</summary>
    public required string RepairId { get; init; }

    /// <summary><see langword="true"/> if the repair completed without errors; otherwise <see langword="false"/>.</summary>
    public required bool Success { get; init; }

    /// <summary>A user-facing summary message describing the outcome.</summary>
    public required string Message { get; init; }

    /// <summary>Raw standard output captured from any child processes invoked during the repair, if applicable.</summary>
    public string? Output { get; init; }

    /// <summary>Raw standard error output captured from any child processes, if applicable.</summary>
    public string? ErrorOutput { get; init; }

    /// <summary>The UTC timestamp at which the repair finished (success or failure).</summary>
    public required DateTime CompletedAt { get; init; }
}
