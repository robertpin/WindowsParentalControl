using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ParentalControl.Admin.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not DataGrid grid) return;

        var scrollViewer = FindVisualChild<ScrollViewer>(grid);
        if (scrollViewer is null) return;

        bool atTop = scrollViewer.VerticalOffset <= 0 && e.Delta > 0;
        bool atBottom = scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight && e.Delta < 0;

        if (atTop || atBottom)
        {
            e.Handled = true;
            var args = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = MouseWheelEvent
            };
            (VisualTreeHelper.GetParent(grid) as UIElement)?.RaiseEvent(args);
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            var result = FindVisualChild<T>(child);
            if (result is not null) return result;
        }
        return null;
    }
}
