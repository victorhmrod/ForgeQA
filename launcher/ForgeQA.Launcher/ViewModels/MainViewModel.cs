using CommunityToolkit.Mvvm.ComponentModel;

namespace ForgeQA.Launcher.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    public partial string Title { get; set; } = "ForgeQA";

    [ObservableProperty]
    public partial string Tagline { get; set; } = "Developer Playtesting Platform";

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = "Launcher foundation ready.";
}
