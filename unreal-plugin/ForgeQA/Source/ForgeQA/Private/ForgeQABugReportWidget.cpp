#include "ForgeQABugReportWidget.h"
#include "ForgeQABugReportingSubsystem.h"
#include "ForgeQASubsystem.h"
#include "ForgeQALog.h"
#include "Engine/GameInstance.h"

void UForgeQABugReportWidget::NativeConstruct()
{
    Super::NativeConstruct();
    RefreshBuildInfo();
}

void UForgeQABugReportWidget::RefreshBuildInfo()
{
    const UGameInstance* GameInstance = GetGameInstance();
    const UForgeQASubsystem* ForgeQA = GameInstance ? GameInstance->GetSubsystem<UForgeQASubsystem>() : nullptr;

    if (!ForgeQA || !ForgeQA->HasValidBuildContext())
    {
        OnBuildInfoUpdated(false, FString(), FString(), FString());
        return;
    }

    const FForgeQABuildContext& Context = ForgeQA->GetBuildContext();
    const FString VersionAndBuildNumber = FString::Printf(TEXT("%s · %s"), *Context.Version, *Context.BuildNumber);
    const FString PlatformAndConfiguration = FString::Printf(TEXT("%s · %s"), *Context.Platform, *Context.Configuration);

    const UWorld* World = GameInstance ? GameInstance->GetWorld() : nullptr;
    const FString MapName = World ? World->GetMapName() : FString();

    OnBuildInfoUpdated(true, VersionAndBuildNumber, PlatformAndConfiguration, MapName);
}

void UForgeQABugReportWidget::CaptureScreenshot()
{
    UGameInstance* GameInstance = GetGameInstance();
    UForgeQABugReportingSubsystem* BugReporting = GameInstance ? GameInstance->GetSubsystem<UForgeQABugReportingSubsystem>() : nullptr;
    if (!BugReporting)
    {
        UE_LOG(LogForgeQA, Warning, TEXT("ForgeQA bug reporting subsystem unavailable; cannot capture screenshot."));
        return;
    }

    FForgeQAOnScreenshotCaptured Delegate;
    Delegate.BindDynamic(this, &UForgeQABugReportWidget::HandleScreenshotCaptured);
    BugReporting->CaptureScreenshot(Delegate);
}

void UForgeQABugReportWidget::HandleScreenshotCaptured(bool bSuccess, const TArray<uint8>& PngBytes)
{
    CapturedScreenshotPng = bSuccess ? PngBytes : TArray<uint8>();
    OnScreenshotPreviewUpdated(bSuccess, CapturedScreenshotPng);
}

void UForgeQABugReportWidget::SubmitBugReport(const FString& Title, const FString& Description, const FString& ReproductionSteps, EForgeQABugSeverity Severity)
{
    UGameInstance* GameInstance = GetGameInstance();
    UForgeQABugReportingSubsystem* BugReporting = GameInstance ? GameInstance->GetSubsystem<UForgeQABugReportingSubsystem>() : nullptr;
    if (!BugReporting)
    {
        OnSubmissionFailed(TEXT("ForgeQA bug reporting is unavailable."));
        return;
    }

    FForgeQABugReportRequest Request;
    Request.Title = Title;
    Request.Description = Description;
    Request.ReproductionSteps = ReproductionSteps;
    Request.Severity = Severity;

    OnSubmissionStateChanged(EForgeQABugSubmissionState::SubmittingReport);

    FForgeQAOnBugReportSubmitted Delegate;
    Delegate.BindDynamic(this, &UForgeQABugReportWidget::HandleSubmissionComplete);
    BugReporting->SubmitBugReport(Request, CapturedScreenshotPng, Delegate);
}

void UForgeQABugReportWidget::HandleSubmissionComplete(bool bSuccess, const FString& ErrorMessage)
{
    if (bSuccess)
    {
        OnSubmissionStateChanged(EForgeQABugSubmissionState::Succeeded);
        OnSubmissionSucceeded();
        ClearCapturedScreenshot();
    }
    else
    {
        OnSubmissionStateChanged(EForgeQABugSubmissionState::Failed);
        OnSubmissionFailed(ErrorMessage);
    }
}
