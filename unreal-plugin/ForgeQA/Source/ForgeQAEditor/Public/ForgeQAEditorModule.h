#pragma once

#include "CoreMinimal.h"
#include "Modules/ModuleManager.h"

/**
 * Editor-only module: authentication, Project/Build linking UI, and manifest generation.
 * Never referenced by the Runtime module — see ForgeQA.Build.cs / ForgeQAEditor.Build.cs.
 */
class FORGEQAEDITOR_API FForgeQAEditorModule : public IModuleInterface
{
public:
    virtual void StartupModule() override;
    virtual void ShutdownModule() override;

private:
    void RegisterMenus();
    void OpenForgeQAPanel();

    TWeakPtr<class SWindow> ForgeQAWindow;
};
