using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ServerPing.GUI.ViewModels;

namespace ServerPing.GUI;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;
    private DispatcherTimer? _hibernateTimer;
    private int _hibernateDurationSeconds = 10;

    public MainWindow()
    {
        InitializeComponent();
        PreviewMouseLeftButtonDown += ClearTextBoxFocus;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        NativeWindowStyler.ApplyWindows11RoundedCorners(this);
    }

    private void ClearTextBoxFocus(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        if (FindAncestor<TextBox>(source) != null) return;

        FocusManager.SetFocusedElement(this, null);
        Keyboard.ClearFocus();
    }

    private static T? FindAncestor<T>(DependencyObject obj) where T : DependencyObject
    {
        while (obj != null)
        {
            if (obj is T target) return target;
            obj = VisualTreeHelper.GetParent(obj);
        }
        return null;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();

        var settings = await ViewModel.LoadSettingsAsync();
        _hibernateDurationSeconds = settings.GuiHibernateDurationSeconds;
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        ViewModel.Shutdown();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    public void HandleToggle(int cursorX, int cursorY)
    {
        if (IsVisible && WindowState != WindowState.Minimized)
        {
            Hide();
            StartHibernateTimer();
        }
        else
        {
            CancelHibernateTimer();
            Show();
            WindowState = WindowState.Normal;
            PositionNearCursor(cursorX, cursorY);
            Topmost = true;
            Activate();
            Topmost = false;
        }
    }

    public void PositionNearCursor(int physicalX, int physicalY)
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget == null) return;

        var transform = source.CompositionTarget.TransformFromDevice;
        var dipX = physicalX * transform.M11;
        var dipY = physicalY * transform.M22;

        var workArea = SystemParameters.WorkArea;

        if (dipY < workArea.Height / 2)
        {
            Top = dipY + 5;
        }
        else
        {
            Top = dipY - ActualHeight;
        }

        Left = Math.Min(dipX, workArea.Right - ActualWidth);
        Left = Math.Max(Left, workArea.Left);

        Top = Math.Max(Top, workArea.Top);
        Top = Math.Min(Top, workArea.Bottom - ActualHeight);
    }

    private void StartHibernateTimer()
    {
        CancelHibernateTimer();
        _hibernateTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_hibernateDurationSeconds) };
        _hibernateTimer.Tick += (s, e) =>
        {
            _hibernateTimer?.Stop();
            Application.Current.Shutdown();
        };
        _hibernateTimer.Start();
    }

    private void CancelHibernateTimer()
    {
        _hibernateTimer?.Stop();
        _hibernateTimer = null;
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        CancelHibernateTimer();
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settings = await ViewModel.LoadSettingsAsync();
        var dialog = new SettingsDialog(ViewModel, settings)
        {
            Owner = this
        };
        if (dialog.ShowDialog() == true)
        {
            var newSettings = await ViewModel.LoadSettingsAsync();
            _hibernateDurationSeconds = newSettings.GuiHibernateDurationSeconds;
        }
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void IdentityTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: ServerViewModel server })
            server.IsEditingIdentity = true;
    }

    private async void IdentityTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox { DataContext: ServerViewModel server } textBox || !server.IsEditingIdentity)
            return;

        await CommitIdentityEditAsync(textBox, moveFocus: false);
    }

    private async void IdentityTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox textBox)
            return;

        e.Handled = true;
        await CommitIdentityEditAsync(textBox, moveFocus: true);
    }

    private async Task CommitIdentityEditAsync(TextBox textBox, bool moveFocus)
    {
        if (textBox.DataContext is not ServerViewModel server)
            return;

        server.IsEditingIdentity = false;
        var saved = await ViewModel.SaveServerAsync(server);

        if (moveFocus && saved)
            textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
    }
}
