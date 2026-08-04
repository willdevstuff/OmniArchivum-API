using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OmniArchivum.Api.Models.DTOs;

namespace OmniArchivum.Api.Tests.Integration;

// End-to-end HTTP tests against the real ASP.NET Core pipeline — proves routing, model
// binding, authentication and DI wiring work, not just the service layer in isolation.
// Each client starts a real guest session, so these also cover the session flow itself.
[Collection("Postgres collection")]
public sealed class NotesApiTests : IAsyncLifetime, IDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public NotesApiTests(PostgresFixture postgres)
    {
        _factory = new CustomWebApplicationFactory(postgres.ConnectionString);
        _client = _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        _client.DefaultRequestHeaders.Authorization = await StartGuestSessionAsync(_client);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private static async Task<AuthenticationHeaderValue> StartGuestSessionAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/session/guest", null);
        response.EnsureSuccessStatusCode();

        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        return new AuthenticationHeaderValue("Bearer", session!.Token);
    }

    [Fact]
    public async Task GuestSessionComesPreloadedWithTheDemoArchive()
    {
        var notes = await _client.GetFromJsonAsync<List<NoteResponse>>("/api/notes");

        Assert.NotNull(notes);
        Assert.NotEmpty(notes!);
        Assert.Contains(notes!, n => n.Tags.Count > 0);
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

    [Fact]
    public async Task RequestsWithoutASessionAreRejected()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/notes");

        // 401 rather than an empty list, so a client can tell an expired session from an
        // empty archive and go and get a new one.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OneGuestSessionCannotSeeAnother_sNotes()
    {
        var unique = Guid.NewGuid().ToString("N");
        var created = await _client.PostAsJsonAsync("/api/notes", new
        {
            title = $"Private {unique}",
            bodyMarkdown = "Only visible to the session that made it."
        });
        var mine = await created.Content.ReadFromJsonAsync<NoteResponse>();

        using var other = _factory.CreateClient();
        other.DefaultRequestHeaders.Authorization = await StartGuestSessionAsync(other);

        var theirNotes = await other.GetFromJsonAsync<List<NoteResponse>>("/api/notes");
        Assert.DoesNotContain(theirNotes!, n => n.Id == mine!.Id);

        // Knowing the id doesn't help either.
        var direct = await other.GetAsync($"/api/notes/{mine!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, direct.StatusCode);

        var deleteAttempt = await other.DeleteAsync($"/api/notes/{mine.Id}");
        Assert.Equal(HttpStatusCode.NotFound, deleteAttempt.StatusCode);
    }

    [Fact]
    public async Task ATamperedTokenIsRejected()
    {
        using var tampered = _factory.CreateClient();
        var real = await StartGuestSessionAsync(tampered);

        // Flip a character in the signature segment.
        var parts = real.Parameter!.Split('.');
        parts[2] = parts[2][0] == 'A' ? "B" + parts[2][1..] : "A" + parts[2][1..];
        tampered.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", string.Join('.', parts));

        var response = await tampered.GetAsync("/api/notes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
