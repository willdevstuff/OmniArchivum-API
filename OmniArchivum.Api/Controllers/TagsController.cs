using Microsoft.AspNetCore.Mvc;
using OmniArchivum.Api.Models.DTOs;
using OmniArchivum.Api.Services;

namespace OmniArchivum.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TagsController : ControllerBase
{
    private readonly INotesService _service;

    public TagsController(INotesService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<TagResponse>>> GetAll()
    {
        return Ok(await _service.GetAllTagsAsync());
    }

    [HttpPost]
    public async Task<ActionResult<TagResponse>> Create(CreateTagRequest request)
    {
        try
        {
            var created = await _service.CreateTagAsync(request);
            return CreatedAtAction(nameof(GetAll), new { }, created);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // Link an existing tag to an existing note
    [HttpPost("{tagId:guid}/notes/{noteId:guid}")]
    public async Task<IActionResult> AddTagToNote(Guid tagId, Guid noteId)
    {
        var ok = await _service.AddTagToNoteAsync(noteId, tagId);
        if (!ok) return NotFound();

        return NoContent();
    }
}
