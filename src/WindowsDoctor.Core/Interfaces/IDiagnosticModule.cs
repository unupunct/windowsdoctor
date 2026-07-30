using WindowsDoctor.Common.Models;

namespace WindowsDoctor.Core.Interfaces;

/// <summary>
/// Contract for a self-contained diagnostic module that inspects one aspect of system health
/// (e.g. storage, security, Windows integrity) and returns a structured report.
/// </summary>
/// <remarks>
/// All modules are discovered at startup via dependency injection and enumerated by
/// the scan orchestrator in <c>WindowsDoctor.Infrastructure</c>. Implement this interface
/// for each diagnostic area and register the implementation as a named service.
/// </remarks>
public interface IDiagnosticModule
{
    /// <summary>
    /// The unique display name of this module, used as the module identifier in
    /// <see cref="DiagnosticReport.ModuleName"/> and in the UI navigation panel.
    /// Examples: "Storage", "Windows Health", "Security".
    /// </summary>
    string Name { get; }

    /// <summary>
    /// A Segoe MDL2 / Fluent UI icon glyph code (e.g. "") or a named resource key
    /// used to render the module's icon in the sidebar and report headers.
    /// </summary>
    string Icon { get; }

    /// <summary>
    /// A one-sentence description of what this module checks, displayed in the UI
    /// as a tooltip or sub-heading.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Executes all diagnostic checks within this module and returns a complete report.
    /// </summary>
    /// <param name="progress">
    /// Receives integer progress values in the range [0, 100] as checks complete.
    /// The implementation should report 0 on entry and 100 on successful completion.
    /// </param>
    /// <param name="ct">
    /// Token used to cancel the operation. Implementations should honour cancellation
    /// at natural check boundaries and throw <see cref="OperationCanceledException"/> promptly.
    /// </param>
    /// <returns>
    /// A <see cref="DiagnosticReport"/> containing all findings and the computed health score
    /// for this module. Must not return <see langword="null"/>.
    /// </returns>
    Task<DiagnosticReport> RunDiagnosticsAsync(IProgress<int> progress, CancellationToken ct = default);
}
