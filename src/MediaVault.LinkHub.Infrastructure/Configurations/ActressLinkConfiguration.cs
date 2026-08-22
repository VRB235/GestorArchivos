using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaVault.LinkHub.Infrastructure.Configurations;

internal sealed class ActressLinkConfiguration : IEntityTypeConfiguration<ActressLink>
{
    public void Configure(EntityTypeBuilder<ActressLink> builder)
    {
        builder.ToTable("ActressLinks");

        builder.HasKey(link => link.Id);

        builder.Property(link => link.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(link => link.Url)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(link => link.Action)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(link => link.Notes)
            .HasMaxLength(2000);

        builder.Property(link => link.ScrapeHintsJson)
            .HasColumnType("TEXT");

        builder.Property(link => link.ScraperKey)
            .HasMaxLength(80);

        builder.Property(link => link.SortOrder)
            .HasDefaultValue(0);

        builder.Property(link => link.CreatedAt)
            .IsRequired();

        builder.HasIndex(link => link.ActressId);
        builder.HasIndex(link => link.WebLinkId);
        builder.HasIndex(link => new { link.ActressId, link.SortOrder });

        builder.HasOne(link => link.Actress)
            .WithMany(actress => actress.Links)
            .HasForeignKey(link => link.ActressId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(link => link.WebLink)
            .WithMany()
            .HasForeignKey(link => link.WebLinkId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
