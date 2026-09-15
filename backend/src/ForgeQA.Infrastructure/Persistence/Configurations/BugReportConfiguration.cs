using ForgeQA.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeQA.Infrastructure.Persistence.Configurations;

public class BugReportConfiguration : IEntityTypeConfiguration<BugReport>
{
    public void Configure(EntityTypeBuilder<BugReport> builder)
    {
        builder.ToTable("BugReports");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Title).IsRequired().HasMaxLength(BugReport.TitleMaxLength);
        builder.Property(b => b.Description).HasMaxLength(BugReport.DescriptionMaxLength);
        builder.Property(b => b.ReproductionSteps).HasMaxLength(BugReport.ReproductionStepsMaxLength);
        builder.Property(b => b.ReporterDisplayName).HasMaxLength(BugReport.ReporterDisplayNameMaxLength);

        builder.Property(b => b.Severity).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.Status).IsRequired().HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.Source).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.Property(b => b.CreatedAt).IsRequired();
        builder.Property(b => b.UpdatedAt).IsRequired();

        // Historical diagnostic context: a value object embedded in the same row, never a
        // separate table — there is exactly one per BugReport and it is never queried on its own.
        builder.OwnsOne(b => b.Environment, environment =>
        {
            environment.Property(e => e.MapName).HasColumnName("Environment_MapName").HasMaxLength(BugEnvironment.MapNameMaxLength);
            environment.Property(e => e.GameMode).HasColumnName("Environment_GameMode").HasMaxLength(BugEnvironment.GameModeMaxLength);
            environment.Property(e => e.Platform).HasColumnName("Environment_Platform").HasMaxLength(BugEnvironment.PlatformMaxLength);
            environment.Property(e => e.EngineVersion).HasColumnName("Environment_EngineVersion").HasMaxLength(BugEnvironment.EngineVersionMaxLength);
            environment.Property(e => e.OsVersion).HasColumnName("Environment_OsVersion").HasMaxLength(BugEnvironment.OsVersionMaxLength);
            environment.Property(e => e.Cpu).HasColumnName("Environment_Cpu").HasMaxLength(BugEnvironment.CpuMaxLength);
            environment.Property(e => e.Gpu).HasColumnName("Environment_Gpu").HasMaxLength(BugEnvironment.GpuMaxLength);
            environment.Property(e => e.MemoryBytes).HasColumnName("Environment_MemoryBytes");
            environment.Property(e => e.Locale).HasColumnName("Environment_Locale").HasMaxLength(BugEnvironment.LocaleMaxLength);
        });
        builder.Navigation(b => b.Environment).IsRequired();

        builder.HasMany(b => b.Attachments)
            .WithOne()
            .HasForeignKey(a => a.BugReportId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(BugReport.Attachments))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Historical QA reports must never disappear because a Build is later archived or its
        // metadata changes — a Build is never hard-deleted, so RESTRICT is a documented no-op
        // safety net rather than a behavior this schema relies on triggering.
        builder.HasOne<Build>()
            .WithMany()
            .HasForeignKey(b => b.BuildId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => new { b.ProjectId, b.CreatedAt });
        builder.HasIndex(b => new { b.ProjectId, b.Status });
        builder.HasIndex(b => new { b.ProjectId, b.Severity });
        builder.HasIndex(b => new { b.ProjectId, b.BuildId });
        builder.HasIndex(b => new { b.ProjectId, b.Source });
    }
}
