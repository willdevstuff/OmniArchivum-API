namespace OmniArchivum.Api.Models.Entities;
using NpgsqlTypes;

public class Note : IOwnedEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Who this note belongs to: "guest:{guid}" for a sandboxed visitor session, or
    /// "user:{name}" for the signed-in owner. A global query filter scopes every read
    /// to the current request's owner, so one database serves everyone in isolation.
    /// </summary>
    public string OwnerKey { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string BodyMarkdown { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public bool IsDeleted { get; set; } = false;

    public NpgsqlTsVector SearchVector { get; set; } = default!;

    public ICollection<NoteTag> NoteTags { get; set; } = new List<NoteTag>();

}
