#pragma once

#include "CoreMinimal.h"
#include "Blueprint/UserWidget.h"
#include "ForgeQABugReportingTypes.h"
#include "ForgeQABugReportWidget.generated.h"

/**
 * C++ base class for the in-game "Report Bug" UMG widget.
 *
 * This class provides the behavior (state, screenshot capture, submission) as
 * BlueprintCallable functions and BlueprintImplementableEvents; the actual visual layout — text
 * boxes, the severity dropdown, the screenshot preview image, the Submit/Cancel buttons — is a
 * .uasset Widget Blueprint that must be authored inside the Unreal Editor (Content Browser >
 * Add > User Interface > Widget Blueprint > Parent Class: ForgeQABugReportWidget). A binary
 * .uasset cannot be produced by this repository's tooling; only the C++ behavior it drives can be.
 *
 * Suggested Blueprint layout (see docs/bug-reporting.md for the full wireframe):
 *   Title / Severity / Description / Steps to Reproduce / [Capture Screenshot] / Build (read-only)
 *   / Map (read-only) / [Cancel] [Submit Bug]
 */
UCLASS(Abstract)
class FORGEQA_API UForgeQABugReportWidget : public UUserWidget
{
    GENERATED_BODY()

public:
    /** Call from the Blueprint's Construct event, or let it run automatically via NativeConstruct. */
    UFUNCTION(BlueprintCallable, Category = "ForgeQA|Bug Reporting")
    void RefreshBuildInfo();

    /** Bind to the "Capture Screenshot" button. Hides this widget for the capture, then re-shows it. */
    UFUNCTION(BlueprintCallable, Category = "ForgeQA|Bug Reporting")
    void CaptureScreenshot();

    /** Bind to the "Submit Bug" button. */
    UFUNCTION(BlueprintCallable, Category = "ForgeQA|Bug Reporting")
    void SubmitBugReport(const FString& Title, const FString& Description, const FString& ReproductionSteps, EForgeQABugSeverity Severity);

    UFUNCTION(BlueprintPure, Category = "ForgeQA|Bug Reporting")
    bool HasCapturedScreenshot() const { return CapturedScreenshotPng.Num() > 0; }

    UFUNCTION(BlueprintCallable, Category = "ForgeQA|Bug Reporting")
    void ClearCapturedScreenshot() { CapturedScreenshotPng.Reset(); }

protected:
    virtual void NativeConstruct() override;

    /** Implement in the Widget Blueprint to populate the read-only Build/Map fields. */
    UFUNCTION(BlueprintImplementableEvent, Category = "ForgeQA|Bug Reporting")
    void OnBuildInfoUpdated(bool bIsLinked, const FString& VersionAndBuildNumber, const FString& PlatformAndConfiguration, const FString& MapName);

    /** Implement to display the captured screenshot (e.g. from PNG bytes via a dynamic texture) or hide the preview. */
    UFUNCTION(BlueprintImplementableEvent, Category = "ForgeQA|Bug Reporting")
    void OnScreenshotPreviewUpdated(bool bHasScreenshot, const TArray<uint8>& PngBytes);

    /** Implement to reflect EForgeQABugSubmissionState in the UI (e.g. disable Submit, show a spinner). */
    UFUNCTION(BlueprintImplementableEvent, Category = "ForgeQA|Bug Reporting")
    void OnSubmissionStateChanged(EForgeQABugSubmissionState NewState);

    /** Implement to show a success toast/panel and typically close the widget. */
    UFUNCTION(BlueprintImplementableEvent, Category = "ForgeQA|Bug Reporting")
    void OnSubmissionSucceeded();

    /** Implement to surface ErrorMessage to the tester (network failure, unauthorized, server rejection, etc.). */
    UFUNCTION(BlueprintImplementableEvent, Category = "ForgeQA|Bug Reporting")
    void OnSubmissionFailed(const FString& ErrorMessage);

private:
    UPROPERTY()
    TArray<uint8> CapturedScreenshotPng;

    UFUNCTION()
    void HandleScreenshotCaptured(bool bSuccess, const TArray<uint8>& PngBytes);

    UFUNCTION()
    void HandleSubmissionComplete(bool bSuccess, const FString& ErrorMessage);
};
