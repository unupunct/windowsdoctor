using Microsoft.Extensions.Logging.Abstractions;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Repair;
using Xunit;

namespace WindowsDoctor.Tests.Repair;

public class RepairServiceTests
{
    private readonly RepairService _sut = new(NullLogger<RepairService>.Instance);

    [Fact]
    public void GetAvailableRepairs_NotEmpty()
        => Assert.NotEmpty(_sut.GetAvailableRepairs());

    [Fact]
    public void AllRepairs_HaveRequiredFields()
    {
        foreach (var r in _sut.GetAvailableRepairs())
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Id),          $"Repair {r.Name} missing Id");
            Assert.False(string.IsNullOrWhiteSpace(r.Name),        $"Repair {r.Id} missing Name");
            Assert.False(string.IsNullOrWhiteSpace(r.Description), $"Repair {r.Id} missing Description");
            Assert.False(string.IsNullOrWhiteSpace(r.Category),    $"Repair {r.Id} missing Category");
        }
    }

    [Fact]
    public async Task UnknownRepairId_ReturnsFailed()
    {
        var result = await _sut.ExecuteAsync("does-not-exist",
            new Progress<RepairProgress>(), CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal("does-not-exist", result.RepairId);
    }

    [Fact]
    public void IsRunningAsAdmin_ReturnsBool()
    {
        var result = _sut.IsRunningAsAdmin();
        Assert.IsType<bool>(result);
    }

    [Fact]
    public void AllRepairIds_AreUnique()
    {
        var ids = _sut.GetAvailableRepairs().Select(r => r.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
