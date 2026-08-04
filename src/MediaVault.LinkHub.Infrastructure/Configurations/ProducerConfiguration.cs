using MediaVault.LinkHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaVault.LinkHub.Infrastructure.Configurations;

internal sealed class ProducerConfiguration : IEntityTypeConfiguration<Producer>
{
    public void Configure(EntityTypeBuilder<Producer> builder)
    {
        builder.ToTable("Producers");

        builder.HasKey(producer => producer.Id);

        builder.Property(producer => producer.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(producer => producer.SortOrder)
            .HasDefaultValue(0);

        builder.HasIndex(producer => producer.Name)
            .IsUnique();

        builder.HasIndex(producer => producer.SortOrder);
    }
}
