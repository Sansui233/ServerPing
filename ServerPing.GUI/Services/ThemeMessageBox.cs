using System.Windows;

namespace ServerPing.GUI.Services;

public static class ThemeMessageBox
{
    public static void Show(string message, string caption, MessageBoxImage image)
    {
        var dialog = new ThemedMessageBox(message, caption, image);
        var owner = Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive)
            ?? Application.Current.MainWindow;

        if (owner != null && owner != dialog)
            dialog.Owner = owner;

        dialog.ShowDialog();
    }

    public static void ShowLocalized(string messageKey, string captionKey, MessageBoxImage image) =>
        Show(LocalizationService.Get(messageKey), LocalizationService.Get(captionKey), image);
}
