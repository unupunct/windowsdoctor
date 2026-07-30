namespace WindowsDoctor.Common.Models;

public sealed record EventLogItem
{
    public required string LogName { get; init; }
    public required DateTime TimeCreated { get; init; }
    public required string Source { get; init; }
    public required int EventId { get; init; }
    public required string Level { get; init; }
    public required string Message { get; init; }
    public string? Diagnosis { get; init; }
    public string? SuggestedFix { get; init; }
}
