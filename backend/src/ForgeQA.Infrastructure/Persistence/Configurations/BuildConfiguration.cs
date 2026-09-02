using ForgeQA.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeQA.Infrastructure.Persistence.Configurations;

public class BuildConfiguration : IEntityTypeConfiguration<Build>
{
    public void Configure(EntityTypeBuilder<Build> builder)
    {
        builder.ToTable("Builds");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Version).IsRequired().HasMaxLength(100);
        builder.Property(b => b.Platform).IsRequired().HasMaxLength(100);
        builder.Property(b => b.Configuration).IsRequired().HasMaxLength(100);
        builder.Property(b => b.Branch).HasMaxLength(200);
        builder.Property(b => b.CommitHash).HasMaxLength(100);
        builder.Property(b => b.EngineVersion).HasMaxLength(50);
        builder.Property(b => b.CreatedAt).IsRequired();

        builder.HasIndex(b => b.ProjectId);
    }
}
