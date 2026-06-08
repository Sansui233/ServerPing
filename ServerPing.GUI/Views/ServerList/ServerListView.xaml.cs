using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ServerPing.GUI.Models;
using ServerPing.GUI.Services;
using ServerPing.GUI.ViewModels;

namespace ServerPing.GUI.Views.ServerList;

public partial class ServerListView : UserControl
{
    private MainViewModel? ViewModel => DataContext as MainViewModel;
    private readonly List<DataGridRow> _pendingEntranceRows = [];
    private Point? _dragStartPoint;
    private ServerViewModel? _draggedServer;
    private int _dropInsertIndex = -1;
    private double? _dropIndicatorY;
    private bool _isEntranceAnimationScheduled;

    public ServerListView()
    {
        InitializeComponent();
    }

    public void RefreshLocalizedText()
    {
        NameColumnHeaderText.Text = LocalizationService.Get("Main.Name");
        AvailabilityColumn.Header = LocalizationService.Get("Main.Availability");
        LastCheckColumn.Header = LocalizationService.Get("Main.LastCheck");
        HostColumnHeaderText.Text = LocalizationService.Get("Main.Host");
        LatencyColumn.Header = LocalizationService.Get("Main.Latency");
        ActionsColumn.Header = LocalizationService.Get("Main.Actions");
    }

    private void ServerRow_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not DataGridRow row)
            return;

        row.BeginAnimation(OpacityProperty, null);
        row.Opacity = 0;
        row.RenderTransform = new TranslateTransform { Y = 5 };

        _pendingEntranceRows.Add(row);
        if (_isEntranceAnimationScheduled)
            return;

        _isEntranceAnimationScheduled = true;
        Dispatcher.BeginInvoke(PlayPendingEntranceAnimations, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private void PlayPendingEntranceAnimations()
    {
        _isEntranceAnimationScheduled = false;

        var rows = _pendingEntranceRows
            .Where(row => row.IsLoaded && row.Item is ServerViewModel)
            .OrderBy(row => row.GetIndex())
            .ToList();
        _pendingEntranceRows.Clear();

        for (var i = 0; i < rows.Count; i++)
            PlayEntranceAnimation(rows[i], i);
    }

    private static void PlayEntranceAnimation(DataGridRow row, int index)
    {
        var delay = TimeSpan.FromMilliseconds(Math.Min(index * 28, 180));
        var transform = row.RenderTransform as TranslateTransform ?? new TranslateTransform { Y = 5 };
        row.RenderTransform = transform;

        var opacityAnimation = new DoubleAnimation
        {
            To = 1,
            Duration = TimeSpan.FromMilliseconds(140),
            BeginTime = delay,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        var translateAnimation = new DoubleAnimation
        {
            To = 0,
            Duration = TimeSpan.FromMilliseconds(170),
            BeginTime = delay,
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        row.BeginAnimation(OpacityProperty, opacityAnimation);
        transform.BeginAnimation(TranslateTransform.YProperty, translateAnimation);
    }

    private void ServerDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        if (e.Column != NameColumn)
        {
            ViewModel?.ResetNameSortMode();
            NameColumn.SortDirection = null;
            return;
        }

        e.Handled = true;
        ClearGridSort();
        ViewModel?.CycleNameSortMode();
        ApplyNameSortDirection();
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
        if (ViewModel == null || textBox.DataContext is not ServerViewModel server)
            return;

        server.IsEditingIdentity = false;
        var saved = await ViewModel.SaveServerAsync(server);

        if (moveFocus && saved)
            textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
    }

    private void ServerDataGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = null;
        _draggedServer = null;

        if (e.OriginalSource is not DependencyObject source)
            return;

        if (FindAncestor<Button>(source) != null || FindAncestor<DataGridColumnHeader>(source) != null)
            return;

        var textBox = FindAncestor<TextBox>(source);
        if (textBox?.IsKeyboardFocusWithin == true)
            return;

        var row = FindAncestor<DataGridRow>(source);
        if (row?.Item is not ServerViewModel server)
            return;

        _dragStartPoint = e.GetPosition(null);
        _draggedServer = server;
    }

    private void ServerDataGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragStartPoint == null || _draggedServer == null || e.LeftButton != MouseButtonState.Pressed)
            return;

        var position = e.GetPosition(null);
        var delta = position - _dragStartPoint.Value;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        PrepareManualOrdering();
        DragDrop.DoDragDrop(ServerDataGrid, _draggedServer, DragDropEffects.Move);
        HideDropInsertionIndicator();
        _dragStartPoint = null;
        _draggedServer = null;
    }

    private void ServerDataGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel == null || _dragStartPoint == null || _draggedServer == null)
            return;

        try
        {
            var source = e.OriginalSource as DependencyObject;
            if (source == null
                || FindAncestor<Button>(source) != null
                || FindAncestor<DataGridColumnHeader>(source) != null
                || FindAncestor<TextBox>(source) != null)
            {
                return;
            }

            var row = FindAncestor<DataGridRow>(source);
            if (row?.Item is ServerViewModel server && ReferenceEquals(server, _draggedServer))
                ViewModel.OpenStatsOverlayCommand.Execute(server);
        }
        finally
        {
            _dragStartPoint = null;
            _draggedServer = null;
        }
    }

    private void ServerDataGrid_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(ServerViewModel)))
        {
            e.Effects = DragDropEffects.None;
            HideDropInsertionIndicator();
            return;
        }

        var result = GetDropInsertion(e.GetPosition(ServerDataGrid));
        ShowDropInsertionIndicator(result.Index, result.Y);
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void ServerDataGrid_DragLeave(object sender, DragEventArgs e)
    {
        var position = e.GetPosition(ServerDataGrid);
        if (position.X < 0
            || position.Y < 0
            || position.X > ServerDataGrid.ActualWidth
            || position.Y > ServerDataGrid.ActualHeight)
        {
            HideDropInsertionIndicator();
        }
    }

    private async void ServerDataGrid_Drop(object sender, DragEventArgs e)
    {
        if (ViewModel == null || !e.Data.GetDataPresent(typeof(ServerViewModel)))
            return;

        var server = (ServerViewModel)e.Data.GetData(typeof(ServerViewModel))!;
        var insertIndex = _dropInsertIndex >= 0
            ? _dropInsertIndex
            : GetDropInsertion(e.GetPosition(ServerDataGrid)).Index;

        HideDropInsertionIndicator();
        await ViewModel.MoveServerAsync(server, insertIndex);
        e.Handled = true;
    }

    private (int Index, double Y) GetDropInsertion(Point gridPoint)
    {
        var row = GetRowAtPoint(gridPoint);
        if (row != null)
        {
            var rowPoint = ServerDataGrid.TranslatePoint(gridPoint, row);
            var insertAfter = rowPoint.Y > row.ActualHeight / 2;
            var y = row.TransformToAncestor(ServerListHost).Transform(new Point(0, insertAfter ? row.ActualHeight : 0)).Y;
            return (row.GetIndex() + (insertAfter ? 1 : 0), y);
        }

        var firstRowTop = GetFirstRowTop();
        if (gridPoint.Y <= firstRowTop)
            return (0, firstRowTop);

        return (ViewModel?.Servers.Count ?? 0, GetLastRowBottom(firstRowTop));
    }

    private DataGridRow? GetRowAtPoint(Point gridPoint)
    {
        var source = ServerDataGrid.InputHitTest(gridPoint) as DependencyObject;
        return source == null ? null : FindAncestor<DataGridRow>(source);
    }

    private double GetFirstRowTop()
    {
        if (ServerDataGrid.ItemContainerGenerator.ContainerFromIndex(0) is DataGridRow row)
            return row.TransformToAncestor(ServerListHost).Transform(new Point(0, 0)).Y;

        var header = FindVisualChild<DataGridColumnHeader>(ServerDataGrid);
        if (header != null)
        {
            var headerTop = header.TransformToAncestor(ServerListHost).Transform(new Point(0, 0)).Y;
            return headerTop + header.ActualHeight;
        }

        return 0;
    }

    private double GetLastRowBottom(double fallbackY)
    {
        var lastIndex = (ViewModel?.Servers.Count ?? 0) - 1;
        if (lastIndex >= 0 && ServerDataGrid.ItemContainerGenerator.ContainerFromIndex(lastIndex) is DataGridRow row)
            return row.TransformToAncestor(ServerListHost).Transform(new Point(0, row.ActualHeight)).Y;

        return fallbackY;
    }

    private void ShowDropInsertionIndicator(int index, double y)
    {
        y = Math.Max(0, y - 1);
        if (_dropInsertIndex == index
            && _dropIndicatorY.HasValue
            && Math.Abs(_dropIndicatorY.Value - y) < 0.5
            && DropInsertionIndicator.Visibility == Visibility.Visible)
        {
            return;
        }

        _dropInsertIndex = index;
        _dropIndicatorY = y;
        DropInsertionIndicatorTransform.Y = y;

        if (DropInsertionIndicator.Visibility != Visibility.Visible)
            DropInsertionIndicator.Visibility = Visibility.Visible;
    }

    private void HideDropInsertionIndicator()
    {
        if (DropInsertionIndicator.Visibility != Visibility.Visible)
            return;

        DropInsertionIndicator.Visibility = Visibility.Collapsed;
        _dropInsertIndex = -1;
        _dropIndicatorY = null;
    }

    private void PrepareManualOrdering()
    {
        if (ViewModel == null)
            return;

        var visibleOrder = ServerDataGrid.Items.OfType<ServerViewModel>().ToList();
        ClearGridSort();
        ViewModel.UseCustomOrder(visibleOrder);
    }

    private void ClearGridSort()
    {
        ServerDataGrid.Items.SortDescriptions.Clear();
        foreach (var column in ServerDataGrid.Columns)
            column.SortDirection = null;
    }

    private void ApplyNameSortDirection()
    {
        var mode = ViewModel?.NameSortMode ?? ServerSortMode.Auto;
        NameColumn.SortDirection = mode switch
        {
            ServerSortMode.AToZ => ListSortDirection.Ascending,
            ServerSortMode.ZToA => ListSortDirection.Descending,
            _ => null
        };
    }

    private static T? FindAncestor<T>(DependencyObject obj) where T : DependencyObject
    {
        while (obj != null)
        {
            if (obj is T target)
                return target;

            obj = VisualTreeHelper.GetParent(obj);
        }

        return null;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T target)
                return target;

            var descendant = FindVisualChild<T>(child);
            if (descendant != null)
                return descendant;
        }

        return null;
    }
}
