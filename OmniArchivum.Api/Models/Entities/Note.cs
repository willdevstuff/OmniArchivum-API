namespace OmniArchivum.Api.Models.Entities;
using NpgsqlTypes;

public class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    public string BodyMarkdown { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;

    public bool IsDeleted { get; set; } = false;

    public NpgsqlTsVector SearchVector { get; set; } = default!;
}
