using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OmniArchivum.Api.Services;

/// <summary>
/// Issues the signed tokens that carry a caller's owner key.
///
/// The owner key lives in a signed token rather than a plain header so a caller can't
/// simply name someone else's session. It also means guest keys can't be enumerated:
/// you can only obtain one from the (rate-limited) issuing endpoint.
///
/// Bearer tokens rather than cookies because the frontend and API are on different
/// sites, which would make any cookie a third-party cookie — blocked outright by Safari
/// and being phased out elsewhere.
/// </summary>
public sealed class SessionTokenService
{
    public const string GuestPrefix = "guest:";
    public const string UserPrefix = "user:";

    private readonly SymmetricSecurityKey _signingKey;
    private readonly string _issuer;

    public SessionTokenService(IConfiguration configuration, IHostEnvironment environment)
    {
        var secret = configuration["Session:SigningKey"];

        if (string.IsNullOrWhiteSpace(secret))
        {
            if (environment.IsProduction())
            {
                throw new InvalidOperationException(
                    "Session:SigningKey is not configured. Set it as a Container Apps " +
                    "secret before deploying.");
            }

            // Outside production, fall back to a random per-process key so a fresh clone
            // runs with no setup. Sessions don't survive a restart, which is fine locally.
            secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        }
        else if (Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException(
                "Session:SigningKey must be at least 32 bytes for HMAC-SHA256.");
        }

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        _issuer = configuration["Session:Issuer"] ?? "omniarchivum";
    }

    public TokenValidationParameters ValidationParameters => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = _signingKey,
        ValidateIssuer = true,
        ValidIssuer = _issuer,
        ValidateAudience = false,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1)
    };

    /// <summary>
    /// How long a guest token stays valid.
    ///
    /// This must never outlive <see cref="GuestDataCleaner"/>'s retention window. If a
    /// token were still valid after its data had been reclaimed, a returning visitor
    /// would get a successful response containing an empty archive — no error, no
    /// re-seed, just a blank page that looks broken. Keeping the token the shorter of
    /// the two means expiry always comes first, so they get a 401, the client quietly
    /// starts a fresh session, and they land on a freshly seeded archive.
    /// </summary>
    public static readonly TimeSpan GuestSessionLifetime = TimeSpan.FromDays(2);

    public static string NewGuestOwnerKey() => GuestPrefix + Guid.NewGuid().ToString("N");

    public IssuedSession IssueGuestSession() =>
        Issue(NewGuestOwnerKey(), GuestSessionLifetime);

    public IssuedSession IssueUserSession(string userName) =>
        Issue(UserPrefix + userName, TimeSpan.FromDays(30));

    private IssuedSession Issue(string ownerKey, TimeSpan lifetime)
    {
        var expiresUtc = DateTime.UtcNow.Add(lifetime);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            claims: new[]
            {
                new Claim(OwnerContext.OwnerClaim, ownerKey),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            },
            expires: expiresUtc,
            signingCredentials: new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256));

        return new IssuedSession(
            new JwtSecurityTokenHandler().WriteToken(token),
            ownerKey,
            expiresUtc);
    }
}

public sealed record IssuedSession(string Token, string OwnerKey, DateTime ExpiresUtc);
