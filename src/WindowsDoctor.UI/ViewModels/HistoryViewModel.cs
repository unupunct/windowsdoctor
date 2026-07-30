using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.UI.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    [ObservableProperty] private bool _isLoading = false;
    [ObservableProperty] private ScanHistoryItemViewModel? _selectedScan;
    [ObservableProperty] private string _statusText = "";

    public ObservableCollection<ScanHistoryItemViewModel> ScanHistory { get; } = [];

    public HistoryViewModel() => _ = LoadAsync();

    private async Task LoadAsync(CancellationToken ct = default)
    {
        IsLoading = true;
        try
        {
            using var scope = App.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IScanHistoryRepository>();
            var summaries = await repo.GetRecentAsync(50, ct);
            ScanHistory.Clear();
            foreach (var s in summaries)
                ScanHistory.Add(new ScanHistoryItemViewModel(s));
            StatusText = $"{ScanHistory.Count} scan(s) in history.";
        }
        catch (Exception ex) { StatusText = $"Error: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand] private async Task Refresh(CancellationToken ct) => await LoadAsync(ct);
}

public class ScanHistoryItemViewModel
{
    private readonly ScanSummary _s;
    public ScanHistoryItemViewModel(ScanSummary s) => _s = s;

    public Guid     Id         => _s.Id;
    public string   ScanDate   => _s.ScanDate.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public double   Score      => _s.OverallScore;
    public int      Findings   => _s.FindingCount;
    public int      Critical   => _s.CriticalCount;
    public string   ScoreColor => _s.OverallScore switch { >= 80 => "#A6E3A1", >= 60 => "#F9E2AF", >= 40 => "#FAB387", _ => "#F38BA8" };
}
