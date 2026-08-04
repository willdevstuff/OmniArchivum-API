using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using OmniArchivum.Api.Services;

namespace OmniArchivum.Api.Tests.Unit;

public sealed class SessionLifetimeTests
{
    private static SessionTokenService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Session:SigningKey"] = "a-test-signing-key-that-is-long-enough-for-hmac-sha256"
            })
            .Build();

        return new SessionTokenService(configuration, new FakeEnvironment());
    }

    [Fact]
    public void GuestTokensExpireBeforeTheirDataIsReclaimed()
    {
        // The relationship between these two values is the point, not their absolute
        // size. A token that outlived its data would give a returning visitor a 200 with
        // an empty archive rather than a 401 and a fresh session — a blank page that
        // looks like a bug rather than a re-seeded sandbox. This reads both real values
        // so the ordering can't be reversed by tuning either number in isolation.
        Assert.True(
            SessionTokenService.GuestSessionLifetime < GuestDataCleanupService.Retention,
            $"Guest tokens live for {SessionTokenService.GuestSessionLifetime}, but data is " +
            $"reclaimed after {GuestDataCleanupService.Retention}. Tokens must expire first.");
    }

    [Fact]
    public void AnIssuedGuestTokenCarriesTheExpectedLifetimeAndOwner()
    {
        var session = CreateService().IssueGuestSession();

        Assert.StartsWith(SessionTokenService.GuestPrefix, session.OwnerKey);

        var expected = DateTime.UtcNow.Add(SessionTokenService.GuestSessionLifetime);
        Assert.True(
            Math.Abs((session.ExpiresUtc - expected).TotalMinutes) < 2,
            $"Expected expiry near {expected:o} but got {session.ExpiresUtc:o}");

        var claims = new JwtSecurityTokenHandler().ReadJwtToken(session.Token);
        Assert.Equal(session.OwnerKey, claims.Claims.Single(c => c.Type == OwnerContext.OwnerClaim).Value);
    }

    [Fact]
    public void EachGuestSessionGetsADistinctOwner()
    {
        var service = CreateService();

        var first = service.IssueGuestSession();
        var second = service.IssueGuestSession();

        Assert.NotEqual(first.OwnerKey, second.OwnerKey);
    }

    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Testing";
        public string ApplicationName { get; set; } = "OmniArchivum.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}
