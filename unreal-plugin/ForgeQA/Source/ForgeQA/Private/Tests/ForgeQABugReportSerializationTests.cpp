#include "CoreMinimal.h"
#include "Misc/AutomationTest.h"

#if WITH_DEV_AUTOMATION_TESTS

#include "ForgeQAApiResponseParser.h"

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQASerializeCreateBugReportRequestTest, "ForgeQA.BugReporting.SerializesCreateBugReportRequest",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQASerializeCreateBugReportRequestTest::RunTest(const FString& Parameters)
{
    FForgeQACreateBugReportRequest Request;
    Request.BuildId = FGuid::NewGuid();
    Request.Title = TEXT("Crash on map load");
    Request.Description = TEXT("Client crashes loading Strike_Factory.");
    Request.Severity = TEXT("CRITICAL");
    Request.Source = TEXT("UNREAL_RUNTIME");
    Request.RuntimeSessionId = FGuid::NewGuid();
    Request.Environment.MapName = TEXT("Strike_Factory");
    Request.Environment.Platform = TEXT("WINDOWS");
    Request.Environment.MemoryBytes = 34359738368;

    const FString Json = FForgeQAApiResponseParser::SerializeCreateBugReportRequest(Request);

    TestTrue(TEXT("Serialized JSON contains the title"), Json.Contains(TEXT("Crash on map load")));
    TestTrue(TEXT("Serialized JSON contains the severity"), Json.Contains(TEXT("\"severity\":\"CRITICAL\"")));
    TestTrue(TEXT("Serialized JSON contains the source"), Json.Contains(TEXT("\"source\":\"UNREAL_RUNTIME\"")));
    TestTrue(TEXT("Serialized JSON contains the buildId"), Json.Contains(Request.BuildId.ToString(EGuidFormats::DigitsWithHyphens)));
    TestTrue(TEXT("Serialized JSON contains the nested environment object"), Json.Contains(TEXT("\"environment\"")));
    TestTrue(TEXT("Serialized JSON contains the map name"), Json.Contains(TEXT("Strike_Factory")));
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQASerializeCreateBugReportRequestWithoutBuildTest, "ForgeQA.BugReporting.SerializesNullBuildIdWhenUnset",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQASerializeCreateBugReportRequestWithoutBuildTest::RunTest(const FString& Parameters)
{
    FForgeQACreateBugReportRequest Request;
    // Request.BuildId left as the default, invalid FGuid — a manual "no Build" report.
    Request.Title = TEXT("General feedback");
    Request.Severity = TEXT("LOW");
    Request.Source = TEXT("WEB");

    const FString Json = FForgeQAApiResponseParser::SerializeCreateBugReportRequest(Request);

    TestTrue(TEXT("An unset BuildId serializes as JSON null, not an empty/invalid GUID string"), Json.Contains(TEXT("\"buildId\":null")));
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQAParseInitiateAttachmentResultTest, "ForgeQA.BugReporting.ParsesInitiateAttachmentResponse",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQAParseInitiateAttachmentResultTest::RunTest(const FString& Parameters)
{
    const FString Json = TEXT(R"({
        "attachmentId": "50b09e84-5693-49cf-817c-5d928d086fc3",
        "uploadUrl": "https://storage.test/put/some-object-key",
        "expiresAt": "2026-01-01T00:15:00Z"
    })");

    FForgeQAInitiateAttachmentResult Result;
    const bool bParsed = FForgeQAApiResponseParser::TryParseInitiateAttachmentResult(Json, Result);

    TestTrue(TEXT("A valid initiate-attachment response parses"), bParsed);
    TestTrue(TEXT("AttachmentId is a valid GUID"), Result.AttachmentId.IsValid());
    TestEqual(TEXT("UploadUrl is extracted"), Result.UploadUrl, FString(TEXT("https://storage.test/put/some-object-key")));
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQAParseBugReportResultTest, "ForgeQA.BugReporting.ParsesBugReportCreationResponse",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQAParseBugReportResultTest::RunTest(const FString& Parameters)
{
    const FString Json = TEXT(R"({ "id": "50b09e84-5693-49cf-817c-5d928d086fc3", "projectId": "9d0f469c-5380-4243-9bb5-a35b6a59ffe7" })");

    FForgeQABugReportResult Result;
    const bool bParsed = FForgeQAApiResponseParser::TryParseBugReportResult(Json, Result);

    TestTrue(TEXT("A valid bug creation response parses"), bParsed);
    TestTrue(TEXT("BugReportId is a valid GUID"), Result.BugReportId.IsValid());
    return true;
}

#endif // WITH_DEV_AUTOMATION_TESTS
