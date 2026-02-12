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
    logger.LogInformation("{Method} {Path}", request.Method, request.Path);
    
    await next(context);
    
    logger.LogInformation("{Method} {Path} -> {StatusCode}", request.Method, request.Path, context.Response.StatusCode);
});

app.UseSwagger();
app.UseSwaggerUI();

var dataRoot = "/data";
Directory.CreateDirectory(dataRoot);

// simple in-memory store
var screens = new Dictionary<string, ScreenInfo>(StringComparer.OrdinalIgnoreCase);

// ---- Firmware: Setup ----
// GET /api/setup
// Headers: ID (device id), optional: MODEL, FIRMWARE, REFRESH_RATE
app.MapGet("/api/setup", (HttpRequest request) =>
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

    var response = new SetupResponse(
        api_key: deviceId,
        friendly_id: screenId.ToUpper(),
        image_url: $"/screens/{screenId}.jpg",
        message: "Welcome to TRMNL BYOS"
    );

    return Results.Ok(response);
});

// ---- Firmware: Log ----
// POST /api/logs
app.MapPost("/api/logs", async (LogRequest logRequest) =>
{
    foreach (var entry in logRequest.logs)
    {
        Console.WriteLine($"[TRMNL LOG] {entry.message}");
        Console.WriteLine($"  Firmware: {entry.firmware_version}, Battery: {entry.battery_voltage}V, WiFi: {entry.wifi_signal}");
    }
    return Results.NoContent();
});

// ---- Firmware: Display ----
// GET /api/display
app.MapGet("/api/display", (HttpRequest request) =>
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

    var response = new DisplayResponse(
        filename: filename,
        firmware_url: "http://localhost:2300/firmware/latest.bin",
        firmware_version: "1.0.0",
        image_url: imagePath,
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
app.MapPost("/api/screens/{id}/image", async Task<Results<Ok<object>, BadRequest<string>>> (string id, HttpRequest request) =>
{
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

    var filePath = Path.Combine(dataRoot, $"{id}{ext}");

    await using (var fs = File.Create(filePath))
    {
        await request.Body.CopyToAsync(fs);
    }

    var screen = screens.TryGetValue(id, out var existing)
        ? existing with { LastUpdated = DateTimeOffset.UtcNow, ImagePath = $"/screens/{id}{ext}" }
        : new ScreenInfo(id, $"Screen {id}", null, DateTimeOffset.UtcNow, $"/screens/{id}{ext}");

    screens[id] = screen;

    var result = new { id, path = screen.ImagePath! };
    return TypedResults.Ok((object)result);
});

// ---- BYOD: serve image ----
// GET /screens/{id}.jpg
app.MapGet("/screens/{id}.jpg", (string id) =>
{
    var jpgPath = Path.Combine(dataRoot, $"{id}.jpg");
    var pngPath = Path.Combine(dataRoot, $"{id}.png");

    if (File.Exists(jpgPath))
    {
        return Results.File(jpgPath, "image/jpeg");
    }

    if (File.Exists(pngPath))
    {
        return Results.File(pngPath, "image/png");
    }

    return Results.NotFound();
});

// Health
app.MapGet("/", () => Results.Ok(new { status = "ok", service = "trmnl-byod-dotnet" }));

app.Run();
