namespace OmniArchivum.Api.Models.Entities;

public class Tag
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // What user sees / types (canonical format)
    public string Name { get; set; } = string.Empty;

    // Optional
    public string? Category { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<NoteTag> NoteTags { get; set; } = new List<NoteTag>();
}
