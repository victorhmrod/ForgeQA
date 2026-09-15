#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "ForgeQABuildContext.h"
#include "ForgeQASubsystem.generated.h"

/**
 * Resolves and exposes the current ForgeQA Build Context (ProjectId + BuildId + metadata) to
 * gameplay code, in C++ and Blueprint.
 *
 * Resolution never contacts the ForgeQA server: a packaged build must know its own identity
 * offline (no network, no ForgeQA API availability, isolated test environments). Precedence,
 * highest first:
 *
 *   1. Command-line override (-ForgeQAProjectId=<uuid> -ForgeQABuildId=<uuid>)
 *   2. Packaged ForgeQA build manifest (Content/ForgeQA/ForgeQABuild.json)
 *   3. Project Settings (ForgeQA Project ID only — Settings alone never supplies a BuildId)
 *
 * If no source yields a fully valid context, HasValidBuildContext() returns false and gameplay
 * code should treat ForgeQA as unlinked — this is the expected state in PIE without a Build bound,
 * and must never crash, assert, or spam the log.
 */
UCLASS()
class FORGEQA_API UForgeQASubsystem : public UGameInstanceSubsystem
{
    GENERATED_BODY()

public:
    virtual void Initialize(FSubsystemCollectionBase& Collection) override;

    /** True once Initialize() has determined ProjectId and BuildId are both valid GUIDs. */
    UFUNCTION(BlueprintPure, Category = "ForgeQA", DisplayName = "Is ForgeQA Build Linked")
    bool HasValidBuildContext() const { return BuildContext.IsValid(); }

    UFUNCTION(BlueprintPure, Category = "ForgeQA", DisplayName = "Get ForgeQA Build Context")
    const FForgeQABuildContext& GetBuildContext() const { return BuildContext; }

    UFUNCTION(BlueprintPure, Category = "ForgeQA", DisplayName = "Get ForgeQA Project ID")
    FGuid GetProjectId() const { return BuildContext.ProjectId; }

    UFUNCTION(BlueprintPure, Category = "ForgeQA", DisplayName = "Get ForgeQA Build ID")
    FGuid GetBuildId() const { return BuildContext.BuildId; }

    /**
     * A local correlation token generated once when this GameInstance starts (see Initialize())
     * and held for its entire lifetime — every Bug Report submitted during this play session
     * carries the same RuntimeSessionId. Not a server-side concept in M4; a future milestone may
     * turn it into a persisted TelemetrySession, but nothing here assumes that will happen.
     */
    UFUNCTION(BlueprintPure, Category = "ForgeQA", DisplayName = "Get ForgeQA Runtime Session ID")
    FGuid GetRuntimeSessionId() const { return RuntimeSessionId; }

    // Exposed as public static, stateless resolution steps so they can be exercised directly by
    // automation tests (see Private/Tests/ForgeQABuildContextPrecedenceTests.cpp) without needing
    // a live UGameInstance. Each returns false without side effects when its source is absent.

    /** Reads -ForgeQAProjectId=/-ForgeQABuildId= from the command line, if both are present and valid. */
    static bool TryResolveFromCommandLine(FForgeQABuildContext& OutContext);

    static bool TryResolveFromManifest(FForgeQABuildContext& OutContext);

    static bool TryResolveFromSettings(FForgeQABuildContext& OutContext);

private:
    UPROPERTY()
    FForgeQABuildContext BuildContext;

    UPROPERTY()
    FGuid RuntimeSessionId;
};
