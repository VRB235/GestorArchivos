using MediaVault.LinkHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MediaVault.LinkHub.Infrastructure.Configurations;

internal sealed class SuggestionAttachmentConfiguration : IEntityTypeConfiguration<SuggestionAttachment>
{
    public void Configure(EntityTypeBuilder<SuggestionAttachment> builder)
    {
        builder.ToTable("SuggestionAttachments");

        builder.HasKey(attachment => attachment.Id);

        builder.Property(attachment => attachment.FilePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(attachment => attachment.OriginalFileName)
            .IsRequired()
            .HasMaxLength(260);

        builder.Property(attachment => attachment.FechaCreacion)
            .IsRequired();

        builder.HasIndex(attachment => attachment.SuggestionId);
    }
}
