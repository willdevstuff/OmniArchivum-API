using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OmniArchivum.Api.Data;

namespace OmniArchivum.Api.Tests.Integration;

// Boots the real app pipeline (routing, model binding, DI) against the shared
// Testcontainers Postgres instance instead of appsettings' connection string.
public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public CustomWebApplicationFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // "Testing" (not "Development") so Program.cs's dev-only auto-migrate/seed
        // block doesn't run — PostgresFixture already migrated the shared database.
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<OmniArchivumDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<OmniArchivumDbContext>(options =>
                options.UseNpgsql(_connectionString));
        });
    }
}
