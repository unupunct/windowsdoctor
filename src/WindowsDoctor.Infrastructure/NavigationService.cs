using WindowsDoctor.Common.Enums;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.Infrastructure;

public class NavigationService : INavigationService
{
    private readonly Stack<NavigationItem> _history = new();

    public NavigationItem CurrentItem { get; private set; } = NavigationItem.Dashboard;

    public event EventHandler<NavigationItem>? Navigated;

    public void NavigateTo(NavigationItem item)
    {
        _history.Push(CurrentItem);
        CurrentItem = item;
        Navigated?.Invoke(this, item);
    }

    public void NavigateBack()
    {
        if (_history.TryPop(out var prev))
        {
            CurrentItem = prev;
            Navigated?.Invoke(this, prev);
        }
    }
}
