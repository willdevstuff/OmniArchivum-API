using OmniArchivum.Api.Models.DTOs;
using OmniArchivum.Api.Services;

namespace OmniArchivum.Api.Tests.Integration;

// Validates PostgreSQL full-text search (tsvector + GIN), which cannot be tested
// against SQLite or an in-memory provider: EF.Functions.PlainToTsQuery is Npgsql-specific
// SQL translation with no equivalent on any other provider.
//
// Tests share one Postgres database for the whole collection (faster than a fresh
// container per test), so each test uses a unique token in its content and only
// asserts about data it created itself.
[Collection("Postgres collection")]
public sealed class SearchIntegrationTests
{
    private readonly PostgresFixture _fixture;

    public SearchIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task SearchAsync_FindsNote_ByWordInBody()
    {
        await using var db = _fixture.CreateContext();
        var service = new NotesService(db);

        var unique = Guid.NewGuid().ToString("N");
        var title = $"Search target {unique}";
        await service.CreateAsync(new CreateNoteRequest
        {
            Title = title,
            BodyMarkdown = $"This note mentions reverberation-{unique} in its body."
        });

        var results = await service.SearchAsync($"reverberation-{unique}", 1, 20);

        Assert.Contains(results, n => n.Title == title);
    }

    [Fact]
    public async Task SearchAsync_DoesNotMatch_UnrelatedWords()
    {
        await using var db = _fixture.CreateContext();
        var service = new NotesService(db);

        var unique = Guid.NewGuid().ToString("N");
        var title = $"Unrelated {unique}";
        await service.CreateAsync(new CreateNoteRequest
        {
            Title = title,
            BodyMarkdown = "Completely unrelated content about gardening."
        });

        var results = await service.SearchAsync($"xyzzy-nonexistent-{unique}", 1, 20);

        Assert.DoesNotContain(results, n => n.Title == title);
    }

    [Fact]
    public async Task SearchAsync_ExcludesSoftDeletedNotes()
    {
        await using var db = _fixture.CreateContext();
        var service = new NotesService(db);

        var unique = Guid.NewGuid().ToString("N");
        var note = await service.CreateAsync(new CreateNoteRequest
        {
            Title = $"Deleted target {unique}",
            BodyMarkdown = $"Findable-{unique} until deleted."
        });

        await service.SoftDeleteAsync(note.Id);

        var results = await service.SearchAsync($"findable-{unique}", 1, 20);

        Assert.DoesNotContain(results, n => n.Id == note.Id);
    }
}
