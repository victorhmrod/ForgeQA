#pragma once

#include "CoreMinimal.h"
#include "Widgets/SCompoundWidget.h"
#include "ForgeQAEditorSession.h"

/**
 * The "Tools > ForgeQA" panel: configure the API URL, authenticate, link a Project, select a
 * Build, validate the binding, and apply it (persisting Settings + writing the build manifest).
 */
class FORGEQAEDITOR_API SForgeQAPanel : public SCompoundWidget
{
public:
    SLATE_BEGIN_ARGS(SForgeQAPanel) {}
    SLATE_END_ARGS()

    void Construct(const FArguments& InArgs);

private:
    TSharedPtr<FForgeQAEditorSession> Session;

    FString ApiBaseUrlText;
    FString EmailText;
    FString PasswordText;
    bool bShowArchivedBuilds = false;

    TArray<TSharedPtr<FForgeQAProjectSummary>> ProjectOptions;
    TArray<TSharedPtr<FForgeQABuildSummary>> BuildOptions;
    TSharedPtr<FForgeQAProjectSummary> SelectedProjectOption;
    TSharedPtr<FForgeQABuildSummary> SelectedBuildOption;

    TSharedPtr<class STextBlock> StatusTextBlock;
    TSharedPtr<class SComboBox<TSharedPtr<FForgeQAProjectSummary>>> ProjectComboBox;
    TSharedPtr<class SComboBox<TSharedPtr<FForgeQABuildSummary>>> BuildComboBox;

    FReply OnLoginClicked();
    FReply OnRefreshBuildsClicked();
    FReply OnValidateClicked();
    FReply OnApplyClicked();

    void RefreshProjectOptions();
    void RefreshBuildOptions();
    void SetStatus(const FString& Message);

    TSharedRef<SWidget> GenerateProjectOptionWidget(TSharedPtr<FForgeQAProjectSummary> Project);
    TSharedRef<SWidget> GenerateBuildOptionWidget(TSharedPtr<FForgeQABuildSummary> Build);
    void OnProjectSelectionChanged(TSharedPtr<FForgeQAProjectSummary> NewSelection, ESelectInfo::Type SelectInfo);
    void OnBuildSelectionChanged(TSharedPtr<FForgeQABuildSummary> NewSelection, ESelectInfo::Type SelectInfo);

    FText GetProjectComboLabel() const;
    FText GetBuildComboLabel() const;
};
