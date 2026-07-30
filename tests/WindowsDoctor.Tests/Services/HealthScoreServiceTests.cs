using Microsoft.Extensions.Logging.Abstractions;
using WindowsDoctor.Common.Enums;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Services;
using Xunit;

namespace WindowsDoctor.Tests.Services;

public class HealthScoreServiceTests
{
    private readonly HealthScoreService _sut = new(NullLogger<HealthScoreService>.Instance);

    private static DiagnosticReport MakeReport(string module, params (HealthStatus status, Severity sev, string title)[] findings)
        => new()
        {
            ModuleName  = module,
            StartedAt   = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            HealthScore = 100,
            Findings    = findings.Select(f => new DiagnosticFinding
            {
                Id        = Guid.NewGuid().ToString(),
                Module    = module,
                Check     = "Test",
                Status    = f.status,
                Severity  = f.sev,
                Title     = f.title,
                Message   = "Test message",
                Timestamp = DateTime.UtcNow
            }).ToList()
        };

    [Fact]
    public async Task NoFindings_ReturnsMaxScore()
    {
        var report = MakeReport("Security");
        var result = await _sut.CalculateAsync([report]);
        Assert.Equal(100, result.OverallScore);
        Assert.Equal(HealthStatus.Good, result.OverallStatus);
    }

    [Fact]
    public async Task CriticalSecurityFinding_DropsScore()
    {
        var report = MakeReport("Security",
            (HealthStatus.Error, Severity.Critical, "Defender disabled"));
        var result = await _sut.CalculateAsync([report]);
        Assert.True(result.OverallScore < 100);
        Assert.Equal(0, result.CategoryScores["Security"]);
    }

    [Fact]
    public async Task MultipleHighFindings_DecreaseScore()
    {
        var report = MakeReport("Performance",
            (HealthStatus.Warning, Severity.High, "High CPU"),
            (HealthStatus.Warning, Severity.High, "High RAM"));
        var result = await _sut.CalculateAsync([report]);
        Assert.True(result.OverallScore < 100);
    }

    [Fact]
    public async Task AllGoodFindings_FullScore()
    {
        var report = MakeReport("Hardware",
            (HealthStatus.Good, Severity.Info, "CPU OK"),
            (HealthStatus.Good, Severity.Info, "RAM OK"));
        var result = await _sut.CalculateAsync([report]);
        Assert.Equal(100, result.OverallScore);
    }

    [Fact]
    public async Task TopRecommendations_ContainCriticalFirst()
    {
        var report = MakeReport("Security",
            (HealthStatus.Error,   Severity.Critical, "Critical issue"),
            (HealthStatus.Warning, Severity.Medium,   "Medium issue"));
        var result = await _sut.CalculateAsync([report]);
        Assert.NotEmpty(result.TopRecommendations);
        Assert.Contains("Critical issue", result.TopRecommendations[0]);
    }
}
