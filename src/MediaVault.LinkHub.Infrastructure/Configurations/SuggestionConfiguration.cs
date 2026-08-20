using MediaVault.LinkHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaVault.LinkHub.Infrastructure.Configurations;

internal sealed class SuggestionConfiguration : IEntityTypeConfiguration<Suggestion>
{
    public void Configure(EntityTypeBuilder<Suggestion> builder)
    {
        builder.ToTable("Suggestions");

        builder.HasKey(suggestion => suggestion.Id);

        builder.Property(suggestion => suggestion.Texto)
            .IsRequired()
            .HasMaxLength(8000);

        builder.Property(suggestion => suggestion.Tipo)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(suggestion => suggestion.FechaCreacion)
            .IsRequired();

        builder.Property(suggestion => suggestion.Resuelto)
            .IsRequired();

        builder.HasIndex(suggestion => suggestion.FechaCreacion);
        builder.HasIndex(suggestion => suggestion.Resuelto);

        builder.HasMany(suggestion => suggestion.Attachments)
            .WithOne(attachment => attachment.Suggestion)
            .HasForeignKey(attachment => attachment.SuggestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
