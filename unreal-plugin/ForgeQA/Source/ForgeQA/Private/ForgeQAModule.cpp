#include "ForgeQAModule.h"

#define LOCTEXT_NAMESPACE "FForgeQAModule"

void FForgeQAModule::StartupModule()
{
    UE_LOG(LogTemp, Log, TEXT("ForgeQA module started."));
}

void FForgeQAModule::ShutdownModule()
{
    UE_LOG(LogTemp, Log, TEXT("ForgeQA module shut down."));
}

#undef LOCTEXT_NAMESPACE

IMPLEMENT_MODULE(FForgeQAModule, ForgeQA)
