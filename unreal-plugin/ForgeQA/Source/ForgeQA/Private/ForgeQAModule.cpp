#include "ForgeQAModule.h"
#include "ForgeQALog.h"

#define LOCTEXT_NAMESPACE "FForgeQAModule"

DEFINE_LOG_CATEGORY(LogForgeQA);

void FForgeQAModule::StartupModule()
{
    UE_LOG(LogForgeQA, Log, TEXT("ForgeQA runtime module started."));
}

void FForgeQAModule::ShutdownModule()
{
    UE_LOG(LogForgeQA, Log, TEXT("ForgeQA runtime module shut down."));
}

#undef LOCTEXT_NAMESPACE

IMPLEMENT_MODULE(FForgeQAModule, ForgeQA)
