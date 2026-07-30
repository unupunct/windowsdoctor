using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;
using WindowsDoctor.Database.Entities;

namespace WindowsDoctor.Database.Repositories;

public class ScanHistoryRepository : IScanHistoryRepository
{
    private readonly WinDoctorDbContext _db;
    private readonly ILogger<ScanHistoryRepository> _logger;

    private static readonly JsonSerializerSettings _jsonSettings = new()
    {
        TypeNameHandling = TypeNameHandling.None,
        NullValueHandling = NullValueHandling.Ignore
    };

    public ScanHistoryRepository(WinDoctorDbContext db, ILogger<ScanHistoryRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SaveScanAsync(FullScanRecord record, CancellationToken ct = default)
    {
        try
        {
            var entity = new ScanEntity
            {
                Id = record.Id,
                StartedAt = record.StartedAt,
                CompletedAt = record.CompletedAt,
                SystemSnapshotJson = JsonConvert.SerializeObject(record.SystemSnapshot, _jsonSettings),
                ReportsJson = JsonConvert.SerializeObject(record.Reports, _jsonSettings),
                HealthReportJson = JsonConvert.SerializeObject(record.HealthReport, _jsonSettings),
                RepairsAppliedJson = JsonConvert.SerializeObject(record.RepairsApplied, _jsonSettings),
                OverallScore = record.HealthReport?.OverallScore ?? 0,
                FindingCount = record.Reports?.Sum(r => r.Findings.Count) ?? 0,
                CriticalCount = record.Reports?.Sum(r => r.Findings.Count(f =>
                    f.Severity == Common.Enums.Severity.Critical)) ?? 0
            };

            _db.Scans.Add(entity);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Saved scan {Id} with score {Score}", record.Id, entity.OverallScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save scan {Id}", record.Id);
            throw;
        }
    }

    public async Task<IReadOnlyList<ScanSummary>> GetRecentAsync(int count = 10, CancellationToken ct = default)
    {
        var entities = await _db.Scans
            .OrderByDescending(s => s.StartedAt)
            .Take(count)
            .ToListAsync(ct);

        return entities.Select(e => new ScanSummary
        {
            Id = e.Id,
            ScanDate = e.StartedAt,
            OverallScore = e.OverallScore,
            FindingCount = e.FindingCount,
            CriticalCount = e.CriticalCount
        }).ToList();
    }

    public async Task<FullScanRecord?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _db.Scans.FindAsync(new object[] { id }, ct);
        if (entity is null) return null;

        try
        {
            return new FullScanRecord
            {
                Id = entity.Id,
                StartedAt = entity.StartedAt,
                CompletedAt = entity.CompletedAt,
                SystemSnapshot = JsonConvert.DeserializeObject<SystemSnapshot>(entity.SystemSnapshotJson, _jsonSettings)!,
                Reports = JsonConvert.DeserializeObject<List<DiagnosticReport>>(entity.ReportsJson, _jsonSettings) ?? [],
                HealthReport = JsonConvert.DeserializeObject<HealthReport>(entity.HealthReportJson, _jsonSettings)!,
                RepairsApplied = JsonConvert.DeserializeObject<List<RepairResult>>(entity.RepairsAppliedJson, _jsonSettings) ?? []
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize scan {Id}", id);
            return null;
        }
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
        => await _db.Scans.CountAsync(ct);
}
