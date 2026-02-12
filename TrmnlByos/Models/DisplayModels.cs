namespace TrmnlByos.Models;

public sealed record DisplayResponse(
    string filename,
    string firmware_url,
    string firmware_version,
    string image_url,
    int image_url_timeout,
    int refresh_rate,
    bool reset_firmware,
    string special_function,
    bool update_firmware
);
