using System.Collections.Concurrent;
using System.Net;
using System.Net.Mime;
using System.Text;
using Microsoft.AspNetCore.Http.Extensions;
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
var serviceStartTime = DateTimeOffset.UtcNow;

// simple thread-safe in-memory store
var screens = new ConcurrentDictionary<string, ScreenInfo>(StringComparer.OrdinalIgnoreCase);
var imageHistoryByDevice = new ConcurrentDictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
var imageUsageCounts = new ConcurrentDictionary<string, int>(StringComparer.OrdinalIgnoreCase);
var imageTrackingLock = new object();

static string? ReadHeader(HttpRequest request, string name)
{
    var value = request.Headers[name].FirstOrDefault();
    return string.IsNullOrWhiteSpace(value) ? null : value;
}

static string? PreferValue(string? incomingValue, string? existingValue)
{
    return string.IsNullOrWhiteSpace(incomingValue) ? existingValue : incomingValue;
}

static int? ParsePositiveIntOrNull(string? value)
{
    return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : null;
}

static string FormatRelativeTime(DateTimeOffset value, DateTimeOffset nowUtc)
{
    if (value == DateTimeOffset.MinValue)
    {
        return "never";
    }

    var elapsed = nowUtc - value;
    if (elapsed < TimeSpan.Zero)
    {
        elapsed = TimeSpan.Zero;
    }

    if (elapsed < TimeSpan.FromMinutes(1))
    {
        return "just now";
    }

    if (elapsed < TimeSpan.FromHours(1))
    {
        return $"{(int)elapsed.TotalMinutes}m ago";
    }

    if (elapsed < TimeSpan.FromDays(1))
    {
        return $"{(int)elapsed.TotalHours}h ago";
    }

    return $"{(int)elapsed.TotalDays}d ago";
}

static string FormatTimestampWithRelative(DateTimeOffset value, DateTimeOffset nowUtc)
{
    if (value == DateTimeOffset.MinValue)
    {
        return "never";
    }

    var utcText = value.ToString("u");
    var relativeText = FormatRelativeTime(value, nowUtc);
    return $"{utcText} ({relativeText})";
}

static string BuildLandingPage(IEnumerable<ScreenInfo> activeScreens, DateTimeOffset startedAtUtc, DateTimeOffset nowUtc)
{
    var uptime = nowUtc - startedAtUtc;
    var sb = new StringBuilder();

    sb.Append("""
<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width,initial-scale=1" />
  <title>TRMNL BYOS Service</title>
  <style>
    :root{color-scheme:light dark;--bg:#0b1020;--panel:#141b34;--panel2:#1b2547;--text:#e8ecff;--sub:#9fb0e3;--accent:#6aa8ff;--line:#2d3a67}
    *{box-sizing:border-box} body{margin:0;font-family:Inter,Segoe UI,system-ui,-apple-system,sans-serif;background:radial-gradient(circle at top,#1a2450 0,#0b1020 45%,#070b17 100%);color:var(--text)}
    main{max-width:1100px;margin:0 auto;padding:24px 18px 48px}
    h1{margin:0 0 8px;font-size:1.8rem} h2{margin:0 0 12px;font-size:1.15rem} h3{margin:0 0 8px;font-size:1rem}
    .subtitle{color:var(--sub);margin:0 0 20px}
    .grid{display:grid;gap:14px;grid-template-columns:repeat(auto-fit,minmax(260px,1fr))}
    .card{background:linear-gradient(180deg,var(--panel),var(--panel2));border:1px solid var(--line);border-radius:12px;padding:14px}
    .meta{display:grid;grid-template-columns:max-content 1fr;gap:6px 12px;font-size:.92rem}
    .k{color:var(--sub)} .v{word-break:break-word}
    .links a{color:var(--accent);text-decoration:none} .links a:hover{text-decoration:underline}
    .device-list{display:grid;gap:12px}
    .empty{color:var(--sub);padding:8px 2px}
    .screen{margin-top:10px}
    .screen img{max-width:100%;height:auto;display:block;border-radius:8px;border:1px solid var(--line);background:#000}
    code{background:#0f1630;padding:2px 6px;border-radius:6px}
  </style>
</head>
<body>
  <main>
    <h1>TRMNL BYOS Server</h1>
    <p class="subtitle">Self-hosted firmware-compatible endpoint for TRMNL devices.</p>
    <section class="grid">
      <article class="card">
        <h2>About TRMNL BYOS</h2>
        <div class="links meta">
          <div class="k">Website</div><div class="v"><a href="https://usetrmnl.com/" target="_blank" rel="noopener noreferrer">usetrmnl.com</a></div>
          <div class="k">Docs</div><div class="v"><a href="https://docs.usetrmnl.com/" target="_blank" rel="noopener noreferrer">docs.usetrmnl.com</a></div>
          <div class="k">BYOS API spec</div><div class="v"><a href="https://github.com/usetrmnl/byos_hanami/blob/main/doc/api.adoc" target="_blank" rel="noopener noreferrer">GitHub API reference</a></div>
          <div class="k">This project</div><div class="v"><a href="https://github.com/bradreimer/trmnl-byos-aspnet" target="_blank" rel="noopener noreferrer">bradreimer/trmnl-byos-aspnet</a></div>
        </div>
      </article>
      <article class="card">
        <h2>Service details</h2>
        <div class="meta">
          <div class="k">Service</div><div class="v">trmnl-byod-dotnet</div>
          <div class="k">Started (UTC)</div><div class="v">
""");
    sb.Append(WebUtility.HtmlEncode(startedAtUtc.ToString("u")));
    sb.Append("""
</div>
          <div class="k">Uptime</div><div class="v">
""");
    sb.Append(WebUtility.HtmlEncode($"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s"));
    sb.Append("""
</div>
          <div class="k">Active devices</div><div class="v">
""");
    sb.Append(activeScreens.Count());
    sb.Append("""
</div>
        </div>
      </article>
    </section>
    <section class="card" style="margin-top:14px">
      <h2>Active TRMNL devices</h2>
      <div class="device-list">
""");

    foreach (var screen in activeScreens.OrderByDescending(s => s.LastSeen))
    {
        sb.Append("""
        <article class="card">
          <h3>
""");
        sb.Append(WebUtility.HtmlEncode(screen.Name));
        sb.Append("""
</h3>
          <div class="meta">
            <div class="k">Device ID</div><div class="v"><code>
""");
        sb.Append(WebUtility.HtmlEncode(screen.DeviceId));
        sb.Append("""
</code></div>
            <div class="k">Screen ID</div><div class="v"><code>
""");
        sb.Append(WebUtility.HtmlEncode(screen.Id));
        sb.Append("""
</code></div>
            <div class="k">Model</div><div class="v">
""");
        sb.Append(WebUtility.HtmlEncode(screen.Model ?? "unknown"));
        sb.Append("""
</div>
            <div class="k">Firmware</div><div class="v">
""");
        sb.Append(WebUtility.HtmlEncode(screen.Firmware ?? "unknown"));
        sb.Append("""
</div>
            <div class="k">Refresh rate</div><div class="v">
""");
        sb.Append(WebUtility.HtmlEncode(screen.RefreshRate is > 0 ? $"{screen.RefreshRate} ms" : "unknown"));
        sb.Append("""
</div>
            <div class="k">Last seen (UTC)</div><div class="v">
""");
        sb.Append(WebUtility.HtmlEncode(screen.LastSeen.ToString("u")));
        sb.Append("</div>\n            <div class=\"k\">Last screen fetched (UTC)</div><div class=\"v\">\n");
        sb.Append(WebUtility.HtmlEncode(FormatTimestampWithRelative(screen.LastScreenFetched, nowUtc)));
        sb.Append("\n</div>\n            <div class=\"k\">Last screen updated (UTC)</div><div class=\"v\">\n");
        sb.Append(WebUtility.HtmlEncode(FormatTimestampWithRelative(screen.LastUpdated, nowUtc)));
        sb.Append("""
</div>
            <div class="k">Latest screen path</div><div class="v">
""");
        sb.Append(WebUtility.HtmlEncode(screen.ImagePath ?? "not uploaded yet"));
        sb.Append("""
</div>
          </div>
""");

        if (!string.IsNullOrWhiteSpace(screen.ImagePath))
        {
            sb.Append("""
          <div class="screen">
            <img loading="lazy" alt="Latest screen for 
""");
            sb.Append(WebUtility.HtmlEncode(screen.DeviceId));
            sb.Append("""
" src="
""");
            sb.Append(WebUtility.HtmlEncode(screen.ImagePath));
            sb.Append("""
" />
          </div>
""");
        }

        sb.Append("""
        </article>
""");
    }

    if (!activeScreens.Any())
    {
        sb.Append("""
        <p class="empty">No active devices have checked in yet.</p>
""");
    }

    sb.Append("""
      </div>
    </section>
  </main>
</body>
</html>
""");

    return sb.ToString();
}

// ---- Firmware: Setup ----
// GET /api/setup
// Headers: ID (device id), optional: MODEL, FIRMWARE, REFRESH_RATE
app.MapGet("/api/setup", (HttpRequest request, ILogger<Program> logger) =>
{
    var deviceId = request.Headers["ID"].FirstOrDefault() ?? "unknown";
    var screenId = deviceId.ToLowerInvariant();
    var model = ReadHeader(request, "MODEL");
    var firmware = ReadHeader(request, "FIRMWARE");
    var refreshRate = ParsePositiveIntOrNull(ReadHeader(request, "REFRESH_RATE")) ?? DefaultRefreshRate;
    var now = DateTimeOffset.UtcNow;

    var screen = screens.AddOrUpdate(
        screenId,
        static (key, state) => new ScreenInfo(
            Id: key,
            Name: $"Screen {key}",
            Description: null,
            LastUpdated: DateTimeOffset.MinValue,
            LastScreenFetched: DateTimeOffset.MinValue,
            ImagePath: null,
            DeviceId: state.DeviceId,
            Model: state.Model,
            Firmware: state.Firmware,
            RefreshRate: state.RefreshRate,
            LastSeen: state.Now
        ),
        static (_, existing, state) => existing with
        {
            LastSeen = state.Now,
            DeviceId = state.DeviceId,
            Model = PreferValue(state.Model, existing.Model),
            Firmware = PreferValue(state.Firmware, existing.Firmware),
            RefreshRate = state.RefreshRate
        },
        (Now: now, DeviceId: deviceId, Model: model, Firmware: firmware, RefreshRate: refreshRate)
    );

    logger.LogInformation("Device setup: {DeviceId}", deviceId);

    var response = new SetupResponse(
        api_key: deviceId,
        friendly_id: screenId.ToUpperInvariant(),
        image_url: screen.ImagePath ?? $"/screens/{screenId}.jpg",
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
    var refreshRate = ParsePositiveIntOrNull(ReadHeader(request, "REFRESH_RATE")) ?? DefaultRefreshRate;
    var model = ReadHeader(request, "MODEL");
    var firmware = ReadHeader(request, "FIRMWARE");
    var now = DateTimeOffset.UtcNow;

    var screen = screens.AddOrUpdate(
        screenId,
        static (key, state) => new ScreenInfo(
            Id: key,
            Name: $"Screen {key}",
            Description: null,
            LastUpdated: DateTimeOffset.MinValue,
            LastScreenFetched: state.Now,
            ImagePath: null,
            DeviceId: state.DeviceId,
            Model: state.Model,
            Firmware: state.Firmware,
            RefreshRate: state.RefreshRate,
            LastSeen: state.Now
        ),
        static (_, existing, state) => existing with
        {
            LastScreenFetched = state.Now,
            LastSeen = state.Now,
            DeviceId = state.DeviceId,
            Model = PreferValue(state.Model, existing.Model),
            Firmware = PreferValue(state.Firmware, existing.Firmware),
            RefreshRate = state.RefreshRate
        },
        (Now: now, DeviceId: deviceId, Model: model, Firmware: firmware, RefreshRate: refreshRate)
    );

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

    if (!File.Exists(newFilePath))
    {
        await File.WriteAllBytesAsync(newFilePath, imageBytes);
    }

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
        try
        {
            if (File.Exists(staleFilePath))
            {
                File.Delete(staleFilePath);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete stale image file {FilePath}", staleFilePath);
        }
    }

    var now = DateTimeOffset.UtcNow;
    var screen = existingScreen != null
        ? existingScreen with { LastUpdated = now, ImagePath = newImagePath, LastSeen = now }
        : new ScreenInfo(
            Id: normalizedId,
            Name: $"Screen {normalizedId}",
            Description: null,
            LastUpdated: now,
            LastScreenFetched: DateTimeOffset.MinValue,
            ImagePath: newImagePath,
            DeviceId: id,
            Model: null,
            Firmware: null,
            RefreshRate: null,
            LastSeen: now
        );

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
app.MapGet("/", () =>
{
    var now = DateTimeOffset.UtcNow;
    var page = BuildLandingPage(screens.Values, serviceStartTime, now);
    return Results.Content(page, "text/html; charset=utf-8");
});
app.MapGet("/health", () => Results.Ok(new { status = "ok", service = "trmnl-byod-dotnet" }));

app.Run();

public partial class Program { }
