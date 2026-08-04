namespace OmniArchivum.Api.Models.DTOs;

public sealed class SessionResponse
{
    /// <summary>Bearer token to send as the Authorization header on subsequent requests.</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>"guest" for a sandboxed session, "user" for the signed-in owner.</summary>
    public string Kind { get; set; } = string.Empty;

    public DateTime ExpiresUtc { get; set; }
}
