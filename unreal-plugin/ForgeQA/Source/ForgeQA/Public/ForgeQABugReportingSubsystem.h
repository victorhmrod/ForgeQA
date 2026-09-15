#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "ForgeQABugReportingTypes.h"
#include "ForgeQABugReportingSubsystem.generated.h"

class FForgeQAApiClient;

DECLARE_DYNAMIC_DELEGATE_TwoParams(FForgeQAOnBugReportSubmitted, bool, bSuccess, const FString&, ErrorMessage);
DECLARE_DYNAMIC_DELEGATE_TwoParams(FForgeQAOnScreenshotCaptured, bool, bSuccess, const TArray<uint8>&, PngBytes);

/**
 * The runtime-facing bug reporting service: screenshot capture and asynchronous submission,
 * separated from UForgeQASubsystem (which only resolves identity) to avoid one subsystem growing
 * into a God object as more ForgeQA runtime features (M5 telemetry, M6 performance, M7 crashes)
 * are added alongside it.
 *
 * Never contacts the ForgeQA server on its own initiative — submission only happens when
 * SubmitBugReport is explicitly called, e.g. from the in-game Bug Report UMG widget.
 */
UCLASS()
class FORGEQA_API UForgeQABugReportingSubsystem : public UGameInstanceSubsystem
{
    GENERATED_BODY()

public:
    virtual void Initialize(FSubsystemCollectionBase& Collection) override;
    virtual void Deinitialize() override;

    /** True only when a valid ForgeQA Build Context AND a resolvable Project API key are both present. */
    UFUNCTION(BlueprintPure, Category = "ForgeQA|Bug Reporting", DisplayName = "Is Bug Reporting Available")
    bool IsBugReportingAvailable() const;

    UFUNCTION(BlueprintPure, Category = "ForgeQA|Bug Reporting", DisplayName = "Get Bug Submission State")
    EForgeQABugSubmissionState GetSubmissionState() const { return SubmissionState; }

    /** Requests a viewport screenshot and returns it as PNG bytes. Safe to call even with no Build linked. */
    UFUNCTION(BlueprintCallable, Category = "ForgeQA|Bug Reporting", DisplayName = "Capture Bug Screenshot")
    void CaptureScreenshot(FForgeQAOnScreenshotCaptured OnComplete);

    /**
     * Submits a bug report with automatically attached ForgeQA identity/environment, then (if
     * ScreenshotPngBytes is non-empty) uploads it as a SCREENSHOT attachment. Ignored — and
     * OnComplete called immediately with a clear error — while a submission is already in flight,
     * so rapid double-clicking Submit can never create duplicate reports.
     */
    UFUNCTION(BlueprintCallable, Category = "ForgeQA|Bug Reporting", DisplayName = "Submit Bug Report")
    void SubmitBugReport(const FForgeQABugReportRequest& Request, const TArray<uint8>& ScreenshotPngBytes, FForgeQAOnBugReportSubmitted OnComplete);

private:
    TSharedPtr<FForgeQAApiClient> ApiClient;

    UPROPERTY()
    EForgeQABugSubmissionState SubmissionState = EForgeQABugSubmissionState::Idle;

    void UploadScreenshotAttachment(const FString& ApiKey, const FGuid& ProjectId, const FGuid& BugReportId, TArray<uint8> PngBytes, FForgeQAOnBugReportSubmitted OnComplete);

    static FString SeverityToString(EForgeQABugSeverity Severity);
};
