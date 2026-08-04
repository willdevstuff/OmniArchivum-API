namespace OmniArchivum.Api.Services;

/// <summary>
/// Supplies the owner key for the current request. Everything the API reads or writes
/// is scoped to this value, which is derived from the caller's bearer token rather than
/// from anything the client sends directly — a client cannot ask for another owner's data
/// by changing a header.
/// </summary>
public interface IOwnerContext
{
    /// <summary>
    /// The current owner, or <c>null</c> when the request carries no session (in which
    /// case nothing is readable — the query filter matches no rows).
    /// </summary>
    string? OwnerKey { get; }
}
