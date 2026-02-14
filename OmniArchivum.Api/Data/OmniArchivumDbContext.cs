using Microsoft.EntityFrameworkCore;
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

            e.HasGeneratedTsVectorColumn(
                    n => n.SearchVector,
                    "english",
                    n => new { n.Title, n.BodyMarkdown });

            e.HasIndex(n => n.SearchVector)
                .HasMethod("GIN");
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

    }
}
