using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using WindowsDoctor.Common.Enums;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.UI.ViewModels;

public partial class ReportsViewModel : ObservableObject
{
    private readonly IReportService  _report;
    private readonly ISettingsService _settings;

    [ObservableProperty] private string _outputPath   = "";
    [ObservableProperty] private string _statusMessage = "Select a format and generate a report.";
    [ObservableProperty] private bool   _isGenerating  = false;
    [ObservableProperty] private string _selectedFormat = "Html";

    private FullScanRecord? _lastScan;

    public ReportsViewModel(IReportService report, ISettingsService settings)
    {
        _report   = report;
        _settings = settings;
        OutputPath = settings.Load().OutputPath;
        _ = LoadLastScanAsync();
    }

    private async Task LoadLastScanAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IScanHistoryRepository>();
            var summaries = await repo.GetRecentAsync(1);
            if (summaries.Count > 0)
            {
                _lastScan = await repo.GetByIdAsync(summaries[0].Id);
                StatusMessage = $"Last scan: {summaries[0].ScanDate.ToLocalTime():yyyy-MM-dd HH:mm} — Score: {summaries[0].OverallScore:0}/100";
            }
            else StatusMessage = "No scan data. Run a scan from the Dashboard first.";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
    }

    [RelayCommand]
    private async Task GenerateReportAsync(CancellationToken ct = default)
    {
        if (_lastScan is null) { StatusMessage = "No scan data available. Run a scan first."; return; }
        if (!Enum.TryParse<ReportFormat>(SelectedFormat, out var fmt)) return;

        IsGenerating = true;
        try
        {
            var ext = fmt switch { ReportFormat.Html => ".html", ReportFormat.Json => ".json", ReportFormat.Csv => ".csv", ReportFormat.Pdf => ".html", _ => ".html" };
            var filename = $"WindowsDoctor_Report_{DateTime.Now:yyyyMMdd_HHmmss}{ext}";
            var fullPath = Path.Combine(OutputPath, filename);
            await _report.GenerateAsync(_lastScan, fmt, fullPath, ct);
            StatusMessage = $"✅ Report saved: {fullPath}";
            Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
        }
        catch (Exception ex) { StatusMessage = $"❌ {ex.Message}"; }
        finally { IsGenerating = false; }
    }
}
