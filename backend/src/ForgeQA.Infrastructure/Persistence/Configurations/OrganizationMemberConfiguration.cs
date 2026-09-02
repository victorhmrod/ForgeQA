using ForgeQA.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeQA.Infrastructure.Persistence.Configurations;

public class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.ToTable("OrganizationMembers");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Role).IsRequired().HasConversion<string>().HasMaxLength(50);
        builder.Property(m => m.CreatedAt).IsRequired();

        builder.HasIndex(m => new { m.OrganizationId, m.UserId }).IsUnique();
    }
}
