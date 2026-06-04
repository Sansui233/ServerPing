using ServerPing.GUI.Services;

namespace ServerPing.GUI.ViewModels;

public class SshProfileViewModel : ViewModelBase
{
    private bool _isSelected = true;

    public SshProfile Profile { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public string Name => Profile.Name;
    public string Host => Profile.Host;
    public string CommandLine => Profile.CommandLine;

    public SshProfileViewModel(SshProfile profile)
    {
        Profile = profile;
    }
}
