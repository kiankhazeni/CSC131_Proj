using System.Collections.Specialized;
using System.Windows.Controls;
using UserInterface.ViewModels;

namespace UserInterface.Views
{
    public partial class LogsView : UserControl
    {
        private INotifyCollectionChanged? _currentCollection;

        public LogsView()
        {
            InitializeComponent();
            DataContextChanged += (_, _) => HookLogCollection();
            Loaded += (_, _) => HookLogCollection();
        }

        private void HookLogCollection()
        {
            if (_currentCollection != null)
                _currentCollection.CollectionChanged -= LogEntries_CollectionChanged;

            _currentCollection = null;

            if (DataContext is LogsViewModel vm && vm.LogEntries is INotifyCollectionChanged collection)
            {
                _currentCollection = collection;
                _currentCollection.CollectionChanged += LogEntries_CollectionChanged;
                ScrollToBottom();
            }
        }

        private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ScrollToBottom();
        }

        private void ScrollToBottom()
        {
            if (LogsDataGrid.Items.Count > 0)
                LogsDataGrid.ScrollIntoView(LogsDataGrid.Items[LogsDataGrid.Items.Count - 1]);
        }
    }
}
