using System.Windows;
using System.Windows.Input;
using ServerPing.GUI.Services;
using ServerPing.GUI.ViewModels;

namespace ServerPing.GUI;

public partial class ImportDialog : Window
{
    private readonly List<SshProfileViewModel> _profiles;

    public List<SshProfile> SelectedProfiles { get; private set; } = [];

    public ImportDialog(List<SshProfile> profiles)
    {
        InitializeComponent();

        _profiles = [.. profiles.Select(p => new SshProfileViewModel(p) { IsSelected = true })];

        ProfileGrid.ItemsSource = _profiles;
        RefreshColumnHeaders();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        NativeWindowStyler.ApplyWindows11RoundedCorners(this);
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var p in _profiles) p.IsSelected = true;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
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

    private void RefreshColumnHeaders()
    {
        NameColumn.Header = LocalizationService.Get("Import.Name");
        HostColumn.Header = LocalizationService.Get("Import.Host");
        CommandColumn.Header = LocalizationService.Get("Import.Command");
    }
}
