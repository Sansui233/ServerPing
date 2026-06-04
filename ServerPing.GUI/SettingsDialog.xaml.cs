using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using ServerPing.GUI.Services;
using ServerPing.Shared;
using ServerPing.GUI.ViewModels;
using ServerPing.Shared.Models;

namespace ServerPing.GUI;

public partial class SettingsDialog : Window
{
    private readonly MainViewModel _viewModel;

    public SettingsDialog(MainViewModel viewModel, MonitoringSettings settings)
    {
        InitializeComponent();
        _viewModel = viewModel;
        PingIntervalTextBox.Text = settings.PingIntervalSeconds.ToString();
        FailureThresholdTextBox.Text = settings.FailureThreshold.ToString();
        SilentStartupCheckBox.IsChecked = settings.SilentStartup;
        LaunchAtStartupCheckBox.IsChecked = settings.LaunchAtStartup;
        HibernateDurationTextBox.Text = settings.GuiHibernateDurationSeconds.ToString();
        LanguageComboBox.ItemsSource = LocalizationService.Languages;
        LanguageComboBox.SelectedValue = LocalizationService.Normalize(settings.Language);
        DataDirectoryTextBlock.Text = ConfigurationManager.ConfigDirectory;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        NativeWindowStyler.ApplyWindows11RoundedCorners(this);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadSettings(out var settings))
            return;

        IsEnabled = false;
        var saved = await _viewModel.SaveSettingsAsync(settings);
        IsEnabled = true;

        if (!saved)
        {
            MessageTextBlock.Text = LocalizationService.Get("Message.SaveSettingsFailed");
            return;
        }

        LocalizationService.Apply(settings.Language);
        DialogResult = true;
        Close();
    }

    private async void TestNotification_Click(object sender, RoutedEventArgs e)
    {
        MessageTextBlock.Text = LocalizationService.Get("Message.SendingTestNotification");
        var sent = await _viewModel.TestNotificationAsync();
        MessageTextBlock.Text = sent
            ? LocalizationService.Get("Message.TestNotificationSent")
            : LocalizationService.Get("Message.TestNotificationFailed");
    }

    private async void TestNotificationSound_Click(object sender, RoutedEventArgs e)
    {
        MessageTextBlock.Text = LocalizationService.Get("Message.PlayingTestNotificationSound");
        var played = await _viewModel.TestNotificationSoundAsync();
        MessageTextBlock.Text = played
            ? LocalizationService.Get("Message.TestNotificationSoundPlayed")
            : LocalizationService.Get("Message.TestNotificationSoundFailed");
    }

    private void OpenDataDirectory_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(ConfigurationManager.ConfigDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = ConfigurationManager.ConfigDirectory,
            UseShellExecute = true
        });
    }

    private bool TryReadSettings(out MonitoringSettings settings)
    {
        settings = new MonitoringSettings();

        if (!int.TryParse(PingIntervalTextBox.Text.Trim(), out var interval) ||
            interval is < MonitoringSettings.MinPingIntervalSeconds or > MonitoringSettings.MaxPingIntervalSeconds)
        {
            MessageTextBlock.Text = LocalizationService.Format(
                "Validation.PingInterval",
                MonitoringSettings.MinPingIntervalSeconds,
                MonitoringSettings.MaxPingIntervalSeconds);
            return false;
        }

        if (!int.TryParse(FailureThresholdTextBox.Text.Trim(), out var threshold) ||
            threshold is < MonitoringSettings.MinFailureThreshold or > MonitoringSettings.MaxFailureThreshold)
        {
            MessageTextBlock.Text = LocalizationService.Format(
                "Validation.FailureThreshold",
                MonitoringSettings.MinFailureThreshold,
                MonitoringSettings.MaxFailureThreshold);
            return false;
        }

        if (!int.TryParse(HibernateDurationTextBox.Text.Trim(), out var hibernate) ||
            hibernate is < MonitoringSettings.MinGuiHibernateDuration or > MonitoringSettings.MaxGuiHibernateDuration)
        {
            MessageTextBlock.Text = LocalizationService.Format(
                "Validation.HibernateDuration",
                MonitoringSettings.MinGuiHibernateDuration,
                MonitoringSettings.MaxGuiHibernateDuration);
            return false;
        }

        settings.PingIntervalSeconds = interval;
        settings.FailureThreshold = threshold;
        settings.SilentStartup = SilentStartupCheckBox.IsChecked == true;
        settings.LaunchAtStartup = LaunchAtStartupCheckBox.IsChecked == true;
        settings.GuiHibernateDurationSeconds = hibernate;
        settings.Language = LanguageComboBox.SelectedValue as string ?? LocalizationService.DefaultLanguage;
        return true;
    }
}
