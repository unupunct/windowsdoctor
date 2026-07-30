using WindowsDoctor.Common.Enums;

namespace WindowsDoctor.Common.Helpers;

/// <summary>
/// Provides semantic colour mappings for health statuses and severity levels,
/// expressed as hex strings that can be consumed by both WPF converters and report generators.
/// </summary>
public static class ColorHelper
{
    // -------------------------------------------------------------------------
    // HealthStatus colour mapping
    // -------------------------------------------------------------------------

    /// <summary>Returns the hex colour string (e.g. "#2ECC71") associated with a <see cref="HealthStatus"/> value.</summary>
    /// <param name="status">The health status to map.</param>
    /// <returns>A 7-character hex colour string beginning with '#'.</returns>
    public static string ForHealthStatus(HealthStatus status) => status switch
    {
        HealthStatus.Good    => "#2ECC71",   // green
        HealthStatus.Warning => "#F39C12",   // amber
        HealthStatus.Error   => "#E74C3C",   // red
        HealthStatus.Unknown => "#95A5A6",   // muted slate
        _                    => "#95A5A6"
    };

    /// <summary>
    /// Returns a muted background-tint hex colour suitable for highlighting a row or card
    /// based on <see cref="HealthStatus"/>.
    /// </summary>
    /// <param name="status">The health status to map.</param>
    /// <returns>A 7-character hex colour string beginning with '#'.</returns>
    public static string BackgroundForHealthStatus(HealthStatus status) => status switch
    {
        HealthStatus.Good    => "#EAF9F1",
        HealthStatus.Warning => "#FEF9EC",
        HealthStatus.Error   => "#FDEDEC",
        HealthStatus.Unknown => "#F2F3F4",
        _                    => "#F2F3F4"
    };

    // -------------------------------------------------------------------------
    // Severity colour mapping
    // -------------------------------------------------------------------------

    /// <summary>Returns the hex colour string associated with a <see cref="Severity"/> value.</summary>
    /// <param name="severity">The severity level to map.</param>
    /// <returns>A 7-character hex colour string beginning with '#'.</returns>
    public static string ForSeverity(Severity severity) => severity switch
    {
        Severity.Info     => "#3498DB",   // blue
        Severity.Low      => "#27AE60",   // green
        Severity.Medium   => "#F39C12",   // amber
        Severity.High     => "#E67E22",   // orange
        Severity.Critical => "#E74C3C",   // red
        _                 => "#95A5A6"
    };

    // -------------------------------------------------------------------------
    // RepairRisk colour mapping
    // -------------------------------------------------------------------------

    /// <summary>Returns the hex colour string associated with a <see cref="RepairRisk"/> value.</summary>
    /// <param name="risk">The repair risk to map.</param>
    /// <returns>A 7-character hex colour string beginning with '#'.</returns>
    public static string ForRepairRisk(RepairRisk risk) => risk switch
    {
        RepairRisk.Safe   => "#2ECC71",
        RepairRisk.Low    => "#27AE60",
        RepairRisk.Medium => "#F39C12",
        RepairRisk.High   => "#E74C3C",
        _                 => "#95A5A6"
    };

    // -------------------------------------------------------------------------
    // Score-based colour (0–100 health score)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns a hex colour appropriate for a numeric health score in [0, 100].
    /// Uses the same semantic colour scale as <see cref="ForHealthStatus"/>.
    /// </summary>
    /// <param name="score">A health score in the range [0, 100].</param>
    /// <returns>A 7-character hex colour string beginning with '#'.</returns>
    public static string ForScore(double score) => score switch
    {
        >= 80 => "#2ECC71",
        >= 60 => "#F39C12",
        >= 40 => "#E67E22",
        _     => "#E74C3C"
    };
}
