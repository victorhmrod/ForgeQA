#pragma once

#include "CoreMinimal.h"
#include "ForgeQABugReportingTypes.generated.h"

UENUM(BlueprintType)
enum class EForgeQABugSeverity : uint8
{
    Low,
    Medium,
    High,
    Critical
};

/** Explicit state machine so UI code never has to infer progress from ad hoc booleans. */
UENUM(BlueprintType)
enum class EForgeQABugSubmissionState : uint8
{
    Idle,
    Capturing,
    SubmittingReport,
    UploadingScreenshot,
    Finalizing,
    Succeeded,
    Failed
};

/**
 * The Blueprint/C++-facing bug report request. ProjectId, BuildId, RuntimeSessionId, and the rest
 * of the captured environment are added automatically by UForgeQABugReportingSubsystem from the
 * active ForgeQA Build Context — callers only supply what a human actually typed.
 */
USTRUCT(BlueprintType)
struct FORGEQA_API FForgeQABugReportRequest
{
    GENERATED_BODY()

    UPROPERTY(BlueprintReadWrite, Category = "ForgeQA")
    FString Title;

    UPROPERTY(BlueprintReadWrite, Category = "ForgeQA")
    FString Description;

    UPROPERTY(BlueprintReadWrite, Category = "ForgeQA")
    FString ReproductionSteps;

    UPROPERTY(BlueprintReadWrite, Category = "ForgeQA")
    EForgeQABugSeverity Severity = EForgeQABugSeverity::Medium;
};
