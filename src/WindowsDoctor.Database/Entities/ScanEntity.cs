using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WindowsDoctor.Database.Entities;

[Table("Scans")]
public class ScanEntity
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    public DateTime StartedAt { get; set; }

    [Required]
    public DateTime CompletedAt { get; set; }

    [Required]
    public string SystemSnapshotJson { get; set; } = string.Empty;

    [Required]
    public string ReportsJson { get; set; } = string.Empty;

    [Required]
    public string HealthReportJson { get; set; } = string.Empty;

    [Required]
    public string RepairsAppliedJson { get; set; } = string.Empty;

    public double OverallScore { get; set; }
    public int FindingCount { get; set; }
    public int CriticalCount { get; set; }
}
