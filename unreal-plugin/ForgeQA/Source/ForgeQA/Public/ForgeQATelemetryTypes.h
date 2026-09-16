#pragma once

#include "CoreMinimal.h"
#include "ForgeQATelemetryTypes.generated.h"

class FJsonObject;

/**
 * Blueprint-friendly structured event properties. C++ callers that need full JSON flexibility
 * (nested objects, arrays) should build an FJsonObject directly and use the C++-only TrackEvent
 * overload on UForgeQATelemetrySubsystem instead — this struct exists only so Blueprint users don't
 * have to touch raw JSON, not to replace it. Deliberately three flat maps, not a generic property
 * type system.
 */
USTRUCT(BlueprintType)
struct FORGEQA_API FForgeQATelemetryProperties
{
    GENERATED_BODY()

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "ForgeQA|Telemetry")
    TMap<FString, FString> StringProperties;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "ForgeQA|Telemetry")
    TMap<FString, float> NumberProperties;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "ForgeQA|Telemetry")
    TMap<FString, bool> BoolProperties;

    bool IsEmpty() const { return StringProperties.Num() == 0 && NumberProperties.Num() == 0 && BoolProperties.Num() == 0; }

    /** Builds a plain JSON object from the three maps — never a JSON array or scalar, since the
     * backend requires event properties to be a JSON object (see docs/telemetry.md). */
    TSharedRef<FJsonObject> ToJsonObject() const;
};
