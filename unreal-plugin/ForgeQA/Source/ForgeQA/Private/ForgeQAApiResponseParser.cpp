#include "ForgeQAApiResponseParser.h"
#include "Dom/JsonObject.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"

namespace
{
    FGuid ParseGuidField(const TSharedPtr<FJsonObject>& Object, const TCHAR* FieldName)
    {
        FString Raw;
        FGuid Result;
        if (Object.IsValid() && Object->TryGetStringField(FieldName, Raw))
        {
            FGuid::Parse(Raw, Result);
        }
        return Result;
    }

    FString GetStringField(const TSharedPtr<FJsonObject>& Object, const TCHAR* FieldName)
    {
        FString Result;
        if (Object.IsValid())
        {
            Object->TryGetStringField(FieldName, Result);
        }
        return Result;
    }

    bool ParseJsonObject(const FString& JsonBody, TSharedPtr<FJsonObject>& OutObject)
    {
        const TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(JsonBody);
        return FJsonSerializer::Deserialize(Reader, OutObject) && OutObject.IsValid();
    }

    bool ParseJsonArray(const FString& JsonBody, TArray<TSharedPtr<FJsonValue>>& OutArray)
    {
        const TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(JsonBody);
        return FJsonSerializer::Deserialize(Reader, OutArray);
    }
}

FForgeQABuildSummary FForgeQAApiResponseParser::ParseBuildSummaryObject(const TSharedPtr<FJsonObject>& Object)
{
    FForgeQABuildSummary Build;
    if (!Object.IsValid())
    {
        return Build;
    }

    Build.Id = ParseGuidField(Object, TEXT("id"));
    Build.ProjectId = ParseGuidField(Object, TEXT("projectId"));
    Build.Name = GetStringField(Object, TEXT("name"));
    Build.Version = GetStringField(Object, TEXT("version"));
    Build.BuildNumber = GetStringField(Object, TEXT("buildNumber"));
    Build.Platform = GetStringField(Object, TEXT("platform"));
    Build.Configuration = GetStringField(Object, TEXT("configuration"));

    const TSharedPtr<FJsonObject>* SourceObject = nullptr;
    if (Object->TryGetObjectField(TEXT("source"), SourceObject) && SourceObject)
    {
        Build.Branch = GetStringField(*SourceObject, TEXT("branch"));
        Build.CommitSha = GetStringField(*SourceObject, TEXT("commitSha"));
    }

    FString ArchivedAt;
    Build.bArchived = Object->TryGetStringField(TEXT("archivedAt"), ArchivedAt) && !ArchivedAt.IsEmpty();

    return Build;
}

bool FForgeQAApiResponseParser::TryParseAuthResult(const FString& JsonBody, FForgeQAAuthResult& OutResult)
{
    TSharedPtr<FJsonObject> Root;
    if (!ParseJsonObject(JsonBody, Root))
    {
        return false;
    }

    OutResult.AccessToken = GetStringField(Root, TEXT("accessToken"));
    OutResult.RefreshToken = GetStringField(Root, TEXT("refreshToken"));

    const TSharedPtr<FJsonObject>* UserObject = nullptr;
    if (Root->TryGetObjectField(TEXT("user"), UserObject) && UserObject)
    {
        OutResult.UserDisplayName = GetStringField(*UserObject, TEXT("displayName"));
        OutResult.UserEmail = GetStringField(*UserObject, TEXT("email"));
    }

    return !OutResult.AccessToken.IsEmpty();
}

bool FForgeQAApiResponseParser::TryParseOrganizations(const FString& JsonBody, TArray<FForgeQAOrganizationSummary>& OutOrganizations)
{
    TArray<TSharedPtr<FJsonValue>> Items;
    if (!ParseJsonArray(JsonBody, Items))
    {
        return false;
    }

    for (const TSharedPtr<FJsonValue>& Item : Items)
    {
        const TSharedPtr<FJsonObject> Object = Item->AsObject();
        if (!Object.IsValid())
        {
            continue;
        }

        FForgeQAOrganizationSummary Organization;
        Organization.Id = ParseGuidField(Object, TEXT("id"));
        Organization.Name = GetStringField(Object, TEXT("name"));
        Organization.Slug = GetStringField(Object, TEXT("slug"));
        Organization.Role = GetStringField(Object, TEXT("role"));
        OutOrganizations.Add(Organization);
    }

    return true;
}

bool FForgeQAApiResponseParser::TryParseProjects(const FString& JsonBody, const FGuid& OrganizationId, TArray<FForgeQAProjectSummary>& OutProjects)
{
    TArray<TSharedPtr<FJsonValue>> Items;
    if (!ParseJsonArray(JsonBody, Items))
    {
        return false;
    }

    for (const TSharedPtr<FJsonValue>& Item : Items)
    {
        const TSharedPtr<FJsonObject> Object = Item->AsObject();
        if (!Object.IsValid())
        {
            continue;
        }

        FForgeQAProjectSummary Project;
        Project.Id = ParseGuidField(Object, TEXT("id"));
        Project.OrganizationId = OrganizationId;
        Project.Name = GetStringField(Object, TEXT("name"));
        Project.Slug = GetStringField(Object, TEXT("slug"));
        OutProjects.Add(Project);
    }

    return true;
}

bool FForgeQAApiResponseParser::TryParsePagedBuilds(const FString& JsonBody, TArray<FForgeQABuildSummary>& OutBuilds)
{
    TSharedPtr<FJsonObject> Root;
    if (!ParseJsonObject(JsonBody, Root))
    {
        return false;
    }

    const TArray<TSharedPtr<FJsonValue>>* ItemsArray = nullptr;
    if (Root->TryGetArrayField(TEXT("items"), ItemsArray) && ItemsArray)
    {
        for (const TSharedPtr<FJsonValue>& Item : *ItemsArray)
        {
            OutBuilds.Add(ParseBuildSummaryObject(Item->AsObject()));
        }
    }

    return true;
}

bool FForgeQAApiResponseParser::TryParseBuildDetail(const FString& JsonBody, FForgeQABuildSummary& OutBuild)
{
    TSharedPtr<FJsonObject> Root;
    if (!ParseJsonObject(JsonBody, Root))
    {
        return false;
    }

    OutBuild = ParseBuildSummaryObject(Root);
    return OutBuild.Id.IsValid();
}

void FForgeQAApiResponseParser::ParseProblemDetails(const FString& JsonBody, int32 StatusCode, FForgeQAApiError& OutError)
{
    OutError.StatusCode = StatusCode;

    TSharedPtr<FJsonObject> Body;
    if (ParseJsonObject(JsonBody, Body))
    {
        OutError.Code = GetStringField(Body, TEXT("title"));
        const FString Detail = GetStringField(Body, TEXT("detail"));
        OutError.Message = Detail.IsEmpty() ? OutError.Code : Detail;
    }

    if (OutError.Message.IsEmpty())
    {
        switch (StatusCode)
        {
        case 401: OutError.Code = TEXT("Unauthorized"); OutError.Message = TEXT("Invalid credentials or expired session."); break;
        case 403: OutError.Code = TEXT("Forbidden"); OutError.Message = TEXT("You do not have access to this resource."); break;
        case 404: OutError.Code = TEXT("NotFound"); OutError.Message = TEXT("The requested resource was not found."); break;
        default: OutError.Code = TEXT("ServerError"); OutError.Message = FString::Printf(TEXT("ForgeQA API returned status %d."), StatusCode); break;
        }
    }
}
