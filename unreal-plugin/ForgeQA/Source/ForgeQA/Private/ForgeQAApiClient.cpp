#include "ForgeQAApiClient.h"
#include "ForgeQAApiResponseParser.h"
#include "ForgeQALog.h"
#include "HttpModule.h"
#include "Interfaces/IHttpResponse.h"
#include "Dom/JsonObject.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"

FForgeQAApiClient::FForgeQAApiClient(FString InApiBaseUrl)
    : ApiBaseUrl(MoveTemp(InApiBaseUrl))
{
}

TSharedRef<IHttpRequest> FForgeQAApiClient::CreateRequest(const FString& Verb, const FString& Path, const FString& AccessToken) const
{
    const TSharedRef<IHttpRequest> Request = FHttpModule::Get().CreateRequest();
    Request->SetURL(ApiBaseUrl + Path);
    Request->SetVerb(Verb);
    Request->SetHeader(TEXT("Content-Type"), TEXT("application/json"));

    if (!AccessToken.IsEmpty())
    {
        Request->SetHeader(TEXT("Authorization"), TEXT("Bearer ") + AccessToken);
    }

    return Request;
}

TSharedRef<IHttpRequest> FForgeQAApiClient::CreateApiKeyRequest(const FString& Verb, const FString& Path, const FString& ProjectApiKey) const
{
    const TSharedRef<IHttpRequest> Request = FHttpModule::Get().CreateRequest();
    Request->SetURL(ApiBaseUrl + Path);
    Request->SetVerb(Verb);
    Request->SetHeader(TEXT("Content-Type"), TEXT("application/json"));
    // Deliberately not "Authorization: Bearer" — see ProjectApiKeyDefaults on the backend for why
    // a dedicated header keeps a Project key from ever being confused with a user JWT.
    Request->SetHeader(TEXT("X-ForgeQA-Key"), ProjectApiKey);
    return Request;
}

bool FForgeQAApiClient::TryExtractError(const FHttpResponsePtr& Response, bool bConnectedSuccessfully, FForgeQAApiError& OutError)
{
    if (!bConnectedSuccessfully || !Response.IsValid())
    {
        OutError.StatusCode = 0;
        OutError.Code = TEXT("NetworkError");
        OutError.Message = TEXT("Could not reach the ForgeQA API. Check the API Base URL and your network connection.");
        return true;
    }

    const int32 StatusCode = Response->GetResponseCode();
    if (StatusCode >= 200 && StatusCode < 300)
    {
        return false;
    }

    FForgeQAApiResponseParser::ParseProblemDetails(Response->GetContentAsString(), StatusCode, OutError);
    return true;
}

void FForgeQAApiClient::Login(const FString& Email, const FString& Password, FLoginCallback OnComplete)
{
    const TSharedRef<IHttpRequest> Request = CreateRequest(TEXT("POST"), TEXT("/api/auth/login"), FString());

    const TSharedRef<FJsonObject> Payload = MakeShared<FJsonObject>();
    Payload->SetStringField(TEXT("email"), Email);
    Payload->SetStringField(TEXT("password"), Password);

    FString Body;
    const TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&Body);
    FJsonSerializer::Serialize(Payload, Writer);
    Request->SetContentAsString(Body);

    Request->OnProcessRequestComplete().BindLambda(
        [OnComplete](FHttpRequestPtr, FHttpResponsePtr Response, bool bConnectedSuccessfully)
        {
            FForgeQAApiError Error;
            if (TryExtractError(Response, bConnectedSuccessfully, Error))
            {
                OnComplete(false, FForgeQAAuthResult(), Error);
                return;
            }

            FForgeQAAuthResult Result;
            if (!FForgeQAApiResponseParser::TryParseAuthResult(Response->GetContentAsString(), Result))
            {
                FForgeQAApiError ParseError;
                ParseError.Code = TEXT("InvalidResponse");
                ParseError.Message = TEXT("ForgeQA API returned a response that could not be parsed.");
                OnComplete(false, FForgeQAAuthResult(), ParseError);
                return;
            }

            OnComplete(true, Result, FForgeQAApiError());
        });

    Request->ProcessRequest();
}

void FForgeQAApiClient::GetOrganizations(const FString& AccessToken, FOrganizationsCallback OnComplete)
{
    const TSharedRef<IHttpRequest> Request = CreateRequest(TEXT("GET"), TEXT("/api/organizations"), AccessToken);

    Request->OnProcessRequestComplete().BindLambda(
        [OnComplete](FHttpRequestPtr, FHttpResponsePtr Response, bool bConnectedSuccessfully)
        {
            FForgeQAApiError Error;
            if (TryExtractError(Response, bConnectedSuccessfully, Error))
            {
                OnComplete(false, {}, Error);
                return;
            }

            TArray<FForgeQAOrganizationSummary> Organizations;
            FForgeQAApiResponseParser::TryParseOrganizations(Response->GetContentAsString(), Organizations);
            OnComplete(true, Organizations, FForgeQAApiError());
        });

    Request->ProcessRequest();
}

void FForgeQAApiClient::GetProjects(const FString& AccessToken, const FGuid& OrganizationId, FProjectsCallback OnComplete)
{
    const FString Path = FString::Printf(TEXT("/api/organizations/%s/projects"), *OrganizationId.ToString(EGuidFormats::DigitsWithHyphens));
    const TSharedRef<IHttpRequest> Request = CreateRequest(TEXT("GET"), Path, AccessToken);

    Request->OnProcessRequestComplete().BindLambda(
        [OnComplete, OrganizationId](FHttpRequestPtr, FHttpResponsePtr Response, bool bConnectedSuccessfully)
        {
            FForgeQAApiError Error;
            if (TryExtractError(Response, bConnectedSuccessfully, Error))
            {
                OnComplete(false, {}, Error);
                return;
            }

            TArray<FForgeQAProjectSummary> Projects;
            FForgeQAApiResponseParser::TryParseProjects(Response->GetContentAsString(), OrganizationId, Projects);
            OnComplete(true, Projects, FForgeQAApiError());
        });

    Request->ProcessRequest();
}

void FForgeQAApiClient::GetBuilds(const FString& AccessToken, const FGuid& ProjectId, bool bIncludeArchived, FBuildsCallback OnComplete)
{
    const FString Status = bIncludeArchived ? TEXT("all") : TEXT("active");
    const FString Path = FString::Printf(
        TEXT("/api/projects/%s/builds?status=%s&pageSize=100"),
        *ProjectId.ToString(EGuidFormats::DigitsWithHyphens), *Status);
    const TSharedRef<IHttpRequest> Request = CreateRequest(TEXT("GET"), Path, AccessToken);

    Request->OnProcessRequestComplete().BindLambda(
        [OnComplete](FHttpRequestPtr, FHttpResponsePtr Response, bool bConnectedSuccessfully)
        {
            FForgeQAApiError Error;
            if (TryExtractError(Response, bConnectedSuccessfully, Error))
            {
                OnComplete(false, {}, Error);
                return;
            }

            TArray<FForgeQABuildSummary> Builds;
            if (!FForgeQAApiResponseParser::TryParsePagedBuilds(Response->GetContentAsString(), Builds))
            {
                FForgeQAApiError ParseError;
                ParseError.Code = TEXT("InvalidResponse");
                ParseError.Message = TEXT("ForgeQA API returned a response that could not be parsed.");
                OnComplete(false, {}, ParseError);
                return;
            }

            OnComplete(true, Builds, FForgeQAApiError());
        });

    Request->ProcessRequest();
}

void FForgeQAApiClient::GetBuildDetail(const FString& AccessToken, const FGuid& ProjectId, const FGuid& BuildId, FBuildDetailCallback OnComplete)
{
    const FString Path = FString::Printf(
        TEXT("/api/projects/%s/builds/%s"),
        *ProjectId.ToString(EGuidFormats::DigitsWithHyphens), *BuildId.ToString(EGuidFormats::DigitsWithHyphens));
    const TSharedRef<IHttpRequest> Request = CreateRequest(TEXT("GET"), Path, AccessToken);

    Request->OnProcessRequestComplete().BindLambda(
        [OnComplete](FHttpRequestPtr, FHttpResponsePtr Response, bool bConnectedSuccessfully)
        {
            FForgeQAApiError Error;
            if (TryExtractError(Response, bConnectedSuccessfully, Error))
            {
                OnComplete(false, FForgeQABuildSummary(), Error);
                return;
            }

            FForgeQABuildSummary Build;
            if (!FForgeQAApiResponseParser::TryParseBuildDetail(Response->GetContentAsString(), Build))
            {
                FForgeQAApiError ParseError;
                ParseError.Code = TEXT("InvalidResponse");
                ParseError.Message = TEXT("ForgeQA API returned a response that could not be parsed.");
                OnComplete(false, FForgeQABuildSummary(), ParseError);
                return;
            }

            OnComplete(true, Build, FForgeQAApiError());
        });

    Request->ProcessRequest();
}

void FForgeQAApiClient::CreateBugReport(const FString& ProjectApiKey, const FGuid& ProjectId, const FForgeQACreateBugReportRequest& BugRequest, FCreateBugReportCallback OnComplete)
{
    const FString Path = FString::Printf(TEXT("/api/projects/%s/bugs"), *ProjectId.ToString(EGuidFormats::DigitsWithHyphens));
    const TSharedRef<IHttpRequest> Request = CreateApiKeyRequest(TEXT("POST"), Path, ProjectApiKey);
    Request->SetContentAsString(FForgeQAApiResponseParser::SerializeCreateBugReportRequest(BugRequest));

    Request->OnProcessRequestComplete().BindLambda(
        [OnComplete](FHttpRequestPtr, FHttpResponsePtr Response, bool bConnectedSuccessfully)
        {
            FForgeQAApiError Error;
            if (TryExtractError(Response, bConnectedSuccessfully, Error))
            {
                OnComplete(false, FForgeQABugReportResult(), Error);
                return;
            }

            FForgeQABugReportResult Result;
            if (!FForgeQAApiResponseParser::TryParseBugReportResult(Response->GetContentAsString(), Result))
            {
                FForgeQAApiError ParseError;
                ParseError.Code = TEXT("InvalidResponse");
                ParseError.Message = TEXT("ForgeQA API returned a response that could not be parsed.");
                OnComplete(false, FForgeQABugReportResult(), ParseError);
                return;
            }

            OnComplete(true, Result, FForgeQAApiError());
        });

    Request->ProcessRequest();
}

void FForgeQAApiClient::InitiateBugAttachment(const FString& ProjectApiKey, const FGuid& ProjectId, const FGuid& BugId, const FForgeQAInitiateAttachmentRequest& AttachmentRequest, FInitiateAttachmentCallback OnComplete)
{
    const FString Path = FString::Printf(
        TEXT("/api/projects/%s/bugs/%s/attachments"),
        *ProjectId.ToString(EGuidFormats::DigitsWithHyphens), *BugId.ToString(EGuidFormats::DigitsWithHyphens));
    const TSharedRef<IHttpRequest> Request = CreateApiKeyRequest(TEXT("POST"), Path, ProjectApiKey);
    Request->SetContentAsString(FForgeQAApiResponseParser::SerializeInitiateAttachmentRequest(AttachmentRequest));

    Request->OnProcessRequestComplete().BindLambda(
        [OnComplete](FHttpRequestPtr, FHttpResponsePtr Response, bool bConnectedSuccessfully)
        {
            FForgeQAApiError Error;
            if (TryExtractError(Response, bConnectedSuccessfully, Error))
            {
                OnComplete(false, FForgeQAInitiateAttachmentResult(), Error);
                return;
            }

            FForgeQAInitiateAttachmentResult Result;
            if (!FForgeQAApiResponseParser::TryParseInitiateAttachmentResult(Response->GetContentAsString(), Result))
            {
                FForgeQAApiError ParseError;
                ParseError.Code = TEXT("InvalidResponse");
                ParseError.Message = TEXT("ForgeQA API returned a response that could not be parsed.");
                OnComplete(false, FForgeQAInitiateAttachmentResult(), ParseError);
                return;
            }

            OnComplete(true, Result, FForgeQAApiError());
        });

    Request->ProcessRequest();
}

void FForgeQAApiClient::CompleteBugAttachment(const FString& ProjectApiKey, const FGuid& ProjectId, const FGuid& BugId, const FGuid& AttachmentId, FCompleteAttachmentCallback OnComplete)
{
    const FString Path = FString::Printf(
        TEXT("/api/projects/%s/bugs/%s/attachments/%s/complete"),
        *ProjectId.ToString(EGuidFormats::DigitsWithHyphens), *BugId.ToString(EGuidFormats::DigitsWithHyphens), *AttachmentId.ToString(EGuidFormats::DigitsWithHyphens));
    const TSharedRef<IHttpRequest> Request = CreateApiKeyRequest(TEXT("POST"), Path, ProjectApiKey);

    Request->OnProcessRequestComplete().BindLambda(
        [OnComplete](FHttpRequestPtr, FHttpResponsePtr Response, bool bConnectedSuccessfully)
        {
            FForgeQAApiError Error;
            if (TryExtractError(Response, bConnectedSuccessfully, Error))
            {
                OnComplete(false, Error);
                return;
            }

            OnComplete(true, FForgeQAApiError());
        });

    Request->ProcessRequest();
}

void FForgeQAApiClient::StartTelemetrySession(const FString& ProjectApiKey, const FGuid& ProjectId, const FForgeQAStartTelemetrySessionRequest& TelemetryRequest, FStartTelemetrySessionCallback OnComplete)
{
    const FString Path = FString::Printf(TEXT("/api/projects/%s/telemetry/sessions"), *ProjectId.ToString(EGuidFormats::DigitsWithHyphens));
    const TSharedRef<IHttpRequest> Request = CreateApiKeyRequest(TEXT("POST"), Path, ProjectApiKey);
    Request->SetContentAsString(FForgeQAApiResponseParser::SerializeStartTelemetrySessionRequest(TelemetryRequest));

    Request->OnProcessRequestComplete().BindLambda(
        [OnComplete](FHttpRequestPtr, FHttpResponsePtr Response, bool bConnectedSuccessfully)
        {
            FForgeQAApiError Error;
            if (TryExtractError(Response, bConnectedSuccessfully, Error))
            {
                OnComplete(false, FForgeQATelemetrySessionResult(), Error);
                return;
            }

            FForgeQATelemetrySessionResult Result;
            if (!FForgeQAApiResponseParser::TryParseTelemetrySessionResult(Response->GetContentAsString(), Result))
            {
                FForgeQAApiError ParseError;
                ParseError.Code = TEXT("InvalidResponse");
                ParseError.Message = TEXT("ForgeQA API returned a response that could not be parsed.");
                OnComplete(false, FForgeQATelemetrySessionResult(), ParseError);
                return;
            }

            OnComplete(true, Result, FForgeQAApiError());
        });

    Request->ProcessRequest();
}

void FForgeQAApiClient::SendTelemetryEvents(const FString& ProjectApiKey, const FGuid& ProjectId, const FGuid& RuntimeSessionId, const TArray<FForgeQATelemetryEvent>& Events, FSendTelemetryEventsCallback OnComplete)
{
    const FString Path = FString::Printf(
        TEXT("/api/projects/%s/telemetry/sessions/%s/events"),
        *ProjectId.ToString(EGuidFormats::DigitsWithHyphens), *RuntimeSessionId.ToString(EGuidFormats::DigitsWithHyphens));
    const TSharedRef<IHttpRequest> Request = CreateApiKeyRequest(TEXT("POST"), Path, ProjectApiKey);
    Request->SetContentAsString(FForgeQAApiResponseParser::SerializeTelemetryEventsBatch(Events));

    Request->OnProcessRequestComplete().BindLambda(
        [OnComplete](FHttpRequestPtr, FHttpResponsePtr Response, bool bConnectedSuccessfully)
        {
            FForgeQAApiError Error;
            if (TryExtractError(Response, bConnectedSuccessfully, Error))
            {
                OnComplete(false, FForgeQAIngestTelemetryEventsResult(), Error);
                return;
            }

            FForgeQAIngestTelemetryEventsResult Result;
            if (!FForgeQAApiResponseParser::TryParseIngestTelemetryEventsResult(Response->GetContentAsString(), Result))
            {
                FForgeQAApiError ParseError;
                ParseError.Code = TEXT("InvalidResponse");
                ParseError.Message = TEXT("ForgeQA API returned a response that could not be parsed.");
                OnComplete(false, FForgeQAIngestTelemetryEventsResult(), ParseError);
                return;
            }

            OnComplete(true, Result, FForgeQAApiError());
        });

    Request->ProcessRequest();
}

void FForgeQAApiClient::EndTelemetrySession(const FString& ProjectApiKey, const FGuid& ProjectId, const FGuid& RuntimeSessionId, FEndTelemetrySessionCallback OnComplete)
{
    const FString Path = FString::Printf(
        TEXT("/api/projects/%s/telemetry/sessions/%s/end"),
        *ProjectId.ToString(EGuidFormats::DigitsWithHyphens), *RuntimeSessionId.ToString(EGuidFormats::DigitsWithHyphens));
    const TSharedRef<IHttpRequest> Request = CreateApiKeyRequest(TEXT("POST"), Path, ProjectApiKey);

    Request->OnProcessRequestComplete().BindLambda(
        [OnComplete](FHttpRequestPtr, FHttpResponsePtr Response, bool bConnectedSuccessfully)
        {
            FForgeQAApiError Error;
            if (TryExtractError(Response, bConnectedSuccessfully, Error))
            {
                OnComplete(false, Error);
                return;
            }

            OnComplete(true, FForgeQAApiError());
        });

    Request->ProcessRequest();
}

void FForgeQAApiClient::SendPerformanceSamples(const FString& ProjectApiKey, const FGuid& ProjectId, const FGuid& RuntimeSessionId, const TArray<FForgeQAPerformanceSample>& Samples, FSendPerformanceSamplesCallback OnComplete)
{
    const FString Path = FString::Printf(
        TEXT("/api/projects/%s/performance/sessions/%s/samples"),
        *ProjectId.ToString(EGuidFormats::DigitsWithHyphens), *RuntimeSessionId.ToString(EGuidFormats::DigitsWithHyphens));
    const TSharedRef<IHttpRequest> Request = CreateApiKeyRequest(TEXT("POST"), Path, ProjectApiKey);
    Request->SetContentAsString(FForgeQAApiResponseParser::SerializePerformanceSamplesBatch(Samples));

    Request->OnProcessRequestComplete().BindLambda(
        [OnComplete](FHttpRequestPtr, FHttpResponsePtr Response, bool bConnectedSuccessfully)
        {
            FForgeQAApiError Error;
            if (TryExtractError(Response, bConnectedSuccessfully, Error))
            {
                OnComplete(false, FForgeQAIngestPerformanceSamplesResult(), Error);
                return;
            }

            FForgeQAIngestPerformanceSamplesResult Result;
            if (!FForgeQAApiResponseParser::TryParseIngestPerformanceSamplesResult(Response->GetContentAsString(), Result))
            {
                FForgeQAApiError ParseError;
                ParseError.Code = TEXT("InvalidResponse");
                ParseError.Message = TEXT("ForgeQA API returned a response that could not be parsed.");
                OnComplete(false, FForgeQAIngestPerformanceSamplesResult(), ParseError);
                return;
            }

            OnComplete(true, Result, FForgeQAApiError());
        });

    Request->ProcessRequest();
}

void FForgeQAApiClient::PutObject(const FString& UploadUrl, const FString& ContentType, TArray<uint8> Bytes, FPutObjectCallback OnComplete)
{
    const TSharedRef<IHttpRequest> Request = FHttpModule::Get().CreateRequest();
    Request->SetURL(UploadUrl);
    Request->SetVerb(TEXT("PUT"));
    Request->SetHeader(TEXT("Content-Type"), ContentType);
    Request->SetContent(MoveTemp(Bytes));

    Request->OnProcessRequestComplete().BindLambda(
        [OnComplete](FHttpRequestPtr, FHttpResponsePtr Response, bool bConnectedSuccessfully)
        {
            const bool bOk = bConnectedSuccessfully && Response.IsValid() && Response->GetResponseCode() >= 200 && Response->GetResponseCode() < 300;
            OnComplete(bOk);
        });

    Request->ProcessRequest();
}
