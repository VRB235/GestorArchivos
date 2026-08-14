using MediaVault.LinkHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaVault.LinkHub.Infrastructure.Configurations;

internal sealed class WebLinkConfiguration : IEntityTypeConfiguration<WebLink>
{
    public void Configure(EntityTypeBuilder<WebLink> builder)
    {
        builder.ToTable("WebLinks");

        builder.HasKey(link => link.Id);

        builder.Property(link => link.Nombre)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(link => link.Url)
            .IsRequired()
            .HasMaxLength(2048);

        builder.Property(link => link.LogoPath)
            .HasMaxLength(1024);

        builder.Property(link => link.LogoZoom)
            .IsRequired()
            .HasDefaultValue(1.0);

        builder.Property(link => link.LogoOffsetX)
            .IsRequired()
            .HasDefaultValue(0.0);

        builder.Property(link => link.LogoOffsetY)
            .IsRequired()
            .HasDefaultValue(0.0);

        builder.Property(link => link.Categoria)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(link => link.FechaCreacion)
            .IsRequired();

        builder.Property(link => link.FechaUltimaActualizacion)
            .IsRequired(false);

        builder.HasIndex(link => link.FechaUltimaActualizacion);

        builder.HasIndex(link => link.Url)
            .IsUnique();

        builder.HasIndex(link => link.Categoria);

        builder.HasMany(link => link.Producers)
            .WithMany(producer => producer.WebLinks)
            .UsingEntity(j => j.ToTable("WebLinkProducers"));
    }
}
