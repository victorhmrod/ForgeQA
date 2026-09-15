#include "ForgeQAEditorSession.h"
#include "ForgeQASettings.h"
#include "ForgeQABuildManifest.h"
#include "ForgeQALog.h"
#include "Misc/ConfigCacheIni.h"

FForgeQAEditorSession::FForgeQAEditorSession()
    : ApiClient(MakeShared<FForgeQAApiClient>(GetDefault<UForgeQASettings>()->GetNormalizedApiBaseUrl()))
{
}

void FForgeQAEditorSession::SetState(EForgeQAConnectionState NewState, const FString& Message)
{
    State = NewState;
    StatusMessage = Message;
}

void FForgeQAEditorSession::SetApiBaseUrl(const FString& InApiBaseUrl)
{
    ApiClient->SetApiBaseUrl(InApiBaseUrl);
}

void FForgeQAEditorSession::Login(const FString& Email, const FString& Password, TFunction<void(bool, const FString&)> OnComplete)
{
    SetState(EForgeQAConnectionState::Authenticating, TEXT("Signing in..."));

    const TSharedRef<FForgeQAEditorSession> SelfRef = SharedThis(this);
    ApiClient->Login(Email, Password,
        [SelfRef, OnComplete](bool bSuccess, const FForgeQAAuthResult& Result, const FForgeQAApiError& Error)
        {
            if (!bSuccess)
            {
                SelfRef->SetState(EForgeQAConnectionState::NotAuthenticated, Error.Message);
                OnComplete(false, Error.Message);
                return;
            }

            SelfRef->AccessToken = Result.AccessToken;
            SelfRef->SetState(EForgeQAConnectionState::Connected, FString::Printf(TEXT("Signed in as %s."), *Result.UserEmail));

            SelfRef->ApiClient->GetOrganizations(SelfRef->AccessToken,
                [SelfRef, OnComplete](bool bOrgSuccess, const TArray<FForgeQAOrganizationSummary>& Orgs, const FForgeQAApiError& OrgError)
                {
                    if (!bOrgSuccess)
                    {
                        SelfRef->SetState(EForgeQAConnectionState::NotAuthenticated, OrgError.Message);
                        OnComplete(false, OrgError.Message);
                        return;
                    }

                    SelfRef->Organizations = Orgs;
                    OnComplete(true, FString());
                });
        });
}

void FForgeQAEditorSession::Logout()
{
    AccessToken.Reset();
    Organizations.Reset();
    Projects.Reset();
    Builds.Reset();
    SelectedProject.Reset();
    SelectedBuild.Reset();
    SetState(EForgeQAConnectionState::NotAuthenticated, TEXT("Signed out."));
}

void FForgeQAEditorSession::RefreshProjects(const FGuid& OrganizationId, TFunction<void(bool, const FString&)> OnComplete)
{
    const TSharedRef<FForgeQAEditorSession> SelfRef = SharedThis(this);
    ApiClient->GetProjects(AccessToken, OrganizationId,
        [SelfRef, OnComplete](bool bSuccess, const TArray<FForgeQAProjectSummary>& InProjects, const FForgeQAApiError& Error)
        {
            if (!bSuccess)
            {
                OnComplete(false, Error.Message);
                return;
            }

            SelfRef->Projects = InProjects;
            OnComplete(true, FString());
        });
}

void FForgeQAEditorSession::RefreshAllProjects(TFunction<void(bool, const FString&)> OnComplete)
{
    if (Organizations.Num() == 0)
    {
        Projects.Reset();
        OnComplete(true, FString());
        return;
    }

    const TSharedRef<FForgeQAEditorSession> SelfRef = SharedThis(this);
    const TSharedRef<TArray<FForgeQAProjectSummary>> Aggregated = MakeShared<TArray<FForgeQAProjectSummary>>();
    const TSharedRef<int32> RemainingCount = MakeShared<int32>(Organizations.Num());
    const TSharedRef<bool> bAnyFailed = MakeShared<bool>(false);
    const TSharedRef<FString> FirstError = MakeShared<FString>();

    for (const FForgeQAOrganizationSummary& Organization : Organizations)
    {
        ApiClient->GetProjects(AccessToken, Organization.Id,
            [SelfRef, Aggregated, RemainingCount, bAnyFailed, FirstError, OnComplete](
                bool bSuccess, const TArray<FForgeQAProjectSummary>& InProjects, const FForgeQAApiError& Error)
            {
                if (bSuccess)
                {
                    Aggregated->Append(InProjects);
                }
                else if (!*bAnyFailed)
                {
                    *bAnyFailed = true;
                    *FirstError = Error.Message;
                }

                if (--(*RemainingCount) == 0)
                {
                    SelfRef->Projects = *Aggregated;
                    OnComplete(!*bAnyFailed, *FirstError);
                }
            });
    }
}

void FForgeQAEditorSession::RefreshBuilds(const FGuid& ProjectId, bool bIncludeArchived, TFunction<void(bool, const FString&)> OnComplete)
{
    const TSharedRef<FForgeQAEditorSession> SelfRef = SharedThis(this);
    ApiClient->GetBuilds(AccessToken, ProjectId, bIncludeArchived,
        [SelfRef, OnComplete](bool bSuccess, const TArray<FForgeQABuildSummary>& InBuilds, const FForgeQAApiError& Error)
        {
            if (!bSuccess)
            {
                OnComplete(false, Error.Message);
                return;
            }

            SelfRef->Builds = InBuilds;
            OnComplete(true, FString());
        });
}

void FForgeQAEditorSession::SetSelectedProject(const FForgeQAProjectSummary& Project)
{
    SelectedProject = Project;
    SelectedBuild.Reset();
    Builds.Reset();
    SetState(EForgeQAConnectionState::ProjectSelected, FString::Printf(TEXT("Project '%s' selected."), *Project.Name));
}

void FForgeQAEditorSession::SetSelectedBuild(const FForgeQABuildSummary& Build)
{
    SelectedBuild = Build;
    SetState(EForgeQAConnectionState::BuildSelected, FString::Printf(TEXT("Build '%s' selected."), *Build.GetDisplayLabel()));
}

void FForgeQAEditorSession::ValidateBinding(TFunction<void(const FForgeQABindingValidationResult&)> OnComplete) const
{
    FForgeQABindingValidationResult Result;

    if (!SelectedProject.IsSet())
    {
        Result.ErrorMessage = TEXT("Select a ForgeQA Project first.");
        OnComplete(Result);
        return;
    }

    if (!SelectedBuild.IsSet())
    {
        Result.ErrorMessage = TEXT("Select a ForgeQA Build first.");
        OnComplete(Result);
        return;
    }

    const FGuid ProjectId = SelectedProject->Id;
    const FGuid BuildId = SelectedBuild->Id;

    // Re-fetch the Build detail scoped to the selected Project: this is the authoritative check
    // that the Build actually belongs to the Project (the backend 404s otherwise), not just a
    // client-side comparison of cached IDs.
    ApiClient->GetBuildDetail(AccessToken, ProjectId, BuildId,
        [OnComplete](bool bSuccess, const FForgeQABuildSummary& Build, const FForgeQAApiError& Error)
        {
            FForgeQABindingValidationResult ValidationResult;
            if (!bSuccess)
            {
                ValidationResult.ErrorMessage = Error.StatusCode == 404
                    ? TEXT("This Build does not belong to the selected Project, or no longer exists.")
                    : Error.Message;
                OnComplete(ValidationResult);
                return;
            }

            ValidationResult.bSuccess = true;
            ValidationResult.ValidatedBuild = Build;
            OnComplete(ValidationResult);
        });
}

bool FForgeQAEditorSession::ApplyBinding(const FForgeQABindingValidationResult& ValidatedResult, FString& OutError)
{
    if (!ValidatedResult.bSuccess || !SelectedProject.IsSet())
    {
        OutError = TEXT("Validate the binding successfully before applying it.");
        return false;
    }

    UForgeQASettings* Settings = GetMutableDefault<UForgeQASettings>();
    Settings->ProjectId = SelectedProject->Id;
    Settings->DefaultBuildId = ValidatedResult.ValidatedBuild.Id;
    Settings->TryUpdateDefaultConfigFile();

    FForgeQABuildManifest Manifest;
    Manifest.ProjectId = SelectedProject->Id;
    Manifest.BuildId = ValidatedResult.ValidatedBuild.Id;
    Manifest.Version = ValidatedResult.ValidatedBuild.Version;
    Manifest.BuildNumber = ValidatedResult.ValidatedBuild.BuildNumber;
    Manifest.Platform = ValidatedResult.ValidatedBuild.Platform;
    Manifest.Configuration = ValidatedResult.ValidatedBuild.Configuration;
    Manifest.Branch = ValidatedResult.ValidatedBuild.Branch;
    Manifest.CommitSha = ValidatedResult.ValidatedBuild.CommitSha;

    if (!FForgeQABuildManifestSerializer::SaveToFile(Manifest, OutError))
    {
        return false;
    }

    SetState(EForgeQAConnectionState::BindingValid, TEXT("Binding applied and manifest generated."));
    return true;
}
