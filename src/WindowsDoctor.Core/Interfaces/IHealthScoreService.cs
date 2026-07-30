using WindowsDoctor.Common.Models;

namespace WindowsDoctor.Core.Interfaces;

/// <summary>
/// Computes an aggregated <see cref="HealthReport"/> from the collection of
/// <see cref="DiagnosticReport"/> objects produced by all modules in a scan.
/// </summary>
/// <remarks>
/// The scoring algorithm weights modules by their relative importance and maps finding
/// severities to score deductions. The overall score is a weighted mean of individual
/// module scores, normalised to [0, 100].
/// </remarks>
public interface IHealthScoreService
{
    /// <summary>
    /// Calculates the overall and per-category health scores from a completed set of module reports.
    /// </summary>
    /// <param name="reports">
    /// The collection of <see cref="DiagnosticReport"/> instances from one scan session.
    /// Must not be <see langword="null"/>; may be empty, in which case scores default to 100.
    /// </param>
    /// <param name="ct">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A <see cref="HealthReport"/> containing the overall score, per-category scores,
    /// ordered top recommendations, and the derived <see cref="Common.Enums.HealthStatus"/>.
    /// </returns>
    Task<HealthReport> CalculateAsync(IEnumerable<DiagnosticReport> reports, CancellationToken ct = default);
}
