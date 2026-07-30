using Microsoft.EntityFrameworkCore;
using WindowsDoctor.Database.Entities;

namespace WindowsDoctor.Database;

public class WinDoctorDbContext : DbContext
{
    public WinDoctorDbContext(DbContextOptions<WinDoctorDbContext> options) : base(options) { }

    public DbSet<ScanEntity> Scans => Set<ScanEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScanEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.StartedAt).IsDescending();
            e.Property(x => x.SystemSnapshotJson).IsRequired();
            e.Property(x => x.ReportsJson).IsRequired();
            e.Property(x => x.HealthReportJson).IsRequired();
            e.Property(x => x.RepairsAppliedJson).IsRequired();
        });
    }
}
