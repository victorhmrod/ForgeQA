using UnrealBuildTool;

public class ForgeQAEditor : ModuleRules
{
    public ForgeQAEditor(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;

        PublicDependencyModuleNames.AddRange(new[]
        {
            "Core",
            "ForgeQA"
        });

        PrivateDependencyModuleNames.AddRange(new[]
        {
            "CoreUObject",
            "Engine",
            "Slate",
            "SlateCore",
            "UnrealEd",
            "ToolMenus",
            "InputCore",
            "DeveloperSettings",
            "Projects"
        });
    }
}
