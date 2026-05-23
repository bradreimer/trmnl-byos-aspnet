using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TrmnlByos.Models;

namespace TrmnlByos.Tests;

[TestClass]
public class TrmnlWorkflowTests
{
    private WebApplicationFactory<Program> m_factory = null!;
    private HttpClient m_client = null!;
    private const string s_TestDeviceId = "AA:BB:CC:DD:EE:FF";

    [TestInitialize]
    public async Task Initialize()
    {
        m_factory = new TrmnlWebApplicationFactory();
        m_client = m_factory.CreateClient();
    }

    [TestCleanup]
    public async Task Cleanup()
    {
        m_client.Dispose();
        await m_factory.DisposeAsync();
    }

    [TestMethod]
    public async Task Workflow_DeviceSetup_ReturnsValidSetupResponse()
    {
        // Arrange
        m_client.DefaultRequestHeaders.Add("ID", s_TestDeviceId);

        // Act
        var response = await m_client.GetAsync("/api/setup");

        // Assert
        Assert.AreEqual(200, (int)response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SetupResponse>();
        Assert.IsNotNull(result);
        Assert.AreEqual(s_TestDeviceId, result.api_key);
        Assert.IsFalse(string.IsNullOrEmpty(result.friendly_id));
        Assert.IsFalse(string.IsNullOrEmpty(result.image_url));
        Assert.IsTrue(result.message.Contains("TRMNL"));
    }

    [TestMethod]
    public async Task Workflow_DisplayPoll_ReturnsValidDisplayResponse()
    {
        // Arrange
        m_client.DefaultRequestHeaders.Add("ID", s_TestDeviceId);
        m_client.DefaultRequestHeaders.Add("REFRESH_RATE", "100");

        // Act
        var response = await m_client.GetAsync("/api/display");

        // Assert
        Assert.AreEqual(200, (int)response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<DisplayResponse>();
        Assert.IsNotNull(result);
        Assert.IsFalse(string.IsNullOrEmpty(result.filename));
        Assert.IsTrue(result.image_url.Contains(s_TestDeviceId.ToLowerInvariant()));
        Assert.IsTrue(result.firmware_url.StartsWith("http"));
        Assert.AreEqual(100, result.refresh_rate);
        Assert.IsFalse(result.reset_firmware);
        Assert.IsFalse(result.update_firmware);
    }

    [TestMethod]
    public async Task Workflow_DeviceLogsData_Returns204()
    {
        // Arrange
        m_client.DefaultRequestHeaders.Add("ID", s_TestDeviceId);
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
        var response = await m_client.PostAsJsonAsync("/api/log", logRequest);

        // Assert
        Assert.AreEqual(204, (int)response.StatusCode);
    }

    [TestMethod]
    public async Task Workflow_DeviceLogsData_AliasEndpoint_Returns204()
    {
        // Arrange
        m_client.DefaultRequestHeaders.Add("ID", s_TestDeviceId);
        var logRequest = new LogRequest(new[]
        {
            new LogEntry(
                id: 2,
                message: "Test log alias endpoint",
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
        var response = await m_client.PostAsJsonAsync("/api/logs", logRequest);

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
        var expectedHash = Convert.ToHexString(SHA256.HashData(imageContent)).ToLowerInvariant();

        // Act
        var response = await m_client.PostAsync($"/api/screens/{s_TestDeviceId}/image", content);

        // Assert
        Assert.AreEqual(200, (int)response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.IsNotNull(result);
        Assert.AreEqual(s_TestDeviceId.ToLowerInvariant(), result["id"].ToString());
        Assert.AreEqual($"/screens/{expectedHash}.jpg", result["path"].ToString());
    }

    [TestMethod]
    public async Task Workflow_ServeUploadedImage_Returns200()
    {
        // Arrange - Upload an image first
        var imageContent = CreateTestImage();
        var uploadContent = new ByteArrayContent(imageContent);
        uploadContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

        var uploadResponse = await m_client.PostAsync($"/api/screens/{s_TestDeviceId}/image", uploadContent);
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.IsNotNull(uploadResult);
        var imagePath = uploadResult["path"].ToString();
        Assert.IsNotNull(imagePath);

        // Act - Fetch the image
        var response = await m_client.GetAsync(imagePath);

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

        await m_client.PostAsync($"/api/screens/{s_TestDeviceId}/image", uploadContent);
        var hash = Convert.ToHexString(SHA256.HashData(imageContent)).ToLowerInvariant();

        // Act - Try to fetch as PNG
        var response = await m_client.GetAsync($"/screens/{hash}.png");

        // Assert
        Assert.AreEqual(404, (int)response.StatusCode);
    }

    [TestMethod]
    public async Task Workflow_UploadPngAndServePng_Returns200()
    {
        // Arrange
        var pngBytes = new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
            0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41,
            0x54, 0x78, 0x9C, 0x63, 0xF8, 0xCF, 0xC0, 0x00,
            0x00, 0x03, 0x01, 0x01, 0x00, 0x18, 0xDD, 0x8D,
            0xB1, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E,
            0x44, 0xAE, 0x42, 0x60, 0x82
        };

        var uploadContent = new ByteArrayContent(pngBytes);
        uploadContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        var hash = Convert.ToHexString(SHA256.HashData(pngBytes)).ToLowerInvariant();

        // Act
        var uploadResponse = await m_client.PostAsync($"/api/screens/{s_TestDeviceId}/image", uploadContent);
        var response = await m_client.GetAsync($"/screens/{hash}.png");

        // Assert
        Assert.AreEqual(200, (int)uploadResponse.StatusCode);
        Assert.AreEqual(200, (int)response.StatusCode);
        Assert.AreEqual("image/png", response.Content.Headers.ContentType?.MediaType);
    }

    [TestMethod]
    public async Task Workflow_UploadInvalidContentType_Returns400()
    {
        // Arrange
        var content = new ByteArrayContent(new byte[] { 0x01, 0x02, 0x03 });
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");

        // Act
        var response = await m_client.PostAsync($"/api/screens/{s_TestDeviceId}/image", content);

        // Assert
        Assert.AreEqual(400, (int)response.StatusCode);
    }

    [TestMethod]
    public async Task Workflow_UploadTooLarge_Returns413()
    {
        // Arrange
        var tooLargeBytes = new byte[11 * 1024 * 1024];
        var content = new ByteArrayContent(tooLargeBytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

        // Act
        var response = await m_client.PostAsync($"/api/screens/{s_TestDeviceId}/image", content);

        // Assert
        Assert.AreEqual(413, (int)response.StatusCode);
    }

    [TestMethod]
    public async Task Workflow_DisplayReturnsLatestHashAfterImageChanges()
    {
        // Arrange
        m_client.DefaultRequestHeaders.Add("ID", s_TestDeviceId);

        var firstImage = CreateTestImage();
        var firstUpload = new ByteArrayContent(firstImage);
        firstUpload.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

        // Act - upload first image and verify display points to first hash
        var firstUploadResponse = await m_client.PostAsync($"/api/screens/{s_TestDeviceId}/image", firstUpload);
        Assert.AreEqual(200, (int)firstUploadResponse.StatusCode);
        var firstDisplayResponse = await m_client.GetAsync("/api/display");
        var firstDisplay = await firstDisplayResponse.Content.ReadFromJsonAsync<DisplayResponse>();

        // Upload a different image and verify display updates to new hash
        var secondImage = CreateDifferentTestImage();
        var secondUpload = new ByteArrayContent(secondImage);
        secondUpload.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        var secondUploadResponse = await m_client.PostAsync($"/api/screens/{s_TestDeviceId}/image", secondUpload);
        Assert.AreEqual(200, (int)secondUploadResponse.StatusCode);
        var secondDisplayResponse = await m_client.GetAsync("/api/display");
        var secondDisplay = await secondDisplayResponse.Content.ReadFromJsonAsync<DisplayResponse>();

        // Assert
        Assert.IsNotNull(firstDisplay);
        Assert.IsNotNull(secondDisplay);

        var firstHash = Convert.ToHexString(SHA256.HashData(firstImage)).ToLowerInvariant();
        var secondHash = Convert.ToHexString(SHA256.HashData(secondImage)).ToLowerInvariant();

        Assert.AreEqual($"{firstHash}.jpg", firstDisplay.filename);
        Assert.AreEqual($"{secondHash}.jpg", secondDisplay.filename);
        Assert.AreNotEqual(firstDisplay.filename, secondDisplay.filename);
        Assert.IsTrue(secondDisplay.image_url.EndsWith($"/screens/{secondHash}.jpg"));
    }

    [TestMethod]
    public async Task Workflow_ReuploadSameImage_KeepsSameHashFilename()
    {
        // Arrange
        m_client.DefaultRequestHeaders.Add("ID", s_TestDeviceId);
        var image = CreateTestImage();
        var expectedHash = Convert.ToHexString(SHA256.HashData(image)).ToLowerInvariant();

        // Act - first upload and display poll
        var firstUpload = new ByteArrayContent(image);
        firstUpload.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        var firstUploadResponse = await m_client.PostAsync($"/api/screens/{s_TestDeviceId}/image", firstUpload);
        Assert.AreEqual(200, (int)firstUploadResponse.StatusCode);

        var firstDisplayResponse = await m_client.GetAsync("/api/display");
        Assert.AreEqual(200, (int)firstDisplayResponse.StatusCode);
        var firstDisplay = await firstDisplayResponse.Content.ReadFromJsonAsync<DisplayResponse>();

        // Re-upload identical content and poll display again
        var secondUpload = new ByteArrayContent(image);
        secondUpload.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        var secondUploadResponse = await m_client.PostAsync($"/api/screens/{s_TestDeviceId}/image", secondUpload);
        Assert.AreEqual(200, (int)secondUploadResponse.StatusCode);

        var secondDisplayResponse = await m_client.GetAsync("/api/display");
        Assert.AreEqual(200, (int)secondDisplayResponse.StatusCode);
        var secondDisplay = await secondDisplayResponse.Content.ReadFromJsonAsync<DisplayResponse>();

        // Assert
        Assert.IsNotNull(firstDisplay);
        Assert.IsNotNull(secondDisplay);
        Assert.AreEqual($"{expectedHash}.jpg", firstDisplay.filename);
        Assert.AreEqual($"{expectedHash}.jpg", secondDisplay.filename);
        Assert.AreEqual(firstDisplay.filename, secondDisplay.filename);
        Assert.IsTrue(secondDisplay.image_url.EndsWith($"/screens/{expectedHash}.jpg"));
    }

    [TestMethod]
    public async Task Workflow_DisplayReturnsAbsoluteUrls()
    {
        // Arrange
        m_client.DefaultRequestHeaders.Add("ID", s_TestDeviceId);

        // Upload an image first
        var imageContent = CreateTestImage();
        var uploadContent = new ByteArrayContent(imageContent);
        uploadContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        await m_client.PostAsync($"/api/screens/{s_TestDeviceId}/image", uploadContent);

        // Act
        var response = await m_client.GetAsync("/api/display");
        var result = await response.Content.ReadFromJsonAsync<DisplayResponse>();

        // Assert
        Assert.IsNotNull(result);
        Assert.IsTrue(result.image_url.StartsWith("http://"));
        Assert.IsTrue(result.firmware_url.StartsWith("http://"));
        Assert.IsTrue(result.image_url.Contains("/screens/"));
        Assert.IsTrue(result.firmware_url.Contains("/firmware/"));
    }

    [TestMethod]
    public async Task Workflow_UploadMoreThanTenImages_DeletesOldestFilesForDevice()
    {
        // Arrange
        var uploadedPaths = new List<string>();

        // Act
        for (var i = 0; i < 12; i++)
        {
            var imageContent = CreateVariantTestImage(i);
            var content = new ByteArrayContent(imageContent);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

            var response = await m_client.PostAsync($"/api/screens/{s_TestDeviceId}/image", content);
            Assert.AreEqual(200, (int)response.StatusCode);

            var result = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>();
            Assert.IsNotNull(result);
            Assert.IsTrue(result.TryGetValue("path", out var pathElement));
            uploadedPaths.Add(pathElement.GetString()!);
        }

        // Assert
        var testDataDir = Environment.GetEnvironmentVariable("TEST_DATA_DIR");
        Assert.IsFalse(string.IsNullOrWhiteSpace(testDataDir));

        foreach (var stalePath in uploadedPaths.Take(2))
        {
            var staleFilePath = Path.Combine(testDataDir!, Path.GetFileName(stalePath));
            Assert.IsFalse(File.Exists(staleFilePath), $"Expected stale image to be deleted: {staleFilePath}");
        }

        foreach (var retainedPath in uploadedPaths.Skip(2))
        {
            var retainedFilePath = Path.Combine(testDataDir!, Path.GetFileName(retainedPath));
            Assert.IsTrue(File.Exists(retainedFilePath), $"Expected retained image to exist: {retainedFilePath}");
        }
    }

    [TestMethod]
    public async Task Workflow_CleanupRetainsSharedImageWhenUsedByAnotherDevice()
    {
        // Arrange
        const string secondDeviceId = "11:22:33:44:55:66";
        var sharedImage = CreateTestImage();
        var sharedHash = Convert.ToHexString(SHA256.HashData(sharedImage)).ToLowerInvariant();

        // Device B uploads image first.
        var secondDeviceUpload = new ByteArrayContent(sharedImage);
        secondDeviceUpload.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        var secondDeviceResponse = await m_client.PostAsync($"/api/screens/{secondDeviceId}/image", secondDeviceUpload);
        Assert.AreEqual(200, (int)secondDeviceResponse.StatusCode);

        // Device A uploads 11 images; first is shared image so it becomes stale for A.
        for (var i = 0; i < 11; i++)
        {
            var imageContent = i == 0 ? sharedImage : CreateVariantTestImage(i + 20);
            var content = new ByteArrayContent(imageContent);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            var uploadResponse = await m_client.PostAsync($"/api/screens/{s_TestDeviceId}/image", content);
            Assert.AreEqual(200, (int)uploadResponse.StatusCode);
        }

        // Act
        var sharedImageResponse = await m_client.GetAsync($"/screens/{sharedHash}.jpg");

        // Assert
        Assert.AreEqual(200, (int)sharedImageResponse.StatusCode);
    }

    [TestMethod]
    public async Task Workflow_HealthCheck_Returns200()
    {
        // Act
        var response = await m_client.GetAsync("/health");

        // Assert
        Assert.AreEqual(200, (int)response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.IsNotNull(result);
        Assert.AreEqual("ok", result["status"].ToString());
        Assert.IsTrue(result["service"].ToString()!.Contains("trmnl"));
    }

    [TestMethod]
    public async Task Workflow_LandingPage_ReturnsHtmlWithServiceAndDeviceSections()
    {
        // Arrange
        m_client.DefaultRequestHeaders.Add("ID", s_TestDeviceId);
        m_client.DefaultRequestHeaders.Add("MODEL", "TRMNL-EINK");
        m_client.DefaultRequestHeaders.Add("FIRMWARE", "1.5.2");
        m_client.DefaultRequestHeaders.Add("REFRESH_RATE", "150");
        await m_client.GetAsync("/api/setup");

        var imageContent = CreateTestImage();
        var uploadContent = new ByteArrayContent(imageContent);
        uploadContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        await m_client.PostAsync($"/api/screens/{s_TestDeviceId}/image", uploadContent);

        await m_client.GetAsync("/api/display");

        // Act
        var response = await m_client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.AreEqual(200, (int)response.StatusCode);
        Assert.AreEqual("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.IsTrue(html.Contains("TRMNL BYOS Server"));
        Assert.IsTrue(html.Contains("Active TRMNL devices"));
        Assert.IsTrue(html.Contains(s_TestDeviceId));
        Assert.IsTrue(html.Contains("TRMNL-EINK"));
        Assert.IsTrue(html.Contains("1.5.2"));
        Assert.IsTrue(html.Contains("Last screen fetched (UTC)"));
        Assert.IsTrue(html.Contains("Last screen updated (UTC)"));
        Assert.IsTrue(html.Contains("ago") || html.Contains("just now"));
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

    private byte[] CreateDifferentTestImage()
    {
        var image = CreateTestImage();
        image[image.Length - 3] ^= 0x01;
        return image;
    }

    private byte[] CreateVariantTestImage(int index)
    {
        var image = CreateTestImage();
        image[image.Length - 3] ^= (byte)(index + 1);
        return image;
    }
}
