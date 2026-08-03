using MediaVault.LinkHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaVault.LinkHub.Infrastructure.Configurations;

internal sealed class VideoCategoryConfiguration : IEntityTypeConfiguration<VideoCategory>
{
    public void Configure(EntityTypeBuilder<VideoCategory> builder)
    {
        builder.ToTable("VideoCategories");

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(category => category.SortOrder)
            .HasDefaultValue(0);

        builder.HasIndex(category => category.Name)
            .IsUnique();

        builder.HasIndex(category => category.SortOrder);
    }
}
