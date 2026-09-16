#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "ForgeQAApiTypes.h"
#include "ForgeQATelemetryTypes.h"
#include "ForgeQATelemetrySubsystem.generated.h"

class FForgeQAApiClient;
class FJsonObject;

/**
 * Runtime QA/developer telemetry: session lifecycle, event queuing, batching, and asynchronous
 * delivery with bounded retry. Separated from UForgeQABugReportingSubsystem — a different
 * responsibility with a different lifecycle (started once per GameInstance, not per user action) —
 * and from UForgeQASubsystem, which only resolves identity. Reuses UForgeQASubsystem's Build
 * Context and RuntimeSessionId, and FForgeQARuntimeCredentials for the Project API key; never
 * duplicates either.
 *
 * TrackEvent() is cheap and safe to call from gameplay-critical paths: it validates locally,
 * assigns a sequence number, enqueues, and returns — it never blocks on network I/O.
 */
UCLASS()
class FORGEQA_API UForgeQATelemetrySubsystem : public UGameInstanceSubsystem
{
    GENERATED_BODY()

public:
    virtual void Initialize(FSubsystemCollectionBase& Collection) override;
    virtual void Deinitialize() override;

    /** True only when telemetry is enabled in settings, a valid Build Context exists, and a
     * Project API key can be resolved. Does not guarantee the key actually holds TELEMETRY_WRITE —
     * the client cannot know server-side scope in advance; a scope failure surfaces as a dropped,
     * logged batch instead. */
    UFUNCTION(BlueprintPure, Category = "ForgeQA|Telemetry", DisplayName = "Is ForgeQA Telemetry Available")
    bool IsTelemetryAvailable() const;

    UFUNCTION(BlueprintPure, Category = "ForgeQA|Telemetry", DisplayName = "Is ForgeQA Telemetry Session Started")
    bool IsSessionStarted() const { return bSessionStarted; }

    UFUNCTION(BlueprintPure, Category = "ForgeQA|Telemetry", DisplayName = "Get ForgeQA Queued Event Count")
    int32 GetQueuedEventCount() const { return PendingEvents.Num(); }

    UFUNCTION(BlueprintPure, Category = "ForgeQA|Telemetry", DisplayName = "Get ForgeQA Dropped Event Count")
    int32 GetDroppedEventCount() const { return DroppedEventCount; }

    UFUNCTION(BlueprintPure, Category = "ForgeQA|Telemetry", DisplayName = "Was Last ForgeQA Telemetry Flush Successful")
    bool WasLastFlushSuccessful() const { return bLastFlushSucceeded; }

    /** Explicit start — normally called automatically from Initialize() when telemetry is
     * available, but exposed so a game can defer starting until e.g. a match actually begins. */
    UFUNCTION(BlueprintCallable, Category = "ForgeQA|Telemetry", DisplayName = "Start ForgeQA Telemetry")
    void StartTelemetry();

    /** Blueprint-friendly overload taking structured properties. */
    UFUNCTION(BlueprintCallable, Category = "ForgeQA|Telemetry", DisplayName = "Track ForgeQA Event")
    void TrackEvent(const FString& EventName, const FForgeQATelemetryProperties& Properties);

    /** Convenience alias for TrackEvent — a breadcrumb is just telemetry, not a separate model. */
    UFUNCTION(BlueprintCallable, Category = "ForgeQA|Telemetry", DisplayName = "Track ForgeQA Breadcrumb")
    void TrackBreadcrumb(const FString& Name, const FForgeQATelemetryProperties& Properties);

    /** C++-only overload accepting full JSON flexibility (nested objects/arrays), never exposed to
     * Blueprint — see FForgeQATelemetryProperties for why. */
    void TrackEvent(const FString& EventName, const TSharedPtr<FJsonObject>& Properties, const FString& Category = FString(), const FString& MapName = FString());

    /** Forces an immediate flush attempt instead of waiting for the next timer tick. A flush
     * already in flight is left to complete; this does not queue a second one (see bFlushInFlight). */
    UFUNCTION(BlueprintCallable, Category = "ForgeQA|Telemetry", DisplayName = "Flush ForgeQA Telemetry")
    void FlushTelemetry();

    /** Best-effort: flushes remaining events, then ends the session. Never blocks the calling
     * thread — both requests are fire-and-forget async, matching Unreal's HttpModule model. */
    UFUNCTION(BlueprintCallable, Category = "ForgeQA|Telemetry", DisplayName = "End ForgeQA Telemetry")
    void EndTelemetry();

private:
    TSharedPtr<FForgeQAApiClient> ApiClient;

    UPROPERTY()
    bool bSessionStarted = false;

    UPROPERTY()
    bool bFlushInFlight = false;

    UPROPERTY()
    bool bLastFlushSucceeded = true;

    UPROPERTY()
    int32 NextSequenceNumber = 1;

    UPROPERTY()
    int32 DroppedEventCount = 0;

    UPROPERTY()
    int32 RetryAttempt = 0;

    double LastOverflowLogTimeSeconds = -1000.0;

    FTimerHandle FlushTimerHandle;
    FTimerHandle RetryTimerHandle;

    TArray<FForgeQATelemetryEvent> PendingEvents;

    void HandleSessionStarted(bool bSuccess, const FForgeQATelemetrySessionResult& Result, const FForgeQAApiError& Error);
    void TryFlush();
    void HandleFlushComplete(TArray<FForgeQATelemetryEvent> AttemptedEvents, bool bSuccess, const FForgeQAIngestTelemetryEventsResult& Result, const FForgeQAApiError& Error);
    void ScheduleRetry(TArray<FForgeQATelemetryEvent> FailedEvents);
    static bool IsTransientFailure(const FForgeQAApiError& Error);

public:
    /** Mirrors the backend's event-name rule (see docs/telemetry.md): 1-128 characters, letters,
     * digits, '.', '_' or '-' only. Exposed as a pure static so it can be exercised directly by
     * automation tests without constructing a subsystem instance. */
    static bool IsValidEventName(const FString& EventName);
};
