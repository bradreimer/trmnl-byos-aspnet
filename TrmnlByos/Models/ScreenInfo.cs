namespace TrmnlByos.Models;

public sealed record ScreenInfo(
    string Id,
    string Name,
    string? Description,
    DateTimeOffset LastUpdated,
    string? ImagePath,
    string DeviceId,
    string? Model,
    string? Firmware,
    int? RefreshRate,
    DateTimeOffset LastSeen
);
