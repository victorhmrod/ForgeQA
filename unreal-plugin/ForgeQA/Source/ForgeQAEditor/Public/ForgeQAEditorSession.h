#pragma once

#include "CoreMinimal.h"
#include "ForgeQAApiTypes.h"
#include "ForgeQAApiClient.h"

/** Human-legible integration state, always paired with explanatory text in the UI — never a bare color. */
enum class EForgeQAConnectionState : uint8
{
    NotConfigured,
    NotAuthenticated,
    Authenticating,
    Connected,
    ProjectSelected,
    BuildSelected,
    Validating,
    BindingValid,
    BindingInvalid,
};

struct FForgeQABindingValidationResult
{
    bool bSuccess = false;
    FString ErrorMessage;
    FForgeQABuildSummary ValidatedBuild;
};

/**
 * Owns the Editor's ForgeQA session: the in-memory access token, cached Organization/Project/Build
 * lists, and the single reusable binding-validation routine used by both the "Validate Binding"
 * and "Apply" actions in SForgeQAPanel.
 *
 * The access token here is editor-session-only. It is never written to disk, never included in the
 * generated manifest, and is cleared when the editor closes (this object is not persisted).
 */
class FORGEQAEDITOR_API FForgeQAEditorSession : public TSharedFromThis<FForgeQAEditorSession>
{
public:
    FForgeQAEditorSession();

    EForgeQAConnectionState GetState() const { return State; }
    const FString& GetStatusMessage() const { return StatusMessage; }

    void SetApiBaseUrl(const FString& InApiBaseUrl);

    void Login(const FString& Email, const FString& Password, TFunction<void(bool bSuccess, const FString& Error)> OnComplete);
    void Logout();

    const TArray<FForgeQAOrganizationSummary>& GetOrganizations() const { return Organizations; }
    const TArray<FForgeQAProjectSummary>& GetProjects() const { return Projects; }
    const TArray<FForgeQABuildSummary>& GetBuilds() const { return Builds; }

    void RefreshProjects(const FGuid& OrganizationId, TFunction<void(bool bSuccess, const FString& Error)> OnComplete);

    /** Fetches every Project across every Organization the signed-in user belongs to, and flattens
     *  them into GetProjects(). The Project picker in the UI has no separate Organization step. */
    void RefreshAllProjects(TFunction<void(bool bSuccess, const FString& Error)> OnComplete);

    void RefreshBuilds(const FGuid& ProjectId, bool bIncludeArchived, TFunction<void(bool bSuccess, const FString& Error)> OnComplete);

    void SetSelectedProject(const FForgeQAProjectSummary& Project);
    void SetSelectedBuild(const FForgeQABuildSummary& Build);

    TOptional<FForgeQAProjectSummary> GetSelectedProject() const { return SelectedProject; }
    TOptional<FForgeQABuildSummary> GetSelectedBuild() const { return SelectedBuild; }

    /**
     * The single validation routine for a Project/Build binding: confirms the Build exists, belongs
     * to the selected Project, and (implicitly, via the API 404 the wrong project would cause) is
     * addressable. Used by both "Validate Binding" and "Apply" — never duplicate these checks elsewhere.
     */
    void ValidateBinding(TFunction<void(const FForgeQABindingValidationResult& Result)> OnComplete) const;

    /** Persists ProjectId to Project Settings and writes the Build manifest from ValidateBinding's result. */
    bool ApplyBinding(const FForgeQABindingValidationResult& ValidatedResult, FString& OutError);

    bool IsAuthenticated() const { return !AccessToken.IsEmpty(); }

private:
    TSharedRef<FForgeQAApiClient> ApiClient;
    FString AccessToken;

    EForgeQAConnectionState State = EForgeQAConnectionState::NotConfigured;
    FString StatusMessage;

    TArray<FForgeQAOrganizationSummary> Organizations;
    TArray<FForgeQAProjectSummary> Projects;
    TArray<FForgeQABuildSummary> Builds;

    TOptional<FForgeQAProjectSummary> SelectedProject;
    TOptional<FForgeQABuildSummary> SelectedBuild;

    void SetState(EForgeQAConnectionState NewState, const FString& Message);
};
