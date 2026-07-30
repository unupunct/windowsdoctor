using WindowsDoctor.Common.Enums;
using WindowsDoctor.Common.Models;
using Xunit;

namespace WindowsDoctor.Tests.Common;

public class ModelTests
{
    [Fact]
    public void DiagnosticFinding_InitializesCorrectly()
    {
        var f = new DiagnosticFinding
        {
            Id        = "test:check:1",
            Module    = "Security",
            Check     = "Defender",
            Status    = HealthStatus.Error,
            Severity  = Severity.Critical,
            Title     = "Defender off",
            Message   = "Real-time protection is disabled",
            Timestamp = DateTime.UtcNow
        };
        Assert.Equal("Security", f.Module);
        Assert.Equal(Severity.Critical, f.Severity);
        Assert.Null(f.Details);
        Assert.Null(f.RecommendedRepairId);
    }

    [Fact]
    public void RepairDefinition_InitializesCorrectly()
    {
        var r = new RepairDefinition
        {
            Id               = "flush-dns",
            Name             = "Flush DNS",
            Description      = "Clears the DNS cache",
            ExpectedImpact   = "Resolves DNS issues",
            Risk             = RepairRisk.Safe,
            RequiresElevation= false,
            RequiresRestart  = false,
            Category         = "Network"
        };
        Assert.Equal(RepairRisk.Safe, r.Risk);
        Assert.False(r.RequiresElevation);
        Assert.Null(r.TargetFindingId);
    }

    [Fact]
    public void HealthReport_ScoreRange()
    {
        var h = new HealthReport
        {
            OverallScore       = 75.5,
            CategoryScores     = new() { ["Security"] = 20.0 },
            TopRecommendations = ["Fix Defender"],
            OverallStatus      = HealthStatus.Warning
        };
        Assert.True(h.OverallScore is >= 0 and <= 100);
        Assert.Equal(HealthStatus.Warning, h.OverallStatus);
    }

    [Fact]
    public void Enums_HaveExpectedValues()
    {
        Assert.True(Enum.IsDefined(typeof(Severity), Severity.Critical));
        Assert.True(Enum.IsDefined(typeof(HealthStatus), HealthStatus.Good));
        Assert.True(Enum.IsDefined(typeof(RepairRisk), RepairRisk.Safe));
        Assert.True(Enum.IsDefined(typeof(NavigationItem), NavigationItem.Dashboard));
    }
}
