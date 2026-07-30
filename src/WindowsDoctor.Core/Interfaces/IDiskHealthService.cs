using WindowsDoctor.Common.Models;

namespace WindowsDoctor.Core.Interfaces;

public interface IDiskHealthService
{
    Task<List<DiskHealthInfo>> GetDiskHealthAsync(CancellationToken ct = default);
}
