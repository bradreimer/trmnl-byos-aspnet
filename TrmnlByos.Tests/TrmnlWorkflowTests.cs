using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using TrmnlByos.Models;

namespace TrmnlByos.Tests;

[TestClass]
public class TrmnlWorkflowTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private const string TestDeviceId = "AA:BB:CC:DD:EE:FF";

    [TestInitialize]
    public async Task Initialize()
    {
        _factory = new TrmnlWebApplicationFactory();
        _client = _factory.CreateClient();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [TestMethod]
    public async Task Workflow_DeviceSetup_ReturnsValidSetupResponse()
    {
        // Arrange
        _client.DefaultRequestHeaders.Add("ID", TestDeviceId);

        // Act
        var response = await _client.GetAsync("/api/setup");

        // Assert
        Assert.AreEqual(200, (int)response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SetupResponse>();
        Assert.IsNotNull(result);
        Assert.AreEqual(TestDeviceId, result.api_key);
        Assert.IsFalse(string.IsNullOrEmpty(result.friendly_id));
        Assert.IsFalse(string.IsNullOrEmpty(result.image_url));
        Assert.IsTrue(result.message.Contains("TRMNL"));
    }

    [TestMethod]
    public async Task Workflow_DisplayPoll_ReturnsValidDisplayResponse()
    {
        // Arrange
        _client.DefaultRequestHeaders.Add("ID", TestDeviceId);
        _client.DefaultRequestHeaders.Add("REFRESH_RATE", "100");

        // Act
        var response = await _client.GetAsync("/api/display");

        // Assert
        Assert.AreEqual(200, (int)response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DisplayResponse>();
        Assert.IsNotNull(result);
        Assert.IsFalse(string.IsNullOrEmpty(result.filename));
        Assert.IsTrue(result.image_url.Contains(TestDeviceId.ToLowerInvariant()));
        Assert.IsTrue(result.firmware_url.StartsWith("http"));
        Assert.AreEqual(100, result.refresh_rate);
        Assert.IsFalse(result.reset_firmware);
        Assert.IsFalse(result.update_firmware);
    }

    [TestMethod]
    public async Task Workflow_DeviceLogsData_Returns204()
    {
        // Arrange
        _client.DefaultRequestHeaders.Add("ID", TestDeviceId);
        var logRequest = new LogRequest(new[]
        {
            new LogEntry(
                id: 1,
                message: "Test log entry",
                wifi_status: "connected",
                created_at: DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                sleep_duration: 30,
                refresh_rate: 100,
                free_heap_size: 165000,
                max_alloc_size: 180000,
                source_path: "src/main.cpp",
                wake_reason: "timer",
                firmware_version: "1.5.2",
                retry: 1,
                battery_voltage: 3.8f,
                source_line: 100,
                special_function: "none",
                wifi_signal: -65
            )
        });

        // Act
        var response = await _client.PostAsJsonAsync("/api/log", logRequest);

        // Assert
        Assert.AreEqual(204, (int)response.StatusCode);
    }

    [TestMethod]
    public async Task Workflow_UploadImage_ReturnsImagePath()
    {
        // Arrange
        var imageContent = CreateTestImage();
        var content = new ByteArrayContent(imageContent);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

        // Act
        var response = await _client.PostAsync($"/api/screens/{TestDeviceId}/image", content);

        // Assert
        Assert.AreEqual(200, (int)response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.IsNotNull(result);
        Assert.AreEqual(TestDeviceId.ToLowerInvariant(), result["id"].ToString());
        Assert.IsTrue(result["path"].ToString()!.Contains(TestDeviceId.ToLowerInvariant()));
    }

    [TestMethod]
    public async Task Workflow_ServeUploadedImage_Returns200()
    {
        // Arrange - Upload an image first
        var imageContent = CreateTestImage();
        var uploadContent = new ByteArrayContent(imageContent);
        uploadContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

        await _client.PostAsync($"/api/screens/{TestDeviceId}/image", uploadContent);

        // Act - Fetch the image
        var response = await _client.GetAsync($"/screens/{TestDeviceId}.jpg");

        // Assert
        Assert.AreEqual(200, (int)response.StatusCode);
        Assert.AreEqual("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        var downloadedImage = await response.Content.ReadAsByteArrayAsync();
        Assert.IsTrue(downloadedImage.Length > 0);
    }

    [TestMethod]
    public async Task Workflow_ServeImageWithWrongFormat_Returns404()
    {
        // Arrange - Upload a JPEG
        var imageContent = CreateTestImage();
        var uploadContent = new ByteArrayContent(imageContent);
        uploadContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

        await _client.PostAsync($"/api/screens/{TestDeviceId}/image", uploadContent);

        // Act - Try to fetch as PNG
        var response = await _client.GetAsync($"/screens/{TestDeviceId}.png");

        // Assert
        Assert.AreEqual(404, (int)response.StatusCode);
    }

    [TestMethod]
    public async Task Workflow_DisplayReturnsAbsoluteUrls()
    {
        // Arrange
        _client.DefaultRequestHeaders.Add("ID", TestDeviceId);

        // Upload an image first
        var imageContent = CreateTestImage();
        var uploadContent = new ByteArrayContent(imageContent);
        uploadContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        await _client.PostAsync($"/api/screens/{TestDeviceId}/image", uploadContent);

        // Act
        var response = await _client.GetAsync("/api/display");
        var result = await response.Content.ReadFromJsonAsync<DisplayResponse>();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.image_url.StartsWith("http://"));
        Assert.IsTrue(result.firmware_url.StartsWith("http://"));
        Assert.IsTrue(result.image_url.Contains("/screens/"));
        Assert.IsTrue(result.firmware_url.Contains("/firmware/"));
    }

    [TestMethod]
    public async Task Workflow_HealthCheck_Returns200()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        Assert.AreEqual(200, (int)response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.IsNotNull(result);
        Assert.AreEqual("ok", result["status"].ToString());
        Assert.IsTrue(result["service"].ToString()!.Contains("trmnl"));
    }

    /// <summary>
    /// Returns a minimal valid JPEG bytes for testing
    /// </summary>
    private byte[] CreateTestImage()
    {
        // Minimal valid JPEG (1x1 pixel)
        return new byte[]
        {
            0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01,
            0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xDB, 0x00, 0x43,
            0x00, 0x08, 0x06, 0x06, 0x07, 0x06, 0x05, 0x08, 0x07, 0x07, 0x07, 0x09,
            0x09, 0x08, 0x0A, 0x0C, 0x14, 0x0D, 0x0C, 0x0B, 0x0B, 0x0C, 0x19, 0x12,
            0x13, 0x0F, 0x14, 0x1D, 0x1A, 0x1F, 0x1E, 0x1D, 0x1A, 0x1C, 0x1C, 0x20,
            0x24, 0x2E, 0x27, 0x20, 0x22, 0x2C, 0x23, 0x1C, 0x1C, 0x28, 0x37, 0x29,
            0x2C, 0x30, 0x31, 0x34, 0x34, 0x34, 0x1F, 0x27, 0x39, 0x3D, 0x38, 0x32,
            0x3C, 0x2E, 0x33, 0x34, 0x32, 0xFF, 0xC0, 0x00, 0x0B, 0x08, 0x00, 0x01,
            0x00, 0x01, 0x01, 0x01, 0x11, 0x00, 0xFF, 0xC4, 0x00, 0x1F, 0x00, 0x00,
            0x01, 0x05, 0x01, 0x01, 0x01, 0x01, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08,
            0x09, 0x0A, 0x0B, 0xFF, 0xC4, 0x00, 0xB5, 0x10, 0x00, 0x02, 0x01, 0x03,
            0x03, 0x02, 0x04, 0x03, 0x05, 0x05, 0x04, 0x04, 0x00, 0x00, 0x01, 0x7D,
            0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06,
            0x13, 0x51, 0x61, 0x07, 0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xA1, 0x08,
            0x23, 0x42, 0xB1, 0xC1, 0x15, 0x52, 0xD1, 0xF0, 0x24, 0x33, 0x62, 0x72,
            0x82, 0x09, 0x0A, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x25, 0x26, 0x27, 0x28,
            0x29, 0x2A, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x43, 0x44, 0x45,
            0x46, 0x47, 0x48, 0x49, 0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59,
            0x5A, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69, 0x6A, 0x73, 0x74, 0x75,
            0x76, 0x77, 0x78, 0x79, 0x7A, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
            0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3,
            0xA4, 0xA5, 0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6,
            0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3, 0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9,
            0xCA, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA, 0xE1, 0xE2,
            0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xF1, 0xF2, 0xF3, 0xF4,
            0xF5, 0xF6, 0xF7, 0xF8, 0xF9, 0xFA, 0xFF, 0xDA, 0x00, 0x08, 0x01, 0x01,
            0x00, 0x00, 0x3F, 0x00, 0xFB, 0xD0, 0xFF, 0xD9
        };
    }
}
