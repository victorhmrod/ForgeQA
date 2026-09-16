using ForgeQA.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeQA.Infrastructure.Persistence.Configurations;

public class TelemetrySessionConfiguration : IEntityTypeConfiguration<TelemetrySession>
{
    public void Configure(EntityTypeBuilder<TelemetrySession> builder)
    {
        builder.ToTable("TelemetrySessions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.StartedAt).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.UpdatedAt).IsRequired();

        // Immutable per-session snapshot, embedded in the same row (never a separate table) — see
        // TelemetrySessionEnvironment for why this deliberately does not duplicate Build metadata.
        builder.OwnsOne(s => s.Environment, environment =>
        {
            environment.Property(e => e.MapName).HasColumnName("Environment_MapName").HasMaxLength(TelemetrySessionEnvironment.MapNameMaxLength);
            environment.Property(e => e.GameMode).HasColumnName("Environment_GameMode").HasMaxLength(TelemetrySessionEnvironment.GameModeMaxLength);
            environment.Property(e => e.Platform).HasColumnName("Environment_Platform").HasMaxLength(TelemetrySessionEnvironment.PlatformMaxLength);
            environment.Property(e => e.Configuration).HasColumnName("Environment_Configuration").HasMaxLength(TelemetrySessionEnvironment.ConfigurationMaxLength);
            environment.Property(e => e.EngineVersion).HasColumnName("Environment_EngineVersion").HasMaxLength(TelemetrySessionEnvironment.EngineVersionMaxLength);
            environment.Property(e => e.OsVersion).HasColumnName("Environment_OsVersion").HasMaxLength(TelemetrySessionEnvironment.OsVersionMaxLength);
            environment.Property(e => e.Locale).HasColumnName("Environment_Locale").HasMaxLength(TelemetrySessionEnvironment.LocaleMaxLength);
        });
        builder.Navigation(s => s.Environment).IsRequired();

        builder.HasMany(s => s.Events)
            .WithOne()
            .HasForeignKey(e => e.TelemetrySessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Metadata.FindNavigation(nameof(TelemetrySession.Events))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);

        // Telemetry must never be lost because a Build is later archived — a Build is never
        // hard-deleted, so RESTRICT is a documented safety net, not a behavior this schema relies
        // on triggering (same rationale as BugReport → Build).
        builder.HasOne<Build>()
            .WithMany()
            .HasForeignKey(s => s.BuildId)
            .OnDelete(DeleteBehavior.Restrict);

        // A runtime session must never accidentally create two TelemetrySession rows due to a
        // client retry — this is the backstop behind TelemetryService's idempotent session start.
        builder.HasIndex(s => new { s.ProjectId, s.RuntimeSessionId }).IsUnique();
        builder.HasIndex(s => new { s.ProjectId, s.StartedAt });
        builder.HasIndex(s => new { s.ProjectId, s.BuildId });
    }
}
