#include "ForgeQATelemetrySubsystem.h"
#include "ForgeQAApiClient.h"
#include "ForgeQASettings.h"
#include "ForgeQASubsystem.h"
#include "ForgeQARuntimeCredentials.h"
#include "ForgeQALog.h"
#include "Engine/GameInstance.h"
#include "TimerManager.h"
#include "Dom/JsonObject.h"
#include "Serialization/JsonWriter.h"
#include "Serialization/JsonSerializer.h"

namespace
{
    // Bounded exponential backoff for transient ingestion failures: 1s, 2s, 4s, 8s, capped at 30s.
    // A permanent failure (401/403/400) never reaches this — see IsTransientFailure.
    constexpr int32 MaxRetryAttempts = 4;
    constexpr double MaxRetryDelaySeconds = 30.0;

    double RetryDelayForAttempt(int32 Attempt)
    {
        const double Base = FMath::Min(MaxRetryDelaySeconds, FMath::Pow(2.0, static_cast<double>(Attempt)));
        const double Jitter = FMath::FRandRange(0.0, Base * 0.2);
        return Base + Jitter;
    }
}

void UForgeQATelemetrySubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
    Super::Initialize(Collection);
    ApiClient = MakeShared<FForgeQAApiClient>(GetDefault<UForgeQASettings>()->GetNormalizedApiBaseUrl());

    if (GetDefault<UForgeQASettings>()->bEnableTelemetry)
    {
        StartTelemetry();
    }
}

void UForgeQATelemetrySubsystem::Deinitialize()
{
    // Best-effort only: these are fire-and-forget async requests, never awaited synchronously —
    // blocking the Game Thread during shutdown to guarantee delivery is explicitly out of scope
    // for M5 (see docs/telemetry.md). Losing the last few queued events on an abrupt shutdown is
    // an accepted tradeoff.
    if (bSessionStarted)
    {
        EndTelemetry();
    }

    if (GetGameInstance())
    {
        GetGameInstance()->GetTimerManager().ClearTimer(FlushTimerHandle);
        GetGameInstance()->GetTimerManager().ClearTimer(RetryTimerHandle);
    }

    ApiClient.Reset();
    Super::Deinitialize();
}

bool UForgeQATelemetrySubsystem::IsTelemetryAvailable() const
{
    if (!GetDefault<UForgeQASettings>()->bEnableTelemetry)
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

void UForgeQATelemetrySubsystem::StartTelemetry()
{
    if (bSessionStarted || !IsTelemetryAvailable())
    {
        return;
    }

    const UForgeQASubsystem* ForgeQA = GetGameInstance()->GetSubsystem<UForgeQASubsystem>();
    const FForgeQABuildContext& Context = ForgeQA->GetBuildContext();
    const FString ApiKey = FForgeQARuntimeCredentials::ResolveApiKey();

    FForgeQAStartTelemetrySessionRequest Request;
    Request.RuntimeSessionId = ForgeQA->GetRuntimeSessionId();
    Request.BuildId = Context.BuildId;
    Request.Environment.Platform = Context.Platform;
    Request.Environment.EngineVersion = Context.EngineVersion;
    const UWorld* World = GetGameInstance()->GetWorld();
    Request.Environment.MapName = World ? World->GetMapName() : FString();

    // Never blocks game startup: this fires and returns immediately, and TrackEvent() calls made
    // before the session is confirmed remain safely queued (see TrackEvent).
    const TSharedRef<FForgeQAApiClient> Client = ApiClient.ToSharedRef();
    const FGuid ProjectId = Context.ProjectId;
    Client->StartTelemetrySession(ApiKey, ProjectId, Request,
        [this](bool bSuccess, const FForgeQATelemetrySessionResult& Result, const FForgeQAApiError& Error)
        {
            HandleSessionStarted(bSuccess, Result, Error);
        });

    // Periodic flush trigger (the other trigger — batch size — is checked inline in TrackEvent).
    GetGameInstance()->GetTimerManager().SetTimer(
        FlushTimerHandle, this, &UForgeQATelemetrySubsystem::TryFlush,
        GetDefault<UForgeQASettings>()->TelemetryFlushIntervalSeconds, /*bLoop=*/true);
}

void UForgeQATelemetrySubsystem::HandleSessionStarted(bool bSuccess, const FForgeQATelemetrySessionResult& Result, const FForgeQAApiError& Error)
{
    if (!bSuccess)
    {
        UE_LOG(LogForgeQA, Warning, TEXT("ForgeQA telemetry session failed to start: %s"), *Error.Message);
        return;
    }

    bSessionStarted = true;
    UE_LOG(LogForgeQA, Log, TEXT("ForgeQA telemetry session started: %s"), *Result.Id.ToString());

    TrackEvent(TEXT("forgeqa.session.started"), MakeShared<FJsonObject>());
}

void UForgeQATelemetrySubsystem::TrackEvent(const FString& EventName, const FForgeQATelemetryProperties& Properties)
{
    TrackEvent(EventName, Properties.ToJsonObject());
}

void UForgeQATelemetrySubsystem::TrackBreadcrumb(const FString& Name, const FForgeQATelemetryProperties& Properties)
{
    TrackEvent(Name, Properties.ToJsonObject(), TEXT("breadcrumb"));
}

void UForgeQATelemetrySubsystem::TrackEvent(const FString& EventName, const TSharedPtr<FJsonObject>& Properties, const FString& Category, const FString& MapName)
{
    if (!GetDefault<UForgeQASettings>()->bEnableTelemetry)
    {
        return;
    }

    if (!IsValidEventName(EventName))
    {
        UE_LOG(LogForgeQA, Warning, TEXT("ForgeQA telemetry event name '%s' is invalid and was not queued."), *EventName);
        return;
    }

    const int32 MaxQueuedEvents = GetDefault<UForgeQASettings>()->TelemetryMaxQueuedEvents;
    if (PendingEvents.Num() >= MaxQueuedEvents)
    {
        // Drop-oldest under sustained overflow, never reject the newest event — a burst of recent
        // activity is usually more useful for QA than whatever was queued first.
        PendingEvents.RemoveAt(0);
        ++DroppedEventCount;

        const double Now = FPlatformTime::Seconds();
        if (Now - LastOverflowLogTimeSeconds > 10.0)
        {
            UE_LOG(LogForgeQA, Warning, TEXT("ForgeQA telemetry queue overflow: dropping oldest events (dropped so far: %d)."), DroppedEventCount);
            LastOverflowLogTimeSeconds = Now;
        }
    }

    FForgeQATelemetryEvent Event;
    Event.SequenceNumber = NextSequenceNumber++;
    Event.EventName = EventName;
    Event.ClientTimestamp = FDateTime::UtcNow();
    Event.Category = Category;
    Event.MapName = MapName;

    if (Properties.IsValid())
    {
        FString Serialized;
        const TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&Serialized);
        FJsonSerializer::Serialize(Properties.ToSharedRef(), Writer);
        Event.PropertiesJson = Serialized;
    }

    PendingEvents.Add(MoveTemp(Event));

    if (PendingEvents.Num() >= GetDefault<UForgeQASettings>()->TelemetryBatchSize)
    {
        TryFlush();
    }
}

void UForgeQATelemetrySubsystem::TryFlush()
{
    // At most one active flush at a time — the next batch (if any) is sent once this one
    // completes, from HandleFlushComplete. This keeps sequencing and retry logic simple.
    if (bFlushInFlight || PendingEvents.Num() == 0 || !bSessionStarted)
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

    const int32 BatchSize = FMath::Min(PendingEvents.Num(), GetDefault<UForgeQASettings>()->TelemetryBatchSize);
    TArray<FForgeQATelemetryEvent> Batch(PendingEvents.GetData(), BatchSize);
    PendingEvents.RemoveAt(0, BatchSize);

    bFlushInFlight = true;

    const TSharedRef<FForgeQAApiClient> Client = ApiClient.ToSharedRef();
    const FGuid ProjectId = ForgeQA->GetProjectId();
    const FGuid RuntimeSessionId = ForgeQA->GetRuntimeSessionId();

    Client->SendTelemetryEvents(ApiKey, ProjectId, RuntimeSessionId, Batch,
        [this, Batch](bool bSuccess, const FForgeQAIngestTelemetryEventsResult& Result, const FForgeQAApiError& Error) mutable
        {
            HandleFlushComplete(MoveTemp(Batch), bSuccess, Result, Error);
        });
}

void UForgeQATelemetrySubsystem::HandleFlushComplete(TArray<FForgeQATelemetryEvent> AttemptedEvents, bool bSuccess, const FForgeQAIngestTelemetryEventsResult& Result, const FForgeQAApiError& Error)
{
    bFlushInFlight = false;

    if (bSuccess)
    {
        bLastFlushSucceeded = true;
        RetryAttempt = 0;
        UE_LOG(LogForgeQA, Verbose, TEXT("ForgeQA telemetry batch flushed: %d accepted, %d duplicates."), Result.Accepted, Result.Duplicates);

        // Another batch may already be waiting (queued while this flush was in flight).
        TryFlush();
        return;
    }

    bLastFlushSucceeded = false;

    if (IsTransientFailure(Error) && RetryAttempt < MaxRetryAttempts)
    {
        UE_LOG(LogForgeQA, Warning, TEXT("ForgeQA telemetry batch upload failed (will retry): %s"), *Error.Message);
        ScheduleRetry(MoveTemp(AttemptedEvents));
        return;
    }

    // Permanent rejection (401/403/400) or retries exhausted: log and drop this batch so one
    // malformed/unauthorized batch never wedges the subsystem forever. Future telemetry continues.
    UE_LOG(LogForgeQA, Error, TEXT("ForgeQA telemetry batch permanently failed and was dropped: %s"), *Error.Message);
    RetryAttempt = 0;
    TryFlush();
}

void UForgeQATelemetrySubsystem::ScheduleRetry(TArray<FForgeQATelemetryEvent> FailedEvents)
{
    // Put the failed batch back at the front of the queue so retry preserves original ordering
    // relative to anything queued afterward.
    PendingEvents.Insert(FailedEvents, 0);
    ++RetryAttempt;

    if (!GetGameInstance())
    {
        return;
    }

    const double Delay = RetryDelayForAttempt(RetryAttempt);
    GetGameInstance()->GetTimerManager().SetTimer(RetryTimerHandle, this, &UForgeQATelemetrySubsystem::TryFlush, Delay, /*bLoop=*/false);
}

bool UForgeQATelemetrySubsystem::IsTransientFailure(const FForgeQAApiError& Error)
{
    // 0 = never reached the server (network/timeout). 5xx and 429 are server-side/rate-limit
    // conditions expected to clear. 401/403/400 are never retried — retrying a bad credential or a
    // malformed request would just fail identically forever.
    return Error.StatusCode == 0 || Error.StatusCode == 429 || Error.StatusCode >= 500;
}

void UForgeQATelemetrySubsystem::FlushTelemetry()
{
    TryFlush();
}

void UForgeQATelemetrySubsystem::EndTelemetry()
{
    if (!bSessionStarted)
    {
        return;
    }

    TrackEvent(TEXT("forgeqa.session.ended"), MakeShared<FJsonObject>());
    TryFlush();

    const UForgeQASubsystem* ForgeQA = GetGameInstance() ? GetGameInstance()->GetSubsystem<UForgeQASubsystem>() : nullptr;
    if (!ForgeQA)
    {
        return;
    }

    const FString ApiKey = FForgeQARuntimeCredentials::ResolveApiKey();
    if (ApiKey.IsEmpty())
    {
        return;
    }

    const TSharedRef<FForgeQAApiClient> Client = ApiClient.ToSharedRef();
    Client->EndTelemetrySession(ApiKey, ForgeQA->GetProjectId(), ForgeQA->GetRuntimeSessionId(),
        [](bool bSuccess, const FForgeQAApiError& Error)
        {
            if (!bSuccess)
            {
                UE_LOG(LogForgeQA, Warning, TEXT("ForgeQA telemetry session end failed: %s"), *Error.Message);
            }
        });

    bSessionStarted = false;
}

bool UForgeQATelemetrySubsystem::IsValidEventName(const FString& EventName)
{
    if (EventName.IsEmpty() || EventName.Len() > 128)
    {
        return false;
    }

    for (const TCHAR Character : EventName)
    {
        const bool bIsAllowed = FChar::IsAlnum(Character) || Character == TEXT('.') || Character == TEXT('_') || Character == TEXT('-');
        if (!bIsAllowed)
        {
            return false;
        }
    }

    return true;
}
