using ForgeQA.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeQA.Infrastructure.Persistence.Configurations;

public class BugAttachmentConfiguration : IEntityTypeConfiguration<BugAttachment>
{
    public void Configure(EntityTypeBuilder<BugAttachment> builder)
    {
        builder.ToTable("BugAttachments");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Type).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.FileName).IsRequired().HasMaxLength(BugAttachment.FileNameMaxLength);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(BugAttachment.ContentTypeMaxLength);
        builder.Property(a => a.StorageObjectKey).IsRequired().HasMaxLength(BugAttachment.StorageObjectKeyMaxLength);
        builder.Property(a => a.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.CreatedAt).IsRequired();

        builder.HasIndex(a => a.BugReportId);
    }
}
