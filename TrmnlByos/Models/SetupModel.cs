namespace TrmnlByos.Models;

public sealed record SetupResponse(
    string api_key,
    string friendly_id,
    string image_url,
    string message
);
