namespace TrmnlByos.Models;

public sealed record SetupRequest(
    string DeviceId,
    string? Model,
    string? FirmwareVersion,
    int? RefreshRate
);

public sealed record SetupResponse(
    string DeviceId,
    string ScreenId,
    string Model,
    int RefreshRate,
    string Success
);
