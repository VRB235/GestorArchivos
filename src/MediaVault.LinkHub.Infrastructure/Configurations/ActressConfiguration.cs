using MediaVault.LinkHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaVault.LinkHub.Infrastructure.Configurations;

internal sealed class ActressConfiguration : IEntityTypeConfiguration<Actress>
{
    public void Configure(EntityTypeBuilder<Actress> builder)
    {
        builder.ToTable("Actresses");

        builder.HasKey(actress => actress.Id);

        builder.Property(actress => actress.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(actress => actress.SortOrder)
            .HasDefaultValue(0);

        builder.HasIndex(actress => actress.Name)
            .IsUnique();

        builder.HasIndex(actress => actress.SortOrder);
    }
}
