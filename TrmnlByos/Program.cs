using System.Net.Mime;
using Microsoft.AspNetCore.Http.HttpResults;
using TrmnlByos.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

// Simple request/response logging middleware
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var request = context.Request;
    var deviceId = request.Headers["ID"].FirstOrDefault() ?? "unknown";

    logger.LogInformation("[{Timestamp:yyyy-MM-dd HH:mm:ss}] {Method} {Path} (Device: {DeviceId})",
        DateTime.UtcNow, request.Method, request.Path, deviceId);

    await next(context);

    logger.LogInformation("[{Timestamp:yyyy-MM-dd HH:mm:ss}] {Method} {Path} -> {StatusCode} (Device: {DeviceId})",
        DateTime.UtcNow, request.Method, request.Path, context.Response.StatusCode, deviceId);
});

app.UseSwagger();
app.UseSwaggerUI();

// Determine data root directory
var dataRoot = Environment.GetEnvironmentVariable("TEST_DATA_DIR")
    ?? (Directory.Exists("/data") ? "/data" : Path.Combine(Path.GetTempPath(), "trmnl-data"));
try
{
    Directory.CreateDirectory(dataRoot);
}
catch
{
    // If /data is not writable, use temp directory
    dataRoot = Path.Combine(Path.GetTempPath(), "trmnl-data");
    Directory.CreateDirectory(dataRoot);
}

// simple in-memory store
var screens = new Dictionary<string, ScreenInfo>(StringComparer.OrdinalIgnoreCase);

// ---- Firmware: Setup ----
// GET /api/setup
// Headers: ID (device id), optional: MODEL, FIRMWARE, REFRESH_RATE
app.MapGet("/api/setup", (HttpRequest request, ILogger<Program> logger) =>
{
    var deviceId = request.Headers["ID"].FirstOrDefault() ?? "unknown";
    var screenId = deviceId.ToLowerInvariant();

    if (!screens.TryGetValue(screenId, out var screen))
    {
        screen = new ScreenInfo(
            screenId,
            $"Screen {screenId}",
            null,
            DateTimeOffset.UtcNow,
            null
        );
        screens[screenId] = screen;
    }

    logger.LogInformation("Device setup: {DeviceId}", deviceId);

    var response = new SetupResponse(
        api_key: deviceId,
        friendly_id: screenId.ToUpper(),
        image_url: $"/screens/{screenId}.jpg",
        message: "Welcome to TRMNL BYOS"
    );

    return Results.Ok(response);
});

// ---- Firmware: Log ----
// POST /api/log
app.MapPost("/api/log", async (LogRequest logRequest, ILogger<Program> logger) =>
{
    foreach (var entry in logRequest.logs)
    {
        logger.LogInformation("Device telemetry: FW {FirmwareVersion} | Battery {BatteryVoltage}V | WiFi {WiFiSignal}dBm | Heap {FreeHeap}B | {Message}",
            entry.firmware_version, entry.battery_voltage, entry.wifi_signal, entry.free_heap_size, entry.message);
    }
    return Results.NoContent();
});

// ---- Firmware: Display ----
// GET /api/display
app.MapGet("/api/display", (HttpRequest request, ILogger<Program> logger) =>
{
    var deviceId = request.Headers["ID"].FirstOrDefault() ?? "unknown";
    var screenId = deviceId.ToLowerInvariant();
    var refreshHeader = request.Headers["REFRESH_RATE"].FirstOrDefault();
    var refreshRate = int.TryParse(refreshHeader, out var r) ? r : 100;

    if (!screens.TryGetValue(screenId, out var screen))
    {
        screen = new ScreenInfo(
            screenId,
            $"Screen {screenId}",
            null,
            DateTimeOffset.MinValue,
            null
        );
        screens[screenId] = screen;
    }

    var imagePath = screen.ImagePath ?? $"/screens/{screenId}.jpg";
    var filename = Path.GetFileName(imagePath);

    // Get the host from the request
    var host = request.Host.Host;
    var port = request.Host.Port ?? (request.IsHttps ? 443 : 80);
    var scheme = request.Scheme;
    var baseUrl = $"{scheme}://{host}:{port}";

    var absoluteImageUrl = $"{baseUrl}{imagePath}";
    var absoluteFirmwareUrl = $"{baseUrl}/firmware/latest.bin";

    logger.LogInformation("Display poll: {DeviceId} | Image: {Filename} | Refresh: {RefreshRate}ms | URLs: {BaseUrl}",
        deviceId, filename, refreshRate, baseUrl);

    var response = new DisplayResponse(
        filename: filename,
        firmware_url: absoluteFirmwareUrl,
        firmware_version: "1.0.0",
        image_url: absoluteImageUrl,
        image_url_timeout: 0,
        refresh_rate: refreshRate,
        reset_firmware: false,
        special_function: "none",
        update_firmware: false
    );

    return Results.Ok(response);
});

// ---- BYOD: upload image ----
// POST /api/screens/{id}/image
app.MapPost("/api/screens/{id}/image", async Task<Results<Ok<object>, BadRequest<string>>> (string id, HttpRequest request, ILogger<Program> logger) =>
{
    // Normalize to lowercase for consistent storage
    var normalizedId = id.ToLowerInvariant();

    var contentType = request.ContentType ?? MediaTypeNames.Image.Jpeg;
    if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
    {
        return TypedResults.BadRequest("Content-Type must be image/*");
    }

    var ext = contentType switch
    {
        "image/png" => ".png",
        _ => ".jpg"
    };

    // Delete any existing images with different extensions
    var jpgPath = Path.Combine(dataRoot, $"{normalizedId}.jpg");
    var pngPath = Path.Combine(dataRoot, $"{normalizedId}.png");

    if (ext == ".jpg" && File.Exists(pngPath))
    {
        File.Delete(pngPath);
    }
    else if (ext == ".png" && File.Exists(jpgPath))
    {
        File.Delete(jpgPath);
    }

    var filePath = Path.Combine(dataRoot, $"{normalizedId}{ext}");

    await using (var fs = File.Create(filePath))
    {
        await request.Body.CopyToAsync(fs);
    }

    var screen = screens.TryGetValue(normalizedId, out var existing)
        ? existing with { LastUpdated = DateTimeOffset.UtcNow, ImagePath = $"/screens/{normalizedId}{ext}" }
        : new ScreenInfo(normalizedId, $"Screen {normalizedId}", null, DateTimeOffset.UtcNow, $"/screens/{normalizedId}{ext}");

    screens[normalizedId] = screen;

    logger.LogInformation("Image uploaded: {ScreenId} | Type: {ContentType}", normalizedId, contentType);

    var result = new { id = normalizedId, path = screen.ImagePath! };
    return TypedResults.Ok((object)result);
});

// ---- BYOD: serve JPEG image ----
// GET /screens/{id}.jpg
app.MapGet("/screens/{id}.jpg", (string id, ILogger<Program> logger) =>
{
    var jpgPath = Path.Combine(dataRoot, $"{id}.jpg");

    if (File.Exists(jpgPath))
    {
        logger.LogInformation("Serving image: {ScreenId} (JPEG)", id);
        return Results.File(jpgPath, "image/jpeg");
    }

    logger.LogInformation("Image not found: {ScreenId}", id);
    return Results.NotFound();
});

// ---- BYOD: serve PNG image ----
// GET /screens/{id}.png
app.MapGet("/screens/{id}.png", (string id, ILogger<Program> logger) =>
{
    var pngPath = Path.Combine(dataRoot, $"{id}.png");

    if (File.Exists(pngPath))
    {
        logger.LogInformation("Serving image: {ScreenId} (PNG)", id);
        return Results.File(pngPath, "image/png");
    }

    logger.LogInformation("Image not found: {ScreenId}", id);
    return Results.NotFound();
});

// Health
app.MapGet("/", () => Results.Ok(new { status = "ok", service = "trmnl-byod-dotnet" }));

app.Run();

public partial class Program { }
