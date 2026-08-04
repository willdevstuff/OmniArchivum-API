using Microsoft.EntityFrameworkCore;
using OmniArchivum.Api.Models.DTOs;
using OmniArchivum.Api.Models.Entities;
using OmniArchivum.Api.Services;

namespace OmniArchivum.Api.Tests.Integration;

// Runs against real Postgres because the cleanup relies on a GROUP BY with a HAVING-style
// aggregate filter and bulk ExecuteDelete — none of which a successful compile guarantees
// will translate to SQL.
[Collection("Postgres collection")]
public sealed class GuestDataCleanerTests
{
    private readonly PostgresFixture _fixture;

    public GuestDataCleanerTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private async Task<string> SeedGuestWithAgeAsync(TimeSpan age)
    {
        var ownerKey = $"guest:{Guid.NewGuid():N}";

        await using var db = _fixture.CreateContext(ownerKey);
        var service = new NotesService(db);

        var note = await service.CreateAsync(new CreateNoteRequest { Title = "Note", BodyMarkdown = "body" });
        var tag = await service.CreateTagAsync(new CreateTagRequest { Name = $"tag-{Guid.NewGuid():N}" });
        await service.AddTagToNoteAsync(note.Id, tag.Id);

        // Age the row directly; the service always stamps "now".
        var timestamp = DateTimeOffset.UtcNow - age;
        await db.Notes
            .IgnoreQueryFilters()
            .Where(n => n.OwnerKey == ownerKey)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.UpdatedUtc, timestamp));

        return ownerKey;
    }

    private async Task<(int notes, int tags, int links)> CountFor(string ownerKey)
    {
        await using var db = _fixture.CreateContext(ownerKey);

        return (
            await db.Notes.IgnoreQueryFilters().CountAsync(n => n.OwnerKey == ownerKey),
            await db.Tags.IgnoreQueryFilters().CountAsync(t => t.OwnerKey == ownerKey),
            await db.NoteTags.IgnoreQueryFilters().CountAsync(nt => nt.Note.OwnerKey == ownerKey)
        );
    }

    [Fact]
    public async Task RemovesGuestSessionsOlderThanRetention()
    {
        var stale = await SeedGuestWithAgeAsync(TimeSpan.FromDays(10));

        await using var db = _fixture.CreateContext();
        var reclaimed = await new GuestDataCleaner(db).PurgeStaleGuestsAsync(TimeSpan.FromDays(2));

        Assert.True(reclaimed >= 1);

        var (notes, tags, links) = await CountFor(stale);
        Assert.Equal(0, notes);
        Assert.Equal(0, tags);
        Assert.Equal(0, links);
    }

    [Fact]
    public async Task LeavesRecentGuestSessionsAlone()
    {
        var fresh = await SeedGuestWithAgeAsync(TimeSpan.FromMinutes(5));

        await using var db = _fixture.CreateContext();
        await new GuestDataCleaner(db).PurgeStaleGuestsAsync(TimeSpan.FromDays(2));

        var (notes, tags, links) = await CountFor(fresh);
        Assert.Equal(1, notes);
        Assert.Equal(1, tags);
        Assert.Equal(1, links);
    }

    [Fact]
    public async Task NeverTouchesSignedInUsersData()
    {
        var ownerKey = $"user:owner-{Guid.NewGuid():N}";

        await using (var seedDb = _fixture.CreateContext(ownerKey))
        {
            seedDb.Notes.Add(new Note
            {
                OwnerKey = ownerKey,
                Title = "Long-lived",
                BodyMarkdown = "Belongs to a signed-in user.",
                // Far older than any retention window — age must not matter here.
                CreatedUtc = DateTimeOffset.UtcNow.AddYears(-1),
                UpdatedUtc = DateTimeOffset.UtcNow.AddYears(-1)
            });
            await seedDb.SaveChangesAsync();
        }

        await using var db = _fixture.CreateContext();
        await new GuestDataCleaner(db).PurgeStaleGuestsAsync(TimeSpan.FromDays(2));

        var (notes, _, _) = await CountFor(ownerKey);
        Assert.Equal(1, notes);
    }
}
