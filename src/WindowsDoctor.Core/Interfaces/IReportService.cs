using WindowsDoctor.Common.Enums;
using WindowsDoctor.Common.Models;

namespace WindowsDoctor.Core.Interfaces;

/// <summary>
/// Generates exportable diagnostic reports from a completed <see cref="FullScanRecord"/>
/// in the requested format and writes the result to disk.
/// </summary>
public interface IReportService
{
    /// <summary>
    /// Generates a report file for the given scan record and writes it to <paramref name="outputPath"/>.
    /// </summary>
    /// <param name="scan">
    /// The completed scan record to report on. Must not be <see langword="null"/>.
    /// </param>
    /// <param name="format">The desired output format (PDF, HTML, JSON, or CSV).</param>
    /// <param name="outputPath">
    /// The fully qualified file system path where the report file will be written.
    /// The directory must exist; the file will be created or overwritten.
    /// </param>
    /// <param name="ct">Token used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="Task"/> that completes when the file has been fully written.</returns>
    Task GenerateAsync(FullScanRecord scan, ReportFormat format, string outputPath, CancellationToken ct = default);
}
