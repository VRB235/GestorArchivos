using MediaVault.LinkHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaVault.LinkHub.Infrastructure.Configurations;

internal sealed class MediaFileThumbnailConfiguration : IEntityTypeConfiguration<MediaFileThumbnail>
{
    public void Configure(EntityTypeBuilder<MediaFileThumbnail> builder)
    {
        builder.ToTable("MediaFileThumbnails");

        builder.HasKey(thumbnail => thumbnail.Id);

        builder.Property(thumbnail => thumbnail.ImagePath)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(thumbnail => thumbnail.SortOrder)
            .HasDefaultValue(0);

        builder.HasOne(thumbnail => thumbnail.MediaFile)
            .WithMany(file => file.Thumbnails)
            .HasForeignKey(thumbnail => thumbnail.MediaFileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(thumbnail => thumbnail.MediaFileId);

        builder.HasIndex(thumbnail => new { thumbnail.MediaFileId, thumbnail.ImagePath })
            .IsUnique();
    }
}
