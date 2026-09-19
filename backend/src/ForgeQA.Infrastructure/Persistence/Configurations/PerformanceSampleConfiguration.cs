using ForgeQA.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeQA.Infrastructure.Persistence.Configurations;

public class PerformanceSampleConfiguration : IEntityTypeConfiguration<PerformanceSample>
{
    public void Configure(EntityTypeBuilder<PerformanceSample> builder)
    {
        builder.ToTable("PerformanceSamples");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.MapName).HasMaxLength(PerformanceSample.MapNameMaxLength);
        builder.Property(s => s.ClientTimestamp).IsRequired();
        builder.Property(s => s.ReceivedAt).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();

        builder.Property(s => s.Fps).IsRequired().HasColumnType("double precision");
        builder.Property(s => s.FrameTimeMs).IsRequired().HasColumnType("double precision");
        builder.Property(s => s.GameThreadTimeMs).HasColumnType("double precision");
        builder.Property(s => s.RenderThreadTimeMs).HasColumnType("double precision");
        builder.Property(s => s.GpuTimeMs).HasColumnType("double precision");
        builder.Property(s => s.CpuUtilizationPercent).HasColumnType("double precision");
        builder.Property(s => s.GpuUtilizationPercent).HasColumnType("double precision");
        builder.Property(s => s.PingMs).HasColumnType("double precision");
        builder.Property(s => s.MemoryUsedBytes).HasColumnType("bigint");
        builder.Property(s => s.MemoryAvailableBytes).HasColumnType("bigint");

        // Telemetry never has its own foreign-key navigation defined here — PerformanceSample is
        // owned by TelemetrySession (M5) via the same cascade-on-session-delete relationship as
        // TelemetryEvent; a session is the only way this table is ever queried.
        builder.HasOne<TelemetrySession>()
            .WithMany()
            .HasForeignKey(s => s.TelemetrySessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Idempotent batch retries (same rationale as TelemetryEvent) and efficient per-session
        // querying, without indexing every metric column.
        builder.HasIndex(s => new { s.TelemetrySessionId, s.SequenceNumber }).IsUnique();
        builder.HasIndex(s => new { s.TelemetrySessionId, s.ClientTimestamp });
        builder.HasIndex(s => new { s.TelemetrySessionId, s.MapName });
    }
}
