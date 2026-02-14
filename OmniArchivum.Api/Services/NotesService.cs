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
            .Include(n => n.NoteTags)
            .ThenInclude(nt => nt.Tag)
            .OrderByDescending(n => n.UpdatedUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NoteResponse
            {
                Id = n.Id,
                Title = n.Title,
                BodyMarkdown = n.BodyMarkdown,
                CreatedUtc = n.CreatedUtc,
                UpdatedUtc = n.UpdatedUtc,
                Tags = n.NoteTags
                    .Select(nt => new TagResponse
                    {
                        Id = nt.Tag.Id,
                        Name = nt.Tag.Name,
                        Category = nt.Tag.Category
                    })
                    .ToList()
            })
            .ToListAsync();
    }



    public async Task<List<NoteResponse>> SearchAsync(string query, int page, int pageSize)
    {
        if (string.IsNullOrWhiteSpace(query)) return new List<NoteResponse>();

        if (page <= 0) page = 1;
        if (pageSize <= 0 || pageSize > 100) pageSize = 20;

        return await _db.Notes
            .Include(n => n.NoteTags)
            .ThenInclude(nt => nt.Tag)
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
                UpdatedUtc = n.UpdatedUtc,
                Tags = n.NoteTags
                    .Select(nt => new TagResponse
                    {
                        Id = nt.Tag.Id,
                        Name = nt.Tag.Name,
                        Category = nt.Tag.Category
                    })
                    .ToList()
            })
            .ToListAsync();
    }



    public async Task<NoteResponse?> GetByIdAsync(Guid id)
    {
        var note = await _db.Notes
            .Include(n => n.NoteTags)
            .ThenInclude(nt => nt.Tag)
            .FirstOrDefaultAsync(n => n.Id == id);

        if (note is null) return null;

        return new NoteResponse
        {
            Id = note.Id,
            Title = note.Title,
            BodyMarkdown = note.BodyMarkdown,
            CreatedUtc = note.CreatedUtc,
            UpdatedUtc = note.UpdatedUtc,
            Tags = note.NoteTags
                .Select(nt => new TagResponse
                {
                    Id = nt.Tag.Id,
                    Name = nt.Tag.Name,
                    Category = nt.Tag.Category
                })
                .ToList()
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

    public async Task<TagResponse> CreateTagAsync(CreateTagRequest request)
    {
        var normalized = request.Name.Trim().ToLowerInvariant();

        if (await _db.Tags.AnyAsync(t => t.Name == normalized))
            throw new InvalidOperationException("Tag already exists.");

        var tag = new Tag
        {
            Id = Guid.NewGuid(),
            Name = normalized,
            Category = request.Category?.Trim(),
            CreatedUtc = DateTimeOffset.UtcNow
        };

        _db.Tags.Add(tag);
        await _db.SaveChangesAsync();

        return new TagResponse
        {
            Id = tag.Id,
            Name = tag.Name,
            Category = tag.Category
        };
    }

    public async Task<bool> AddTagToNoteAsync(Guid noteId, Guid tagId)
    {
        var noteExists = await _db.Notes.AnyAsync(n => n.Id == noteId);
        var tagExists = await _db.Tags.AnyAsync(t => t.Id == tagId);

        if (!noteExists || !tagExists)
            return false;

        var alreadyLinked = await _db.NoteTags
            .AnyAsync(nt => nt.NoteId == noteId && nt.TagId == tagId);

        if (alreadyLinked)
            return true;

        _db.NoteTags.Add(new NoteTag
        {
            NoteId = noteId,
            TagId = tagId
        });

        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<List<TagResponse>> GetAllTagsAsync()
    {
        return await _db.Tags
            .OrderBy(t => t.Name)
            .Select(t => new TagResponse
            {
                Id = t.Id,
                Name = t.Name,
                Category = t.Category
            })
            .ToListAsync();
    }

}
