using MediaVault.LinkHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaVault.LinkHub.Infrastructure.Configurations;

internal sealed class VideoDownloadConfiguration : IEntityTypeConfiguration<VideoDownload>
{
    public void Configure(EntityTypeBuilder<VideoDownload> builder)
    {
        builder.ToTable("VideoDownloads");

        builder.HasKey(download => download.Id);

        builder.Property(download => download.SourceUrl)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(download => download.DisplayName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(download => download.Status)
            .IsRequired();

        builder.Property(download => download.LocalPath)
            .HasMaxLength(1000);

        builder.Property(download => download.ErrorMessage)
            .HasMaxLength(2000);

        builder.Property(download => download.CreatedAt)
            .IsRequired();

        builder.HasIndex(download => download.CreatedAt);
        builder.HasIndex(download => download.Status);
    }
}
