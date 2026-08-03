using Microsoft.EntityFrameworkCore;
using OmniArchivum.Api.Data;
using Testcontainers.PostgreSql;

namespace OmniArchivum.Api.Tests.Integration;

// Spins up a real, disposable Postgres container once per test collection. Full-text
// search relies on Postgres-specific tsvector/GIN behavior that no in-memory or SQLite
// substitute can reproduce, so this is the only way to test it honestly.
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("omniarchivum_test")
        .WithUsername("omni")
        .WithPassword("omni_password")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    public OmniArchivumDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OmniArchivumDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        return new OmniArchivumDbContext(options);
    }
}

[CollectionDefinition("Postgres collection")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
}
