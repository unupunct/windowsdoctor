using System.Diagnostics.Eventing.Reader;
using Microsoft.Extensions.Logging;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.Services;

public class EventLogService : IEventLogService
{
    private readonly ILogger<EventLogService> _logger;
    private static readonly string[] LogNames = ["System", "Application"];

    public EventLogService(ILogger<EventLogService> logger) => _logger = logger;

    public async Task<List<EventLogItem>> GetRecentErrorsAsync(int hours = 72, int maxPerLog = 100, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var results = new List<EventLogItem>();
            foreach (var logName in LogNames)
            {
                try
                {
                    results.AddRange(ReadLog(logName, hours, maxPerLog));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to read {Log} event log", logName);
                }
            }
            return results.OrderByDescending(e => e.TimeCreated).ToList();
        }, ct);
    }

    private List<EventLogItem> ReadLog(string logName, int hours, int maxCount)
    {
        var items = new List<EventLogItem>();
        var cutoffTicks = DateTime.UtcNow.AddHours(-hours).ToFileTimeUtc();

        // Level 1 = Critical, 2 = Error
        var query = $"*[System[(Level=1 or Level=2) and TimeCreated[timediff(@SystemTime) <= {hours * 3600000L}]]]";
        var eventQuery = new EventLogQuery(logName, PathType.LogName, query) { ReverseDirection = true };

        using var reader = new EventLogReader(eventQuery);
        EventRecord? record;
        int count = 0;
        while (count < maxCount && (record = reader.ReadEvent()) is not null)
        {
            using (record)
            {
                try
                {
                    var source = record.ProviderName ?? "Unknown";
                    var eventId = record.Id;
                    string message;
                    try { message = record.FormatDescription() ?? "(no description available)"; }
                    catch { message = "(description unavailable — provider metadata not installed)"; }

                    var level = record.Level switch { 1 => "Critical", 2 => "Error", 3 => "Warning", _ => "Info" };
                    var (diagnosis, fix) = EventDiagnosisCatalog.Diagnose(source, eventId, message);

                    items.Add(new EventLogItem
                    {
                        LogName = logName,
                        TimeCreated = record.TimeCreated?.ToLocalTime() ?? DateTime.Now,
                        Source = source,
                        EventId = eventId,
                        Level = level,
                        Message = message,
                        Diagnosis = diagnosis,
                        SuggestedFix = fix
                    });
                    count++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse an event record in {Log}", logName);
                }
            }
        }
        return items;
    }
}
