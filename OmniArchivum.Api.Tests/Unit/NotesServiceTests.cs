using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using OmniArchivum.Api.Data;
using OmniArchivum.Api.Models.DTOs;
using OmniArchivum.Api.Services;

namespace OmniArchivum.Api.Tests.Unit;

// Fast, provider-agnostic tests for business logic in NotesService, run against a
// fresh SQLite in-memory database per test. Deliberately doesn't cover SearchAsync,
// since full-text search relies on Postgres-specific tsvector/GIN behavior that
// SQLite can't reproduce — see Integration/SearchIntegrationTests.cs for that.
public sealed class NotesServiceTests : IDisposable
{
    private const string TestOwner = "guest:test-owner";

    private readonly SqliteConnection _connection;
    private readonly OmniArchivumDbContext _db;
    private readonly NotesService _service;

    public NotesServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _db = CreateContext(TestOwner);
        _db.Database.EnsureCreated();

        _service = new NotesService(_db);
    }

    // A second context on the same database but a different owner, for proving that
    // one session cannot see another's data.
    private OmniArchivumDbContext CreateContext(string ownerKey)
    {
        var options = new DbContextOptionsBuilder<OmniArchivumDbContext>()
            .UseSqlite(_connection)
            .Options;

        return new OmniArchivumDbContext(options, new StaticOwnerContext(ownerKey));
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateAsync_TrimsTitle_AndPersistsNote()
    {
        var created = await _service.CreateAsync(new CreateNoteRequest
        {
            Title = "  My Note  ",
            BodyMarkdown = "Body"
        });

        Assert.Equal("My Note", created.Title);

        var fetched = await _service.GetByIdAsync(created.Id);
        Assert.NotNull(fetched);
        Assert.Equal("Body", fetched!.BodyMarkdown);
    }

    [Fact]
    public async Task SoftDeleteAsync_HidesNoteFromGetAllAndGetById()
    {
        var note = await _service.CreateAsync(new CreateNoteRequest { Title = "To delete", BodyMarkdown = "x" });

        var deleted = await _service.SoftDeleteAsync(note.Id);
        Assert.True(deleted);

        var all = await _service.GetAllAsync(1, 20, null);
        Assert.DoesNotContain(all, n => n.Id == note.Id);

        var fetched = await _service.GetByIdAsync(note.Id);
        Assert.Null(fetched);
    }

    [Fact]
    public async Task SoftDeleteAsync_ReturnsFalse_ForUnknownId()
    {
        var result = await _service.SoftDeleteAsync(Guid.NewGuid());
        Assert.False(result);
    }

    [Fact]
    public async Task UpdateAsync_ChangesTitleAndBody()
    {
        var note = await _service.CreateAsync(new CreateNoteRequest { Title = "Old", BodyMarkdown = "Old body" });

        var updated = await _service.UpdateAsync(note.Id, new UpdateNoteRequest { Title = "New", BodyMarkdown = "New body" });

        Assert.NotNull(updated);
        Assert.Equal("New", updated!.Title);
        Assert.Equal("New body", updated.BodyMarkdown);
        Assert.True(updated.UpdatedUtc >= note.UpdatedUtc);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNull_ForUnknownId()
    {
        var result = await _service.UpdateAsync(Guid.NewGuid(), new UpdateNoteRequest { Title = "x", BodyMarkdown = "y" });
        Assert.Null(result);
    }

    [Fact]
    public async Task CreateTagAsync_NormalizesName_ToLowercase()
    {
        var tag = await _service.CreateTagAsync(new CreateTagRequest { Name = "  Unity  ", Category = "Tool" });
        Assert.Equal("unity", tag.Name);
    }

    [Fact]
    public async Task CreateTagAsync_Throws_OnDuplicateName()
    {
        await _service.CreateTagAsync(new CreateTagRequest { Name = "fmod" });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service.CreateTagAsync(new CreateTagRequest { Name = "FMOD" }));
    }

    [Fact]
    public async Task AddTagToNoteAsync_IsIdempotent()
    {
        var note = await _service.CreateAsync(new CreateNoteRequest { Title = "N", BodyMarkdown = "b" });
        var tag = await _service.CreateTagAsync(new CreateTagRequest { Name = "unity" });

        var first = await _service.AddTagToNoteAsync(note.Id, tag.Id);
        var second = await _service.AddTagToNoteAsync(note.Id, tag.Id);

        Assert.True(first);
        Assert.True(second);

        var fetched = await _service.GetByIdAsync(note.Id);
        Assert.Single(fetched!.Tags);
    }

    [Fact]
    public async Task AddTagToNoteAsync_ReturnsFalse_WhenNoteOrTagMissing()
    {
        var tag = await _service.CreateTagAsync(new CreateTagRequest { Name = "unity" });
        var result = await _service.AddTagToNoteAsync(Guid.NewGuid(), tag.Id);
        Assert.False(result);
    }

    [Fact]
    public async Task RemoveTagFromNoteAsync_UnlinksTag_ButKeepsTagItself()
    {
        var note = await _service.CreateAsync(new CreateNoteRequest { Title = "N", BodyMarkdown = "b" });
        var tag = await _service.CreateTagAsync(new CreateTagRequest { Name = "unity" });
        await _service.AddTagToNoteAsync(note.Id, tag.Id);

        var removed = await _service.RemoveTagFromNoteAsync(note.Id, tag.Id);
        Assert.True(removed);

        var fetchedNote = await _service.GetByIdAsync(note.Id);
        Assert.Empty(fetchedNote!.Tags);

        var allTags = await _service.GetAllTagsAsync();
        Assert.Contains(allTags, t => t.Id == tag.Id);
    }

    [Fact]
    public async Task DeleteTagAsync_RemovesTag_AndCascadesNoteTagLinks()
    {
        var note = await _service.CreateAsync(new CreateNoteRequest { Title = "N", BodyMarkdown = "b" });
        var tag = await _service.CreateTagAsync(new CreateTagRequest { Name = "unity" });
        await _service.AddTagToNoteAsync(note.Id, tag.Id);

        var deleted = await _service.DeleteTagAsync(tag.Id);
        Assert.True(deleted);

        var allTags = await _service.GetAllTagsAsync();
        Assert.DoesNotContain(allTags, t => t.Id == tag.Id);

        // Assert the join row itself is gone, not just filtered out of a query result.
        var remainingLinks = await _db.NoteTags.CountAsync(nt => nt.TagId == tag.Id);
        Assert.Equal(0, remainingLinks);
    }

    [Fact]
    public async Task DeleteTagAsync_ReturnsFalse_ForUnknownId()
    {
        var result = await _service.DeleteTagAsync(Guid.NewGuid());
        Assert.False(result);
    }

    [Fact]
    public async Task GetAllAsync_FiltersByMultipleTags_UsingAndLogic()
    {
        var noteWithBoth = await _service.CreateAsync(new CreateNoteRequest { Title = "Both", BodyMarkdown = "b" });
        var noteWithOne = await _service.CreateAsync(new CreateNoteRequest { Title = "One", BodyMarkdown = "b" });

        var unity = await _service.CreateTagAsync(new CreateTagRequest { Name = "unity" });
        var fmod = await _service.CreateTagAsync(new CreateTagRequest { Name = "fmod" });

        await _service.AddTagToNoteAsync(noteWithBoth.Id, unity.Id);
        await _service.AddTagToNoteAsync(noteWithBoth.Id, fmod.Id);
        await _service.AddTagToNoteAsync(noteWithOne.Id, unity.Id);

        var results = await _service.GetAllAsync(1, 20, new[] { "unity", "fmod" });

        Assert.Single(results);
        Assert.Equal(noteWithBoth.Id, results[0].Id);
    }

    // Session isolation. These are the tests that matter for the public demo: one
    // visitor's activity must be completely invisible to another's.

    [Fact]
    public async Task NotesAreInvisibleToOtherOwners()
    {
        var mine = await _service.CreateAsync(new CreateNoteRequest { Title = "Mine", BodyMarkdown = "b" });

        using var otherDb = CreateContext("guest:someone-else");
        var otherService = new NotesService(otherDb);

        var theirNotes = await otherService.GetAllAsync(1, 20, null);
        Assert.Empty(theirNotes);

        var directLookup = await otherService.GetByIdAsync(mine.Id);
        Assert.Null(directLookup);
    }

    [Fact]
    public async Task AnotherOwnerCannotDeleteOrEditMyNote()
    {
        var mine = await _service.CreateAsync(new CreateNoteRequest { Title = "Mine", BodyMarkdown = "b" });

        using var otherDb = CreateContext("guest:someone-else");
        var otherService = new NotesService(otherDb);

        // Knowing the id is not enough — the row is outside their filter entirely.
        Assert.False(await otherService.SoftDeleteAsync(mine.Id));
        Assert.Null(await otherService.UpdateAsync(mine.Id, new UpdateNoteRequest { Title = "Hijacked", BodyMarkdown = "x" }));

        var stillMine = await _service.GetByIdAsync(mine.Id);
        Assert.NotNull(stillMine);
        Assert.Equal("Mine", stillMine!.Title);
    }

    [Fact]
    public async Task TagsAreInvisibleToOtherOwners()
    {
        await _service.CreateTagAsync(new CreateTagRequest { Name = "unity" });

        using var otherDb = CreateContext("guest:someone-else");
        var otherService = new NotesService(otherDb);

        Assert.Empty(await otherService.GetAllTagsAsync());
    }

    [Fact]
    public async Task TheSameTagNameCanExistForDifferentOwners()
    {
        await _service.CreateTagAsync(new CreateTagRequest { Name = "unity" });

        using var otherDb = CreateContext("guest:someone-else");
        var otherService = new NotesService(otherDb);

        // Uniqueness is per owner, so this must not collide with the tag above.
        var theirs = await otherService.CreateTagAsync(new CreateTagRequest { Name = "unity" });
        Assert.Equal("unity", theirs.Name);
    }

    [Fact]
    public async Task NewEntitiesAreStampedWithTheCurrentOwner()
    {
        var note = await _service.CreateAsync(new CreateNoteRequest { Title = "N", BodyMarkdown = "b" });

        var stored = await _db.Notes.IgnoreQueryFilters().SingleAsync(n => n.Id == note.Id);
        Assert.Equal(TestOwner, stored.OwnerKey);
    }
}
