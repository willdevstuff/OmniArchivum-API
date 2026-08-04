using Microsoft.EntityFrameworkCore;
using OmniArchivum.Api.Data;

namespace OmniArchivum.Api.Services;

/// <summary>
/// Reclaims the data belonging to guest sessions that have gone quiet. Separate from the
/// background service that schedules it so the query can be tested directly — the
/// grouping it relies on has to translate to SQL, which is not something a compile
/// catches.
/// </summary>
public sealed class GuestDataCleaner
{
    private readonly OmniArchivumDbContext _db;

    public GuestDataCleaner(OmniArchivumDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Deletes notes and tags for guest sessions whose most recent activity is older than
    /// <paramref name="retention"/>. Signed-in users' data is never touched.
    /// </summary>
    /// <returns>The number of sessions reclaimed.</returns>
    public async Task<int> PurgeStaleGuestsAsync(TimeSpan retention, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTimeOffset.UtcNow - retention;

        // IgnoreQueryFilters throughout: this runs without a request, so there's no owner
        // to scope to, and it deliberately needs to see soft-deleted rows too.
        var staleOwners = await _db.Notes
            .IgnoreQueryFilters()
            .Where(n => n.OwnerKey.StartsWith(SessionTokenService.GuestPrefix))
            .GroupBy(n => n.OwnerKey)
            .Where(g => g.Max(n => n.UpdatedUtc) < cutoff)
            .Select(g => g.Key)
            .ToListAsync(cancellationToken);

        if (staleOwners.Count == 0) return 0;

        // Join rows first — the dependent side — rather than relying on cascade behaviour
        // during a bulk delete.
        await _db.NoteTags
            .IgnoreQueryFilters()
            .Where(nt => staleOwners.Contains(nt.Note.OwnerKey))
            .ExecuteDeleteAsync(cancellationToken);

        await _db.Notes
            .IgnoreQueryFilters()
            .Where(n => staleOwners.Contains(n.OwnerKey))
            .ExecuteDeleteAsync(cancellationToken);

        await _db.Tags
            .IgnoreQueryFilters()
            .Where(t => staleOwners.Contains(t.OwnerKey))
            .ExecuteDeleteAsync(cancellationToken);

        return staleOwners.Count;
    }
}
