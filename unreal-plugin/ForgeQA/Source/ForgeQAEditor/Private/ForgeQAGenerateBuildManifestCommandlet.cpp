#include "ForgeQAGenerateBuildManifestCommandlet.h"
#include "ForgeQABuildManifest.h"
#include "ForgeQALog.h"
#include "Misc/Parse.h"
#include "Misc/CommandLine.h"

int32 UForgeQAGenerateBuildManifestCommandlet::Main(const FString& Params)
{
    FString ProjectIdString;
    FString BuildIdString;

    if (!FParse::Value(*Params, TEXT("ForgeQAProjectId="), ProjectIdString) ||
        !FParse::Value(*Params, TEXT("ForgeQABuildId="), BuildIdString))
    {
        UE_LOG(LogForgeQA, Error, TEXT("ForgeQAGenerateBuildManifest requires -ForgeQAProjectId=<uuid> and -ForgeQABuildId=<uuid>."));
        return 1;
    }

    FGuid ProjectId;
    FGuid BuildId;
    if (!FGuid::Parse(ProjectIdString, ProjectId) || !ProjectId.IsValid())
    {
        UE_LOG(LogForgeQA, Error, TEXT("-ForgeQAProjectId='%s' is not a valid UUID."), *ProjectIdString);
        return 1;
    }
    if (!FGuid::Parse(BuildIdString, BuildId) || !BuildId.IsValid())
    {
        UE_LOG(LogForgeQA, Error, TEXT("-ForgeQABuildId='%s' is not a valid UUID."), *BuildIdString);
        return 1;
    }

    FForgeQABuildManifest Manifest;
    Manifest.ProjectId = ProjectId;
    Manifest.BuildId = BuildId;
    FParse::Value(*Params, TEXT("ForgeQAVersion="), Manifest.Version);
    FParse::Value(*Params, TEXT("ForgeQABuildNumber="), Manifest.BuildNumber);
    FParse::Value(*Params, TEXT("ForgeQAPlatform="), Manifest.Platform);
    FParse::Value(*Params, TEXT("ForgeQAConfiguration="), Manifest.Configuration);
    FParse::Value(*Params, TEXT("ForgeQABranch="), Manifest.Branch);
    FParse::Value(*Params, TEXT("ForgeQACommitSha="), Manifest.CommitSha);

    FString Error;
    if (!FForgeQABuildManifestSerializer::SaveToFile(Manifest, Error))
    {
        UE_LOG(LogForgeQA, Error, TEXT("Failed to write ForgeQA build manifest: %s"), *Error);
        return 1;
    }

    UE_LOG(LogForgeQA, Log, TEXT("ForgeQA build manifest written to '%s'."), *FForgeQABuildManifestSerializer::GetManifestFilePath());
    return 0;
}
