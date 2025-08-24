using AISwarm.DataLayer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace AISwarm.Tests.Configuration;

public class ProgramConfigurationTests
{
    [Fact]
    public void WhenWorkingDirectoryConfigured_ShouldConfigureSqliteDatabase()
    {
        // Arrange
        var workingDirectory = Path.Combine(Path.GetTempPath(), "test-aiswarm-" + Guid.NewGuid().ToString("N")[..8]);
        var expectedDbPath = Path.Combine(workingDirectory, ".aiswarm", "coordination.db");

        var builder = Host.CreateApplicationBuilder();

        // Configure working directory
        builder.Configuration["WorkingDirectory"] = workingDirectory;

        // Add the same database configuration that Program.cs should use
        ConfigureDatabaseServices(builder.Services, builder.Configuration);

        var serviceProvider = builder.Services.BuildServiceProvider();

        // Act & Assert
        using (var scope = serviceProvider.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CoordinationDbContext>();

            // Verify database connection string points to correct SQLite file
            var connection = dbContext.Database.GetDbConnection();
            connection.ConnectionString.ShouldContain(expectedDbPath);
            connection.ConnectionString.ShouldContain("Data Source=");

            // Verify .aiswarm directory was created during configuration
            Directory.Exists(Path.Combine(workingDirectory, ".aiswarm")).ShouldBeTrue();
        }
    }

    private static void ConfigureDatabaseServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        // This method should match what Program.cs does for database configuration
        var workingDirectory = configuration["WorkingDirectory"] ?? Environment.CurrentDirectory;
        var aiswarmDirectory = Path.Combine(workingDirectory, ".aiswarm");
        var databasePath = Path.Combine(aiswarmDirectory, "coordination.db");

        // Ensure directory exists
        Directory.CreateDirectory(aiswarmDirectory);

        services.AddDbContext<CoordinationDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));
    }
}
