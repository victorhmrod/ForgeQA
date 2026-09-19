#pragma once

#include "CoreMinimal.h"
#include "ForgeQAApiTypes.h"
#include "ForgeQAPerformanceTypes.h"

class FJsonObject;

/**
 * Pure JSON <-> DTO parsing for the ForgeQA API responses this plugin consumes, kept separate from
 * FForgeQAApiClient's transport/HTTP concerns so it can be unit-tested with literal JSON strings
 * (see Private/Tests/ForgeQAApiResponseParserTests.cpp) without a live HTTP server.
 */
class FORGEQA_API FForgeQAApiResponseParser
{
public:
    static bool TryParseAuthResult(const FString& JsonBody, FForgeQAAuthResult& OutResult);
    static bool TryParseOrganizations(const FString& JsonBody, TArray<FForgeQAOrganizationSummary>& OutOrganizations);
    static bool TryParseProjects(const FString& JsonBody, const FGuid& OrganizationId, TArray<FForgeQAProjectSummary>& OutProjects);
    static bool TryParsePagedBuilds(const FString& JsonBody, TArray<FForgeQABuildSummary>& OutBuilds);
    static bool TryParseBuildDetail(const FString& JsonBody, FForgeQABuildSummary& OutBuild);
    static bool TryParseBugReportResult(const FString& JsonBody, FForgeQABugReportResult& OutResult);
    static bool TryParseInitiateAttachmentResult(const FString& JsonBody, FForgeQAInitiateAttachmentResult& OutResult);
    static FString SerializeCreateBugReportRequest(const FForgeQACreateBugReportRequest& Request);
    static FString SerializeInitiateAttachmentRequest(const FForgeQAInitiateAttachmentRequest& Request);
    static FString SerializeStartTelemetrySessionRequest(const FForgeQAStartTelemetrySessionRequest& Request);
    static FString SerializeTelemetryEventsBatch(const TArray<FForgeQATelemetryEvent>& Events);
    static bool TryParseTelemetrySessionResult(const FString& JsonBody, FForgeQATelemetrySessionResult& OutResult);
    static bool TryParseIngestTelemetryEventsResult(const FString& JsonBody, FForgeQAIngestTelemetryEventsResult& OutResult);
    static FString SerializePerformanceSamplesBatch(const TArray<FForgeQAPerformanceSample>& Samples);
    static bool TryParseIngestPerformanceSamplesResult(const FString& JsonBody, FForgeQAIngestPerformanceSamplesResult& OutResult);

    /** Parses an ASP.NET Core ProblemDetails body ({"title","status","detail"}) into a display message. */
    static void ParseProblemDetails(const FString& JsonBody, int32 StatusCode, FForgeQAApiError& OutError);

    static FForgeQABuildSummary ParseBuildSummaryObject(const TSharedPtr<FJsonObject>& Object);
};
