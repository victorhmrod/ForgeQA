#include "ForgeQATelemetryTypes.h"
#include "Dom/JsonObject.h"

TSharedRef<FJsonObject> FForgeQATelemetryProperties::ToJsonObject() const
{
    const TSharedRef<FJsonObject> Object = MakeShared<FJsonObject>();

    for (const TPair<FString, FString>& Pair : StringProperties)
    {
        Object->SetStringField(Pair.Key, Pair.Value);
    }
    for (const TPair<FString, float>& Pair : NumberProperties)
    {
        Object->SetNumberField(Pair.Key, Pair.Value);
    }
    for (const TPair<FString, bool>& Pair : BoolProperties)
    {
        Object->SetBoolField(Pair.Key, Pair.Value);
    }

    return Object;
}
