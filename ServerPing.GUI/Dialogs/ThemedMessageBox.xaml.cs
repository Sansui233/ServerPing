using System.Windows;
using System.Windows.Input;
using ServerPing.GUI.Services;

namespace ServerPing.GUI;

public partial class ThemedMessageBox : Window
{
    public ThemedMessageBox(string message, string caption, MessageBoxImage image)
    {
        InitializeComponent();

        Title = caption;
        TitleTextBlock.Text = caption;
        MessageTextBlock.Text = message;
        OkButton.Content = LocalizationService.Get("Dialog.OK");
        ApplyImage(image);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        NativeWindowStyler.ApplyWindows11RoundedCorners(this);
    }

    private void ApplyImage(MessageBoxImage image)
    {
        switch (image)
        {
            case MessageBoxImage.Error:
                IconTextBlock.Text = "!";
                IconBorder.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "MessageErrorBackgroundBrush");
                IconTextBlock.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "MessageErrorForegroundBrush");
                break;
            case MessageBoxImage.Warning:
                IconTextBlock.Text = "!";
                IconBorder.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "MessageWarningBackgroundBrush");
                IconTextBlock.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "MessageWarningForegroundBrush");
                break;
            default:
                IconTextBlock.Text = "i";
                IconBorder.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "MessageInfoBackgroundBrush");
                IconTextBlock.SetResourceReference(System.Windows.Controls.TextBlock.ForegroundProperty, "MessageInfoForegroundBrush");
                break;
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
