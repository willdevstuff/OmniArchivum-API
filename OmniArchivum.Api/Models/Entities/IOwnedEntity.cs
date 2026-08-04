namespace OmniArchivum.Api.Models.Entities;

/// <summary>
/// An entity that belongs to a single guest session or signed-in user. The DbContext
/// stamps <see cref="OwnerKey"/> automatically on insert, so a new entity can't
/// accidentally be saved unowned — and therefore invisible to everyone — because a
/// call site forgot to set it.
/// </summary>
public interface IOwnedEntity
{
    string OwnerKey { get; set; }
}
