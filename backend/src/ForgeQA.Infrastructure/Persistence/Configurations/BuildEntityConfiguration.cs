using ForgeQA.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeQA.Infrastructure.Persistence.Configurations;

public class BuildEntityConfiguration : IEntityTypeConfiguration<Build>
{
    public void Configure(EntityTypeBuilder<Build> builder)
    {
        builder.ToTable("Builds");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Name).HasMaxLength(Build.NameMaxLength);
        builder.Property(b => b.Version).IsRequired().HasMaxLength(Build.VersionMaxLength);
        builder.Property(b => b.BuildNumber).IsRequired().HasMaxLength(Build.BuildNumberMaxLength);

        builder.Property(b => b.Platform).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.Configuration).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.Property(b => b.Branch).HasMaxLength(Build.BranchMaxLength);
        builder.Property(b => b.CommitSha).HasMaxLength(Build.CommitShaMaxLength);
        builder.Property(b => b.EngineVersion).HasMaxLength(Build.EngineVersionMaxLength);
        builder.Property(b => b.Changelog).HasMaxLength(Build.ChangelogMaxLength);

        builder.Property(b => b.CreatedByUserId).IsRequired();
        builder.Property(b => b.CreatedByDisplayName).IsRequired().HasMaxLength(256);

        builder.Property(b => b.CreatedAt).IsRequired();
        builder.Property(b => b.UpdatedAt).IsRequired();

        // Queried on nearly every list request (default active-only view, project scoping).
        builder.HasIndex(b => b.ProjectId);
        builder.HasIndex(b => new { b.ProjectId, b.CreatedAt });
        builder.HasIndex(b => new { b.ProjectId, b.ArchivedAt });
        builder.HasIndex(b => new { b.ProjectId, b.Platform });
        builder.HasIndex(b => new { b.ProjectId, b.Configuration });

        // Logical identity: prevents accidental duplicate registration of the same build.
        builder.HasIndex(b => new { b.ProjectId, b.BuildNumber, b.Platform, b.Configuration }).IsUnique();
    }
}
