using ForgeQA.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ForgeQA.Infrastructure.Persistence.Configurations;

public class TelemetryEventConfiguration : IEntityTypeConfiguration<TelemetryEvent>
{
    public void Configure(EntityTypeBuilder<TelemetryEvent> builder)
    {
        builder.ToTable("TelemetryEvents");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.EventName).IsRequired().HasMaxLength(TelemetryEvent.EventNameMaxLength);
        builder.Property(e => e.Category).HasMaxLength(TelemetryEvent.CategoryMaxLength);
        builder.Property(e => e.MapName).HasMaxLength(TelemetryEvent.MapNameMaxLength);
        builder.Property(e => e.ClientTimestamp).IsRequired();
        builder.Property(e => e.ReceivedAt).IsRequired();

        // Arbitrary structured developer instrumentation — jsonb, never an Entity-Attribute-Value
        // table and never a plain text/opaque column. Queried only by presence via the API layer in
        // M5 (no GIN index yet — add one only once a real query pattern needs it).
        builder.Property(e => e.PropertiesJson).IsRequired().HasColumnName("Properties").HasColumnType("jsonb");

        // Lets the backend/UI reconstruct true event order despite batching, retries, and client
        // clock drift, and makes a retried batch idempotent (a duplicate sequence number is treated
        // as already-accepted, never inserted twice).
        builder.HasIndex(e => new { e.TelemetrySessionId, e.SequenceNumber }).IsUnique();
        builder.HasIndex(e => new { e.TelemetrySessionId, e.EventName });
        builder.HasIndex(e => new { e.TelemetrySessionId, e.ClientTimestamp });
    }
}
