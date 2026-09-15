#include "ForgeQASubsystem.h"
#include "ForgeQASettings.h"
#include "ForgeQABuildManifest.h"
#include "ForgeQALog.h"
#include "Misc/CommandLine.h"
#include "Misc/Parse.h"

void UForgeQASubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
    Super::Initialize(Collection);

    // Generated exactly once per GameInstance lifetime — every Bug Report submitted during this
    // play session shares this token; a new session ID is only generated on the next launch.
    RuntimeSessionId = FGuid::NewGuid();

    FForgeQABuildContext ResolvedContext;

    if (TryResolveFromCommandLine(ResolvedContext))
    {
        UE_LOG(LogForgeQA, Log, TEXT("ForgeQA Build Context resolved from command-line override."));
    }
    else if (TryResolveFromManifest(ResolvedContext))
    {
        UE_LOG(LogForgeQA, Log, TEXT("ForgeQA Build Context resolved from packaged build manifest."));
    }
    else if (TryResolveFromSettings(ResolvedContext))
    {
        UE_LOG(LogForgeQA, Warning, TEXT("ForgeQA Build Context resolved from Project Settings only (no BuildId available). "
            "This project is linked to a ForgeQA Project but no Build has been bound — Build-scoped ForgeQA features will be unavailable."));
    }
    else
    {
        UE_LOG(LogForgeQA, Log, TEXT("No ForgeQA Build Context is available. This is expected when running without a linked ForgeQA Build."));
    }

    BuildContext = ResolvedContext;
}

bool UForgeQASubsystem::TryResolveFromCommandLine(FForgeQABuildContext& OutContext)
{
    FString ProjectIdString;
    FString BuildIdString;

    const bool bHasProjectId = FParse::Value(FCommandLine::Get(), TEXT("ForgeQAProjectId="), ProjectIdString);
    const bool bHasBuildId = FParse::Value(FCommandLine::Get(), TEXT("ForgeQABuildId="), BuildIdString);

    if (!bHasProjectId || !bHasBuildId)
    {
        return false;
    }

    FGuid ProjectId;
    FGuid BuildId;
    if (!FGuid::Parse(ProjectIdString, ProjectId) || !ProjectId.IsValid())
    {
        UE_LOG(LogForgeQA, Warning, TEXT("-ForgeQAProjectId='%s' is not a valid UUID; ignoring command-line override."), *ProjectIdString);
        return false;
    }
    if (!FGuid::Parse(BuildIdString, BuildId) || !BuildId.IsValid())
    {
        UE_LOG(LogForgeQA, Warning, TEXT("-ForgeQABuildId='%s' is not a valid UUID; ignoring command-line override."), *BuildIdString);
        return false;
    }

    OutContext = FForgeQABuildContext();
    OutContext.ProjectId = ProjectId;
    OutContext.BuildId = BuildId;

    // Optional supplemental metadata, useful when CI supplies identity without a manifest file.
    FParse::Value(FCommandLine::Get(), TEXT("ForgeQAVersion="), OutContext.Version);
    FParse::Value(FCommandLine::Get(), TEXT("ForgeQABuildNumber="), OutContext.BuildNumber);
    FParse::Value(FCommandLine::Get(), TEXT("ForgeQAPlatform="), OutContext.Platform);
    FParse::Value(FCommandLine::Get(), TEXT("ForgeQAConfiguration="), OutContext.Configuration);

    return true;
}

bool UForgeQASubsystem::TryResolveFromManifest(FForgeQABuildContext& OutContext)
{
    FForgeQABuildManifest Manifest;
    FString Error;
    if (!FForgeQABuildManifestSerializer::TryLoadFromFile(Manifest, Error))
    {
        UE_LOG(LogForgeQA, Log, TEXT("ForgeQA build manifest not available: %s"), *Error);
        return false;
    }

    if (!Manifest.IsValid())
    {
        UE_LOG(LogForgeQA, Warning, TEXT("ForgeQA build manifest was read but does not contain a valid ProjectId/BuildId; ignoring it."));
        return false;
    }

    OutContext = Manifest.ToBuildContext();
    return true;
}

bool UForgeQASubsystem::TryResolveFromSettings(FForgeQABuildContext& OutContext)
{
    const UForgeQASettings* Settings = GetDefault<UForgeQASettings>();
    if (!Settings || !Settings->ProjectId.IsValid())
    {
        return false;
    }

    OutContext = FForgeQABuildContext();
    OutContext.ProjectId = Settings->ProjectId;
    // Deliberately no BuildId: Settings alone is never a valid source of Build identity, so
    // HasValidBuildContext() correctly remains false until a manifest or override supplies one.
    // This still counts as a successful resolution step (of a partial context) so Initialize()
    // can log that a Project is linked but no Build is bound, rather than treating it as "nothing found".
    return true;
}
