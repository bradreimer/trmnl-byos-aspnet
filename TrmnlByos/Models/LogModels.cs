namespace TrmnlByos.Models;

public sealed record LogEntry(
    string Level,
    string Message,
    DateTimeOffset Timestamp,
    Dictionary<string, object>? Context
);
