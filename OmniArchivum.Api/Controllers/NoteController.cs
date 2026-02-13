using Microsoft.AspNetCore.Mvc;
using OmniArchivum.Api.Models.DTOs;
using OmniArchivum.Api.Services;


namespace OmniArchivum.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotesController : ControllerBase
{
    private readonly INotesService _service;
    public NotesController(INotesService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<List<NoteResponse>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        return Ok(await _service.GetAllAsync(page, pageSize));
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
