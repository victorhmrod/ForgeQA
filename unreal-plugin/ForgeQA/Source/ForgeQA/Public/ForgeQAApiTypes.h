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
