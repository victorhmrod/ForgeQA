#pragma once

#include "CoreMinimal.h"
#include "ForgeQAApiTypes.h"
#include "Interfaces/IHttpRequest.h"

/**
 * Centralized, transport-only client for the ForgeQA HTTP API. Every request is asynchronous and
 * resolves on the Game Thread via its callback, matching Unreal's HttpModule delegate behavior —
 * callers must never block waiting for a response.
 *
 * This class knows the concrete ForgeQA DTO shapes it consumes (see ForgeQAApiTypes.h); it does
 * not pass raw FJsonObject instances back to callers.
 */
class FORGEQA_API FForgeQAApiClient
{
public:
    explicit FForgeQAApiClient(FString InApiBaseUrl);

    void SetApiBaseUrl(FString InApiBaseUrl) { ApiBaseUrl = MoveTemp(InApiBaseUrl); }
    const FString& GetApiBaseUrl() const { return ApiBaseUrl; }

    using FLoginCallback = TFunction<void(bool bSuccess, const FForgeQAAuthResult& Result, const FForgeQAApiError& Error)>;
    using FOrganizationsCallback = TFunction<void(bool bSuccess, const TArray<FForgeQAOrganizationSummary>& Organizations, const FForgeQAApiError& Error)>;
    using FProjectsCallback = TFunction<void(bool bSuccess, const TArray<FForgeQAProjectSummary>& Projects, const FForgeQAApiError& Error)>;
    using FBuildsCallback = TFunction<void(bool bSuccess, const TArray<FForgeQABuildSummary>& Builds, const FForgeQAApiError& Error)>;
    using FBuildDetailCallback = TFunction<void(bool bSuccess, const FForgeQABuildSummary& Build, const FForgeQAApiError& Error)>;

    /** POST /api/auth/login. AccessToken/RefreshToken in the result are editor-session-only. */
    void Login(const FString& Email, const FString& Password, FLoginCallback OnComplete);

    /** GET /api/organizations. */
    void GetOrganizations(const FString& AccessToken, FOrganizationsCallback OnComplete);

    /** GET /api/organizations/{organizationId}/projects. */
    void GetProjects(const FString& AccessToken, const FGuid& OrganizationId, FProjectsCallback OnComplete);

    /** GET /api/projects/{projectId}/builds?status=active (or "all" when bIncludeArchived). */
    void GetBuilds(const FString& AccessToken, const FGuid& ProjectId, bool bIncludeArchived, FBuildsCallback OnComplete);

    /** GET /api/projects/{projectId}/builds/{buildId}. Used to validate a Project/Build binding. */
    void GetBuildDetail(const FString& AccessToken, const FGuid& ProjectId, const FGuid& BuildId, FBuildDetailCallback OnComplete);

private:
    FString ApiBaseUrl;

    TSharedRef<IHttpRequest> CreateRequest(const FString& Verb, const FString& Path, const FString& AccessToken) const;
    static bool TryExtractError(const FHttpResponsePtr& Response, bool bConnectedSuccessfully, FForgeQAApiError& OutError);
};
