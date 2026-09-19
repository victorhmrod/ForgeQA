#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "ForgeQAApiTypes.h"
#include "ForgeQAPerformanceTypes.h"
#include "ForgeQAPerformanceSubsystem.generated.h"

class FForgeQAApiClient;

/**
 * Runtime performance monitoring: periodic FPS/frame-time/memory (and, where reliably available,
 * thread/GPU timing) sampling, queuing, batching, and asynchronous delivery with bounded retry.
 * A separate UGameInstanceSubsystem from both UForgeQASubsystem (identity only) and
 * UForgeQATelemetrySubsystem (event telemetry only) — a different responsibility (continuous
 * lightweight sampling) with its own queue/lifecycle. Reuses UForgeQASubsystem's Build Context and
 * RuntimeSessionId and FForgeQARuntimeCredentials for the Project API key; never re-parses the
 * Build manifest and never introduces a second identity/credential path.
 *
 * This is not a profiler: it samples once per configured interval (default 1s), never per frame,
 * and never performs per-function/per-draw-call instrumentation. See docs/performance.md.
 */
UCLASS()
class FORGEQA_API UForgeQAPerformanceSubsystem : public UGameInstanceSubsystem
{
    GENERATED_BODY()

public:
    virtual void Initialize(FSubsystemCollectionBase& Collection) override;
    virtual void Deinitialize() override;

    /** True only when performance monitoring is enabled in settings, a valid Build Context exists,
     * and a Project API key can be resolved. Does not guarantee the key holds PERFORMANCE_WRITE or
     * that a TelemetrySession has been started — a rejection there surfaces as a dropped, logged
     * batch instead. */
    UFUNCTION(BlueprintPure, Category = "ForgeQA|Performance", DisplayName = "Is ForgeQA Performance Monitoring Active")
    bool IsPerformanceMonitoringActive() const { return bSamplingActive; }

    UFUNCTION(BlueprintPure, Category = "ForgeQA|Performance", DisplayName = "Get ForgeQA Queued Sample Count")
    int32 GetQueuedSampleCount() const { return PendingSamples.Num(); }

    UFUNCTION(BlueprintPure, Category = "ForgeQA|Performance", DisplayName = "Get ForgeQA Dropped Sample Count")
    int32 GetDroppedSampleCount() const { return DroppedSampleCount; }

    UFUNCTION(BlueprintPure, Category = "ForgeQA|Performance", DisplayName = "Get Current ForgeQA Performance Sample")
    const FForgeQAPerformanceSample& GetLastSample() const { return LastSample; }

    UFUNCTION(BlueprintPure, Category = "ForgeQA|Performance", DisplayName = "Was Last ForgeQA Performance Flush Successful")
    bool WasLastFlushSuccessful() const { return bLastFlushSucceeded; }

    /** Explicit start — normally called automatically from Initialize() when monitoring is
     * available. Sampling only produces useful server-side data once the Telemetry session this
     * Build/RuntimeSession pair belongs to has actually started (see docs/performance.md); this
     * subsystem does not create or wait on that session itself. */
    UFUNCTION(BlueprintCallable, Category = "ForgeQA|Performance", DisplayName = "Start ForgeQA Performance Monitoring")
    void StartPerformanceMonitoring();

    /** Forces an immediate flush attempt instead of waiting for the next batch trigger. */
    UFUNCTION(BlueprintCallable, Category = "ForgeQA|Performance", DisplayName = "Flush ForgeQA Performance")
    void FlushPerformance();

    /** Stops sampling and flushes remaining queued samples. Never blocks the calling thread. */
    UFUNCTION(BlueprintCallable, Category = "ForgeQA|Performance", DisplayName = "Stop ForgeQA Performance Monitoring")
    void StopPerformanceMonitoring();

private:
    TSharedPtr<FForgeQAApiClient> ApiClient;

    UPROPERTY()
    bool bSamplingActive = false;

    UPROPERTY()
    bool bFlushInFlight = false;

    UPROPERTY()
    bool bLastFlushSucceeded = true;

    UPROPERTY()
    int32 NextSequenceNumber = 1;

    UPROPERTY()
    int32 DroppedSampleCount = 0;

    UPROPERTY()
    int32 RetryAttempt = 0;

    UPROPERTY()
    FForgeQAPerformanceSample LastSample;

    double LastOverflowLogTimeSeconds = -1000.0;

    // Per-frame accumulators for the current sampling interval — reset on every CollectSample().
    int32 FrameCountThisInterval = 0;
    double AccumulatedDeltaTimeThisInterval = 0.0;
    FDelegateHandle EndFrameDelegateHandle;

    FTimerHandle SampleTimerHandle;
    FTimerHandle FlushTimerHandle;
    FTimerHandle RetryTimerHandle;

    TArray<FForgeQAPerformanceSample> PendingSamples;

    void HandleEndFrame();
    void CollectSample();
    void TryFlush();
    void HandleFlushComplete(TArray<FForgeQAPerformanceSample> AttemptedSamples, bool bSuccess, const FForgeQAIngestPerformanceSamplesResult& Result, const FForgeQAApiError& Error);
    void ScheduleRetry(TArray<FForgeQAPerformanceSample> FailedSamples);
    bool IsAvailable() const;
};
