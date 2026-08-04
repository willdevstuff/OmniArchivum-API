using System.Security.Claims;

namespace OmniArchivum.Api.Services;

/// <summary>
/// Reads the owner key from the authenticated principal's claims. The claim is only
/// present because the JWT bearer handler validated the token's signature first, so
/// the value cannot be forged by the caller.
/// </summary>
public sealed class OwnerContext : IOwnerContext
{
    /// <summary>Claim carrying the owner key on issued session tokens.</summary>
    public const string OwnerClaim = "owner";

    private readonly IHttpContextAccessor _accessor;

    public OwnerContext(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    public string? OwnerKey =>
        _accessor.HttpContext?.User?.FindFirstValue(OwnerClaim);
}

/// <summary>
/// Fixed owner for work that happens outside a request — seeding a session, or the
/// background cleanup of expired guest data.
/// </summary>
public sealed class StaticOwnerContext : IOwnerContext
{
    public StaticOwnerContext(string? ownerKey) => OwnerKey = ownerKey;

    public string? OwnerKey { get; }
}
