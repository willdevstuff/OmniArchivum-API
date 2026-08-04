using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OmniArchivum.Api.Models.Entities;
using OmniArchivum.Api.Services;

namespace OmniArchivum.Api.Data;

public class OmniArchivumDbContext : DbContext
{
    private readonly IOwnerContext _ownerContext;

    public OmniArchivumDbContext(DbContextOptions<OmniArchivumDbContext> options, IOwnerContext ownerContext)
        : base(options)
    {
        _ownerContext = ownerContext;
    }

    /// <summary>
    /// Read by the global query filters. EF Core re-evaluates this per query rather than
    /// baking it into the cached model, so one context type serves every owner.
    /// </summary>
    public string? CurrentOwnerKey => _ownerContext.OwnerKey;

    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<NoteTag> NoteTags => Set<NoteTag>();


    public override int SaveChanges()
    {
        StampOwnerOnNewEntities();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampOwnerOnNewEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Assigns the current owner to anything being inserted that doesn't already have one.
    /// Done here rather than at each call site so a new write path can't silently create
    /// unowned rows.
    /// </summary>
    private void StampOwnerOnNewEntities()
    {
        var owner = CurrentOwnerKey;
        if (string.IsNullOrEmpty(owner)) return;

        foreach (var entry in ChangeTracker.Entries<IOwnedEntity>())
        {
            if (entry.State == EntityState.Added && string.IsNullOrEmpty(entry.Entity.OwnerKey))
            {
                entry.Entity.OwnerKey = owner;
            }
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Note>(e =>
        {
            e.HasKey(x => x.Id);

            e.Property(x => x.Title)
                .HasMaxLength(200)
                .IsRequired();

            e.Property(x => x.BodyMarkdown)
                .IsRequired();

            e.Property(x => x.OwnerKey)
                .HasMaxLength(64)
                .IsRequired();

            e.HasIndex(x => x.OwnerKey);

            // EF Core allows a single query filter per entity, so soft delete and owner
            // scoping are combined here rather than declared separately. A request with
            // no session has a null owner key, which matches nothing — the API is closed
            // by default rather than leaking another session's notes.
            e.HasQueryFilter(n => !n.IsDeleted && n.OwnerKey == CurrentOwnerKey);

            // SearchVector relies on Npgsql-specific generated columns and GIN indexing,
            // which only make sense against a real Postgres provider. Other providers
            // (e.g. SQLite, used for fast unit tests) simply don't map the column.
            if (Database.IsNpgsql())
            {
                e.HasGeneratedTsVectorColumn(
                        n => n.SearchVector,
                        "english",
                        n => new { n.Title, n.BodyMarkdown });

                e.HasIndex(n => n.SearchVector)
                    .HasMethod("GIN");
            }
            else
            {
                e.Ignore(n => n.SearchVector);
            }
        });

        modelBuilder.Entity<Tag>(e =>
        {
            e.HasKey(t => t.Id);

            e.Property(t => t.Name)
                .HasMaxLength(64)
                .IsRequired();

            e.Property(t => t.OwnerKey)
                .HasMaxLength(64)
                .IsRequired();

            // Tag names are unique per owner, not globally — two sessions must both be
            // able to have a tag called "unity" without colliding.
            e.HasIndex(t => new { t.OwnerKey, t.Name })
                .IsUnique();

            e.Property(t => t.Category)
                .HasMaxLength(32);

            e.HasQueryFilter(t => t.OwnerKey == CurrentOwnerKey);
        });

        modelBuilder.Entity<NoteTag>(e =>
        {
            e.HasKey(nt => new { nt.NoteId, nt.TagId });

            // Scoped through the note it belongs to, which is already owner-filtered.
            e.HasQueryFilter(nt => !nt.Note.IsDeleted && nt.Note.OwnerKey == CurrentOwnerKey);

            e.HasOne(nt => nt.Note)
                .WithMany(n => n.NoteTags)
                .HasForeignKey(nt => nt.NoteId);

            e.HasOne(nt => nt.Tag)
                .WithMany(t => t.NoteTags)
                .HasForeignKey(nt => nt.TagId);
        });

        // SQLite (used for fast unit tests) has no native DateTimeOffset type and can't
        // translate ORDER BY on one. Store it as a sortable long instead — Postgres keeps
        // its native representation since it handles DateTimeOffset natively.
        if (!Database.IsNpgsql())
        {
            var converter = new DateTimeOffsetToBinaryConverter();
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                    {
                        property.SetValueConverter(converter);
                    }
                }
            }
        }
    }
}
