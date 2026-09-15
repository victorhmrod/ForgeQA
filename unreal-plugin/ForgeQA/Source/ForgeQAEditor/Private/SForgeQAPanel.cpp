#include "SForgeQAPanel.h"
#include "ForgeQASettings.h"
#include "Widgets/Layout/SScrollBox.h"
#include "Widgets/Input/SEditableTextBox.h"
#include "Widgets/Input/SCheckBox.h"
#include "Widgets/Input/SButton.h"
#include "Widgets/Input/SComboBox.h"
#include "Widgets/Text/STextBlock.h"

#define LOCTEXT_NAMESPACE "ForgeQAEditor"

void SForgeQAPanel::Construct(const FArguments& InArgs)
{
    Session = MakeShared<FForgeQAEditorSession>();

    const UForgeQASettings* Settings = GetDefault<UForgeQASettings>();
    ApiBaseUrlText = Settings->ApiBaseUrl;

    ChildSlot
    [
        SNew(SScrollBox)
        + SScrollBox::Slot().Padding(8)
        [
            SNew(SVerticalBox)

            + SVerticalBox::Slot().AutoHeight().Padding(0, 4)
            [
                SNew(STextBlock).Text(LOCTEXT("ApiSectionTitle", "API"))
            ]
            + SVerticalBox::Slot().AutoHeight().Padding(0, 2)
            [
                SNew(SEditableTextBox)
                .HintText(LOCTEXT("ApiUrlHint", "http://localhost:5000"))
                .Text(FText::FromString(ApiBaseUrlText))
                .OnTextCommitted_Lambda([this](const FText& NewText, ETextCommit::Type)
                {
                    ApiBaseUrlText = NewText.ToString();
                    Session->SetApiBaseUrl(ApiBaseUrlText);
                })
            ]

            + SVerticalBox::Slot().AutoHeight().Padding(0, 8, 0, 2)
            [
                SNew(STextBlock).Text(LOCTEXT("AuthSectionTitle", "Authentication"))
            ]
            + SVerticalBox::Slot().AutoHeight().Padding(0, 2)
            [
                SNew(SEditableTextBox)
                .HintText(LOCTEXT("EmailHint", "Email"))
                .OnTextCommitted_Lambda([this](const FText& NewText, ETextCommit::Type) { EmailText = NewText.ToString(); })
            ]
            + SVerticalBox::Slot().AutoHeight().Padding(0, 2)
            [
                SNew(SEditableTextBox)
                .HintText(LOCTEXT("PasswordHint", "Password"))
                .IsPassword(true)
                .OnTextCommitted_Lambda([this](const FText& NewText, ETextCommit::Type) { PasswordText = NewText.ToString(); })
            ]
            + SVerticalBox::Slot().AutoHeight().Padding(0, 4)
            [
                SNew(SButton)
                .Text(LOCTEXT("LoginButton", "Login"))
                .OnClicked(this, &SForgeQAPanel::OnLoginClicked)
            ]

            + SVerticalBox::Slot().AutoHeight().Padding(0, 8, 0, 2)
            [
                SNew(STextBlock).Text(LOCTEXT("ProjectSectionTitle", "Project"))
            ]
            + SVerticalBox::Slot().AutoHeight().Padding(0, 2)
            [
                SAssignNew(ProjectComboBox, SComboBox<TSharedPtr<FForgeQAProjectSummary>>)
                .OptionsSource(&ProjectOptions)
                .OnGenerateWidget(this, &SForgeQAPanel::GenerateProjectOptionWidget)
                .OnSelectionChanged(this, &SForgeQAPanel::OnProjectSelectionChanged)
                [
                    SNew(STextBlock).Text(this, &SForgeQAPanel::GetProjectComboLabel)
                ]
            ]

            + SVerticalBox::Slot().AutoHeight().Padding(0, 8, 0, 2)
            [
                SNew(STextBlock).Text(LOCTEXT("BuildSectionTitle", "Build"))
            ]
            + SVerticalBox::Slot().AutoHeight().Padding(0, 2)
            [
                SNew(SCheckBox)
                .IsChecked_Lambda([this]() { return bShowArchivedBuilds ? ECheckBoxState::Checked : ECheckBoxState::Unchecked; })
                .OnCheckStateChanged_Lambda([this](ECheckBoxState NewState)
                {
                    bShowArchivedBuilds = (NewState == ECheckBoxState::Checked);
                    RefreshBuildOptions();
                })
                [
                    SNew(STextBlock).Text(LOCTEXT("ShowArchivedBuilds", "Show archived Builds"))
                ]
            ]
            + SVerticalBox::Slot().AutoHeight().Padding(0, 2)
            [
                SAssignNew(BuildComboBox, SComboBox<TSharedPtr<FForgeQABuildSummary>>)
                .OptionsSource(&BuildOptions)
                .OnGenerateWidget(this, &SForgeQAPanel::GenerateBuildOptionWidget)
                .OnSelectionChanged(this, &SForgeQAPanel::OnBuildSelectionChanged)
                [
                    SNew(STextBlock).Text(this, &SForgeQAPanel::GetBuildComboLabel)
                ]
            ]

            + SVerticalBox::Slot().AutoHeight().Padding(0, 8, 0, 2)
            [
                SNew(STextBlock).Text(LOCTEXT("StatusSectionTitle", "Status"))
            ]
            + SVerticalBox::Slot().AutoHeight().Padding(0, 2)
            [
                SAssignNew(StatusTextBlock, STextBlock).Text(LOCTEXT("StatusNotConfigured", "Not authenticated."))
            ]

            + SVerticalBox::Slot().AutoHeight().Padding(0, 8, 0, 2)
            [
                SNew(SHorizontalBox)
                + SHorizontalBox::Slot().AutoWidth().Padding(0, 0, 4, 0)
                [
                    SNew(SButton)
                    .Text(LOCTEXT("ValidateButton", "Validate Binding"))
                    .OnClicked(this, &SForgeQAPanel::OnValidateClicked)
                ]
                + SHorizontalBox::Slot().AutoWidth()
                [
                    SNew(SButton)
                    .Text(LOCTEXT("ApplyButton", "Apply"))
                    .OnClicked(this, &SForgeQAPanel::OnApplyClicked)
                ]
            ]
        ]
    ];
}

void SForgeQAPanel::SetStatus(const FString& Message)
{
    if (StatusTextBlock.IsValid())
    {
        StatusTextBlock->SetText(FText::FromString(Message));
    }
}

FReply SForgeQAPanel::OnLoginClicked()
{
    SetStatus(TEXT("Signing in..."));

    TWeakPtr<SForgeQAPanel> WeakSelf = SharedThis(this);
    Session->Login(EmailText, PasswordText, [WeakSelf](bool bSuccess, const FString& Error)
    {
        const TSharedPtr<SForgeQAPanel> Self = WeakSelf.Pin();
        if (!Self.IsValid())
        {
            return;
        }

        if (!bSuccess)
        {
            Self->SetStatus(FString::Printf(TEXT("Login failed: %s"), *Error));
            return;
        }

        Self->SetStatus(TEXT("Connected. Loading Projects..."));
        Self->Session->RefreshAllProjects([WeakSelf](bool bProjectsSuccess, const FString& ProjectsError)
        {
            const TSharedPtr<SForgeQAPanel> InnerSelf = WeakSelf.Pin();
            if (!InnerSelf.IsValid())
            {
                return;
            }

            if (!bProjectsSuccess)
            {
                InnerSelf->SetStatus(FString::Printf(TEXT("Failed to load Projects: %s"), *ProjectsError));
                return;
            }

            InnerSelf->RefreshProjectOptions();
            InnerSelf->SetStatus(TEXT("Connected."));
        });
    });

    return FReply::Handled();
}

void SForgeQAPanel::RefreshProjectOptions()
{
    ProjectOptions.Reset();
    for (const FForgeQAProjectSummary& Project : Session->GetProjects())
    {
        ProjectOptions.Add(MakeShared<FForgeQAProjectSummary>(Project));
    }

    if (ProjectComboBox.IsValid())
    {
        ProjectComboBox->RefreshOptions();
    }
}

void SForgeQAPanel::RefreshBuildOptions()
{
    if (!SelectedProjectOption.IsValid())
    {
        return;
    }

    TWeakPtr<SForgeQAPanel> WeakSelf = SharedThis(this);
    Session->RefreshBuilds(SelectedProjectOption->Id, bShowArchivedBuilds, [WeakSelf](bool bSuccess, const FString& Error)
    {
        const TSharedPtr<SForgeQAPanel> Self = WeakSelf.Pin();
        if (!Self.IsValid())
        {
            return;
        }

        if (!bSuccess)
        {
            Self->SetStatus(FString::Printf(TEXT("Failed to load Builds: %s"), *Error));
            return;
        }

        Self->BuildOptions.Reset();
        for (const FForgeQABuildSummary& Build : Self->Session->GetBuilds())
        {
            Self->BuildOptions.Add(MakeShared<FForgeQABuildSummary>(Build));
        }

        if (Self->BuildComboBox.IsValid())
        {
            Self->BuildComboBox->RefreshOptions();
        }
    });
}

FReply SForgeQAPanel::OnRefreshBuildsClicked()
{
    RefreshBuildOptions();
    return FReply::Handled();
}

TSharedRef<SWidget> SForgeQAPanel::GenerateProjectOptionWidget(TSharedPtr<FForgeQAProjectSummary> Project)
{
    return SNew(STextBlock).Text(FText::FromString(Project.IsValid() ? Project->Name : FString()));
}

TSharedRef<SWidget> SForgeQAPanel::GenerateBuildOptionWidget(TSharedPtr<FForgeQABuildSummary> Build)
{
    FString Label = Build.IsValid() ? Build->GetDisplayLabel() : FString();
    if (Build.IsValid() && Build->bArchived)
    {
        Label += TEXT("  [Archived]");
    }
    return SNew(STextBlock).Text(FText::FromString(Label));
}

void SForgeQAPanel::OnProjectSelectionChanged(TSharedPtr<FForgeQAProjectSummary> NewSelection, ESelectInfo::Type)
{
    SelectedProjectOption = NewSelection;
    SelectedBuildOption.Reset();
    BuildOptions.Reset();
    if (BuildComboBox.IsValid())
    {
        BuildComboBox->RefreshOptions();
    }

    if (NewSelection.IsValid())
    {
        Session->SetSelectedProject(*NewSelection);
        RefreshBuildOptions();
    }
}

void SForgeQAPanel::OnBuildSelectionChanged(TSharedPtr<FForgeQABuildSummary> NewSelection, ESelectInfo::Type)
{
    SelectedBuildOption = NewSelection;
    if (NewSelection.IsValid())
    {
        Session->SetSelectedBuild(*NewSelection);

        if (NewSelection->bArchived)
        {
            SetStatus(TEXT("This ForgeQA Build is archived."));
        }
    }
}

FText SForgeQAPanel::GetProjectComboLabel() const
{
    return SelectedProjectOption.IsValid() ? FText::FromString(SelectedProjectOption->Name) : LOCTEXT("SelectProject", "Select a Project...");
}

FText SForgeQAPanel::GetBuildComboLabel() const
{
    return SelectedBuildOption.IsValid() ? FText::FromString(SelectedBuildOption->GetDisplayLabel()) : LOCTEXT("SelectBuild", "Select a Build...");
}

FReply SForgeQAPanel::OnValidateClicked()
{
    SetStatus(TEXT("Validating binding..."));

    TWeakPtr<SForgeQAPanel> WeakSelf = SharedThis(this);
    Session->ValidateBinding([WeakSelf](const FForgeQABindingValidationResult& Result)
    {
        const TSharedPtr<SForgeQAPanel> Self = WeakSelf.Pin();
        if (!Self.IsValid())
        {
            return;
        }

        Self->SetStatus(Result.bSuccess
            ? FString::Printf(TEXT("Binding valid: Project + Build '%s' confirmed."), *Result.ValidatedBuild.GetDisplayLabel())
            : FString::Printf(TEXT("Binding invalid: %s"), *Result.ErrorMessage));
    });

    return FReply::Handled();
}

FReply SForgeQAPanel::OnApplyClicked()
{
    SetStatus(TEXT("Validating and applying binding..."));

    TWeakPtr<SForgeQAPanel> WeakSelf = SharedThis(this);
    Session->ValidateBinding([WeakSelf](const FForgeQABindingValidationResult& Result)
    {
        const TSharedPtr<SForgeQAPanel> Self = WeakSelf.Pin();
        if (!Self.IsValid())
        {
            return;
        }

        if (!Result.bSuccess)
        {
            Self->SetStatus(FString::Printf(TEXT("Cannot apply: %s"), *Result.ErrorMessage));
            return;
        }

        FString ApplyError;
        if (!Self->Session->ApplyBinding(Result, ApplyError))
        {
            Self->SetStatus(FString::Printf(TEXT("Failed to apply binding: %s"), *ApplyError));
            return;
        }

        Self->SetStatus(TEXT("Binding applied. ForgeQA build manifest generated."));
    });

    return FReply::Handled();
}

#undef LOCTEXT_NAMESPACE
