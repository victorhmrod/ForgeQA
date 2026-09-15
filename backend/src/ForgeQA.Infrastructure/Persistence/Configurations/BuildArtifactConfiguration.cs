using ForgeQA.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeQA.Infrastructure.Persistence.Configurations;

public class BuildArtifactConfiguration : IEntityTypeConfiguration<BuildArtifact>
{
    public void Configure(EntityTypeBuilder<BuildArtifact> builder)
    {
        builder.ToTable("BuildArtifacts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.FileName).IsRequired().HasMaxLength(BuildArtifact.FileNameMaxLength);
        builder.Property(a => a.DisplayName).IsRequired().HasMaxLength(BuildArtifact.DisplayNameMaxLength);
        builder.Property(a => a.ArtifactType).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(BuildArtifact.ContentTypeMaxLength);
        builder.Property(a => a.SizeBytes).IsRequired();
        builder.Property(a => a.Sha256).HasMaxLength(BuildArtifact.Sha256Length);
        builder.Property(a => a.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.StorageObjectKey).IsRequired().HasMaxLength(BuildArtifact.StorageObjectKeyMaxLength);
        builder.Property(a => a.CreatedByUserId).IsRequired();
        builder.Property(a => a.CreatedByDisplayName).IsRequired().HasMaxLength(256);
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.UpdatedAt).IsRequired();

        builder.HasOne<Build>()
            .WithMany()
            .HasForeignKey(a => a.BuildId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => a.BuildId);
        builder.HasIndex(a => new { a.BuildId, a.CreatedAt });
        builder.HasIndex(a => new { a.BuildId, a.Status });
        builder.HasIndex(a => a.StorageObjectKey).IsUnique();
    }
}
