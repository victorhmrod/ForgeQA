#include "CoreMinimal.h"
#include "Misc/AutomationTest.h"

#if WITH_DEV_AUTOMATION_TESTS

#include "ForgeQASubsystem.h"
#include "ForgeQABuildManifest.h"
#include "Misc/CommandLine.h"
#include "HAL/PlatformFileManager.h"

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQACommandLineOverrideValidTest, "ForgeQA.BuildContext.CommandLineOverride.ParsesValidUuids",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQACommandLineOverrideValidTest::RunTest(const FString& Parameters)
{
    const FString SavedCommandLine = FCommandLine::Get();
    const FGuid ProjectId = FGuid::NewGuid();
    const FGuid BuildId = FGuid::NewGuid();

    FCommandLine::Set(*FString::Printf(
        TEXT("-ForgeQAProjectId=%s -ForgeQABuildId=%s"),
        *ProjectId.ToString(EGuidFormats::DigitsWithHyphens),
        *BuildId.ToString(EGuidFormats::DigitsWithHyphens)));

    FForgeQABuildContext Context;
    const bool bResolved = UForgeQASubsystem::TryResolveFromCommandLine(Context);

    FCommandLine::Set(*SavedCommandLine);

    TestTrue(TEXT("A command line with both valid UUIDs resolves"), bResolved);
    TestEqual(TEXT("ProjectId matches the command line"), Context.ProjectId, ProjectId);
    TestEqual(TEXT("BuildId matches the command line"), Context.BuildId, BuildId);
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQACommandLineOverrideMissingBuildIdTest, "ForgeQA.BuildContext.CommandLineOverride.RequiresBothIds",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQACommandLineOverrideMissingBuildIdTest::RunTest(const FString& Parameters)
{
    const FString SavedCommandLine = FCommandLine::Get();
    FCommandLine::Set(*FString::Printf(TEXT("-ForgeQAProjectId=%s"), *FGuid::NewGuid().ToString(EGuidFormats::DigitsWithHyphens)));

    FForgeQABuildContext Context;
    const bool bResolved = UForgeQASubsystem::TryResolveFromCommandLine(Context);

    FCommandLine::Set(*SavedCommandLine);

    TestFalse(TEXT("ProjectId alone (no BuildId) does not resolve a command-line override"), bResolved);
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQACommandLineOverrideInvalidUuidTest, "ForgeQA.BuildContext.CommandLineOverride.RejectsInvalidUuid",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQACommandLineOverrideInvalidUuidTest::RunTest(const FString& Parameters)
{
    const FString SavedCommandLine = FCommandLine::Get();
    FCommandLine::Set(*FString::Printf(
        TEXT("-ForgeQAProjectId=not-a-guid -ForgeQABuildId=%s"),
        *FGuid::NewGuid().ToString(EGuidFormats::DigitsWithHyphens)));

    FForgeQABuildContext Context;
    const bool bResolved = UForgeQASubsystem::TryResolveFromCommandLine(Context);

    FCommandLine::Set(*SavedCommandLine);

    TestFalse(TEXT("An invalid ProjectId UUID on the command line is rejected, not silently accepted"), bResolved);
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQAManifestResolutionMissingFileTest, "ForgeQA.BuildContext.Manifest.MissingFileDoesNotResolve",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQAManifestResolutionMissingFileTest::RunTest(const FString& Parameters)
{
    // Exercises the "manifest does not exist" path end-to-end via a path that is guaranteed absent.
    FForgeQABuildManifest Manifest;
    FString Error;
    const bool bLoaded = FForgeQABuildManifestSerializer::TryLoadFromFile(
        TEXT("/this/path/does/not/exist/ForgeQABuild.json"), Manifest, Error);

    TestFalse(TEXT("Loading a missing manifest file fails cleanly, without crashing"), bLoaded);
    TestFalse(TEXT("A descriptive error is produced"), Error.IsEmpty());
    return true;
}

#endif // WITH_DEV_AUTOMATION_TESTS
