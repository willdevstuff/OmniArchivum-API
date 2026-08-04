namespace OmniArchivum.Api.Models.Entities;

public class Tag : IOwnedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Owning session or user. See <see cref="Note.OwnerKey"/>.
    /// </summary>
    public string OwnerKey { get; set; } = string.Empty;

    // What user sees / types (canonical format)
    public string Name { get; set; } = string.Empty;

    // Optional
    public string? Category { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<NoteTag> NoteTags { get; set; } = new List<NoteTag>();
}
