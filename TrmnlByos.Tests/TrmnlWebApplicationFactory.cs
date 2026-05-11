using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace TrmnlByos.Tests;

public class TrmnlWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string m_testDataDirectory;
    private readonly string? m_previousTestDataDir;
    private readonly string? m_previousMaxImageBytes;

    public TrmnlWebApplicationFactory()
    {
        m_testDataDirectory = Path.Combine(Path.GetTempPath(), "trmnl-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(m_testDataDirectory);

        m_previousTestDataDir = Environment.GetEnvironmentVariable("TEST_DATA_DIR");
        m_previousMaxImageBytes = Environment.GetEnvironmentVariable("Uploads__MaxImageBytes");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set the test data directory BEFORE building
        Environment.SetEnvironmentVariable("TEST_DATA_DIR", m_testDataDirectory);
        Environment.SetEnvironmentVariable("Uploads__MaxImageBytes", "1024");

        // Override environment variable to use test directory
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Override any services if needed for testing
        });
    }

    public override async ValueTask DisposeAsync()
    {
        try
        {
            Environment.SetEnvironmentVariable("TEST_DATA_DIR", m_previousTestDataDir);
            Environment.SetEnvironmentVariable("Uploads__MaxImageBytes", m_previousMaxImageBytes);

            if (Directory.Exists(m_testDataDirectory))
            {
                Directory.Delete(m_testDataDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }

        await base.DisposeAsync();
    }
}

