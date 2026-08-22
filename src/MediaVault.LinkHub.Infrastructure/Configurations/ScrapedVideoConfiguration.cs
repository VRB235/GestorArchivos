using MediaVault.LinkHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaVault.LinkHub.Infrastructure.Configurations;

internal sealed class ScrapedVideoConfiguration : IEntityTypeConfiguration<ScrapedVideo>
{
    public void Configure(EntityTypeBuilder<ScrapedVideo> builder)
    {
        builder.ToTable("ScrapedVideos");

        builder.HasKey(video => video.Id);

        builder.Property(video => video.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(video => video.SourceUrl)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(video => video.ThumbnailUrl)
            .HasMaxLength(2048);

        builder.Property(video => video.PreviewUrl)
            .HasMaxLength(2048);

        builder.Property(video => video.IsNew)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(video => video.Code)
            .HasMaxLength(100);

        builder.Property(video => video.DurationText)
            .HasMaxLength(40);

        builder.Property(video => video.ScrapedAt)
            .IsRequired();

        builder.Property(video => video.ExtraJson)
            .HasColumnType("TEXT");

        builder.HasIndex(video => video.ActressLinkId);
        builder.HasIndex(video => video.ActressId);
        builder.HasIndex(video => video.SourceUrl);
        builder.HasIndex(video => new { video.ActressLinkId, video.SourceUrl });

        builder.HasOne(video => video.ActressLink)
            .WithMany(link => link.ScrapedVideos)
            .HasForeignKey(video => video.ActressLinkId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict evita múltiples rutas de cascade Actress→ScrapedVideo vs Actress→Link→ScrapedVideo.
        // Al borrar actriz, los links caen en cascade y arrastran ScrapedVideos.
        builder.HasOne(video => video.Actress)
            .WithMany(actress => actress.ScrapedVideos)
            .HasForeignKey(video => video.ActressId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
