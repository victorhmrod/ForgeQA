using UnrealBuildTool;

public class ForgeQA : ModuleRules
{
    public ForgeQA(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

        PublicDependencyModuleNames.AddRange(new[]
        {
            "Core"
        });

        PrivateDependencyModuleNames.AddRange(new[]
        {
            "CoreUObject",
            "Engine",
            "HTTP",
            "Json",
            "JsonUtilities",
            "DeveloperSettings",
            "Projects",
            "UMG",
            "Slate",
            "SlateCore",
            "ImageWrapper",
            "RenderCore"
        });
    }
}
