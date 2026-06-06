using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ServerPing.GUI.Services;
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
        LocalizationService.Apply(settings.Language);
        ViewModel.RefreshLocalizedText();
        RefreshColumnHeaders();
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
            ShowNearCursor(cursorX, cursorY);
        }
    }

    public void ShowNearCursor(int physicalX, int physicalY)
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        PositionNearCursorBeforeShow(physicalX, physicalY);
        WindowState = WindowState.Normal;
        ShowAndActivate();
    }

    public void ShowCentered()
    {
        WindowStartupLocation = WindowStartupLocation.Manual;
        PositionAtPrimaryScreenCenterBeforeShow();
        WindowState = WindowState.Normal;
        ShowAndActivate();
    }

    private void ShowAndActivate()
    {
        Show();
        Topmost = true;
        Activate();
        Topmost = false;
    }

    private void PositionNearCursorBeforeShow(int physicalX, int physicalY)
    {
        var windowSize = MeasureWindowSize();
        var monitor = MonitorFromPoint(new NativePoint(physicalX, physicalY), MonitorDefaultToNearest);
        var scale = GetMonitorScale(monitor);
        var workArea = GetMonitorWorkArea(monitor, scale);

        var dipX = physicalX / scale.X;
        var dipY = physicalY / scale.Y;

        Top = dipY < workArea.Top + workArea.Height / 2
            ? dipY + 5
            : dipY - windowSize.Height;

        Left = Math.Min(dipX, workArea.Right - windowSize.Width);
        Left = Math.Max(Left, workArea.Left);

        Top = Math.Max(Top, workArea.Top);
        Top = Math.Min(Top, workArea.Bottom - windowSize.Height);
    }

    private void PositionAtPrimaryScreenCenterBeforeShow()
    {
        var windowSize = MeasureWindowSize();
        var workArea = SystemParameters.WorkArea;

        Left = workArea.Left + (workArea.Width - windowSize.Width) / 2;
        Top = workArea.Top + (workArea.Height - windowSize.Height) / 2;

        if (double.IsNaN(Left) || double.IsNaN(Top))
        {
            Left = 0;
            Top = 0;
        }
    }

    private Size MeasureWindowSize()
    {
        var maxWidth = double.IsInfinity(MaxWidth) ? SystemParameters.WorkArea.Width : MaxWidth;
        Measure(new Size(maxWidth, double.PositiveInfinity));

        var width = !double.IsNaN(Width) && Width > 0 ? Width : DesiredSize.Width;
        var height = !double.IsNaN(Height) && Height > 0 ? Height : DesiredSize.Height;

        if (width <= 0 || double.IsNaN(width))
            width = maxWidth;
        if (height <= 0 || double.IsNaN(height))
            height = MinHeight;

        width = Math.Min(width, maxWidth);
        if (MinWidth > 0)
            width = Math.Max(width, MinWidth);

        height = Math.Max(height, MinHeight);
        height = Math.Min(height, MaxHeight);

        return new Size(width, height);
    }

    private static Rect GetMonitorWorkArea(IntPtr monitor, Point scale)
    {
        var info = new MonitorInfo();
        info.cbSize = Marshal.SizeOf<MonitorInfo>();

        if (!GetMonitorInfo(monitor, ref info))
            return SystemParameters.WorkArea;

        return new Rect(
            info.rcWork.Left / scale.X,
            info.rcWork.Top / scale.Y,
            (info.rcWork.Right - info.rcWork.Left) / scale.X,
            (info.rcWork.Bottom - info.rcWork.Top) / scale.Y);
    }

    private Point GetMonitorScale(IntPtr monitor)
    {
        if (GetDpiForMonitor(monitor, MonitorDpiType.Effective, out var dpiX, out var dpiY) == 0)
            return new Point(dpiX / 96.0, dpiY / 96.0);

        return GetWpfScaleFallback();
    }

    private Point GetWpfScaleFallback()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        return new Point(dpi.DpiScaleX, dpi.DpiScaleY);
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
            LocalizationService.Apply(newSettings.Language);
            ViewModel.RefreshLocalizedText();
            RefreshColumnHeaders();
            _hibernateDurationSeconds = newSettings.GuiHibernateDurationSeconds;
        }
    }

    private void RefreshColumnHeaders()
    {
        ServerList.RefreshLocalizedText();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private const uint MonitorDefaultToNearest = 2;

    private enum MonitorDpiType
    {
        Effective = 0
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct NativePoint
    {
        public NativePoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public readonly int X;
        public readonly int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int cbSize;
        public NativeRect rcMonitor;
        public NativeRect rcWork;
        public uint dwFlags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint pt, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [DllImport("shcore.dll")]
    private static extern int GetDpiForMonitor(IntPtr hmonitor, MonitorDpiType dpiType, out uint dpiX, out uint dpiY);
}
