using System.ComponentModel.DataAnnotations;

namespace OmniArchivum.Api.Models.DTOs;

public sealed class CreateTagRequest
{
    [Required]
    [MaxLength(64)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(32)]
    public string? Category { get; set; }
}
