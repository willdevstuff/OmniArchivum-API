using Microsoft.EntityFrameworkCore;
using OmniArchivum.Api.Data;
using OmniArchivum.Api.Models.DTOs;
using OmniArchivum.Api.Models.Entities;

namespace OmniArchivum.Api.Services;

public sealed class NotesService : INotesService
{
    private readonly OmniArchivumDbContext _db;

    public NotesService(OmniArchivumDbContext db)
    {
        _db = db;
    }

    public async Task<List<NoteResponse>> GetAllAsync(int page, int pageSize)
    {
        if (page <= 0) page = 1;
        if (pageSize <= 0 || pageSize > 100) pageSize = 20;

        return await _db.Notes
            .OrderByDescending(n => n.UpdatedUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NoteResponse
            {
                Id = n.Id,
                Title = n.Title,
                BodyMarkdown = n.BodyMarkdown,
                CreatedUtc = n.CreatedUtc,
                UpdatedUtc = n.UpdatedUtc
            })
            .ToListAsync();
    }


    public async Task<List<NoteResponse>> SearchAsync(string query, int page, int pageSize)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<NoteResponse>();

        if (page <= 0) page = 1;
        if (pageSize <= 0 || pageSize > 100) pageSize = 20;

        return await _db.Notes
            .Where(n => n.SearchVector.Matches(EF.Functions.PlainToTsQuery("english", query)))
            .OrderByDescending(n => n.UpdatedUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NoteResponse
            {
                Id = n.Id,
                Title = n.Title,
                BodyMarkdown = n.BodyMarkdown,
                CreatedUtc = n.CreatedUtc,
                UpdatedUtc = n.UpdatedUtc
            })
            .ToListAsync();
    }


    public async Task<NoteResponse?> GetByIdAsync(Guid id)
    {
        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id);
        if (note is null) return null;

        return new NoteResponse
        {
            Id = note.Id,
            Title = note.Title,
            BodyMarkdown = note.BodyMarkdown,
            CreatedUtc = note.CreatedUtc,
            UpdatedUtc = note.UpdatedUtc
        };
    }

    public async Task<NoteResponse> CreateAsync(CreateNoteRequest request)
    {
        var note = new Note
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            BodyMarkdown = request.BodyMarkdown,
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        };

        _db.Notes.Add(note);
        await _db.SaveChangesAsync();

        return new NoteResponse
        {
            Id = note.Id,
            Title = note.Title,
            BodyMarkdown = note.BodyMarkdown,
            CreatedUtc = note.CreatedUtc,
            UpdatedUtc = note.UpdatedUtc
        };
    }

    public async Task<bool> SoftDeleteAsync(Guid id)
    {
        var note = await _db.Notes.FirstOrDefaultAsync(n => n.Id == id);
        if (note is null) return false;

        note.IsDeleted = true;
        note.UpdatedUtc = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync();
        return true;
    }
}
