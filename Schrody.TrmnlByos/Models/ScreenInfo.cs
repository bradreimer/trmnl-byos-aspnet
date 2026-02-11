namespace Schrody.TrmnlByos.Models;

public sealed record ScreenInfo(
    string Id,
    string Name,
    string? Description,
    DateTimeOffset LastUpdated,
    string? ImagePath
);
