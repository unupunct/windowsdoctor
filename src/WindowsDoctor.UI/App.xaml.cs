using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.IO;
using System.Windows;
using WindowsDoctor.Database;
using WindowsDoctor.Infrastructure;
using WindowsDoctor.UI.ViewModels;

namespace WindowsDoctor.UI;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider Services { get; private set; } = null!;

    public static T GetService<T>() where T : class
        => Services.GetRequiredService<T>();

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsDoctor", "logs", "log-.txt");

        _host = Host.CreateDefaultBuilder()
            .UseSerilog((ctx, cfg) => cfg
                .MinimumLevel.Information()
                .WriteTo.File(logPath, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
                .WriteTo.Console())
            .ConfigureServices((ctx, svc) =>
            {
                svc.AddWindowsDoctorServices(ctx.Configuration);

                // ViewModels
                svc.AddTransient<MainWindowViewModel>();
                svc.AddTransient<DashboardViewModel>();
                svc.AddTransient<HardwareViewModel>();
                svc.AddTransient<StorageViewModel>();
                svc.AddTransient<WindowsHealthViewModel>();
                svc.AddTransient<PerformanceViewModel>();
                svc.AddTransient<SecurityViewModel>();
                svc.AddTransient<NetworkViewModel>();
                svc.AddTransient<SoftwareViewModel>();
                svc.AddTransient<EventLogsViewModel>();
                svc.AddTransient<RepairCenterViewModel>();
                svc.AddTransient<ReportsViewModel>();
                svc.AddTransient<HistoryViewModel>();
                svc.AddTransient<SettingsViewModel>();
                svc.AddTransient<AdvancedToolsViewModel>();

                svc.AddTransient<MainWindow>();
            })
            .Build();

        Services = _host.Services;
        await _host.StartAsync();

        await DatabaseInitializer.InitializeAsync(_host.Services);

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        base.OnExit(e);
    }
}
