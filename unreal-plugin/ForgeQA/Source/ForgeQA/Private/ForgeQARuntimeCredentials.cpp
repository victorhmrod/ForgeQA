#include "ForgeQARuntimeCredentials.h"
#include "ForgeQASettings.h"
#include "Misc/CommandLine.h"
#include "Misc/Parse.h"
#include "HAL/PlatformMisc.h"

FString FForgeQARuntimeCredentials::ResolveApiKey()
{
    FString CommandLineKey;
    if (FParse::Value(FCommandLine::Get(), TEXT("ForgeQAApiKey="), CommandLineKey) && !CommandLineKey.IsEmpty())
    {
        return CommandLineKey;
    }

    const FString EnvironmentKey = FPlatformMisc::GetEnvironmentVariable(TEXT("FORGEQA_API_KEY"));
    if (!EnvironmentKey.IsEmpty())
    {
        return EnvironmentKey;
    }

    if (const UForgeQASettings* Settings = GetDefault<UForgeQASettings>())
    {
        return Settings->DevelopmentRuntimeApiKey;
    }

    return FString();
}
