using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using OmniArchivum.Api.Models.Entities;

namespace OmniArchivum.Api.Data;

public class OmniArchivumDbContext : DbContext
{
    public OmniArchivumDbContext(DbContextOptions<OmniArchivumDbContext> options)
        : base(options) { }

    public DbSet<Note> Notes => Set<Note>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<NoteTag> NoteTags => Set<NoteTag>();


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

            e.HasQueryFilter(n => !n.IsDeleted);

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

            // Prevent duplicates
            e.HasIndex(t => t.Name)
                .IsUnique();

            e.Property(t => t.Category)
                .HasMaxLength(32);
        });

        modelBuilder.Entity<NoteTag>(e =>
        {
            e.HasKey(nt => new { nt.NoteId, nt.TagId });

            e.HasQueryFilter(nt => !nt.Note.IsDeleted);

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
