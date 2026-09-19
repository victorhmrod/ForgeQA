#include "ForgeQAPerformanceSubsystem.h"
#include "ForgeQAApiClient.h"
#include "ForgeQASettings.h"
#include "ForgeQASubsystem.h"
#include "ForgeQARuntimeCredentials.h"
#include "ForgeQARetryPolicy.h"
#include "ForgeQALog.h"
#include "Engine/GameInstance.h"
#include "Engine/Engine.h"
#include "TimerManager.h"
#include "HAL/PlatformMemory.h"
#include "Misc/CoreDelegates.h"
#include "Misc/App.h"

namespace
{
    // GGameThreadTime / GRenderThreadTime / GGPUFrameTime are the same engine-maintained globals
    // (microseconds) that back the built-in "stat unit" HUD — updated every frame by the engine
    // regardless of whether that overlay is visible, so reading them here adds no extra
    // instrumentation cost. This is the lightest-weight source of thread timing UE 5.8 exposes;
    // if a future engine version renames/removes them, these three fields simply need to go back
    // to unavailable (bHasGameThreadTime = false etc.) rather than faking a value.
    bool TryGetEngineThreadTimingsMs(float& OutGameThreadMs, float& OutRenderThreadMs, float& OutGpuMs)
    {
        extern ENGINE_API uint32 GGameThreadTime;
        extern ENGINE_API uint32 GRenderThreadTime;
        extern ENGINE_API uint32 GGPUFrameTime;

        OutGameThreadMs = GGameThreadTime / 1000.0f;
        OutRenderThreadMs = GRenderThreadTime / 1000.0f;
        OutGpuMs = GGPUFrameTime / 1000.0f;
        return true;
    }
}

void UForgeQAPerformanceSubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
    Super::Initialize(Collection);
    ApiClient = MakeShared<FForgeQAApiClient>(GetDefault<UForgeQASettings>()->GetNormalizedApiBaseUrl());

    if (GetDefault<UForgeQASettings>()->bEnablePerformanceMonitoring)
    {
        StartPerformanceMonitoring();
    }
}

void UForgeQAPerformanceSubsystem::Deinitialize()
{
    // Shutdown ordering: stop sampling and flush before this subsystem tears down. Telemetry's own
    // Deinitialize (a separate subsystem) independently flushes/ends its session; the relative
    // order between the two subsystems' Deinitialize calls is engine-determined, so this never
    // assumes telemetry has already ended — it only ever targets the still-open session by ID.
    StopPerformanceMonitoring();

    if (GetGameInstance())
    {
        GetGameInstance()->GetTimerManager().ClearTimer(SampleTimerHandle);
        GetGameInstance()->GetTimerManager().ClearTimer(FlushTimerHandle);
        GetGameInstance()->GetTimerManager().ClearTimer(RetryTimerHandle);
    }

    if (EndFrameDelegateHandle.IsValid())
    {
        FCoreDelegates::OnEndFrame.Remove(EndFrameDelegateHandle);
        EndFrameDelegateHandle.Reset();
    }

    ApiClient.Reset();
    Super::Deinitialize();
}

bool UForgeQAPerformanceSubsystem::IsAvailable() const
{
    if (!GetDefault<UForgeQASettings>()->bEnablePerformanceMonitoring)
    {
        return false;
    }

    const UForgeQASubsystem* ForgeQA = GetGameInstance() ? GetGameInstance()->GetSubsystem<UForgeQASubsystem>() : nullptr;
    if (!ForgeQA || !ForgeQA->HasValidBuildContext())
    {
        return false;
    }

    return !FForgeQARuntimeCredentials::ResolveApiKey().IsEmpty();
}

void UForgeQAPerformanceSubsystem::StartPerformanceMonitoring()
{
    if (bSamplingActive || !IsAvailable())
    {
        return;
    }

    bSamplingActive = true;
    FrameCountThisInterval = 0;
    AccumulatedDeltaTimeThisInterval = 0.0;

    // Frame counting/accumulation happens every frame (cheap: one increment, one add — no
    // allocation, no serialization) — only CollectSample(), on the slower timer below, does any
    // real work, and TryFlush() is the only path that ever touches the network.
    EndFrameDelegateHandle = FCoreDelegates::OnEndFrame.AddUObject(this, &UForgeQAPerformanceSubsystem::HandleEndFrame);

    GetGameInstance()->GetTimerManager().SetTimer(
        SampleTimerHandle, this, &UForgeQAPerformanceSubsystem::CollectSample,
        GetDefault<UForgeQASettings>()->PerformanceSampleIntervalSeconds, /*bLoop=*/true);

    GetGameInstance()->GetTimerManager().SetTimer(
        FlushTimerHandle, this, &UForgeQAPerformanceSubsystem::TryFlush,
        GetDefault<UForgeQASettings>()->PerformanceSampleIntervalSeconds * 5.0f, /*bLoop=*/true);
}

void UForgeQAPerformanceSubsystem::HandleEndFrame()
{
    ++FrameCountThisInterval;
    AccumulatedDeltaTimeThisInterval += FApp::GetDeltaTime();
}

void UForgeQAPerformanceSubsystem::CollectSample()
{
    if (FrameCountThisInterval <= 0 || AccumulatedDeltaTimeThisInterval <= 0.0)
    {
        // No frames observed this interval (e.g. the game is paused/backgrounded) — nothing
        // meaningful to report; skip rather than emit a fabricated FPS of 0.
        return;
    }

    const double AverageFrameTimeSeconds = AccumulatedDeltaTimeThisInterval / FrameCountThisInterval;

    FForgeQAPerformanceSample Sample;
    Sample.SequenceNumber = NextSequenceNumber++;
    Sample.Timestamp = FDateTime::UtcNow();
    Sample.FPS = static_cast<float>(1.0 / AverageFrameTimeSeconds);
    Sample.FrameTimeMs = static_cast<float>(AverageFrameTimeSeconds * 1000.0);

    const UForgeQASubsystem* ForgeQA = GetGameInstance() ? GetGameInstance()->GetSubsystem<UForgeQASubsystem>() : nullptr;
    const UWorld* World = GetGameInstance() ? GetGameInstance()->GetWorld() : nullptr;
    Sample.MapName = World ? World->GetMapName() : FString();

    float GameThreadMs, RenderThreadMs, GpuMs;
    if (TryGetEngineThreadTimingsMs(GameThreadMs, RenderThreadMs, GpuMs))
    {
        Sample.bHasGameThreadTime = true;
        Sample.GameThreadTimeMs = GameThreadMs;
        Sample.bHasRenderThreadTime = true;
        Sample.RenderThreadTimeMs = RenderThreadMs;
        Sample.bHasGpuTime = true;
        Sample.GpuTimeMs = GpuMs;
    }

    // FPlatformMemory::GetStats().UsedPhysical is the process's used physical memory as reported
    // by the platform abstraction layer — documented here explicitly per docs/performance.md, so
    // this is never confused with total system memory or virtual/committed memory.
    const FPlatformMemoryStats MemoryStats = FPlatformMemory::GetStats();
    Sample.bHasMemoryUsed = true;
    Sample.MemoryUsedBytes = static_cast<int64>(MemoryStats.UsedPhysical);

    LastSample = Sample;

    FrameCountThisInterval = 0;
    AccumulatedDeltaTimeThisInterval = 0.0;

    const int32 MaxQueuedSamples = GetDefault<UForgeQASettings>()->PerformanceMaxQueuedSamples;
    if (PendingSamples.Num() >= MaxQueuedSamples)
    {
        PendingSamples.RemoveAt(0);
        ++DroppedSampleCount;

        const double Now = FPlatformTime::Seconds();
        if (Now - LastOverflowLogTimeSeconds > 10.0)
        {
            UE_LOG(LogForgeQA, Warning, TEXT("ForgeQA performance queue overflow: dropping oldest samples (dropped so far: %d)."), DroppedSampleCount);
            LastOverflowLogTimeSeconds = Now;
        }
    }

    PendingSamples.Add(Sample);

    if (PendingSamples.Num() >= GetDefault<UForgeQASettings>()->PerformanceBatchSize)
    {
        TryFlush();
    }
}

void UForgeQAPerformanceSubsystem::TryFlush()
{
    if (bFlushInFlight || PendingSamples.Num() == 0)
    {
        return;
    }

    const UForgeQASubsystem* ForgeQA = GetGameInstance() ? GetGameInstance()->GetSubsystem<UForgeQASubsystem>() : nullptr;
    if (!ForgeQA || !ForgeQA->HasValidBuildContext())
    {
        return;
    }

    const FString ApiKey = FForgeQARuntimeCredentials::ResolveApiKey();
    if (ApiKey.IsEmpty())
    {
        return;
    }

    const int32 BatchSize = FMath::Min(PendingSamples.Num(), GetDefault<UForgeQASettings>()->PerformanceBatchSize);
    TArray<FForgeQAPerformanceSample> Batch(PendingSamples.GetData(), BatchSize);
    PendingSamples.RemoveAt(0, BatchSize);

    bFlushInFlight = true;

    const TSharedRef<FForgeQAApiClient> Client = ApiClient.ToSharedRef();
    const FGuid ProjectId = ForgeQA->GetProjectId();
    const FGuid RuntimeSessionId = ForgeQA->GetRuntimeSessionId();

    Client->SendPerformanceSamples(ApiKey, ProjectId, RuntimeSessionId, Batch,
        [this, Batch](bool bSuccess, const FForgeQAIngestPerformanceSamplesResult& Result, const FForgeQAApiError& Error) mutable
        {
            HandleFlushComplete(MoveTemp(Batch), bSuccess, Result, Error);
        });
}

void UForgeQAPerformanceSubsystem::HandleFlushComplete(TArray<FForgeQAPerformanceSample> AttemptedSamples, bool bSuccess, const FForgeQAIngestPerformanceSamplesResult& Result, const FForgeQAApiError& Error)
{
    bFlushInFlight = false;

    if (bSuccess)
    {
        bLastFlushSucceeded = true;
        RetryAttempt = 0;
        UE_LOG(LogForgeQA, Verbose, TEXT("ForgeQA performance batch flushed: %d accepted, %d duplicates."), Result.Accepted, Result.Duplicates);

        TryFlush();
        return;
    }

    bLastFlushSucceeded = false;

    if (FForgeQARetryPolicy::IsTransientFailure(Error) && RetryAttempt < FForgeQARetryPolicy::MaxRetryAttempts)
    {
        UE_LOG(LogForgeQA, Warning, TEXT("ForgeQA performance batch upload failed (will retry): %s"), *Error.Message);
        ScheduleRetry(MoveTemp(AttemptedSamples));
        return;
    }

    // Permanent rejection (400/401/403/404/409) or retries exhausted: drop this batch so one bad
    // batch (e.g. submitted after the telemetry session ended) never wedges the subsystem forever.
    UE_LOG(LogForgeQA, Error, TEXT("ForgeQA performance batch permanently failed and was dropped: %s"), *Error.Message);
    RetryAttempt = 0;
    TryFlush();
}

void UForgeQAPerformanceSubsystem::ScheduleRetry(TArray<FForgeQAPerformanceSample> FailedSamples)
{
    PendingSamples.Insert(FailedSamples, 0);
    ++RetryAttempt;

    if (!GetGameInstance())
    {
        return;
    }

    const double Delay = FForgeQARetryPolicy::DelayForAttempt(RetryAttempt);
    GetGameInstance()->GetTimerManager().SetTimer(RetryTimerHandle, this, &UForgeQAPerformanceSubsystem::TryFlush, Delay, /*bLoop=*/false);
}

void UForgeQAPerformanceSubsystem::FlushPerformance()
{
    TryFlush();
}

void UForgeQAPerformanceSubsystem::StopPerformanceMonitoring()
{
    if (!bSamplingActive)
    {
        return;
    }

    bSamplingActive = false;

    if (EndFrameDelegateHandle.IsValid())
    {
        FCoreDelegates::OnEndFrame.Remove(EndFrameDelegateHandle);
        EndFrameDelegateHandle.Reset();
    }

    if (GetGameInstance())
    {
        GetGameInstance()->GetTimerManager().ClearTimer(SampleTimerHandle);
    }

    // Best-effort, fire-and-forget flush of whatever is left — never awaited synchronously,
    // matching Telemetry's shutdown behavior exactly.
    TryFlush();
}
