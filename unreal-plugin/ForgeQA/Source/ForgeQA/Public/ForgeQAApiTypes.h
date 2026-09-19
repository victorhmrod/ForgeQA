#pragma once

#include "CoreMinimal.h"

/** A ForgeQA API failure: an HTTP-level or network-level error, never a raw JSON blob. */
struct FORGEQA_API FForgeQAApiError
{
    /** 0 when the request never reached the server (DNS/timeout/connection failure). */
    int32 StatusCode = 0;

    /** Short machine-oriented reason, e.g. "NetworkError", "Unauthorized", "NotFound". */
    FString Code;

    /** Human-readable message safe to surface in the Editor UI. */
    FString Message;

    bool IsSet() const { return !Message.IsEmpty(); }
};

struct FORGEQA_API FForgeQAAuthResult
{
    FString AccessToken;
    FString RefreshToken;
    FString UserDisplayName;
    FString UserEmail;
};

struct FORGEQA_API FForgeQAOrganizationSummary
{
    FGuid Id;
    FString Name;
    FString Slug;
    FString Role;
};

struct FORGEQA_API FForgeQAProjectSummary
{
    FGuid Id;
    FGuid OrganizationId;
    FString Name;
    FString Slug;
};

struct FORGEQA_API FForgeQABuildSummary
{
    FGuid Id;
    FGuid ProjectId;
    FString Name;
    FString Version;
    FString BuildNumber;
    FString Platform;
    FString Configuration;
    FString Branch;
    FString CommitSha;
    bool bArchived = false;

    /** Label suitable for a picker: prefers Name, falls back to "<version> (<buildNumber>)". */
    FString GetDisplayLabel() const
    {
        return Name.IsEmpty() ? FString::Printf(TEXT("%s (%s)"), *Version, *BuildNumber) : Name;
    }
};

/** Diagnostic context sent alongside a bug report. Every field is optional; omit anything unavailable. */
struct FORGEQA_API FForgeQABugEnvironment
{
    FString MapName;
    FString GameMode;
    FString Platform;
    FString EngineVersion;
    FString OsVersion;
    FString Cpu;
    FString Gpu;
    int64 MemoryBytes = 0;
    FString Locale;
};

struct FORGEQA_API FForgeQACreateBugReportRequest
{
    FGuid BuildId; // Invalid FGuid() means "no Build correlation".
    FString Title;
    FString Description;
    FString ReproductionSteps;
    FString Severity;    // "LOW" | "MEDIUM" | "HIGH" | "CRITICAL"
    FString Source;      // "UNREAL_RUNTIME" | "WEB" | "API"
    FGuid RuntimeSessionId;
    FForgeQABugEnvironment Environment;
};

struct FORGEQA_API FForgeQABugReportResult
{
    FGuid BugReportId;
};

struct FORGEQA_API FForgeQAInitiateAttachmentRequest
{
    FString Type; // "SCREENSHOT" | "LOG" | "OTHER"
    FString FileName;
    FString ContentType;
    int64 SizeBytes = 0;
};

struct FORGEQA_API FForgeQAInitiateAttachmentResult
{
    FGuid AttachmentId;
    FString UploadUrl;
};

/** Immutable snapshot captured once when a telemetry session starts. Deliberately does not
 * duplicate the Build's own Version/BuildNumber/Branch/CommitSha — the backend joins those from
 * the correlated Build. Every field is optional. */
struct FORGEQA_API FForgeQATelemetrySessionEnvironment
{
    FString MapName;
    FString GameMode;
    FString Platform;
    FString Configuration;
    FString EngineVersion;
    FString OsVersion;
    FString Locale;
};

struct FORGEQA_API FForgeQAStartTelemetrySessionRequest
{
    FGuid RuntimeSessionId;
    FGuid BuildId;
    FForgeQATelemetrySessionEnvironment Environment;
};

struct FORGEQA_API FForgeQATelemetrySessionResult
{
    FGuid Id;
    FGuid RuntimeSessionId;
};

/** One event queued for delivery. Properties travel as an already-serialized JSON object string —
 * built once at TrackEvent() time via FForgeQATelemetryProperties, never re-serialized per retry. */
struct FORGEQA_API FForgeQATelemetryEvent
{
    int32 SequenceNumber = 0;
    FString EventName;
    FDateTime ClientTimestamp;
    FString Category;
    FString PropertiesJson = TEXT("{}");
    FString MapName;
};

struct FORGEQA_API FForgeQAIngestTelemetryEventsResult
{
    int32 Accepted = 0;
    int32 Duplicates = 0;
};

/** Same accepted/duplicates shape as telemetry ingestion, kept as a distinct type since Performance
 * and Telemetry are separate ingestion pipelines with separate API client methods. */
struct FORGEQA_API FForgeQAIngestPerformanceSamplesResult
{
    int32 Accepted = 0;
    int32 Duplicates = 0;
};
