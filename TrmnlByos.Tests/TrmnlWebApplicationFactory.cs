using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace TrmnlByos.Tests;

public class TrmnlWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _testDataDirectory;

    public TrmnlWebApplicationFactory()
    {
        _testDataDirectory = Path.Combine(Path.GetTempPath(), "trmnl-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDataDirectory);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set the test data directory BEFORE building
        Environment.SetEnvironmentVariable("TEST_DATA_DIR", _testDataDirectory);
        
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
            if (Directory.Exists(_testDataDirectory))
            {
                Directory.Delete(_testDataDirectory, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }

        await base.DisposeAsync();
    }
}

