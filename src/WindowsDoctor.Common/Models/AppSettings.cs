namespace WindowsDoctor.Common.Models;

/// <summary>
/// User-configurable application settings persisted between sessions.
/// Loaded and saved via <see cref="Core.Interfaces.ISettingsService"/>.
/// </summary>
public sealed record AppSettings
{
    /// <summary>
    /// <see langword="true"/> to use the dark colour theme; <see langword="false"/> for the light theme.
    /// </summary>
    public required bool DarkMode { get; init; }

    /// <summary>
    /// <see langword="true"/> to run a diagnostic scan automatically on a schedule
    /// defined by <see cref="AutoScanSchedule"/>.
    /// </summary>
    public required bool AutoScan { get; init; }

    /// <summary>
    /// A cron expression or human-readable schedule string that controls when the automatic
    /// scan fires when <see cref="AutoScan"/> is <see langword="true"/>
    /// (e.g. "0 9 * * 1" for every Monday at 09:00, or "Daily" for once per day).
    /// </summary>
    public required string AutoScanSchedule { get; init; }

    /// <summary>
    /// Maximum number of days to retain full scan records in the history database.
    /// Records older than this threshold are pruned on startup.
    /// </summary>
    public required int MaxHistoryDays { get; init; }

    /// <summary>
    /// Minimum log level to emit, expressed as a Serilog/Microsoft.Extensions.Logging level string
    /// (e.g. "Information", "Warning", "Debug").
    /// </summary>
    public required string LogLevel { get; init; }

    /// <summary>
    /// BCP-47 language tag for the UI locale (e.g. "en-US", "de-DE").
    /// Falls back to the system locale when empty.
    /// </summary>
    public required string Language { get; init; }

    /// <summary>
    /// Default file system path where generated reports are saved when no explicit
    /// path is provided at export time.
    /// </summary>
    public required string OutputPath { get; init; }
}
