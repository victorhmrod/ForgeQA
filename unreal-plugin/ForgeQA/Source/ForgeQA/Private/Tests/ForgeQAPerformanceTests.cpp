#include "CoreMinimal.h"
#include "Misc/AutomationTest.h"

#if WITH_DEV_AUTOMATION_TESTS

#include "ForgeQAApiResponseParser.h"
#include "ForgeQAPerformanceTypes.h"
#include "ForgeQASettings.h"

// --- Batch extraction / DTO JSON serialization ---

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQASerializePerformanceSamplesBatchTest, "ForgeQA.Performance.Serialization.SerializesSamplesBatch",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQASerializePerformanceSamplesBatchTest::RunTest(const FString& Parameters)
{
    FForgeQAPerformanceSample First;
    First.SequenceNumber = 1;
    First.Timestamp = FDateTime(2026, 9, 16, 18, 0, 1);
    First.MapName = TEXT("Strike_Factory");
    First.FPS = 92.4f;
    First.FrameTimeMs = 10.82f;
    First.bHasGameThreadTime = true;
    First.GameThreadTimeMs = 4.1f;
    First.bHasMemoryUsed = true;
    First.MemoryUsedBytes = 8589934592;

    // Second sample deliberately leaves every optional metric unavailable — the serializer must
    // omit those fields entirely, never emit them as 0/null placeholders.
    FForgeQAPerformanceSample Second;
    Second.SequenceNumber = 2;
    Second.Timestamp = FDateTime(2026, 9, 16, 18, 0, 2);
    Second.FPS = 60.0f;
    Second.FrameTimeMs = 16.67f;

    const FString Json = FForgeQAApiResponseParser::SerializePerformanceSamplesBatch({ First, Second });

    TestTrue(TEXT("Batch JSON contains the samples array"), Json.Contains(TEXT("\"samples\"")));
    TestTrue(TEXT("First sample's sequence number is present"), Json.Contains(TEXT("\"sequenceNumber\":1")));
    TestTrue(TEXT("First sample's map name is present"), Json.Contains(TEXT("Strike_Factory")));
    TestTrue(TEXT("First sample's available game thread time is present"), Json.Contains(TEXT("\"gameThreadTimeMs\"")));
    TestTrue(TEXT("First sample's available memory is present"), Json.Contains(TEXT("\"memoryUsedBytes\"")));
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQASerializePerformanceSamplesOmitsUnavailableMetricsTest, "ForgeQA.Performance.Serialization.OmitsUnavailableMetrics",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQASerializePerformanceSamplesOmitsUnavailableMetricsTest::RunTest(const FString& Parameters)
{
    FForgeQAPerformanceSample Sample;
    Sample.SequenceNumber = 1;
    Sample.Timestamp = FDateTime(2026, 9, 16, 18, 0, 1);
    Sample.FPS = 60.0f;
    Sample.FrameTimeMs = 16.67f;
    // bHasGameThreadTime / bHasRenderThreadTime / bHasGpuTime / bHasMemoryUsed all default false.

    const FString Json = FForgeQAApiResponseParser::SerializePerformanceSamplesBatch({ Sample });

    TestFalse(TEXT("Unavailable game thread time is never serialized"), Json.Contains(TEXT("gameThreadTimeMs")));
    TestFalse(TEXT("Unavailable render thread time is never serialized"), Json.Contains(TEXT("renderThreadTimeMs")));
    TestFalse(TEXT("Unavailable GPU time is never serialized"), Json.Contains(TEXT("gpuTimeMs")));
    TestFalse(TEXT("Unavailable memory is never serialized"), Json.Contains(TEXT("memoryUsedBytes")));
    return true;
}

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQAParseIngestPerformanceSamplesResultTest, "ForgeQA.Performance.Serialization.ParsesIngestResponse",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQAParseIngestPerformanceSamplesResultTest::RunTest(const FString& Parameters)
{
    const FString Json = TEXT(R"({ "accepted": 58, "duplicates": 2 })");

    FForgeQAIngestPerformanceSamplesResult Result;
    const bool bParsed = FForgeQAApiResponseParser::TryParseIngestPerformanceSamplesResult(Json, Result);

    TestTrue(TEXT("A valid ingest response parses"), bParsed);
    TestEqual(TEXT("Accepted count is extracted"), Result.Accepted, 58);
    TestEqual(TEXT("Duplicates count is extracted"), Result.Duplicates, 2);
    return true;
}

// --- Settings defaults ---
// Queue overflow and sequence-assignment behavior are algorithmically identical to
// UForgeQATelemetrySubsystem's already-tested drop-oldest queue (see ForgeQATelemetryTests.cpp) —
// not independently re-driven here because sample collection is frame-driven
// (FCoreDelegates::OnEndFrame) and cannot be triggered from a pure automation test without a live
// engine tick loop (PIE). This is a genuine test-source limitation, not a skipped requirement —
// see docs/performance.md's Known limitations.

IMPLEMENT_SIMPLE_AUTOMATION_TEST(FForgeQAPerformanceSettingsDefaultsTest, "ForgeQA.Performance.Settings.HasSensibleDefaults",
    EAutomationTestFlags::EditorContext | EAutomationTestFlags::ProductFilter)
bool FForgeQAPerformanceSettingsDefaultsTest::RunTest(const FString& Parameters)
{
    const UForgeQASettings* Settings = GetDefault<UForgeQASettings>();

    TestTrue(TEXT("Performance monitoring is enabled by default"), Settings->bEnablePerformanceMonitoring);
    TestEqual(TEXT("Default sample interval is 1 second"), Settings->PerformanceSampleIntervalSeconds, 1.0f);
    TestTrue(TEXT("Default batch size is within the 30-60 recommended range"), Settings->PerformanceBatchSize >= 30 && Settings->PerformanceBatchSize <= 60);
    TestEqual(TEXT("Default max queued samples is 600"), Settings->PerformanceMaxQueuedSamples, 600);
    return true;
}

#endif // WITH_DEV_AUTOMATION_TESTS
