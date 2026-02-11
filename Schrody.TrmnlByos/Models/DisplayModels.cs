namespace Schrody.TrmnlByos.Models;

public sealed record DisplayResponse(
    string ScreenId,
    string ImageUrl,
    DateTimeOffset UpdatedAt,
    int RefreshRate
);
