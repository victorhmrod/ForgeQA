#pragma once

#include "CoreMinimal.h"
#include "ForgeQAApiTypes.h"

/**
 * Shared transient-failure classification and bounded exponential backoff, used identically by
 * UForgeQATelemetrySubsystem and UForgeQAPerformanceSubsystem so the two runtime ingestion
 * pipelines never drift into subtly different retry behavior. Deliberately small — this is not a
 * generic retry framework, just the one policy both subsystems need.
 */
class FORGEQA_API FForgeQARetryPolicy
{
public:
    static constexpr int32 MaxRetryAttempts = 4;
    static constexpr double MaxRetryDelaySeconds = 30.0;

    /** Transient: never reached the server (StatusCode 0), rate-limited (429), or a server error
     * (5xx) — all expected to clear. Permanent: a bad credential/request (400/401/403) or a
     * not-found/ended-session response (404/409) — retrying would fail identically forever. */
    static bool IsTransientFailure(const FForgeQAApiError& Error);

    /** 1s, 2s, 4s, 8s, capped at 30s, with up to 20% jitter. */
    static double DelayForAttempt(int32 Attempt);
};
