using WindowsDoctor.Common.Enums;

namespace WindowsDoctor.Core.Interfaces;

/// <summary>
/// Displays non-blocking in-app toast notifications to the user.
/// Abstracts the UI notification mechanism so that non-UI layers can trigger alerts
/// without taking a direct dependency on WPF.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Displays a transient toast notification with the given title, message, and severity.
    /// </summary>
    /// <param name="title">
    /// A short heading for the notification (e.g. "Scan Complete", "Repair Failed").
    /// Should be no longer than 60 characters.
    /// </param>
    /// <param name="message">
    /// The body text of the notification. Should explain what happened and, where relevant,
    /// what the user should do next.
    /// </param>
    /// <param name="severity">
    /// Controls the visual treatment of the notification (icon colour, accent stripe).
    /// Defaults to <see cref="Severity.Info"/>.
    /// </param>
    void Show(string title, string message, Severity severity = Severity.Info);
}
