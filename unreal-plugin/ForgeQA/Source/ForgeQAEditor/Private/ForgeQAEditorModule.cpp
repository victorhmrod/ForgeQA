#include "ForgeQAEditorModule.h"
#include "ForgeQALog.h"
#include "SForgeQAPanel.h"
#include "ToolMenus.h"
#include "Widgets/SWindow.h"
#include "Framework/Application/SlateApplication.h"

#define LOCTEXT_NAMESPACE "ForgeQAEditor"

void FForgeQAEditorModule::StartupModule()
{
    UE_LOG(LogForgeQA, Log, TEXT("ForgeQA editor module started."));

    UToolMenus::RegisterStartupCallback(
        FSimpleMulticastDelegate::FDelegate::CreateRaw(this, &FForgeQAEditorModule::RegisterMenus));
}

void FForgeQAEditorModule::ShutdownModule()
{
    UToolMenus::UnRegisterStartupCallback(this);
    UToolMenus::UnregisterOwner(this);

    if (const TSharedPtr<SWindow> Window = ForgeQAWindow.Pin())
    {
        Window->RequestDestroyWindow();
    }

    UE_LOG(LogForgeQA, Log, TEXT("ForgeQA editor module shut down."));
}

void FForgeQAEditorModule::RegisterMenus()
{
    FToolMenuOwnerScoped OwnerScoped(this);

    UToolMenu* ToolsMenu = UToolMenus::Get()->ExtendMenu("LevelEditor.MainMenu.Tools");
    FToolMenuSection& Section = ToolsMenu->FindOrAddSection("ForgeQA");
    Section.Label = LOCTEXT("ForgeQASectionLabel", "ForgeQA");

    Section.AddMenuEntry(
        "OpenForgeQAPanel",
        LOCTEXT("OpenForgeQAPanelLabel", "ForgeQA"),
        LOCTEXT("OpenForgeQAPanelTooltip", "Configure the ForgeQA integration: authenticate, link a Project, and bind a Build."),
        FSlateIcon(),
        FUIAction(FExecuteAction::CreateRaw(this, &FForgeQAEditorModule::OpenForgeQAPanel)));
}

void FForgeQAEditorModule::OpenForgeQAPanel()
{
    if (const TSharedPtr<SWindow> ExistingWindow = ForgeQAWindow.Pin())
    {
        ExistingWindow->BringToFront();
        return;
    }

    const TSharedRef<SWindow> Window = SNew(SWindow)
        .Title(LOCTEXT("ForgeQAWindowTitle", "ForgeQA"))
        .ClientSize(FVector2D(480, 560))
        .SupportsMaximize(false)
        .SupportsMinimize(false)
        [
            SNew(SForgeQAPanel)
        ];

    ForgeQAWindow = Window;
    FSlateApplication::Get().AddWindow(Window);
}

#undef LOCTEXT_NAMESPACE

IMPLEMENT_MODULE(FForgeQAEditorModule, ForgeQAEditor)
