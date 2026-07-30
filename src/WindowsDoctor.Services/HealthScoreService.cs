using Microsoft.Extensions.Logging;
using WindowsDoctor.Common.Enums;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.Services;

public class HealthScoreService : IHealthScoreService
{
    private readonly ILogger<HealthScoreService> _logger;

    // Category -> max points
    private static readonly Dictionary<string, double> _categoryWeights = new()
    {
        ["Windows Update"]    = 10,
        ["Disk Health"]       = 20,
        ["RAM"]               = 10,
        ["Security"]          = 20,
        ["Performance"]       = 20,
        ["Drivers"]           = 10,
        ["System Integrity"]  = 10,
    };

    // Module name -> which categories it contributes to
    private static readonly Dictionary<string, string[]> _moduleCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Hardware"]       = ["Disk Health", "RAM"],
        ["Storage"]        = ["Disk Health"],
        ["Windows Health"] = ["Windows Update", "System Integrity", "Drivers"],
        ["Performance"]    = ["Performance"],
        ["Security"]       = ["Security"],
        ["Network"]        = ["System Integrity"],
        ["Software"]       = ["System Integrity"],
        ["Event Logs"]     = ["System Integrity"],
    };

    public HealthScoreService(ILogger<HealthScoreService> logger) => _logger = logger;

    public async Task<HealthReport> CalculateAsync(
        IEnumerable<DiagnosticReport> reports,
        CancellationToken ct = default)
    {
        return await Task.Run(() => Calculate(reports), ct);
    }

    private HealthReport Calculate(IEnumerable<DiagnosticReport> reports)
    {
        var allFindings = reports.SelectMany(r => r.Findings).ToList();
        var categoryScores = new Dictionary<string, double>(_categoryWeights);

        // group findings by module
        var byModule = allFindings.GroupBy(f => f.Module, StringComparer.OrdinalIgnoreCase);
        foreach (var group in byModule)
        {
            if (!_moduleCategories.TryGetValue(group.Key, out var cats)) cats = ["System Integrity"];
            foreach (var cat in cats)
            {
                if (!categoryScores.ContainsKey(cat)) continue;
                double maxDeduct = _categoryWeights[cat];
                double deduct = 0;
                foreach (var f in group)
                {
                    if (f.Status == HealthStatus.Good) continue;
                    deduct += f.Severity switch
                    {
                        Severity.Critical => maxDeduct,
                        Severity.High     => maxDeduct * 0.70,
                        Severity.Medium   => maxDeduct * 0.40,
                        Severity.Low      => maxDeduct * 0.20,
                        _                 => 0
                    };
                }
                categoryScores[cat] = Math.Max(0, _categoryWeights[cat] - deduct);
            }
        }

        double overall = Math.Round(Math.Min(100, categoryScores.Values.Sum()), 1);
        var status = overall switch
        {
            >= 80 => HealthStatus.Good,
            >= 60 => HealthStatus.Warning,
            >= 40 => HealthStatus.Error,
            _     => HealthStatus.Error
        };

        var recommendations = allFindings
            .Where(f => f.Severity is Severity.Critical or Severity.High)
            .OrderByDescending(f => f.Severity)
            .Take(5)
            .Select(f => $"[{f.Module}] {f.Title}: {f.Message}")
            .ToList();

        _logger.LogInformation("Health score calculated: {Score}", overall);

        return new HealthReport
        {
            OverallScore = overall,
            CategoryScores = categoryScores.ToDictionary(k => k.Key, k => Math.Round(k.Value, 1)),
            TopRecommendations = recommendations,
            OverallStatus = status
        };
    }
}
