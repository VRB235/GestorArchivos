using MediaVault.LinkHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaVault.LinkHub.Infrastructure.Configurations;

internal sealed class MediaFileConfiguration : IEntityTypeConfiguration<MediaFile>
{
    public void Configure(EntityTypeBuilder<MediaFile> builder)
    {
        builder.ToTable("MediaFiles");

        builder.HasKey(file => file.Id);

        builder.Property(file => file.Path)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(file => file.Name)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(file => file.Extension)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(file => file.VecesAbierto)
            .HasDefaultValue(0);

        builder.Property(file => file.RankingCalidad)
            .HasDefaultValue(0.0);

        builder.Property(file => file.RankingContenido)
            .HasDefaultValue(0.0);

        builder.Property(file => file.RankingGusto)
            .HasDefaultValue(0.0);

        builder.Property(file => file.LastOpenedAt);

        builder.HasMany(file => file.Categories)
            .WithMany(category => category.MediaFiles)
            .UsingEntity(j => j.ToTable("MediaFileCategories"));

        builder.HasMany(file => file.Actresses)
            .WithMany(actress => actress.MediaFiles)
            .UsingEntity(j => j.ToTable("MediaFileActresses"));

        builder.HasMany(file => file.Producers)
            .WithMany(producer => producer.MediaFiles)
            .UsingEntity(j => j.ToTable("MediaFileProducers"));

        builder.HasIndex(file => file.Path)
            .IsUnique();

        builder.HasIndex(file => file.VecesAbierto);

        builder.HasIndex(file => file.Extension);
    }
}
