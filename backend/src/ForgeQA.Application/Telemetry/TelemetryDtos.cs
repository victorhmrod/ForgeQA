using System.Text.Json;
using ForgeQA.Domain.Enums;

namespace ForgeQA.Application.Telemetry;

public record TelemetrySessionEnvironmentDto(
    string? MapName,
    string? GameMode,
    string? Platform,
    string? Configuration,
    string? EngineVersion,
    string? OsVersion,
    string? Locale);

public record StartTelemetrySessionRequest(Guid RuntimeSessionId, Guid BuildId, TelemetrySessionEnvironmentDto? Environment);

public record TelemetryBuildSummaryResponse(Guid Id, string Version, string BuildNumber, BuildPlatform Platform, BuildConfiguration Configuration);

public record TelemetrySessionResponse(
    Guid Id,
    Guid RuntimeSessionId,
    TelemetryBuildSummaryResponse Build,
    DateTime StartedAt,
    DateTime? EndedAt,
    DateTime? LastEventAt,
    int EventCount,
    TelemetrySessionEnvironmentDto Environment);

public record TelemetrySessionListItemResponse(
    Guid Id,
    Guid RuntimeSessionId,
    TelemetryBuildSummaryResponse Build,
    DateTime StartedAt,
    DateTime? EndedAt,
    DateTime? LastEventAt,
    int EventCount,
    string? Platform,
    string? MapName);

public record ListTelemetrySessionsRequest(
    int Page,
    int PageSize,
    Guid? BuildId,
    DateTime? From,
    DateTime? To,
    Guid? RuntimeSessionId,
    bool? ActiveOnly);

/// <summary>One event within an ingestion batch. <c>Properties</c> is bound directly from the
/// request JSON as a <see cref="JsonElement"/> — the service re-serializes it to canonical text for
/// storage rather than the API layer manipulating raw JSON strings.</summary>
public record TelemetryEventRequest(
    int SequenceNumber,
    string EventName,
    DateTime ClientTimestamp,
    string? Category,
    JsonElement? Properties,
    string? MapName);

public record IngestTelemetryEventsRequest(IReadOnlyList<TelemetryEventRequest> Events);

public record IngestTelemetryEventsResponse(int Accepted, int Duplicates);

public record TelemetryEventResponse(
    Guid Id,
    int SequenceNumber,
    string EventName,
    DateTime ClientTimestamp,
    DateTime ReceivedAt,
    string? Category,
    JsonElement Properties,
    string? MapName);

public record ListTelemetryEventsRequest(int Page, int PageSize, string? EventName);
