#include "ForgeQABuildManifest.h"
#include "ForgeQALog.h"
#include "Dom/JsonObject.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"
#include "Misc/FileHelper.h"
#include "Misc/Paths.h"

FForgeQABuildContext FForgeQABuildManifest::ToBuildContext() const
{
    FForgeQABuildContext Context;
    Context.ProjectId = ProjectId;
    Context.BuildId = BuildId;
    Context.Version = Version;
    Context.BuildNumber = BuildNumber;
    Context.Platform = Platform;
    Context.Configuration = Configuration;
    Context.Branch = Branch;
    Context.CommitSha = CommitSha;
    return Context;
}

FString FForgeQABuildManifestSerializer::GetManifestFilePath()
{
    return FPaths::ProjectContentDir() / TEXT("ForgeQA") / TEXT("ForgeQABuild.json");
}

FString FForgeQABuildManifestSerializer::ToJsonString(const FForgeQABuildManifest& Manifest)
{
    const TSharedRef<FJsonObject> Root = MakeShared<FJsonObject>();
    Root->SetNumberField(TEXT("schemaVersion"), Manifest.SchemaVersion);
    Root->SetStringField(TEXT("projectId"), Manifest.ProjectId.ToString(EGuidFormats::DigitsWithHyphens));
    Root->SetStringField(TEXT("buildId"), Manifest.BuildId.ToString(EGuidFormats::DigitsWithHyphens));
    Root->SetStringField(TEXT("version"), Manifest.Version);
    Root->SetStringField(TEXT("buildNumber"), Manifest.BuildNumber);
    Root->SetStringField(TEXT("platform"), Manifest.Platform);
    Root->SetStringField(TEXT("configuration"), Manifest.Configuration);
    Root->SetStringField(TEXT("branch"), Manifest.Branch);
    Root->SetStringField(TEXT("commitSha"), Manifest.CommitSha);

    FString Output;
    const TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&Output);
    FJsonSerializer::Serialize(Root, Writer);
    return Output;
}

static bool TryGetGuidField(const TSharedPtr<FJsonObject>& Object, const TCHAR* FieldName, FGuid& OutGuid, FString& OutError)
{
    FString Raw;
    if (!Object->TryGetStringField(FieldName, Raw))
    {
        OutError = FString::Printf(TEXT("Manifest is missing required field '%s'."), FieldName);
        return false;
    }

    if (!FGuid::Parse(Raw, OutGuid) || !OutGuid.IsValid())
    {
        OutError = FString::Printf(TEXT("Manifest field '%s' is not a valid UUID: '%s'."), FieldName, *Raw);
        return false;
    }

    return true;
}

bool FForgeQABuildManifestSerializer::TryParseJsonString(const FString& JsonString, FForgeQABuildManifest& OutManifest, FString& OutError)
{
    TSharedPtr<FJsonObject> Root;
    const TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(JsonString);
    if (!FJsonSerializer::Deserialize(Reader, Root) || !Root.IsValid())
    {
        OutError = TEXT("Manifest is not valid JSON.");
        return false;
    }

    int32 SchemaVersion = 0;
    if (!Root->TryGetNumberField(TEXT("schemaVersion"), SchemaVersion))
    {
        OutError = TEXT("Manifest is missing required field 'schemaVersion'.");
        return false;
    }

    if (SchemaVersion > ForgeQABuildManifestSchemaVersion)
    {
        OutError = FString::Printf(
            TEXT("Manifest schema version %d is newer than this plugin supports (%d)."),
            SchemaVersion, ForgeQABuildManifestSchemaVersion);
        return false;
    }

    FForgeQABuildManifest Manifest;
    Manifest.SchemaVersion = SchemaVersion;

    if (!TryGetGuidField(Root, TEXT("projectId"), Manifest.ProjectId, OutError))
    {
        return false;
    }

    if (!TryGetGuidField(Root, TEXT("buildId"), Manifest.BuildId, OutError))
    {
        return false;
    }

    Root->TryGetStringField(TEXT("version"), Manifest.Version);
    Root->TryGetStringField(TEXT("buildNumber"), Manifest.BuildNumber);
    Root->TryGetStringField(TEXT("platform"), Manifest.Platform);
    Root->TryGetStringField(TEXT("configuration"), Manifest.Configuration);
    Root->TryGetStringField(TEXT("branch"), Manifest.Branch);
    Root->TryGetStringField(TEXT("commitSha"), Manifest.CommitSha);

    OutManifest = Manifest;
    return true;
}

bool FForgeQABuildManifestSerializer::TryLoadFromFile(FForgeQABuildManifest& OutManifest, FString& OutError)
{
    return TryLoadFromFile(GetManifestFilePath(), OutManifest, OutError);
}

bool FForgeQABuildManifestSerializer::TryLoadFromFile(const FString& FilePath, FForgeQABuildManifest& OutManifest, FString& OutError)
{
    FString FileContents;
    if (!FFileHelper::LoadFileToString(FileContents, *FilePath))
    {
        OutError = FString::Printf(TEXT("Manifest file not found at '%s'."), *FilePath);
        return false;
    }

    return TryParseJsonString(FileContents, OutManifest, OutError);
}

bool FForgeQABuildManifestSerializer::SaveToFile(const FForgeQABuildManifest& Manifest, FString& OutError)
{
    const FString FilePath = GetManifestFilePath();

    if (!FFileHelper::SaveStringToFile(ToJsonString(Manifest), *FilePath))
    {
        OutError = FString::Printf(TEXT("Failed to write manifest to '%s'."), *FilePath);
        return false;
    }

    return true;
}
