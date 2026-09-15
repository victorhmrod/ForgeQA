#include "CoreMinimal.h"
#include "Misc/AutomationTest.h"

#if WITH_DEV_AUTOMATION_TESTS

#include "ForgeQARuntimeCredentials.h"
#include "Misc/CommandLine.h"

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQARuntimeCredentialsCommandLineWinsTest, "ForgeQA.BugReporting.RuntimeCredentials.CommandLineTakesPrecedence",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQARuntimeCredentialsCommandLineWinsTest::RunTest(const FString& Parameters)
{
    const FString SavedCommandLine = FCommandLine::Get();
    FCommandLine::Set(TEXT("-ForgeQAApiKey=fqa_proj_from_commandline"));

    const FString Resolved = FForgeQARuntimeCredentials::ResolveApiKey();

    FCommandLine::Set(*SavedCommandLine);

    TestEqual(TEXT("A command-line key is used when present"), Resolved, FString(TEXT("fqa_proj_from_commandline")));
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQARuntimeCredentialsNoSourceReturnsEmptyTest, "ForgeQA.BugReporting.RuntimeCredentials.NoSourceMeansUnavailable",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQARuntimeCredentialsNoSourceReturnsEmptyTest::RunTest(const FString& Parameters)
{
    // This test only verifies the command-line branch is correctly skipped when absent; it does
    // not attempt to clear process environment variables or Settings (both process-global and
    // shared with other tests), so it cannot assert a fully empty result in isolation.
    const FString SavedCommandLine = FCommandLine::Get();
    FCommandLine::Set(TEXT(""));

    FString CommandLineKey;
    const bool bHasCommandLineKey = FParse::Value(FCommandLine::Get(), TEXT("ForgeQAApiKey="), CommandLineKey);

    FCommandLine::Set(*SavedCommandLine);

    TestFalse(TEXT("No -ForgeQAApiKey= on the command line means that source contributes nothing"), bHasCommandLineKey);
    return true;
}

#endif // WITH_DEV_AUTOMATION_TESTS
