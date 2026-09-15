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
