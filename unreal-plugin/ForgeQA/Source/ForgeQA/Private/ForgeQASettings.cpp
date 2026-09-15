#include "ForgeQASettings.h"

UForgeQASettings::UForgeQASettings()
{
    CategoryName = TEXT("Plugins");
    SectionName = TEXT("ForgeQA");

    ApiBaseUrl = TEXT("http://localhost:5000");
}

FString UForgeQASettings::GetNormalizedApiBaseUrl() const
{
    FString Result = ApiBaseUrl;
    Result.TrimStartAndEndInline();

    while (Result.EndsWith(TEXT("/")))
    {
        Result.LeftChopInline(1);
    }

    return Result;
}
