using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace UserInterface.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
        }

        private void ForwardMouseWheelToParentScrollViewer(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled)
                return;

            DependencyObject? current = sender as DependencyObject;

            while (current != null)
            {
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);

                if (current is ScrollViewer scrollViewer)
                {
                    e.Handled = true;

                    var forwardedEvent = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                    {
                        RoutedEvent = UIElement.MouseWheelEvent,
                        Source = sender
                    };

                    scrollViewer.RaiseEvent(forwardedEvent);
                    return;
                }
            }
        }
    }
}