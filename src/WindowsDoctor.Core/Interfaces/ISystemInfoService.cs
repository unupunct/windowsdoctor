using WindowsDoctor.Common.Models;

namespace WindowsDoctor.Core.Interfaces;

/// <summary>
/// Provides access to live system hardware, OS, and resource information.
/// Used both by the scan orchestrator to capture a <see cref="SystemSnapshot"/> and
/// by the dashboard to display real-time CPU and RAM metrics.
/// </summary>
public interface ISystemInfoService
{
    /// <summary>
    /// Captures a complete point-in-time snapshot of the current system state.
    /// </summary>
    /// <param name="ct">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A fully populated <see cref="SystemSnapshot"/> reflecting the system at the time of the call.
    /// </returns>
    Task<SystemSnapshot> GetSnapshotAsync(CancellationToken ct = default);

    /// <summary>
    /// Samples the total CPU utilisation across all logical processors.
    /// </summary>
    /// <param name="ct">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A value in the range [0.0, 100.0] representing the current CPU usage percentage.
    /// </returns>
    Task<double> GetCpuUsageAsync(CancellationToken ct = default);

    /// <summary>
    /// Calculates the current RAM utilisation as a percentage of total physical memory.
    /// </summary>
    /// <param name="ct">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A value in the range [0.0, 100.0] representing the percentage of RAM currently in use.
    /// </returns>
    Task<double> GetRamUsagePercentAsync(CancellationToken ct = default);
}
