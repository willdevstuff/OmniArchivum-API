namespace OmniArchivum.Api.Models.DTOs;

public sealed class NoteResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string BodyMarkdown { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}
