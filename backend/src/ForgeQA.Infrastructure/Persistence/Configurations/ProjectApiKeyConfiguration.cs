using ForgeQA.Domain.Entities;
using ForgeQA.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeQA.Infrastructure.Persistence.Configurations;

public class ProjectApiKeyConfiguration : IEntityTypeConfiguration<ProjectApiKey>
{
    public void Configure(EntityTypeBuilder<ProjectApiKey> builder)
    {
        builder.ToTable("ProjectApiKeys");
        builder.HasKey(k => k.Id);

        builder.Property(k => k.Name).IsRequired().HasMaxLength(ProjectApiKey.NameMaxLength);
        builder.Property(k => k.Prefix).IsRequired().HasMaxLength(ProjectApiKey.PrefixMaxLength);
        builder.Property(k => k.KeyHash).IsRequired().HasMaxLength(ProjectApiKey.KeyHashMaxLength);
        builder.Property(k => k.CreatedAt).IsRequired();

        var scopesComparer = new ValueComparer<IReadOnlyCollection<ProjectApiKeyScope>>(
            (a, b) => (a ?? Array.Empty<ProjectApiKeyScope>()).SequenceEqual(b ?? Array.Empty<ProjectApiKeyScope>()),
            scopes => scopes.Aggregate(0, (hash, scope) => HashCode.Combine(hash, scope)),
            scopes => scopes.ToList());

        builder.Property(k => k.Scopes)
            .HasConversion(
                scopes => string.Join(',', scopes.Select(s => s.ToString())),
                value => string.IsNullOrEmpty(value)
                    ? new List<ProjectApiKeyScope>()
                    : value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Enum.Parse<ProjectApiKeyScope>).ToList())
            .HasColumnName("Scopes")
            .HasMaxLength(500)
            .Metadata.SetValueComparer(scopesComparer);

        builder.HasIndex(k => k.ProjectId);
        builder.HasIndex(k => k.Prefix);
    }
}
