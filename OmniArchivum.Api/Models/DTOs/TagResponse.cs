namespace OmniArchivum.Api.Models.DTOs;

public sealed class TagResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
}
