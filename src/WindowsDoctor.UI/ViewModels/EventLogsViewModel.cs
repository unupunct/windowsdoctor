using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindowsDoctor.Common.Models;
using WindowsDoctor.Core.Interfaces;
using WindowsDoctor.Diagnostics.Modules;

namespace WindowsDoctor.UI.ViewModels;

public partial class EventLogsViewModel : DiagnosticBaseViewModel
{
    private readonly EventLogModule _module;
    private readonly IEventLogService _eventLogService;
    protected override IDiagnosticModule Module => _module;

    [ObservableProperty] private bool _isLoadingEvents;
    [ObservableProperty] private string _eventsStatus = "Press \"Load Recent Errors\" to read the System and Application event logs.";

    public ObservableCollection<EventLogItemViewModel> RecentEvents { get; } = [];

    public EventLogsViewModel(EventLogModule module, IEventLogService eventLogService)
    {
        _module = module;
        _eventLogService = eventLogService;
        SetModuleInfo();
        _ = LoadRecentErrorsAsync();
    }

    [RelayCommand]
    private async Task LoadRecentErrorsAsync(CancellationToken ct = default)
    {
        if (IsLoadingEvents) return;
        IsLoadingEvents = true;
        EventsStatus = "Reading System and Application event logs…";
        RecentEvents.Clear();

        try
        {
            var results = await _eventLogService.GetRecentErrorsAsync(hours: 72, maxPerLog: 100, ct: ct);
            foreach (var e in results)
                RecentEvents.Add(new EventLogItemViewModel(e));

            EventsStatus = results.Count == 0
                ? "No critical or error events in the last 72 hours."
                : $"{results.Count} critical/error event(s) in the last 72 hours, newest first.";
        }
        catch (Exception ex)
        {
            EventsStatus = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoadingEvents = false;
        }
    }

    [RelayCommand]
    private void LaunchEventViewer()
    {
        try { Process.Start(new ProcessStartInfo("eventvwr.msc") { UseShellExecute = true }); }
        catch { /* ignore — nothing sensible to do if Event Viewer can't launch */ }
    }

    [RelayCommand]
    private void ShowEventDetails(EventLogItemViewModel? item)
    {
        if (item is null) return;
        System.Windows.MessageBox.Show(item.FullDetails, $"Event {item.EventId} — {item.Source}",
            System.Windows.MessageBoxButton.OK,
            item.Level == "Critical" ? System.Windows.MessageBoxImage.Error : System.Windows.MessageBoxImage.Warning);
    }
}

public class EventLogItemViewModel(EventLogItem e)
{
    public int EventId => e.EventId;
    public string Source => e.Source;
    public string Level => e.Level;
    public string TimeLabel => e.TimeCreated.ToString("yyyy-MM-dd HH:mm:ss");
    public string LogLabel => e.LogName;
    public string LevelColor => e.Level switch { "Critical" => "#F38BA8", "Error" => "#FAB387", "Warning" => "#F9E2AF", _ => "#CDD6F4" };
    public string MessageExcerpt => Truncate(e.Message, 160);
    public bool HasDiagnosis => !string.IsNullOrEmpty(e.Diagnosis);
    public string DiagnosisLabel => e.Diagnosis ?? "";

    public string FullDetails
    {
        get
        {
            var lines = new List<string>
            {
                $"Log:        {e.LogName}",
                $"Source:     {e.Source}",
                $"Event ID:   {e.EventId}",
                $"Level:      {e.Level}",
                $"Time:       {e.TimeCreated:yyyy-MM-dd HH:mm:ss}",
                "",
                "Message:",
                e.Message,
            };
            if (!string.IsNullOrEmpty(e.Diagnosis))
            {
                lines.Add("");
                lines.Add("What this likely means:");
                lines.Add(e.Diagnosis);
            }
            if (!string.IsNullOrEmpty(e.SuggestedFix))
            {
                lines.Add("");
                lines.Add("Suggested fix:");
                lines.Add(e.SuggestedFix);
            }
            return string.Join(Environment.NewLine, lines);
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max].TrimEnd() + "…";
}
