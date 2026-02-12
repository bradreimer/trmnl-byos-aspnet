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
    var model = request.Headers["MODEL"].FirstOrDefault() ?? "byod";
    var firmware = request.Headers["FIRMWARE"].FirstOrDefault();
    var refreshHeader = request.Headers["REFRESH_RATE"].FirstOrDefault();
    var refreshRate = int.TryParse(refreshHeader, out var r) ? r : 100;

    var screenId = deviceId.ToLowerInvariant();

    if (!screens.TryGetValue(screenId, out var screen))
    {
        screen = new ScreenInfo(
            screenId,
            $"Screen {screenId}",
            $"Model {model}, Firmware {firmware ?? "unknown"}",
            DateTimeOffset.UtcNow,
            null
        );
        screens[screenId] = screen;
    }

    var response = new SetupResponse(
        DeviceId: deviceId,
        ScreenId: screenId,
        Model: model,
        RefreshRate: refreshRate,
        Success: "Device setup successful."
    );

    return Results.Ok(response);
});

// ---- Firmware: Log ----
// POST /api/logs
app.MapPost("/api/logs", async (LogEntry entry) =>
{
    Console.WriteLine($"[TRMNL LOG] {entry.Timestamp:u} [{entry.Level}] {entry.Message}");
    if (entry.Context is not null)
    {
        foreach (var kv in entry.Context)
        {
            Console.WriteLine($"  {kv.Key}: {kv.Value}");
        }
    }
    return Results.NoContent();
});

// ---- Firmware: Display ----
// GET /api/display
app.MapGet("/api/display", (HttpRequest request) =>
{
    var deviceId = request.Headers["ID"].FirstOrDefault() ?? "unknown";
    var screenId = deviceId.ToLowerInvariant();

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
    var updatedAt = screen.LastUpdated == DateTimeOffset.MinValue
        ? DateTimeOffset.UtcNow
        : screen.LastUpdated;

    var refreshHeader = request.Headers["REFRESH_RATE"].FirstOrDefault();
    var refreshRate = int.TryParse(refreshHeader, out var r) ? r : 100;

    var response = new DisplayResponse(
        ScreenId: screenId,
        ImageUrl: imagePath,
        UpdatedAt: updatedAt,
        RefreshRate: refreshRate
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
