#pragma once

#include "CoreMinimal.h"
#include "Engine/DeveloperSettings.h"
#include "ForgeQASettings.generated.h"

/**
 * Project Settings > Plugins > ForgeQA.
 *
 * Two source-control behaviors are intentionally mixed in this single settings object:
 *
 *  - ApiBaseUrl and ProjectId are `defaultconfig`: they are written to Config/DefaultGame.ini,
 *    which is normally checked into source control. The ForgeQA Project a repository belongs to
 *    is long-lived team configuration, so it is appropriate to share it.
 *
 *  - DefaultBuildId is a plain `config` property: it is written to the per-user Saved/Config
 *    override, which is normally *not* checked in (see the project's default .gitignore for
 *    Saved/). A Build is rebuilt frequently and binding one developer's last-selected Build to
 *    the shared project config would create constant, meaningless source-control churn. The
 *    authoritative, packaged Build identity is the generated Build manifest
 *    (see FForgeQABuildManifest), not this field — DefaultBuildId only seeds the Editor's Build
 *    picker with the last selection and is never read by the runtime subsystem.
 */
UCLASS(Config = Game, DefaultConfig, meta = (DisplayName = "ForgeQA"))
class FORGEQA_API UForgeQASettings : public UDeveloperSettings
{
    GENERATED_BODY()

public:
    UForgeQASettings();

    /** Base URL of the ForgeQA API, e.g. http://localhost:5000. Trailing slashes are normalized. */
    UPROPERTY(Config, EditAnywhere, Category = "ForgeQA", meta = (DisplayName = "API Base URL"))
    FString ApiBaseUrl;

    /** Canonical ForgeQA Project this Unreal project is linked to. */
    UPROPERTY(Config, EditAnywhere, Category = "ForgeQA", meta = (DisplayName = "ForgeQA Project ID"))
    FGuid ProjectId;

    /** Last Build selected in the Editor. Per-developer only; not the runtime identity source. */
    UPROPERTY(Config, VisibleAnywhere, Category = "ForgeQA|Editor", meta = (DisplayName = "Last Selected Build ID"))
    FGuid DefaultBuildId;

    /**
     * DEVELOPMENT CONVENIENCE ONLY. A Project API key (see docs/bug-reporting.md) the runtime uses
     * to submit bug reports. Deliberately a plain `config` property (per-developer Saved/Config,
     * never committed) — never `defaultconfig`, and never written into the Build manifest.
     *
     * A packaged Shipping build should not rely on this field: inject the key via
     * -ForgeQAApiKey=<key> on the command line or the FORGEQA_API_KEY environment variable
     * instead (see UForgeQARuntimeCredentials), which a future CI pipeline can supply without
     * ever needing this settings value to be set at all.
     */
    UPROPERTY(Config, EditAnywhere, Category = "ForgeQA|Bug Reporting", meta = (DisplayName = "Runtime API Key (dev only)", PasswordField = true))
    FString DevelopmentRuntimeApiKey;

    /** Master switch for UForgeQATelemetrySubsystem. Telemetry is not compiled out of Shipping —
     * developers may want it from external playtests — this is a runtime opt-out only. */
    UPROPERTY(Config, EditAnywhere, Category = "ForgeQA|Telemetry", meta = (DisplayName = "Enable Telemetry"))
    bool bEnableTelemetry = true;

    UPROPERTY(Config, EditAnywhere, Category = "ForgeQA|Telemetry", meta = (DisplayName = "Flush Interval (seconds)", ClampMin = "1"))
    float TelemetryFlushIntervalSeconds = 5.0f;

    UPROPERTY(Config, EditAnywhere, Category = "ForgeQA|Telemetry", meta = (DisplayName = "Batch Size", ClampMin = "1"))
    int32 TelemetryBatchSize = 50;

    UPROPERTY(Config, EditAnywhere, Category = "ForgeQA|Telemetry", meta = (DisplayName = "Max Queued Events", ClampMin = "1"))
    int32 TelemetryMaxQueuedEvents = 1000;

    /** Returns ApiBaseUrl with exactly one trailing slash removed, so callers can safely append "/api/...". */
    FString GetNormalizedApiBaseUrl() const;
};
