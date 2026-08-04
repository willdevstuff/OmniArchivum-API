using Microsoft.EntityFrameworkCore;
using OmniArchivum.Api.Models.Entities;

namespace OmniArchivum.Api.Data;

/// <summary>
/// Gives every new session its own copy of a small representative archive, so the demo
/// shows the app doing something real — full-text search, multi-tag filtering, category
/// colours, Markdown rendering — rather than an empty grid. Because the copy belongs to
/// the session that requested it, a visitor can edit or delete any of it freely without
/// affecting anyone else.
/// </summary>
public static class DemoData
{
    /// <summary>
    /// Populates the demo archive for a single owner. Safe to call more than once for the
    /// same owner: it no-ops if that owner already has notes.
    /// </summary>
    public static async Task SeedForOwnerAsync(OmniArchivumDbContext db, string ownerKey)
    {
        // Query filters are driven by the *request's* owner, which isn't necessarily the
        // one being seeded, so scope explicitly here.
        var alreadySeeded = await db.Notes
            .IgnoreQueryFilters()
            .AnyAsync(n => n.OwnerKey == ownerKey);

        if (alreadySeeded) return;

        Build(db, ownerKey);
        await db.SaveChangesAsync();
    }

    private static void Build(OmniArchivumDbContext db, string ownerKey)
    {
        var unity = new Tag { OwnerKey = ownerKey, Name = "unity", Category = "tool" };
        var fmod = new Tag { OwnerKey = ownerKey, Name = "fmod", Category = "tool" };
        var ableton = new Tag { OwnerKey = ownerKey, Name = "ableton", Category = "tool" };
        var postgres = new Tag { OwnerKey = ownerKey, Name = "postgres", Category = "tool" };
        var csharp = new Tag { OwnerKey = ownerKey, Name = "csharp", Category = "language" };
        var javascript = new Tag { OwnerKey = ownerKey, Name = "javascript", Category = "language" };
        var audio = new Tag { OwnerKey = ownerKey, Name = "audio", Category = "topic" };
        var performance = new Tag { OwnerKey = ownerKey, Name = "performance", Category = "topic" };

        var now = DateTimeOffset.UtcNow;

        AddNote(db, ownerKey, now.AddDays(-1), "FMOD footstep surfaces",
            """
            Driving footstep variation from a single event instead of one event per surface.

            ## Setup

            - Multi Instrument on the footstep track, one playlist entry per surface
            - Discrete parameter `Surface` (0 = concrete, 1 = grass, 2 = metal, 3 = water)
            - Playlist set to **Shuffle** so repeated steps don't machine-gun
            - Route the track to a dedicated `Footsteps` bus

            ## Calling it from code

            ```csharp
            instance.setParameterByName("Surface", (float)currentSurface);
            instance.start();
            ```

            Set the parameter *before* `start()` — setting it after means the first
            playthrough picks from whichever playlist entry was previously selected.
            """,
            unity, fmod, audio);

        AddNote(db, ownerKey, now.AddDays(-3), "Ableton vocal chain",
            """
            Default starting point for a dry vocal take. Adjust to taste, but this order matters.

            1. **EQ Eight** — high-pass at ~120 Hz to clear rumble
            2. **Compressor** — 4:1, fast attack, release timed to the track
            3. **Saturator** — very light, mostly for harmonics rather than drive
            4. **Reverb** — on a send, never inserted directly

            Keeping reverb on a send means several vocal tracks share one space, which
            sits better in the mix than each track having its own.
            """,
            ableton, audio);

        AddNote(db, ownerKey, now.AddDays(-5), "EF Core global query filters",
            """
            Soft delete is far less error-prone as a model-level filter than as a `Where`
            clause repeated in every query.

            ```csharp
            modelBuilder.Entity<Note>()
                .HasQueryFilter(n => !n.IsDeleted);
            ```

            Every LINQ query against `Notes` now excludes deleted rows automatically.

            Two things worth knowing:

            - Filters do **not** apply to raw SQL, so `FromSqlRaw` still sees deleted rows
            - Use `IgnoreQueryFilters()` to deliberately bypass it, e.g. for an admin view
            """,
            csharp, postgres);

        AddNote(db, ownerKey, now.AddDays(-8), "PostgreSQL full-text search with tsvector",
            """
            A generated `tsvector` column plus a GIN index gives real full-text search
            without any external search service.

            ```sql
            ALTER TABLE "Notes"
              ADD COLUMN "SearchVector" tsvector
              GENERATED ALWAYS AS (
                to_tsvector('english', coalesce("Title",'') || ' ' || coalesce("BodyMarkdown",''))
              ) STORED;

            CREATE INDEX "IX_Notes_SearchVector" ON "Notes" USING GIN ("SearchVector");
            ```

            Because the column is `GENERATED ALWAYS`, it stays in sync on every insert and
            update with no trigger to maintain. `plainto_tsquery` handles stemming, so
            searching *running* also matches *run*.
            """,
            postgres, performance);

        AddNote(db, ownerKey, now.AddDays(-11), "Unity object pooling",
            """
            Instantiating and destroying frequently-spawned objects causes GC spikes that
            show up as frame hitches. Pool them instead.

            ```csharp
            private readonly Queue<Projectile> _pool = new();

            public Projectile Get()
            {
                var p = _pool.Count > 0 ? _pool.Dequeue() : Instantiate(_prefab);
                p.gameObject.SetActive(true);
                return p;
            }

            public void Return(Projectile p)
            {
                p.gameObject.SetActive(false);
                _pool.Enqueue(p);
            }
            ```

            Worth doing for projectiles, impact effects and audio one-shots. Not worth the
            indirection for objects spawned a handful of times per level.
            """,
            unity, csharp, performance);

        AddNote(db, ownerKey, now.AddDays(-14), "Handling fetch errors properly",
            """
            `fetch` only rejects on network failure — a 404 or 500 still resolves. Checking
            `res.ok` explicitly is the difference between a real error message and a
            confusing `undefined` further down.

            ```javascript
            const res = await fetch(url);
            if (!res.ok) {
              throw new Error(`Request failed (${res.status})`);
            }
            return res.status === 204 ? null : res.json();
            ```

            Handling `204 No Content` separately matters too: calling `.json()` on an empty
            body throws a parse error that looks nothing like the actual problem.
            """,
            javascript);

        AddNote(db, ownerKey, now.AddDays(-18), "Reverb send buses",
            """
            Shared reverb buses are the main tool for making sources sound like they're in
            the same room.

            - One send per space (`Room`, `Hall`, `Outdoor`), not one per source
            - Sources vary *send level*, not reverb settings
            - Duck the reverb return under dialogue rather than lowering the send

            The same principle applies in **Ableton** and **FMOD** — the routing differs,
            the reasoning doesn't.
            """,
            fmod, ableton, audio);
    }

    private static void AddNote(
        OmniArchivumDbContext db,
        string ownerKey,
        DateTimeOffset timestamp,
        string title,
        string bodyMarkdown,
        params Tag[] tags)
    {
        var note = new Note
        {
            // Set explicitly rather than relying on SaveChanges stamping it, since seeding
            // runs for the session being created, which isn't the request's owner yet.
            OwnerKey = ownerKey,
            Title = title,
            BodyMarkdown = bodyMarkdown,
            CreatedUtc = timestamp,
            UpdatedUtc = timestamp
        };

        foreach (var tag in tags)
        {
            note.NoteTags.Add(new NoteTag { Tag = tag });
        }

        db.Notes.Add(note);
    }
}
