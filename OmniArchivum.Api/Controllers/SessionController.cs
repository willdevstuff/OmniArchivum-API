using Microsoft.AspNetCore.Mvc;
using OmniArchivum.Api.Data;
using OmniArchivum.Api.Models.DTOs;
using OmniArchivum.Api.Services;

namespace OmniArchivum.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionController : ControllerBase
{
    private readonly SessionTokenService _tokens;
    private readonly OmniArchivumDbContext _db;

    public SessionController(SessionTokenService tokens, OmniArchivumDbContext db)
    {
        _tokens = tokens;
        _db = db;
    }

    /// <summary>
    /// Starts a sandboxed guest session, pre-populated with the demo notes. Everything
    /// the caller then creates, edits or deletes is visible only to this session, so a
    /// visitor can freely experiment without affecting anyone else's view.
    /// </summary>
    [HttpPost("guest")]
    public async Task<ActionResult<SessionResponse>> CreateGuestSession()
    {
        var session = _tokens.IssueGuestSession();

        await DemoData.SeedForOwnerAsync(_db, session.OwnerKey);

        return Ok(new SessionResponse
        {
            Token = session.Token,
            Kind = "guest",
            ExpiresUtc = session.ExpiresUtc
        });
    }
}
