using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OmniArchivum.Api.Models.DTOs;
using OmniArchivum.Api.Services;


namespace OmniArchivum.Api.Controllers;

// Every note belongs to a session, so a caller without a valid one gets 401 rather than
// an empty archive — that way a client can tell "nothing here" from "your token expired"
// and go get a new session instead of silently showing an empty page.
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class NotesController : ControllerBase
{
    private readonly INotesService _service;
    public NotesController(INotesService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<NoteResponse>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] List<string>? tag = null)
    {
        return Ok(await _service.GetAllAsync(page, pageSize, tag));
    }


    [HttpPost]
    public async Task<ActionResult<NoteResponse>> Create(CreateNoteRequest request)
    {
        var created = await _service.CreateAsync(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NoteResponse>> GetById(Guid id)
    {
        var note = await _service.GetByIdAsync(id);
        if (note is null) return NotFound();
        return Ok(note);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<NoteResponse>> Update(Guid id, UpdateNoteRequest request)
    {
        var updated = await _service.UpdateAsync(id, request);
        if (updated is null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id) //dont remove deleted notes. just mark them. they will then be ignored by queries.
    {
        var success = await _service.SoftDeleteAsync(id);
        if (!success) return NotFound();
        return NoContent();
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<NoteResponse>>> Search(
    [FromQuery] string q,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
    {
        return Ok(await _service.SearchAsync(q, page, pageSize));
    }
}
