using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
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
        HibernateDurationTextBox.Text = settings.GuiHibernateDurationSeconds.ToString();
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
            MessageTextBlock.Text = "保存失败，请确认服务正在运行。";
            return;
        }

        DialogResult = true;
        Close();
    }

    private async void TestNotification_Click(object sender, RoutedEventArgs e)
    {
        MessageTextBlock.Text = "正在发送测试通知...";
        var sent = await _viewModel.TestNotificationAsync();
        MessageTextBlock.Text = sent ? "测试通知已发送。" : "测试通知发送失败。";
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
            MessageTextBlock.Text = "Ping 间隔必须在 1-300 秒之间。";
            return false;
        }

        if (!int.TryParse(FailureThresholdTextBox.Text.Trim(), out var threshold) ||
            threshold is < MonitoringSettings.MinFailureThreshold or > MonitoringSettings.MaxFailureThreshold)
        {
            MessageTextBlock.Text = "失败通知次数必须在 1-20 次之间。";
            return false;
        }

        if (!int.TryParse(HibernateDurationTextBox.Text.Trim(), out var hibernate) ||
            hibernate is < MonitoringSettings.MinGuiHibernateDuration or > MonitoringSettings.MaxGuiHibernateDuration)
        {
            MessageTextBlock.Text = $"窗口休眠时长必须在 {MonitoringSettings.MinGuiHibernateDuration}-{MonitoringSettings.MaxGuiHibernateDuration} 秒之间。";
            return false;
        }

        settings.PingIntervalSeconds = interval;
        settings.FailureThreshold = threshold;
        settings.SilentStartup = SilentStartupCheckBox.IsChecked == true;
        settings.GuiHibernateDurationSeconds = hibernate;
        return true;
    }
}
