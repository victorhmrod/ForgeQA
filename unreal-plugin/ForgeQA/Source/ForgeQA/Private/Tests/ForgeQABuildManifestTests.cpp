#include "CoreMinimal.h"
#include "Misc/AutomationTest.h"

#if WITH_DEV_AUTOMATION_TESTS

#include "ForgeQABuildManifest.h"

namespace
{
    FString MakeValidManifestJson()
    {
        return TEXT(R"({
            "schemaVersion": 1,
            "projectId": "9d0f469c-5380-4243-9bb5-a35b6a59ffe7",
            "buildId": "50b09e84-5693-49cf-817c-5d928d086fc3",
            "version": "0.4.2",
            "buildNumber": "1842",
            "platform": "WINDOWS",
            "configuration": "DEVELOPMENT",
            "branch": "main",
            "commitSha": "a941de3"
        })");
    }
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQABuildManifestParseValidTest, "ForgeQA.BuildManifest.ParsesValidManifest",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQABuildManifestParseValidTest::RunTest(const FString& Parameters)
{
    FForgeQABuildManifest Manifest;
    FString Error;
    const bool bParsed = FForgeQABuildManifestSerializer::TryParseJsonString(MakeValidManifestJson(), Manifest, Error);

    TestTrue(TEXT("A well-formed manifest parses successfully"), bParsed);
    TestTrue(TEXT("Parsed manifest is valid"), Manifest.IsValid());
    TestEqual(TEXT("Version round-trips"), Manifest.Version, FString(TEXT("0.4.2")));
    TestEqual(TEXT("BuildNumber round-trips"), Manifest.BuildNumber, FString(TEXT("1842")));
    TestEqual(TEXT("Platform round-trips"), Manifest.Platform, FString(TEXT("WINDOWS")));
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQABuildManifestRoundTripTest, "ForgeQA.BuildManifest.SerializeThenParseRoundTrips",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQABuildManifestRoundTripTest::RunTest(const FString& Parameters)
{
    FForgeQABuildManifest Original;
    Original.ProjectId = FGuid::NewGuid();
    Original.BuildId = FGuid::NewGuid();
    Original.Version = TEXT("1.0.0");
    Original.BuildNumber = TEXT("gha-3421");
    Original.Platform = TEXT("LINUX");
    Original.Configuration = TEXT("SHIPPING");
    Original.Branch = TEXT("release/1.0");
    Original.CommitSha = TEXT("deadbeef");

    const FString Json = FForgeQABuildManifestSerializer::ToJsonString(Original);

    FForgeQABuildManifest Parsed;
    FString Error;
    const bool bParsed = FForgeQABuildManifestSerializer::TryParseJsonString(Json, Parsed, Error);

    TestTrue(TEXT("Serialized manifest parses back successfully"), bParsed);
    TestEqual(TEXT("ProjectId round-trips"), Parsed.ProjectId, Original.ProjectId);
    TestEqual(TEXT("BuildId round-trips"), Parsed.BuildId, Original.BuildId);
    TestEqual(TEXT("Branch round-trips"), Parsed.Branch, Original.Branch);
    TestEqual(TEXT("CommitSha round-trips"), Parsed.CommitSha, Original.CommitSha);
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQABuildManifestRejectsInvalidJsonTest, "ForgeQA.BuildManifest.RejectsInvalidJson",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQABuildManifestRejectsInvalidJsonTest::RunTest(const FString& Parameters)
{
    FForgeQABuildManifest Manifest;
    FString Error;
    const bool bParsed = FForgeQABuildManifestSerializer::TryParseJsonString(TEXT("{ not valid json"), Manifest, Error);

    TestFalse(TEXT("Malformed JSON is rejected"), bParsed);
    TestFalse(TEXT("Error message is populated"), Error.IsEmpty());
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQABuildManifestRejectsMissingSchemaVersionTest, "ForgeQA.BuildManifest.RejectsMissingSchemaVersion",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQABuildManifestRejectsMissingSchemaVersionTest::RunTest(const FString& Parameters)
{
    const FString Json = TEXT(R"({ "projectId": "9d0f469c-5380-4243-9bb5-a35b6a59ffe7", "buildId": "50b09e84-5693-49cf-817c-5d928d086fc3" })");

    FForgeQABuildManifest Manifest;
    FString Error;
    const bool bParsed = FForgeQABuildManifestSerializer::TryParseJsonString(Json, Manifest, Error);

    TestFalse(TEXT("A manifest missing schemaVersion is rejected"), bParsed);
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQABuildManifestRejectsNewerSchemaTest, "ForgeQA.BuildManifest.RejectsNewerUnsupportedSchema",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQABuildManifestRejectsNewerSchemaTest::RunTest(const FString& Parameters)
{
    const FString Json = TEXT(R"({
        "schemaVersion": 999,
        "projectId": "9d0f469c-5380-4243-9bb5-a35b6a59ffe7",
        "buildId": "50b09e84-5693-49cf-817c-5d928d086fc3"
    })");

    FForgeQABuildManifest Manifest;
    FString Error;
    const bool bParsed = FForgeQABuildManifestSerializer::TryParseJsonString(Json, Manifest, Error);

    TestFalse(TEXT("A manifest with a newer, unsupported schema version is rejected, not misinterpreted"), bParsed);
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQABuildManifestRejectsInvalidProjectIdTest, "ForgeQA.BuildManifest.RejectsInvalidProjectIdUuid",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQABuildManifestRejectsInvalidProjectIdTest::RunTest(const FString& Parameters)
{
    const FString Json = TEXT(R"({
        "schemaVersion": 1,
        "projectId": "not-a-guid",
        "buildId": "50b09e84-5693-49cf-817c-5d928d086fc3"
    })");

    FForgeQABuildManifest Manifest;
    FString Error;
    const bool bParsed = FForgeQABuildManifestSerializer::TryParseJsonString(Json, Manifest, Error);

    TestFalse(TEXT("An invalid ProjectId UUID is rejected"), bParsed);
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQABuildManifestRejectsMissingBuildIdTest, "ForgeQA.BuildManifest.RejectsMissingBuildId",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQABuildManifestRejectsMissingBuildIdTest::RunTest(const FString& Parameters)
{
    const FString Json = TEXT(R"({
        "schemaVersion": 1,
        "projectId": "9d0f469c-5380-4243-9bb5-a35b6a59ffe7"
    })");

    FForgeQABuildManifest Manifest;
    FString Error;
    const bool bParsed = FForgeQABuildManifestSerializer::TryParseJsonString(Json, Manifest, Error);

    TestFalse(TEXT("A manifest missing buildId is rejected"), bParsed);
    return true;
}

#endif // WITH_DEV_AUTOMATION_TESTS
