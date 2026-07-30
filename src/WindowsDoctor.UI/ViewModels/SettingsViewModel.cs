using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settings;

    [ObservableProperty] private bool   _darkMode        = true;
    [ObservableProperty] private bool   _autoScan        = false;
    [ObservableProperty] private string _autoScanSchedule= "Daily";
    [ObservableProperty] private int    _maxHistoryDays  = 90;
    [ObservableProperty] private string _logLevel        = "Information";
    [ObservableProperty] private string _outputPath      = "";
    [ObservableProperty] private string _statusMessage   = "";

    public SettingsViewModel(ISettingsService settings)
    {
        _settings = settings;
        var s = settings.Load();
        DarkMode         = s.DarkMode;
        AutoScan         = s.AutoScan;
        AutoScanSchedule = s.AutoScanSchedule;
        MaxHistoryDays   = s.MaxHistoryDays;
        LogLevel         = s.LogLevel;
        OutputPath       = s.OutputPath;
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            _settings.Save(new AppSettings
            {
                DarkMode         = DarkMode,
                AutoScan         = AutoScan,
                AutoScanSchedule = AutoScanSchedule,
                MaxHistoryDays   = MaxHistoryDays,
                LogLevel         = LogLevel,
                Language         = "en",
                OutputPath       = OutputPath
            });
            StatusMessage = "✅ Settings saved.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (Directory.Exists(OutputPath))
            Process.Start("explorer.exe", OutputPath);
        else
            StatusMessage = "Output folder does not exist.";
    }
}
