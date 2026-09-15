#include "CoreMinimal.h"
#include "Misc/AutomationTest.h"

#if WITH_DEV_AUTOMATION_TESTS

#include "ForgeQAApiResponseParser.h"

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQAParseValidAuthResponseTest, "ForgeQA.ApiResponseParser.ParsesValidAuthResponse",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQAParseValidAuthResponseTest::RunTest(const FString& Parameters)
{
    const FString Json = TEXT(R"({
        "accessToken": "token-abc",
        "accessTokenExpiresAt": "2026-01-01T00:00:00Z",
        "refreshToken": "refresh-xyz",
        "refreshTokenExpiresAt": "2026-02-01T00:00:00Z",
        "user": { "id": "9d0f469c-5380-4243-9bb5-a35b6a59ffe7", "email": "victor@example.com", "displayName": "Victor", "createdAt": "2026-01-01T00:00:00Z" }
    })");

    FForgeQAAuthResult Result;
    const bool bParsed = FForgeQAApiResponseParser::TryParseAuthResult(Json, Result);

    TestTrue(TEXT("A valid login response parses"), bParsed);
    TestEqual(TEXT("AccessToken is extracted"), Result.AccessToken, FString(TEXT("token-abc")));
    TestEqual(TEXT("UserEmail is extracted from the nested user object"), Result.UserEmail, FString(TEXT("victor@example.com")));
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQAParseInvalidAuthResponseTest, "ForgeQA.ApiResponseParser.RejectsInvalidJsonAuthResponse",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQAParseInvalidAuthResponseTest::RunTest(const FString& Parameters)
{
    FForgeQAAuthResult Result;
    const bool bParsed = FForgeQAApiResponseParser::TryParseAuthResult(TEXT("not json at all"), Result);

    TestFalse(TEXT("Malformed JSON does not parse as an auth result"), bParsed);
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQAParseProjectsResponseTest, "ForgeQA.ApiResponseParser.ParsesProjectsResponse",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQAParseProjectsResponseTest::RunTest(const FString& Parameters)
{
    const FString Json = TEXT(R"([
        { "id": "50b09e84-5693-49cf-817c-5d928d086fc3", "organizationId": "9d0f469c-5380-4243-9bb5-a35b6a59ffe7", "name": "FRONTLINE", "slug": "frontline" }
    ])");

    const FGuid OrganizationId = FGuid::NewGuid();
    TArray<FForgeQAProjectSummary> Projects;
    const bool bParsed = FForgeQAApiResponseParser::TryParseProjects(Json, OrganizationId, Projects);

    TestTrue(TEXT("A projects array parses"), bParsed);
    TestEqual(TEXT("One project is returned"), Projects.Num(), 1);
    TestEqual(TEXT("Project name is extracted"), Projects[0].Name, FString(TEXT("FRONTLINE")));
    TestEqual(TEXT("Caller-supplied OrganizationId is attached"), Projects[0].OrganizationId, OrganizationId);
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQAParseBuildDetailResponseTest, "ForgeQA.ApiResponseParser.ParsesBuildDetailResponse",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQAParseBuildDetailResponseTest::RunTest(const FString& Parameters)
{
    const FString Json = TEXT(R"({
        "id": "50b09e84-5693-49cf-817c-5d928d086fc3",
        "projectId": "9d0f469c-5380-4243-9bb5-a35b6a59ffe7",
        "name": "QA Candidate",
        "version": "0.4.2",
        "buildNumber": "1842",
        "platform": "WINDOWS",
        "configuration": "DEVELOPMENT",
        "source": { "branch": "main", "commitSha": "a941de3" },
        "archivedAt": null
    })");

    FForgeQABuildSummary Build;
    const bool bParsed = FForgeQAApiResponseParser::TryParseBuildDetail(Json, Build);

    TestTrue(TEXT("A build detail response parses"), bParsed);
    TestEqual(TEXT("BuildNumber is extracted"), Build.BuildNumber, FString(TEXT("1842")));
    TestEqual(TEXT("Nested source.branch is extracted"), Build.Branch, FString(TEXT("main")));
    TestFalse(TEXT("A null archivedAt means the build is active, not archived"), Build.bArchived);
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQAParseArchivedBuildDetailTest, "ForgeQA.ApiResponseParser.DetectsArchivedBuild",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQAParseArchivedBuildDetailTest::RunTest(const FString& Parameters)
{
    const FString Json = TEXT(R"({
        "id": "50b09e84-5693-49cf-817c-5d928d086fc3",
        "projectId": "9d0f469c-5380-4243-9bb5-a35b6a59ffe7",
        "version": "0.4.2",
        "buildNumber": "1842",
        "platform": "WINDOWS",
        "configuration": "DEVELOPMENT",
        "archivedAt": "2026-02-01T00:00:00Z"
    })");

    FForgeQABuildSummary Build;
    FForgeQAApiResponseParser::TryParseBuildDetail(Json, Build);

    TestTrue(TEXT("A non-null archivedAt marks the build as archived"), Build.bArchived);
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQAParseProblemDetailsTest, "ForgeQA.ApiResponseParser.ParsesProblemDetailsError",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQAParseProblemDetailsTest::RunTest(const FString& Parameters)
{
    const FString Json = TEXT(R"({ "title": "Forbidden", "status": 403, "detail": "You are not a member of this project's organization." })");

    FForgeQAApiError Error;
    FForgeQAApiResponseParser::ParseProblemDetails(Json, 403, Error);

    TestEqual(TEXT("StatusCode is recorded"), Error.StatusCode, 403);
    TestEqual(TEXT("Detail is preferred as the display message"), Error.Message, FString(TEXT("You are not a member of this project's organization.")));
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQAParseProblemDetailsWithoutBodyTest, "ForgeQA.ApiResponseParser.FallsBackToGenericMessageWhenBodyIsEmpty",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQAParseProblemDetailsWithoutBodyTest::RunTest(const FString& Parameters)
{
    FForgeQAApiError Error;
    FForgeQAApiResponseParser::ParseProblemDetails(TEXT(""), 404, Error);

    TestFalse(TEXT("A generic message is still produced when the body is empty"), Error.Message.IsEmpty());
    TestEqual(TEXT("StatusCode is recorded"), Error.StatusCode, 404);
    return true;
}

#endif // WITH_DEV_AUTOMATION_TESTS
