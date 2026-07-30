using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace WindowsDoctor.Database;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILogger<WinDoctorDbContext>>();
        try
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WinDoctorDbContext>();
            await db.Database.EnsureCreatedAsync();
            logger.LogInformation("Database initialized at {Path}", db.Database.GetDbConnection().DataSource);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database initialization failed");
        }
    }
}
