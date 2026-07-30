using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using WindowsDoctor.Common.Enums;
using WindowsDoctor.Core.Interfaces;

namespace WindowsDoctor.Services;

public class NotificationService : INotificationService
{
    private readonly ILogger<NotificationService> _logger;
    private readonly ConcurrentQueue<(string Title, string Message, Severity Severity)> _pending = new();
    private const int MaxPending = 20;

    public NotificationService(ILogger<NotificationService> logger) => _logger = logger;

    public void Show(string title, string message, Severity severity = Severity.Info)
    {
        _logger.LogInformation("[{Severity}] {Title}: {Message}", severity, title, message);
        _pending.Enqueue((title, message, severity));
        while (_pending.Count > MaxPending && _pending.TryDequeue(out _)) { }
    }

    public List<(string Title, string Message, Severity Severity)> GetPending()
    {
        var list = new List<(string, string, Severity)>();
        while (_pending.TryDequeue(out var item))
            list.Add(item);
        return list;
    }
}
