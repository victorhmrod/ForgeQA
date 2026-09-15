#pragma once

#include "CoreMinimal.h"
#include "Commandlets/Commandlet.h"
#include "ForgeQAGenerateBuildManifestCommandlet.generated.h"

/**
 * Non-interactive manifest generation, so a future CI/BuildGraph step can inject ForgeQA identity
 * without opening the Unreal Editor UI. This intentionally does not contact the ForgeQA API — it
 * writes exactly the metadata supplied on the command line, which keeps it usable even before M8
 * (CI/CD) exists to supply credentials.
 *
 * Usage:
 *   UnrealEditor-Cmd.exe Project.uproject -run=ForgeQAGenerateBuildManifest
 *     -ForgeQAProjectId=<uuid> -ForgeQABuildId=<uuid>
 *     [-ForgeQAVersion=...] [-ForgeQABuildNumber=...] [-ForgeQAPlatform=...]
 *     [-ForgeQAConfiguration=...] [-ForgeQABranch=...] [-ForgeQACommitSha=...]
 */
UCLASS()
class UForgeQAGenerateBuildManifestCommandlet : public UCommandlet
{
    GENERATED_BODY()

public:
    virtual int32 Main(const FString& Params) override;
};
