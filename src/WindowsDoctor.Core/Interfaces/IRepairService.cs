using WindowsDoctor.Common.Models;

namespace WindowsDoctor.Core.Interfaces;

/// <summary>
/// Manages the catalogue of available repair actions and orchestrates their execution.
/// </summary>
/// <remarks>
/// Repair actions are registered by the <c>WindowsDoctor.Repair</c> assembly via dependency injection.
/// The service is responsible for routing execution requests to the correct handler and
/// reporting progress back to the caller in real time.
/// </remarks>
public interface IRepairService
{
    /// <summary>
    /// Returns the complete catalogue of repair actions available in the current session.
    /// </summary>
    /// <returns>
    /// A read-only list of <see cref="RepairDefinition"/> objects. The list is stable for
    /// the lifetime of the application process.
    /// </returns>
    IReadOnlyList<RepairDefinition> GetAvailableRepairs();

    /// <summary>
    /// Executes a single repair action identified by <paramref name="repairId"/>.
    /// </summary>
    /// <param name="repairId">
    /// The <see cref="RepairDefinition.Id"/> of the repair to execute.
    /// Throws <see cref="KeyNotFoundException"/> if no repair with this ID is registered.
    /// </param>
    /// <param name="progress">
    /// Receives <see cref="RepairProgress"/> updates as the repair proceeds.
    /// May be <see langword="null"/> when the caller does not require progress updates.
    /// </param>
    /// <param name="ct">Token used to cancel the operation mid-execution.</param>
    /// <returns>
    /// A <see cref="RepairResult"/> describing whether the repair succeeded or failed,
    /// including any captured output or error text.
    /// </returns>
    Task<RepairResult> ExecuteAsync(string repairId, IProgress<RepairProgress> progress, CancellationToken ct = default);

    /// <summary>
    /// Determines whether the current process is running with local Administrator privileges.
    /// Used to warn the user before attempting repairs that require elevation.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the current process token includes the Administrators group
    /// and UAC elevation is active (or UAC is disabled); otherwise <see langword="false"/>.
    /// </returns>
    bool IsRunningAsAdmin();
}
