#pragma once

#include "CoreMinimal.h"

/**
 * Resolves the Project API key used to authenticate runtime bug submissions. Never reads from or
 * writes to the Build manifest — a distributed game executable's identity (Project/Build) and its
 * credential are deliberately different files with different lifecycles and different exposure
 * (the manifest ships in every build; a key should be rotated/scoped independently).
 *
 * Precedence, highest first:
 *   1. -ForgeQAApiKey=<key> command-line override (the intended path for a future CI packaging step)
 *   2. FORGEQA_API_KEY environment variable
 *   3. UForgeQASettings::DevelopmentRuntimeApiKey (local development convenience only)
 */
class FORGEQA_API FForgeQARuntimeCredentials
{
public:
    static FString ResolveApiKey();
};
