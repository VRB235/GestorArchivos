using MediaVault.LinkHub.Domain.Entities;
using MediaVault.LinkHub.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaVault.LinkHub.Infrastructure.Configurations;

internal sealed class ProducerLinkConfiguration : IEntityTypeConfiguration<ProducerLink>
{
    public void Configure(EntityTypeBuilder<ProducerLink> builder)
    {
        builder.ToTable("ProducerLinks");

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

        builder.Property(link => link.SortOrder)
            .HasDefaultValue(0);

        builder.Property(link => link.CreatedAt)
            .IsRequired();

        builder.HasIndex(link => link.ProducerId);
        builder.HasIndex(link => link.WebLinkId);
        builder.HasIndex(link => new { link.ProducerId, link.SortOrder });

        builder.HasOne(link => link.Producer)
            .WithMany(producer => producer.Links)
            .HasForeignKey(link => link.ProducerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(link => link.WebLink)
            .WithMany()
            .HasForeignKey(link => link.WebLinkId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
