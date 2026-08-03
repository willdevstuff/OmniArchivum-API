using System.Net;
using System.Net.Http.Json;
using OmniArchivum.Api.Models.DTOs;

namespace OmniArchivum.Api.Tests.Integration;

// End-to-end HTTP tests against the real ASP.NET Core pipeline — proves routing,
// model binding, and DI wiring work, not just the service layer in isolation.
[Collection("Postgres collection")]
public sealed class NotesApiTests : IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public NotesApiTests(PostgresFixture postgres)
    {
        _factory = new CustomWebApplicationFactory(postgres.ConnectionString);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task PostNotes_CreatesNote_AndReturns201WithLocation()
    {
        var unique = Guid.NewGuid().ToString("N");
        var response = await _client.PostAsJsonAsync("/api/notes", new
        {
            title = $"API test note {unique}",
            bodyMarkdown = "Created via HTTP test."
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<NoteResponse>();
        Assert.NotNull(created);
        Assert.Equal($"API test note {unique}", created!.Title);
    }

    [Fact]
    public async Task DeleteNotes_ReturnsNotFound_ForUnknownId()
    {
        var response = await _client.DeleteAsync($"/api/notes/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostTags_ReturnsConflict_OnDuplicateName()
    {
        var unique = Guid.NewGuid().ToString("N");
        var first = await _client.PostAsJsonAsync("/api/tags", new { name = unique });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/api/tags", new { name = unique.ToUpperInvariant() });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task GetNotes_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/notes");
        response.EnsureSuccessStatusCode();
    }
}
