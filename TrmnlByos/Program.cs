using System.Collections.Concurrent;
using System.Net.Mime;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.HttpOverrides;
using TrmnlByos.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedHost
        | ForwardedHeaders.XForwardedProto;
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

app.UseForwardedHeaders();

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

const int DefaultRefreshRate = 100;
const int DefaultMaxImageBytes = 10 * 1024 * 1024;
const int DefaultMaxImagesPerDevice = 10;
var maxImageBytes = builder.Configuration.GetValue<int?>("Uploads:MaxImageBytes") ?? DefaultMaxImageBytes;
var maxImagesPerDevice = builder.Configuration.GetValue<int?>("Uploads:MaxImagesPerDevice") ?? DefaultMaxImagesPerDevice;

// simple thread-safe in-memory store
var screens = new ConcurrentDictionary<string, ScreenInfo>(StringComparer.OrdinalIgnoreCase);
var imageHistoryByDevice = new ConcurrentDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
var imageUsageCounts = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
var imageTrackingLock = new object();

// ---- Firmware: Setup ----
// GET /api/setup
// Headers: ID (device id), optional: MODEL, FIRMWARE, REFRESH_RATE
app.MapGet("/api/setup", (HttpRequest request, ILogger<Program> logger) =>
{
    var deviceId = request.Headers["ID"].FirstOrDefault() ?? "unknown";
    var screenId = deviceId.ToLowerInvariant();

    screens.GetOrAdd(screenId, static key => new ScreenInfo(
        key,
        $"Screen {key}",
        null,
        DateTimeOffset.UtcNow,
        null
    ));

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
// POST /api/logs (compatibility alias)
static IResult LogDeviceTelemetry(LogRequest logRequest, ILogger<Program> logger)
{
    foreach (var entry in logRequest.logs)
    {
        logger.LogInformation("Device telemetry: FW {FirmwareVersion} | Battery {BatteryVoltage}V | WiFi {WiFiSignal}dBm | Heap {FreeHeap}B | {Message}",
            entry.firmware_version, entry.battery_voltage, entry.wifi_signal, entry.free_heap_size, entry.message);
    }

    return Results.NoContent();
}

app.MapPost("/api/log", LogDeviceTelemetry);
app.MapPost("/api/logs", LogDeviceTelemetry);

// ---- Firmware: Display ----
// GET /api/display
app.MapGet("/api/display", (HttpRequest request, ILogger<Program> logger) =>
{
    var deviceId = request.Headers["ID"].FirstOrDefault() ?? "unknown";
    var screenId = deviceId.ToLowerInvariant();
    var refreshHeader = request.Headers["REFRESH_RATE"].FirstOrDefault();
    var refreshRate = int.TryParse(refreshHeader, out var r) && r > 0 ? r : DefaultRefreshRate;

    var screen = screens.GetOrAdd(screenId, static key => new ScreenInfo(
        key,
        $"Screen {key}",
        null,
        DateTimeOffset.MinValue,
        null
    ));

    var imagePath = screen.ImagePath ?? $"/screens/{screenId}.jpg";
    var filename = Path.GetFileName(imagePath);

    var absoluteImageUrl = UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase, imagePath);
    var absoluteFirmwareUrl = UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase, "/firmware/latest.bin");

    logger.LogInformation("Display poll: {DeviceId} | Image: {Filename} | Refresh: {RefreshRate}ms",
        deviceId, filename, refreshRate);

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
app.MapPost("/api/screens/{id}/image", async Task<IResult> (string id, HttpRequest request, ILogger<Program> logger) =>
{
    var normalizedId = id.ToLowerInvariant();

    if (request.ContentLength is long contentLength && contentLength > maxImageBytes)
    {
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
    }

    var contentType = request.ContentType ?? MediaTypeNames.Image.Jpeg;
    var mediaType = contentType.Split(';', 2, StringSplitOptions.TrimEntries)[0];
    if (!mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest("Content-Type must be image/*");
    }

    var ext = mediaType.ToLowerInvariant() switch
    {
        "image/png" => ".png",
        "image/jpg" => ".jpg",
        "image/jpeg" => ".jpg",
        _ => ".jpg"
    };

    // Read body into memory to compute SHA256 content hash
    using var ms = new MemoryStream();
    await request.Body.CopyToAsync(ms);
    var imageBytes = ms.ToArray();

    if (imageBytes.Length > maxImageBytes)
    {
        return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
    }

    var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(imageBytes)).ToLowerInvariant();
    var newImagePath = $"/screens/{hash}{ext}";
    var newFilePath = Path.Combine(dataRoot, $"{hash}{ext}");

    await File.WriteAllBytesAsync(newFilePath, imageBytes);

    screens.TryGetValue(normalizedId, out var existingScreen);

    var staleImagePaths = new List<string>();
    lock (imageTrackingLock)
    {
        var history = imageHistoryByDevice.GetOrAdd(normalizedId, static _ => []);
        // Re-uploads of the same bytes keep the same hash filename, so only track
        // transitions to a different image path for retention cleanup.
        if (history.Count == 0 || !string.Equals(history[^1], newImagePath, StringComparison.Ordinal))
        {
            history.Add(newImagePath);
            imageUsageCounts.AddOrUpdate(newImagePath, 1, static (_, count) => count + 1);
        }

        while (history.Count > maxImagesPerDevice)
        {
            var removedImagePath = history[0];
            history.RemoveAt(0);

            if (!imageUsageCounts.TryGetValue(removedImagePath, out var usageCount))
            {
                logger.LogWarning("Image usage count missing for path {ImagePath}", removedImagePath);
                continue;
            }

            var remainingUsage = usageCount - 1;
            if (remainingUsage > 0)
            {
                imageUsageCounts[removedImagePath] = remainingUsage;
            }
            else
            {
                imageUsageCounts.TryRemove(removedImagePath, out _);
                staleImagePaths.Add(removedImagePath);
            }
        }
    }

    foreach (var staleImagePath in staleImagePaths)
    {
        var staleFilePath = Path.Combine(dataRoot, Path.GetFileName(staleImagePath));
        if (File.Exists(staleFilePath))
        {
            File.Delete(staleFilePath);
        }
    }

    var screen = existingScreen != null
        ? existingScreen with { LastUpdated = DateTimeOffset.UtcNow, ImagePath = newImagePath }
        : new ScreenInfo(normalizedId, $"Screen {normalizedId}", null, DateTimeOffset.UtcNow, newImagePath);

    screens[normalizedId] = screen;

    logger.LogInformation("Image uploaded: {ScreenId} | Type: {ContentType} | Hash: {Hash}", normalizedId, contentType, hash[..8]);

    var result = new { id = normalizedId, path = screen.ImagePath! };
    return Results.Ok((object)result);
});

// ---- BYOD: serve JPEG image ----
// GET /screens/{id}.jpg
app.MapGet("/screens/{id}.jpg", (string id, ILogger<Program> logger) =>
{
    // Normalize to lowercase for consistent lookup (matches upload behavior)
    var normalizedId = id.ToLowerInvariant();
    var jpgPath = Path.Combine(dataRoot, $"{normalizedId}.jpg");

    if (File.Exists(jpgPath))
    {
        logger.LogInformation("Serving image: {ScreenId} (JPEG)", normalizedId);
        return Results.File(jpgPath, "image/jpeg");
    }

    logger.LogInformation("Image not found: {ScreenId}", normalizedId);
    return Results.NotFound();
});

// ---- BYOD: serve PNG image ----
// GET /screens/{id}.png
app.MapGet("/screens/{id}.png", (string id, ILogger<Program> logger) =>
{
    // Normalize to lowercase for consistent lookup (matches upload behavior)
    var normalizedId = id.ToLowerInvariant();
    var pngPath = Path.Combine(dataRoot, $"{normalizedId}.png");

    if (File.Exists(pngPath))
    {
        logger.LogInformation("Serving image: {ScreenId} (PNG)", normalizedId);
        return Results.File(pngPath, "image/png");
    }

    logger.LogInformation("Image not found: {ScreenId}", normalizedId);
    return Results.NotFound();
});

// Health
app.MapGet("/", () => Results.Ok(new { status = "ok", service = "trmnl-byod-dotnet" }));

app.Run();

public partial class Program { }
