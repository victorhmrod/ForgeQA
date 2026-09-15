#include "ForgeQABugReportingSubsystem.h"
#include "ForgeQAApiClient.h"
#include "ForgeQASettings.h"
#include "ForgeQASubsystem.h"
#include "ForgeQARuntimeCredentials.h"
#include "ForgeQALog.h"
#include "Engine/GameViewportClient.h"
#include "HighResScreenshot.h"
#include "ImageUtils.h"
#include "HAL/PlatformMisc.h"
#include "HAL/PlatformMemory.h"
#include "Internationalization/Culture.h"

void UForgeQABugReportingSubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
    Super::Initialize(Collection);
    ApiClient = MakeShared<FForgeQAApiClient>(GetDefault<UForgeQASettings>()->GetNormalizedApiBaseUrl());
}

void UForgeQABugReportingSubsystem::Deinitialize()
{
    ApiClient.Reset();
    Super::Deinitialize();
}

bool UForgeQABugReportingSubsystem::IsBugReportingAvailable() const
{
    const UForgeQASubsystem* ForgeQA = GetGameInstance() ? GetGameInstance()->GetSubsystem<UForgeQASubsystem>() : nullptr;
    if (!ForgeQA || !ForgeQA->HasValidBuildContext())
    {
        return false;
    }

    return !FForgeQARuntimeCredentials::ResolveApiKey().IsEmpty();
}

void UForgeQABugReportingSubsystem::CaptureScreenshot(FForgeQAOnScreenshotCaptured OnComplete)
{
    if (!GEngine || !GEngine->GameViewport)
    {
        UE_LOG(LogForgeQA, Warning, TEXT("Cannot capture a ForgeQA bug screenshot: no active game viewport."));
        OnComplete.ExecuteIfBound(false, TArray<uint8>());
        return;
    }

    SubmissionState = EForgeQABugSubmissionState::Capturing;

    const TSharedRef<FDelegateHandle> HandleRef = MakeShared<FDelegateHandle>();
    *HandleRef = GEngine->GameViewport->OnScreenshotCaptured().AddLambda(
        [this, OnComplete, HandleRef](int32 Width, int32 Height, const TArray<FColor>& Colors)
        {
            if (GEngine && GEngine->GameViewport)
            {
                GEngine->GameViewport->OnScreenshotCaptured().Remove(*HandleRef);
            }

            TArray<FColor> OpaqueColors(Colors);
            for (FColor& Pixel : OpaqueColors)
            {
                Pixel.A = 255;
            }

            TArray<uint8> PngBytes;
            FImageUtils::PNGCompressImageArray(Width, Height, OpaqueColors, PngBytes);

            SubmissionState = EForgeQABugSubmissionState::Idle;
            OnComplete.ExecuteIfBound(true, PngBytes);
        });

    FScreenshotRequest::RequestScreenshot(false);
}

void UForgeQABugReportingSubsystem::SubmitBugReport(const FForgeQABugReportRequest& Request, const TArray<uint8>& ScreenshotPngBytes, FForgeQAOnBugReportSubmitted OnComplete)
{
    // Duplicate-submission protection: the caller (e.g. the Bug Report UMG widget) should already
    // disable its Submit button while a request is in flight, but this is the authoritative guard.
    if (SubmissionState != EForgeQABugSubmissionState::Idle && SubmissionState != EForgeQABugSubmissionState::Succeeded && SubmissionState != EForgeQABugSubmissionState::Failed)
    {
        OnComplete.ExecuteIfBound(false, TEXT("A bug report submission is already in progress."));
        return;
    }

    if (!IsBugReportingAvailable())
    {
        OnComplete.ExecuteIfBound(false, TEXT("ForgeQA bug reporting is unavailable: no ForgeQA Build is linked, or no runtime API key is configured."));
        return;
    }

    const UForgeQASubsystem* ForgeQA = GetGameInstance()->GetSubsystem<UForgeQASubsystem>();
    const FForgeQABuildContext& Context = ForgeQA->GetBuildContext();
    const FString ApiKey = FForgeQARuntimeCredentials::ResolveApiKey();

    FForgeQACreateBugReportRequest ApiRequest;
    ApiRequest.BuildId = Context.BuildId;
    ApiRequest.Title = Request.Title;
    ApiRequest.Description = Request.Description;
    ApiRequest.ReproductionSteps = Request.ReproductionSteps;
    ApiRequest.Severity = SeverityToString(Request.Severity);
    ApiRequest.Source = TEXT("UNREAL_RUNTIME");
    ApiRequest.RuntimeSessionId = ForgeQA->GetRuntimeSessionId();

    ApiRequest.Environment.Platform = Context.Platform;
    ApiRequest.Environment.EngineVersion = Context.EngineVersion;
    const UWorld* World = GetGameInstance() ? GetGameInstance()->GetWorld() : nullptr;
    ApiRequest.Environment.MapName = World ? World->GetMapName() : FString();
    ApiRequest.Environment.OsVersion = FPlatformMisc::GetOSVersion();
    ApiRequest.Environment.Cpu = FPlatformMisc::GetCPUBrand();
    ApiRequest.Environment.Gpu = FPlatformMisc::GetPrimaryGPUBrand();
    ApiRequest.Environment.MemoryBytes = static_cast<int64>(FPlatformMemory::GetConstants().TotalPhysical);
    if (const FCulturePtr Culture = FInternationalization::Get().GetCurrentLocale())
    {
        ApiRequest.Environment.Locale = Culture->GetName();
    }

    SubmissionState = EForgeQABugSubmissionState::SubmittingReport;

    const TSharedRef<FForgeQAApiClient> Client = ApiClient.ToSharedRef();
    const FGuid ProjectId = Context.ProjectId;

    Client->CreateBugReport(ApiKey, ProjectId, ApiRequest,
        [this, ApiKey, ProjectId, ScreenshotPngBytes, OnComplete](bool bSuccess, const FForgeQABugReportResult& Result, const FForgeQAApiError& Error)
        {
            if (!bSuccess)
            {
                SubmissionState = EForgeQABugSubmissionState::Failed;
                UE_LOG(LogForgeQA, Warning, TEXT("ForgeQA bug report submission failed: %s"), *Error.Message);
                OnComplete.ExecuteIfBound(false, Error.Message);
                return;
            }

            UE_LOG(LogForgeQA, Log, TEXT("ForgeQA bug report created: %s"), *Result.BugReportId.ToString());

            if (ScreenshotPngBytes.Num() == 0)
            {
                SubmissionState = EForgeQABugSubmissionState::Succeeded;
                OnComplete.ExecuteIfBound(true, FString());
                return;
            }

            UploadScreenshotAttachment(ApiKey, ProjectId, Result.BugReportId, ScreenshotPngBytes, OnComplete);
        });
}

void UForgeQABugReportingSubsystem::UploadScreenshotAttachment(const FString& ApiKey, const FGuid& ProjectId, const FGuid& BugReportId, TArray<uint8> PngBytes, FForgeQAOnBugReportSubmitted OnComplete)
{
    SubmissionState = EForgeQABugSubmissionState::UploadingScreenshot;

    FForgeQAInitiateAttachmentRequest AttachmentRequest;
    AttachmentRequest.Type = TEXT("SCREENSHOT");
    AttachmentRequest.FileName = TEXT("screenshot.png");
    AttachmentRequest.ContentType = TEXT("image/png");
    AttachmentRequest.SizeBytes = PngBytes.Num();

    const TSharedRef<FForgeQAApiClient> Client = ApiClient.ToSharedRef();

    Client->InitiateBugAttachment(ApiKey, ProjectId, BugReportId, AttachmentRequest,
        [this, Client, ApiKey, ProjectId, BugReportId, PngBytes, OnComplete](bool bSuccess, const FForgeQAInitiateAttachmentResult& InitiateResult, const FForgeQAApiError& Error) mutable
        {
            if (!bSuccess)
            {
                // The bug report itself was already created successfully; a screenshot failure
                // is logged and surfaced as a soft success, not a hard failure of the whole submission.
                UE_LOG(LogForgeQA, Warning, TEXT("ForgeQA bug report was created, but the screenshot attachment could not be initiated: %s"), *Error.Message);
                SubmissionState = EForgeQABugSubmissionState::Succeeded;
                OnComplete.ExecuteIfBound(true, FString());
                return;
            }

            Client->PutObject(InitiateResult.UploadUrl, TEXT("image/png"), PngBytes,
                [this, Client, ApiKey, ProjectId, BugReportId, AttachmentId = InitiateResult.AttachmentId, OnComplete](bool bPutSuccess)
                {
                    if (!bPutSuccess)
                    {
                        UE_LOG(LogForgeQA, Warning, TEXT("ForgeQA bug report was created, but the screenshot upload failed."));
                        SubmissionState = EForgeQABugSubmissionState::Succeeded;
                        OnComplete.ExecuteIfBound(true, FString());
                        return;
                    }

                    SubmissionState = EForgeQABugSubmissionState::Finalizing;
                    Client->CompleteBugAttachment(ApiKey, ProjectId, BugReportId, AttachmentId,
                        [this, OnComplete](bool bCompleteSuccess, const FForgeQAApiError& CompleteError)
                        {
                            if (!bCompleteSuccess)
                            {
                                UE_LOG(LogForgeQA, Warning, TEXT("ForgeQA bug report was created, but the screenshot attachment could not be finalized: %s"), *CompleteError.Message);
                            }

                            SubmissionState = EForgeQABugSubmissionState::Succeeded;
                            OnComplete.ExecuteIfBound(true, FString());
                        });
                });
        });
}

FString UForgeQABugReportingSubsystem::SeverityToString(EForgeQABugSeverity Severity)
{
    switch (Severity)
    {
    case EForgeQABugSeverity::Low: return TEXT("LOW");
    case EForgeQABugSeverity::Medium: return TEXT("MEDIUM");
    case EForgeQABugSeverity::High: return TEXT("HIGH");
    case EForgeQABugSeverity::Critical: return TEXT("CRITICAL");
    default: return TEXT("MEDIUM");
    }
}
