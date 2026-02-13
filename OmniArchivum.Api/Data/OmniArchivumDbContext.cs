using Microsoft.EntityFrameworkCore;
using OmniArchivum.Api.Models.Entities;

namespace OmniArchivum.Api.Data;

public class OmniArchivumDbContext : DbContext
{
    public OmniArchivumDbContext(DbContextOptions<OmniArchivumDbContext> options) : base(options) { }

    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Note>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.BodyMarkdown).IsRequired();
        });
    }
}
