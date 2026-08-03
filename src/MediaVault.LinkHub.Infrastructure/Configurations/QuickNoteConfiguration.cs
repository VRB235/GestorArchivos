using MediaVault.LinkHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaVault.LinkHub.Infrastructure.Configurations;

internal sealed class QuickNoteConfiguration : IEntityTypeConfiguration<QuickNote>
{
    public void Configure(EntityTypeBuilder<QuickNote> builder)
    {
        builder.ToTable("QuickNotes");

        builder.HasKey(note => note.Id);

        builder.Property(note => note.Contenido)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(note => note.FechaCreacion)
            .IsRequired();

        builder.HasIndex(note => note.FechaCreacion);
    }
}
