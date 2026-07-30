using WindowsDoctor.Common.Models;

namespace WindowsDoctor.Core.Interfaces;

public interface IEventLogService
{
    Task<List<EventLogItem>> GetRecentErrorsAsync(int hours = 72, int maxPerLog = 100, CancellationToken ct = default);
}
