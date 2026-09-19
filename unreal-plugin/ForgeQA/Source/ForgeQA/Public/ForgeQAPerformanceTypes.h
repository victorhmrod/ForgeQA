#pragma once

#include "CoreMinimal.h"
#include "ForgeQAPerformanceTypes.generated.h"

/**
 * One lightweight runtime performance snapshot, collected over a short sampling interval (not
 * per-frame — see UForgeQAPerformanceSubsystem). Doubles as both the Blueprint-facing diagnostic
 * struct (GetLastSample) and the internal queued/sent representation, to avoid maintaining two
 * parallel shapes.
 *
 * Optional metrics use an explicit `bHas*` availability flag rather than a sentinel value — a
 * platform where GPU timing is unavailable must never be recorded as GPUTimeMs = 0, since zero is
 * a legitimate (if suspicious) reading. Blueprint has no nullable primitives, so this is the
 * safe equivalent of the backend DTOs' nullable doubles.
 */
USTRUCT(BlueprintType)
struct FORGEQA_API FForgeQAPerformanceSample
{
    GENERATED_BODY()

    UPROPERTY(BlueprintReadOnly, Category = "ForgeQA|Performance")
    int32 SequenceNumber = 0;

    UPROPERTY(BlueprintReadOnly, Category = "ForgeQA|Performance")
    FDateTime Timestamp;

    UPROPERTY(BlueprintReadOnly, Category = "ForgeQA|Performance")
    FString MapName;

    UPROPERTY(BlueprintReadOnly, Category = "ForgeQA|Performance")
    float FPS = 0.0f;

    UPROPERTY(BlueprintReadOnly, Category = "ForgeQA|Performance")
    float FrameTimeMs = 0.0f;

    UPROPERTY(BlueprintReadOnly, Category = "ForgeQA|Performance")
    bool bHasGameThreadTime = false;
    UPROPERTY(BlueprintReadOnly, Category = "ForgeQA|Performance")
    float GameThreadTimeMs = 0.0f;

    UPROPERTY(BlueprintReadOnly, Category = "ForgeQA|Performance")
    bool bHasRenderThreadTime = false;
    UPROPERTY(BlueprintReadOnly, Category = "ForgeQA|Performance")
    float RenderThreadTimeMs = 0.0f;

    UPROPERTY(BlueprintReadOnly, Category = "ForgeQA|Performance")
    bool bHasGpuTime = false;
    UPROPERTY(BlueprintReadOnly, Category = "ForgeQA|Performance")
    float GpuTimeMs = 0.0f;

    UPROPERTY(BlueprintReadOnly, Category = "ForgeQA|Performance")
    bool bHasMemoryUsed = false;
    UPROPERTY(BlueprintReadOnly, Category = "ForgeQA|Performance")
    int64 MemoryUsedBytes = 0;
};
