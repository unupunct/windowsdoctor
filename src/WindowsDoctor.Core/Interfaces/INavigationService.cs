using WindowsDoctor.Common.Enums;

namespace WindowsDoctor.Core.Interfaces;

/// <summary>
/// Controls top-level navigation within the Windows Doctor shell window,
/// mediating between the sidebar and the main content frame.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Navigates the main content frame to the view associated with <paramref name="item"/>
    /// and raises the <see cref="Navigated"/> event.
    /// </summary>
    /// <param name="item">The destination navigation item.</param>
    void NavigateTo(NavigationItem item);

    /// <summary>
    /// Gets the navigation item that is currently active (displayed in the main content frame).
    /// </summary>
    NavigationItem CurrentItem { get; }

    /// <summary>
    /// Raised after navigation completes. The event argument carries the
    /// <see cref="NavigationItem"/> that was navigated to.
    /// </summary>
    event EventHandler<NavigationItem> Navigated;
}
