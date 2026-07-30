using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.Services;

public class SettingsService : ISettingsService
{
    private readonly ILogger<SettingsService> _logger;
    private static readonly string _settingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsDoctor", "settings.json");

    public SettingsService(ILogger<SettingsService> logger) => _logger = logger;

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                var json = File.ReadAllText(_settingsPath);
                return JsonConvert.DeserializeObject<AppSettings>(json) ?? Defaults();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings, using defaults");
        }
        return Defaults();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
            File.WriteAllText(_settingsPath, JsonConvert.SerializeObject(settings, Formatting.Indented));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            throw;
        }
    }

    private static AppSettings Defaults() => new()
    {
        DarkMode = true,
        AutoScan = false,
        AutoScanSchedule = "Daily",
        MaxHistoryDays = 90,
        LogLevel = "Information",
        Language = "en",
        OutputPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
    };
}
