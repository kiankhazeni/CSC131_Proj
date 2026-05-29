using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace UserInterface.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
        }

        private void DataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled)
                return;

            e.Handled = true;
            var forwardedEvent = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };

            SettingsScrollViewer.RaiseEvent(forwardedEvent);
        }

        private void SettingsScrollViewer_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
        {
            if (e.OriginalSource is Button)
                e.Handled = true;
        }

        private void ConfigScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
            {
                return;
            }

            bool canScrollUp = scrollViewer.VerticalOffset > 0;
            bool canScrollDown = scrollViewer.VerticalOffset < scrollViewer.ScrollableHeight;

            bool scrollingUp = e.Delta > 0;
            bool scrollingDown = e.Delta < 0;

            bool shouldLetInnerScroll =
                (scrollingUp && canScrollUp) ||
                (scrollingDown && canScrollDown);

            if (shouldLetInnerScroll)
            {
                return;
            }

            e.Handled = true;

            var forwardedEvent = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };

            SettingsScrollViewer.RaiseEvent(forwardedEvent);
        }

    }
}
