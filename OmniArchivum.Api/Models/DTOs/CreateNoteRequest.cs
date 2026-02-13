using System.ComponentModel.DataAnnotations;

namespace OmniArchivum.Api.Models.DTOs;

public sealed class CreateNoteRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string BodyMarkdown { get; set; } = string.Empty;
}
