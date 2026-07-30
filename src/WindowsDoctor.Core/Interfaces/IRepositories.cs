using WindowsDoctor.Common.Models;

namespace WindowsDoctor.Core.Interfaces;

/// <summary>
/// Provides persistent storage and retrieval of completed full scan records.
/// Implemented by the SQLite-backed repository in <c>WindowsDoctor.Database</c>.
/// </summary>
public interface IScanHistoryRepository
{
    /// <summary>
    /// Persists a completed <see cref="FullScanRecord"/> to the history store.
    /// If a record with the same <see cref="FullScanRecord.Id"/> already exists it is replaced.
    /// </summary>
    /// <param name="record">The scan record to save. Must not be <see langword="null"/>.</param>
    /// <param name="ct">Token used to cancel the asynchronous operation.</param>
    Task SaveScanAsync(FullScanRecord record, CancellationToken ct = default);

    /// <summary>
    /// Returns the most recent scan summaries in reverse chronological order (newest first).
    /// </summary>
    /// <param name="count">Maximum number of summaries to return. Defaults to 10.</param>
    /// <param name="ct">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// A read-only list of <see cref="ScanSummary"/> objects. May be shorter than
    /// <paramref name="count"/> when fewer records exist.
    /// </returns>
    Task<IReadOnlyList<ScanSummary>> GetRecentAsync(int count = 10, CancellationToken ct = default);

    /// <summary>
    /// Retrieves a single full scan record by its unique identifier.
    /// </summary>
    /// <param name="id">The scan record GUID.</param>
    /// <param name="ct">Token used to cancel the asynchronous operation.</param>
    /// <returns>
    /// The matching <see cref="FullScanRecord"/>, or <see langword="null"/> when no record
    /// with the given <paramref name="id"/> exists in the store.
    /// </returns>
    Task<FullScanRecord?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns the total number of scan records currently held in the history store.
    /// </summary>
    /// <param name="ct">Token used to cancel the asynchronous operation.</param>
    /// <returns>The count of records, or zero when the store is empty.</returns>
    Task<int> GetTotalCountAsync(CancellationToken ct = default);
}
