using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WindowsDoctor.Core.Interfaces;
using WindowsDoctor.Database;
using WindowsDoctor.Database.Repositories;
using WindowsDoctor.Diagnostics.Modules;
using WindowsDoctor.Repair;
using WindowsDoctor.Services;

namespace WindowsDoctor.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsDoctorServices(
        this IServiceCollection services,
        IConfiguration config)
    {
        // Ensure data directory exists
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsDoctor");
        Directory.CreateDirectory(dataDir);

        var dbPath = Path.Combine(dataDir, "history.db");
        var connStr = $"Data Source={dbPath}";

        // Database
        services.AddDbContext<WinDoctorDbContext>(o => o.UseSqlite(connStr));
        services.AddScoped<IScanHistoryRepository, ScanHistoryRepository>();

        // Core services (singleton — stateless or intentionally shared)
        services.AddSingleton<ISystemInfoService, SystemInfoService>();
        services.AddSingleton<IDiskHealthService, DiskHealthService>();
        services.AddSingleton<IEventLogService, EventLogService>();
        services.AddSingleton<IHealthScoreService, HealthScoreService>();
        services.AddSingleton<IRepairService, RepairService>();
        services.AddSingleton<IReportService, ReportService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<INavigationService, NavigationService>();

        // Diagnostic modules (transient — each scan gets fresh instances)
        services.AddTransient<HardwareModule>();
        services.AddTransient<StorageModule>();
        services.AddTransient<WindowsHealthModule>();
        services.AddTransient<PerformanceModule>();
        services.AddTransient<SecurityModule>();
        services.AddTransient<NetworkModule>();
        services.AddTransient<SoftwareModule>();
        services.AddTransient<EventLogModule>();

        services.AddTransient<IEnumerable<IDiagnosticModule>>(sp => new IDiagnosticModule[]
        {
            sp.GetRequiredService<HardwareModule>(),
            sp.GetRequiredService<StorageModule>(),
            sp.GetRequiredService<WindowsHealthModule>(),
            sp.GetRequiredService<PerformanceModule>(),
            sp.GetRequiredService<SecurityModule>(),
            sp.GetRequiredService<NetworkModule>(),
            sp.GetRequiredService<SoftwareModule>(),
            sp.GetRequiredService<EventLogModule>(),
        });

        return services;
    }
}
