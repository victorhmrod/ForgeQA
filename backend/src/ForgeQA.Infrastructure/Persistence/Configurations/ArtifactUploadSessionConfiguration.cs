using ForgeQA.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeQA.Infrastructure.Persistence.Configurations;

public class ArtifactUploadSessionConfiguration : IEntityTypeConfiguration<ArtifactUploadSession>
{
    public void Configure(EntityTypeBuilder<ArtifactUploadSession> builder)
    {
        builder.ToTable("ArtifactUploadSessions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.ProviderUploadId).IsRequired().HasMaxLength(ArtifactUploadSession.ProviderUploadIdMaxLength);
        builder.Property(s => s.PartSizeBytes).IsRequired();
        builder.Property(s => s.ExpectedParts).IsRequired();
        builder.Property(s => s.ExpiresAt).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();

        builder.HasOne<BuildArtifact>()
            .WithMany()
            .HasForeignKey(s => s.ArtifactId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.ArtifactId);
        builder.HasIndex(s => s.ExpiresAt);
    }
}
