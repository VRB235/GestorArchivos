using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Infrastructure.Configurations;
using Microsoft.EntityFrameworkCore;

namespace MediaVault.LinkHub.Infrastructure.Data;

/// <summary>
/// Contexto principal de Entity Framework Core para MediaVault &amp; LinkHub.
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public DbSet<WebLink> WebLinks => Set<WebLink>();

    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();

    public DbSet<QuickNote> QuickNotes => Set<QuickNote>();

    public DbSet<Suggestion> Suggestions => Set<Suggestion>();

    public DbSet<SuggestionAttachment> SuggestionAttachments => Set<SuggestionAttachment>();

    public DbSet<VideoCategory> VideoCategories => Set<VideoCategory>();

    public DbSet<Actress> Actresses => Set<Actress>();

    public DbSet<Producer> Producers => Set<Producer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new WebLinkConfiguration());
        modelBuilder.ApplyConfiguration(new MediaFileConfiguration());
        modelBuilder.ApplyConfiguration(new QuickNoteConfiguration());
        modelBuilder.ApplyConfiguration(new SuggestionConfiguration());
        modelBuilder.ApplyConfiguration(new SuggestionAttachmentConfiguration());
        modelBuilder.ApplyConfiguration(new VideoCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new ActressConfiguration());
        modelBuilder.ApplyConfiguration(new ProducerConfiguration());
    }

    /// <summary>
    /// Aplica migraciones pendientes y garantiza que el esquema exista.
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
