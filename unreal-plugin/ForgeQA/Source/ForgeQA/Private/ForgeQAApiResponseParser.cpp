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

bool FForgeQAApiResponseParser::TryParseBugReportResult(const FString& JsonBody, FForgeQABugReportResult& OutResult)
{
    TSharedPtr<FJsonObject> Root;
    if (!ParseJsonObject(JsonBody, Root))
    {
        return false;
    }

    OutResult.BugReportId = ParseGuidField(Root, TEXT("id"));
    return OutResult.BugReportId.IsValid();
}

bool FForgeQAApiResponseParser::TryParseInitiateAttachmentResult(const FString& JsonBody, FForgeQAInitiateAttachmentResult& OutResult)
{
    TSharedPtr<FJsonObject> Root;
    if (!ParseJsonObject(JsonBody, Root))
    {
        return false;
    }

    OutResult.AttachmentId = ParseGuidField(Root, TEXT("attachmentId"));
    OutResult.UploadUrl = GetStringField(Root, TEXT("uploadUrl"));
    return OutResult.AttachmentId.IsValid() && !OutResult.UploadUrl.IsEmpty();
}

FString FForgeQAApiResponseParser::SerializeCreateBugReportRequest(const FForgeQACreateBugReportRequest& Request)
{
    const TSharedRef<FJsonObject> Root = MakeShared<FJsonObject>();

    if (Request.BuildId.IsValid())
    {
        Root->SetStringField(TEXT("buildId"), Request.BuildId.ToString(EGuidFormats::DigitsWithHyphens));
    }
    else
    {
        Root->SetField(TEXT("buildId"), MakeShared<FJsonValueNull>());
    }

    Root->SetStringField(TEXT("title"), Request.Title);
    Root->SetStringField(TEXT("description"), Request.Description);
    Root->SetStringField(TEXT("reproductionSteps"), Request.ReproductionSteps);
    Root->SetStringField(TEXT("severity"), Request.Severity);
    Root->SetStringField(TEXT("source"), Request.Source);

    if (Request.RuntimeSessionId.IsValid())
    {
        Root->SetStringField(TEXT("runtimeSessionId"), Request.RuntimeSessionId.ToString(EGuidFormats::DigitsWithHyphens));
    }

    const TSharedRef<FJsonObject> EnvironmentObject = MakeShared<FJsonObject>();
    EnvironmentObject->SetStringField(TEXT("mapName"), Request.Environment.MapName);
    EnvironmentObject->SetStringField(TEXT("gameMode"), Request.Environment.GameMode);
    EnvironmentObject->SetStringField(TEXT("platform"), Request.Environment.Platform);
    EnvironmentObject->SetStringField(TEXT("engineVersion"), Request.Environment.EngineVersion);
    EnvironmentObject->SetStringField(TEXT("osVersion"), Request.Environment.OsVersion);
    EnvironmentObject->SetStringField(TEXT("cpu"), Request.Environment.Cpu);
    EnvironmentObject->SetStringField(TEXT("gpu"), Request.Environment.Gpu);
    if (Request.Environment.MemoryBytes > 0)
    {
        EnvironmentObject->SetNumberField(TEXT("memoryBytes"), static_cast<double>(Request.Environment.MemoryBytes));
    }
    EnvironmentObject->SetStringField(TEXT("locale"), Request.Environment.Locale);
    Root->SetObjectField(TEXT("environment"), EnvironmentObject);

    FString Output;
    const TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&Output);
    FJsonSerializer::Serialize(Root, Writer);
    return Output;
}

FString FForgeQAApiResponseParser::SerializeInitiateAttachmentRequest(const FForgeQAInitiateAttachmentRequest& Request)
{
    const TSharedRef<FJsonObject> Root = MakeShared<FJsonObject>();
    Root->SetStringField(TEXT("type"), Request.Type);
    Root->SetStringField(TEXT("fileName"), Request.FileName);
    Root->SetStringField(TEXT("contentType"), Request.ContentType);
    Root->SetNumberField(TEXT("sizeBytes"), static_cast<double>(Request.SizeBytes));

    FString Output;
    const TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&Output);
    FJsonSerializer::Serialize(Root, Writer);
    return Output;
}

FString FForgeQAApiResponseParser::SerializeStartTelemetrySessionRequest(const FForgeQAStartTelemetrySessionRequest& Request)
{
    const TSharedRef<FJsonObject> Root = MakeShared<FJsonObject>();
    Root->SetStringField(TEXT("runtimeSessionId"), Request.RuntimeSessionId.ToString(EGuidFormats::DigitsWithHyphens));
    Root->SetStringField(TEXT("buildId"), Request.BuildId.ToString(EGuidFormats::DigitsWithHyphens));

    const TSharedRef<FJsonObject> EnvironmentObject = MakeShared<FJsonObject>();
    EnvironmentObject->SetStringField(TEXT("mapName"), Request.Environment.MapName);
    EnvironmentObject->SetStringField(TEXT("gameMode"), Request.Environment.GameMode);
    EnvironmentObject->SetStringField(TEXT("platform"), Request.Environment.Platform);
    EnvironmentObject->SetStringField(TEXT("configuration"), Request.Environment.Configuration);
    EnvironmentObject->SetStringField(TEXT("engineVersion"), Request.Environment.EngineVersion);
    EnvironmentObject->SetStringField(TEXT("osVersion"), Request.Environment.OsVersion);
    EnvironmentObject->SetStringField(TEXT("locale"), Request.Environment.Locale);
    Root->SetObjectField(TEXT("environment"), EnvironmentObject);

    FString Output;
    const TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&Output);
    FJsonSerializer::Serialize(Root, Writer);
    return Output;
}

FString FForgeQAApiResponseParser::SerializeTelemetryEventsBatch(const TArray<FForgeQATelemetryEvent>& Events)
{
    const TSharedRef<FJsonObject> Root = MakeShared<FJsonObject>();
    TArray<TSharedPtr<FJsonValue>> EventValues;

    for (const FForgeQATelemetryEvent& Event : Events)
    {
        const TSharedRef<FJsonObject> EventObject = MakeShared<FJsonObject>();
        EventObject->SetNumberField(TEXT("sequenceNumber"), Event.SequenceNumber);
        EventObject->SetStringField(TEXT("eventName"), Event.EventName);
        EventObject->SetStringField(TEXT("clientTimestamp"), Event.ClientTimestamp.ToIso8601());
        if (!Event.Category.IsEmpty())
        {
            EventObject->SetStringField(TEXT("category"), Event.Category);
        }
        if (!Event.MapName.IsEmpty())
        {
            EventObject->SetStringField(TEXT("mapName"), Event.MapName);
        }

        // Properties travel as an already-serialized JSON object string (built once by
        // FForgeQATelemetryProperties::ToJsonObject at TrackEvent() time) — parse it back into a
        // JSON value here rather than nesting it as an escaped string, so the wire format matches
        // the backend's expected JSON object shape exactly.
        TSharedPtr<FJsonValue> PropertiesValue;
        const TSharedRef<TJsonReader<>> PropertiesReader = TJsonReaderFactory<>::Create(Event.PropertiesJson);
        if (FJsonSerializer::Deserialize(PropertiesReader, PropertiesValue) && PropertiesValue.IsValid())
        {
            EventObject->SetField(TEXT("properties"), PropertiesValue);
        }

        EventValues.Add(MakeShared<FJsonValueObject>(EventObject));
    }

    Root->SetArrayField(TEXT("events"), EventValues);

    FString Output;
    const TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&Output);
    FJsonSerializer::Serialize(Root, Writer);
    return Output;
}

bool FForgeQAApiResponseParser::TryParseTelemetrySessionResult(const FString& JsonBody, FForgeQATelemetrySessionResult& OutResult)
{
    TSharedPtr<FJsonObject> Root;
    if (!ParseJsonObject(JsonBody, Root))
    {
        return false;
    }

    OutResult.Id = ParseGuidField(Root, TEXT("id"));
    OutResult.RuntimeSessionId = ParseGuidField(Root, TEXT("runtimeSessionId"));
    return OutResult.Id.IsValid();
}

bool FForgeQAApiResponseParser::TryParseIngestTelemetryEventsResult(const FString& JsonBody, FForgeQAIngestTelemetryEventsResult& OutResult)
{
    TSharedPtr<FJsonObject> Root;
    if (!ParseJsonObject(JsonBody, Root))
    {
        return false;
    }

    Root->TryGetNumberField(TEXT("accepted"), OutResult.Accepted);
    Root->TryGetNumberField(TEXT("duplicates"), OutResult.Duplicates);
    return true;
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
