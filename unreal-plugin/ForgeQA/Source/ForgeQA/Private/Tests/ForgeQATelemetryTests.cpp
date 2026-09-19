#include "CoreMinimal.h"
#include "Misc/AutomationTest.h"

#if WITH_DEV_AUTOMATION_TESTS

#include "ForgeQATelemetrySubsystem.h"
#include "ForgeQAApiResponseParser.h"
#include "ForgeQATelemetryTypes.h"
#include "ForgeQASettings.h"
#include "Dom/JsonObject.h"

// --- Event name validation (mirrors the backend's TelemetryEvent.EventName rule) ---

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQATelemetryValidEventNamesTest, "ForgeQA.Telemetry.EventNameValidation.AcceptsValidNames",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQATelemetryValidEventNamesTest::RunTest(const FString& Parameters)
{
    TestTrue(TEXT("Simple dotted name is valid"), UForgeQATelemetrySubsystem::IsValidEventName(TEXT("weapon.fired")));
    TestTrue(TEXT("Underscore-separated name is valid"), UForgeQATelemetrySubsystem::IsValidEventName(TEXT("player_died")));
    TestTrue(TEXT("Hyphenated name is valid"), UForgeQATelemetrySubsystem::IsValidEventName(TEXT("ui-menu-opened")));
    TestTrue(TEXT("Name with digits is valid"), UForgeQATelemetrySubsystem::IsValidEventName(TEXT("qa.checkpoint-1")));
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQATelemetryInvalidEventNamesTest, "ForgeQA.Telemetry.EventNameValidation.RejectsInvalidNames",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQATelemetryInvalidEventNamesTest::RunTest(const FString& Parameters)
{
    TestFalse(TEXT("Empty name is invalid"), UForgeQATelemetrySubsystem::IsValidEventName(FString()));
    TestFalse(TEXT("Name with spaces is invalid"), UForgeQATelemetrySubsystem::IsValidEventName(TEXT("has spaces")));
    TestFalse(TEXT("Name with a slash is invalid"), UForgeQATelemetrySubsystem::IsValidEventName(TEXT("has/slash")));
    TestFalse(TEXT("Name over 128 characters is invalid"), UForgeQATelemetrySubsystem::IsValidEventName(FString::ChrN(129, TEXT('a'))));
    return true;
}

// --- Retry classification ---
// Moved to the shared FForgeQARetryPolicy in M6 (Telemetry and Performance both use it — see
// ForgeQARetryPolicyTests.cpp) so it is exercised once rather than duplicated per subsystem.

// --- Sequence assignment and queue maximum behavior ---
// UForgeQATelemetrySubsystem's TrackEvent()/queue logic only touches GetGameInstance() inside
// TryFlush(), which is never reached without a valid Build Context — so a plain NewObject instance
// (no live UGameInstance) can safely exercise queuing without a full PIE session.

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQATelemetrySequenceAssignmentTest, "ForgeQA.Telemetry.Queue.EachTrackedEventIsQueuedOnce",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQATelemetrySequenceAssignmentTest::RunTest(const FString& Parameters)
{
    UForgeQATelemetrySubsystem* Subsystem = NewObject<UForgeQATelemetrySubsystem>();
    FForgeQATelemetryProperties Properties;

    // TelemetryBatchSize defaults to 50, so 3 events never trigger an auto-flush (which would
    // require a live GameInstance and is out of scope for this test).
    Subsystem->TrackEvent(TEXT("game.started"), Properties);
    Subsystem->TrackEvent(TEXT("level.loaded"), Properties);
    Subsystem->TrackEvent(TEXT("player.spawned"), Properties);

    TestEqual(TEXT("Three valid events are queued exactly once each"), Subsystem->GetQueuedEventCount(), 3);
    TestEqual(TEXT("No events were dropped"), Subsystem->GetDroppedEventCount(), 0);
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQATelemetryInvalidEventIsNotQueuedTest, "ForgeQA.Telemetry.Queue.InvalidEventNameIsNotQueued",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQATelemetryInvalidEventIsNotQueuedTest::RunTest(const FString& Parameters)
{
    UForgeQATelemetrySubsystem* Subsystem = NewObject<UForgeQATelemetrySubsystem>();
    FForgeQATelemetryProperties Properties;

    Subsystem->TrackEvent(TEXT("valid.event"), Properties);
    Subsystem->TrackEvent(TEXT("has spaces"), Properties);

    TestEqual(TEXT("Only the valid event was queued"), Subsystem->GetQueuedEventCount(), 1);
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQATelemetryQueueOverflowTest, "ForgeQA.Telemetry.Queue.OverflowDropsOldestEvents",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQATelemetryQueueOverflowTest::RunTest(const FString& Parameters)
{
    UForgeQASettings* Settings = GetMutableDefault<UForgeQASettings>();
    const int32 SavedMaxQueuedEvents = Settings->TelemetryMaxQueuedEvents;
    const int32 SavedBatchSize = Settings->TelemetryBatchSize;
    Settings->TelemetryMaxQueuedEvents = 3;
    Settings->TelemetryBatchSize = 1000; // prevent an auto-flush attempt from interfering with this test

    UForgeQATelemetrySubsystem* Subsystem = NewObject<UForgeQATelemetrySubsystem>();
    FForgeQATelemetryProperties Properties;

    for (int32 Index = 0; Index < 5; ++Index)
    {
        Subsystem->TrackEvent(FString::Printf(TEXT("qa.checkpoint-%d"), Index), Properties);
    }

    TestEqual(TEXT("Queue never exceeds the configured maximum"), Subsystem->GetQueuedEventCount(), 3);
    TestEqual(TEXT("The two oldest events were dropped"), Subsystem->GetDroppedEventCount(), 2);

    Settings->TelemetryMaxQueuedEvents = SavedMaxQueuedEvents;
    Settings->TelemetryBatchSize = SavedBatchSize;
    return true;
}

// --- Batch extraction / DTO JSON serialization ---

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQASerializeTelemetryEventsBatchTest, "ForgeQA.Telemetry.Serialization.SerializesEventsBatch",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQASerializeTelemetryEventsBatchTest::RunTest(const FString& Parameters)
{
    FForgeQATelemetryEvent First;
    First.SequenceNumber = 1;
    First.EventName = TEXT("game.started");
    First.ClientTimestamp = FDateTime(2026, 9, 15, 18, 0, 0);
    First.PropertiesJson = TEXT("{\"map\":\"Lobby\"}");

    FForgeQATelemetryEvent Second;
    Second.SequenceNumber = 2;
    Second.EventName = TEXT("level.loaded");
    Second.ClientTimestamp = FDateTime(2026, 9, 15, 18, 0, 3);
    Second.MapName = TEXT("Strike_Factory");

    const FString Json = FForgeQAApiResponseParser::SerializeTelemetryEventsBatch({ First, Second });

    TestTrue(TEXT("Batch JSON contains the events array"), Json.Contains(TEXT("\"events\"")));
    TestTrue(TEXT("First event's sequence number is present"), Json.Contains(TEXT("\"sequenceNumber\":1")));
    TestTrue(TEXT("First event's name is present"), Json.Contains(TEXT("game.started")));
    TestTrue(TEXT("First event's properties are nested as a JSON object, not an escaped string"), Json.Contains(TEXT("\"map\":\"Lobby\"")));
    TestTrue(TEXT("Second event's map name is present"), Json.Contains(TEXT("Strike_Factory")));
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQASerializeStartTelemetrySessionRequestTest, "ForgeQA.Telemetry.Serialization.SerializesSessionStartRequest",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQASerializeStartTelemetrySessionRequestTest::RunTest(const FString& Parameters)
{
    FForgeQAStartTelemetrySessionRequest Request;
    Request.RuntimeSessionId = FGuid::NewGuid();
    Request.BuildId = FGuid::NewGuid();
    Request.Environment.MapName = TEXT("Strike_Factory");
    Request.Environment.Platform = TEXT("WINDOWS");

    const FString Json = FForgeQAApiResponseParser::SerializeStartTelemetrySessionRequest(Request);

    TestTrue(TEXT("Serialized JSON contains the runtimeSessionId"), Json.Contains(Request.RuntimeSessionId.ToString(EGuidFormats::DigitsWithHyphens)));
    TestTrue(TEXT("Serialized JSON contains the buildId"), Json.Contains(Request.BuildId.ToString(EGuidFormats::DigitsWithHyphens)));
    TestTrue(TEXT("Serialized JSON contains the nested environment object"), Json.Contains(TEXT("\"environment\"")));
    TestTrue(TEXT("Serialized JSON contains the map name"), Json.Contains(TEXT("Strike_Factory")));
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQAParseTelemetrySessionResultTest, "ForgeQA.Telemetry.Serialization.ParsesSessionStartResponse",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQAParseTelemetrySessionResultTest::RunTest(const FString& Parameters)
{
    const FString Json = TEXT(R"({ "id": "50b09e84-5693-49cf-817c-5d928d086fc3", "runtimeSessionId": "9d0f469c-5380-4243-9bb5-a35b6a59ffe7" })");

    FForgeQATelemetrySessionResult Result;
    const bool bParsed = FForgeQAApiResponseParser::TryParseTelemetrySessionResult(Json, Result);

    TestTrue(TEXT("A valid session-start response parses"), bParsed);
    TestTrue(TEXT("Id is a valid GUID"), Result.Id.IsValid());
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQAParseIngestTelemetryEventsResultTest, "ForgeQA.Telemetry.Serialization.ParsesIngestResponse",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQAParseIngestTelemetryEventsResultTest::RunTest(const FString& Parameters)
{
    const FString Json = TEXT(R"({ "accepted": 2, "duplicates": 1 })");

    FForgeQAIngestTelemetryEventsResult Result;
    const bool bParsed = FForgeQAApiResponseParser::TryParseIngestTelemetryEventsResult(Json, Result);

    TestTrue(TEXT("A valid ingest response parses"), bParsed);
    TestEqual(TEXT("Accepted count is extracted"), Result.Accepted, 2);
    TestEqual(TEXT("Duplicates count is extracted"), Result.Duplicates, 1);
    return true;
}

// --- FForgeQATelemetryProperties ---

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQATelemetryPropertiesToJsonObjectTest, "ForgeQA.Telemetry.Properties.BuildsPlainJsonObject",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQATelemetryPropertiesToJsonObjectTest::RunTest(const FString& Parameters)
{
    FForgeQATelemetryProperties Properties;
    Properties.StringProperties.Add(TEXT("weaponId"), TEXT("rifle_a"));
    Properties.NumberProperties.Add(TEXT("ammoRemaining"), 23.0f);
    Properties.BoolProperties.Add(TEXT("isAiming"), true);

    const TSharedRef<FJsonObject> Object = Properties.ToJsonObject();

    FString StringValue;
    TestTrue(TEXT("String property round-trips"), Object->TryGetStringField(TEXT("weaponId"), StringValue) && StringValue == TEXT("rifle_a"));

    double NumberValue = 0.0;
    TestTrue(TEXT("Number property round-trips"), Object->TryGetNumberField(TEXT("ammoRemaining"), NumberValue) && FMath::IsNearlyEqual(NumberValue, 23.0));

    bool bBoolValue = false;
    TestTrue(TEXT("Bool property round-trips"), Object->TryGetBoolField(TEXT("isAiming"), bBoolValue) && bBoolValue);
    return true;
}

#endif // WITH_DEV_AUTOMATION_TESTS
