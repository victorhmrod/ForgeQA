#pragma once

#include "CoreMinimal.h"
#include "ForgeQABuildContext.generated.h"

/**
 * Identifies exactly which ForgeQA Project and Build a running instance of the game belongs to.
 *
 * ProjectId and BuildId are the canonical identity (ForgeQA UUIDs). Every other field is
 * descriptive metadata copied from the ForgeQA Build Registry at manifest-generation time and
 * must never be used as a substitute identity for correlating data with ForgeQA.
 */
USTRUCT(BlueprintType)
struct FORGEQA_API FForgeQABuildContext
{
    GENERATED_BODY()

    UPROPERTY(BlueprintReadOnly, Category = "ForgeQA")
    FGuid ProjectId;

    UPROPERTY(BlueprintReadOnly, Category = "ForgeQA")
    FGuid BuildId;

    UPROPERTY(BlueprintReadOnly, Category = "ForgeQA")
    FString Version;

    UPROPERTY(BlueprintReadOnly, Category = "ForgeQA")
    FString BuildNumber;

    UPROPERTY(BlueprintReadOnly, Category = "ForgeQA")
    FString Platform;

    UPROPERTY(BlueprintReadOnly, Category = "ForgeQA")
    FString Configuration;

    UPROPERTY(BlueprintReadOnly, Category = "ForgeQA")
    FString Branch;

    UPROPERTY(BlueprintReadOnly, Category = "ForgeQA")
    FString CommitSha;

    /** True when both ProjectId and BuildId are non-zero GUIDs. Metadata is not required. */
    bool IsValid() const
    {
        return ProjectId.IsValid() && BuildId.IsValid();
    }
};
