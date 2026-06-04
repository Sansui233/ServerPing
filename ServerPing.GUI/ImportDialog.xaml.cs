using System.Windows;
using ServerPing.GUI.Services;
using ServerPing.GUI.ViewModels;

namespace ServerPing.GUI;

public partial class ImportDialog : Window
{
    private readonly List<SshProfileViewModel> _profiles;

    public List<SshProfile> SelectedProfiles { get; private set; } = [];

    public ImportDialog(List<SshProfile> profiles, HashSet<string> existingHosts)
    {
        InitializeComponent();

        _profiles = profiles.Select(p => new SshProfileViewModel(p)
        {
            IsSelected = !existingHosts.Contains(p.Host)
        }).ToList();

        ProfileGrid.ItemsSource = _profiles;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var p in _profiles) p.IsSelected = true;
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var p in _profiles) p.IsSelected = false;
    }

    private void Import_Click(object sender, RoutedEventArgs e)
    {
        SelectedProfiles = _profiles.Where(p => p.IsSelected).Select(p => p.Profile).ToList();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
