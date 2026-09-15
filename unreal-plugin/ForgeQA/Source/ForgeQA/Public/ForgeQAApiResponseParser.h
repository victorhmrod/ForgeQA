#pragma once

#include "CoreMinimal.h"
#include "ForgeQAApiTypes.h"

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

    /** Parses an ASP.NET Core ProblemDetails body ({"title","status","detail"}) into a display message. */
    static void ParseProblemDetails(const FString& JsonBody, int32 StatusCode, FForgeQAApiError& OutError);

    static FForgeQABuildSummary ParseBuildSummaryObject(const TSharedPtr<FJsonObject>& Object);
};
