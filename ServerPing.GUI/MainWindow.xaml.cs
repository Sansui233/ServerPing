using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ServerPing.GUI.ViewModels;

namespace ServerPing.GUI;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        ViewModel.Shutdown();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        DragMove();
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settings = await ViewModel.LoadSettingsAsync();
        var dialog = new SettingsDialog(ViewModel, settings)
        {
            Owner = this
        };
        dialog.ShowDialog();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void NameTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: ServerViewModel server })
        {
            await ViewModel.SaveServerAsync(server);
        }
    }

    private async void NameTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox { DataContext: ServerViewModel server } textBox)
            return;

        e.Handled = true;
        await ViewModel.SaveServerAsync(server);
        textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
    }

    private async void HostTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: ServerViewModel server })
        {
            await ViewModel.SaveServerAsync(server);
        }
    }

    private async void HostTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox { DataContext: ServerViewModel server } textBox)
            return;

        e.Handled = true;
        await ViewModel.SaveServerAsync(server);
        textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
    }
}
