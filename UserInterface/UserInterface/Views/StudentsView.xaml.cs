using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UserInterface.Models;
using UserInterface.ViewModels;
using System.Windows.Controls.Primitives;

namespace UserInterface.Views
{
    public partial class StudentsView : UserControl
    {
        private bool _isApplyingColumnLayout;
        private ScrollViewer? _studentsGridScrollViewer;
        private ScrollBar? _studentsHorizontalScrollBar;
        private bool _syncingHorizontalScroll;

        public StudentsView()
        {
            InitializeComponent();
            Loaded += (_, _) => ApplySavedColumnLayout();
        }

        private StudentsViewModel? ViewModel => DataContext as StudentsViewModel;

        private void StudentsDataGrid_Loaded(object sender, RoutedEventArgs e)
        {
            ApplySavedColumnLayout();

            _studentsGridScrollViewer = FindVisualChild<ScrollViewer>(StudentsDataGrid);
            _studentsHorizontalScrollBar = FindVisualChildByName<ScrollBar>(this, "StudentsHorizontalScrollBar");

            if (_studentsGridScrollViewer != null)
            {
                _studentsGridScrollViewer.ScrollChanged += StudentsGridScrollViewer_ScrollChanged;
                _studentsGridScrollViewer.ScrollChanged += StudentsGridScrollViewer_ScrollChanged;
                
            }

            SyncStudentsHorizontalScrollBar();
        }

        private void ColumnCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isApplyingColumnLayout)
                return;

            UpdateColumnVisibilityFromCheckboxes();
            SaveCurrentColumnLayout();
        }

        private void StudentsDataGrid_ColumnReordered(object sender, DataGridColumnEventArgs e)
        {
            SaveCurrentColumnLayout();
        }

        private void StudentsDataGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            SaveCurrentColumnLayout();
        }

        private void ResetColumns_Click(object sender, RoutedEventArgs e)
        {
            ViewModel?.ResetColumnSettings();
            ApplySavedColumnLayout();
        }

        private void ApplySavedColumnLayout()
        {
            if (ViewModel == null || StudentsDataGrid == null)
                return;

            _isApplyingColumnLayout = true;

            try
            {
                var map = GetColumnMap();
                var checkBoxes = GetCheckBoxMap();
                var settings = BuildSafeColumnSettings(map.Keys);

                for (int i = 0; i < settings.Count; i++)
                {
                    var setting = settings[i];
                    if (!map.TryGetValue(setting.Key, out var column) || column == null)
                        continue;

                    column.DisplayIndex = Math.Min(i, StudentsDataGrid.Columns.Count - 1);
                    column.Visibility = setting.IsVisible ? Visibility.Visible : Visibility.Collapsed;

                    if (setting.Width > 10)
                        column.Width = new DataGridLength(setting.Width);

                    if (checkBoxes.TryGetValue(setting.Key, out var checkBox) && checkBox != null)
                    {
                        checkBox.IsChecked = setting.IsVisible;
                        checkBox.Foreground = setting.IsVisible
                            ? System.Windows.Media.Brushes.White
                            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175));
                    }
                }
            }
            catch
            {
                ViewModel.ResetColumnSettings();
                ApplyBuiltInColumnLayout();
            }
            finally
            {
                _isApplyingColumnLayout = false;
            }
        }

        private List<StudentColumnSetting> BuildSafeColumnSettings(IEnumerable<string> knownKeys)
        {
            var known = knownKeys.ToHashSet();
            var defaultSettings = UserInterface.Models.AppSettings.CreateDefaultStudentColumns()
                .Where(x => known.Contains(x.Key))
                .ToList();

            var saved = ViewModel?.StudentColumns?
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Key) && known.Contains(x.Key))
                .GroupBy(x => x.Key)
                .Select(x => x.First())
                .OrderBy(x => x.DisplayIndex)
                .ToList() ?? new List<StudentColumnSetting>();

            foreach (var defaultColumn in defaultSettings)
            {
                if (!saved.Any(x => x.Key == defaultColumn.Key))
                    saved.Add(defaultColumn);
            }

            return saved.OrderBy(x => x.DisplayIndex).ToList();
        }

        private void ApplyBuiltInColumnLayout()
        {
            var map = GetColumnMap();
            var checkBoxes = GetCheckBoxMap();
            var defaults = UserInterface.Models.AppSettings.CreateDefaultStudentColumns();

            for (int i = 0; i < defaults.Count; i++)
            {
                var setting = defaults[i];
                if (!map.TryGetValue(setting.Key, out var column) || column == null)
                    continue;

                column.DisplayIndex = Math.Min(i, StudentsDataGrid.Columns.Count - 1);
                column.Visibility = setting.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                column.Width = new DataGridLength(setting.Width);

                if (checkBoxes.TryGetValue(setting.Key, out var checkBox) && checkBox != null)
                    checkBox.IsChecked = setting.IsVisible;
            }
        }

        private void UpdateColumnVisibilityFromCheckboxes()
        {
            var columns = GetColumnMap();
            var checkBoxes = GetCheckBoxMap();

            foreach (var pair in columns)
            {
                if (!checkBoxes.TryGetValue(pair.Key, out var checkBox) || checkBox == null || pair.Value == null)
                    continue;

                bool isVisible = checkBox.IsChecked == true;
                pair.Value.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
                checkBox.Foreground = isVisible
                    ? System.Windows.Media.Brushes.White
                    : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175));
            }
        }

        private void SaveCurrentColumnLayout()
        {
            if (_isApplyingColumnLayout || ViewModel == null)
                return;

            var columns = GetColumnMap();
            var checkBoxes = GetCheckBoxMap();

            foreach (var setting in ViewModel.StudentColumns.Where(x => x != null).ToList())
            {
                if (!columns.TryGetValue(setting.Key, out var column) || column == null)
                    continue;

                setting.DisplayIndex = column.DisplayIndex;

                if (checkBoxes.TryGetValue(setting.Key, out var checkBox) && checkBox != null)
                    setting.IsVisible = checkBox.IsChecked == true;
                else
                    setting.IsVisible = column.Visibility == Visibility.Visible;

                if (column.ActualWidth > 10)
                    setting.Width = column.ActualWidth;
                else if (column.Width.Value > 10)
                    setting.Width = column.Width.Value;
            }

            ViewModel.SaveColumnSettings();
        }

        private Dictionary<string, DataGridColumn?> GetColumnMap()
        {
            return new Dictionary<string, DataGridColumn?>
            {
                { "FirstName", FirstNameColumn },
                { "MiddleName", MiddleNameColumn },
                { "LastName", LastNameColumn },
                { "Email", EmailColumn },
                { "Phone", PhoneColumn },
                { "Course", CourseColumn },
                { "Date", DateColumn },
                { "LocationName", LocationNameColumn },
                { "Group", GroupColumn },
                { "Status", StatusColumn },
                { "AcuityRegistration", AcuityRegistrationColumn },
                { "AhaRegistration", AhaRegistrationColumn },
                { "ReminderEmailSent", ReminderEmailSentColumn }
            };
        }

        private Dictionary<string, CheckBox?> GetCheckBoxMap()
        {
            return new Dictionary<string, CheckBox?>
            {
                { "FirstName", FirstNameCheckBox },
                { "MiddleName", MiddleNameCheckBox },
                { "LastName", LastNameCheckBox },
                { "Email", EmailCheckBox },
                { "Phone", PhoneCheckBox },
                { "Course", CourseCheckBox },
                { "Date", DateCheckBox },
                { "LocationName", LocationNameCheckBox },
                { "Group", GroupCheckBox },
                { "Status", StatusCheckBox },
                { "AcuityRegistration", AcuityRegistrationCheckBox },
                { "AhaRegistration", AhaRegistrationCheckBox },
                { "ReminderEmailSent", ReminderEmailSentCheckBox }
            };
        }

        private void StudentsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void StudentsDataGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not DataGrid dataGrid)
            {
                return;
            }

            ScrollViewer? gridScrollViewer = FindVisualChild<ScrollViewer>(dataGrid);

            if (gridScrollViewer == null)
            {
                return;
            }

            bool scrollingUp = e.Delta > 0;
            bool scrollingDown = e.Delta < 0;

            bool gridCanScrollUp = gridScrollViewer.VerticalOffset > 0;
            bool gridCanScrollDown = gridScrollViewer.VerticalOffset < gridScrollViewer.ScrollableHeight;

            bool shouldLetGridScroll =
                (scrollingUp && gridCanScrollUp) ||
                (scrollingDown && gridCanScrollDown);

            if (shouldLetGridScroll)
            {
                return;
            }

            e.Handled = true;

            var forwardedEvent = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };

            ScrollViewer? parentScrollViewer = FindParentScrollViewer(dataGrid);

            if (parentScrollViewer != null)
            {
                parentScrollViewer.RaiseEvent(forwardedEvent);
            }


        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                {
                    return typedChild;
                }

                T? descendant = FindVisualChild<T>(child);

                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }

        private static T? FindVisualChildByName<T>(DependencyObject parent, string name)
            where T : FrameworkElement
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild && typedChild.Name == name)
                {
                    return typedChild;
                }

                T? descendant = FindVisualChildByName<T>(child, name);

                if (descendant != null)
                {
                    return descendant;
                }
            }

            return null;
        }

        private static ScrollViewer? FindParentScrollViewer(DependencyObject child)
        {
            DependencyObject? current = child;

            while (current != null)
            {
                current = VisualTreeHelper.GetParent(current);

                if (current is ScrollViewer scrollViewer)
                {
                    return scrollViewer;
                }
            }

            return null;
        }

        private void StudentsGridScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.HorizontalChange != 0 ||
                e.ExtentWidthChange != 0 ||
                e.ViewportWidthChange != 0)
            {
                SyncStudentsHorizontalScrollBar();
            }
        }

        private void SyncStudentsHorizontalScrollBar()
        {
            if (_studentsGridScrollViewer == null || _studentsHorizontalScrollBar == null)
            {
                return;
            }

            _syncingHorizontalScroll = true;

            _studentsHorizontalScrollBar.Minimum = 0;
            _studentsHorizontalScrollBar.Maximum = _studentsGridScrollViewer.ScrollableWidth;
            _studentsHorizontalScrollBar.ViewportSize = _studentsGridScrollViewer.ViewportWidth;
            _studentsHorizontalScrollBar.LargeChange = Math.Max(20, _studentsGridScrollViewer.ViewportWidth * 0.8);
            _studentsHorizontalScrollBar.SmallChange = 24;
            _studentsHorizontalScrollBar.Value = _studentsGridScrollViewer.HorizontalOffset;
            _studentsHorizontalScrollBar.Visibility = _studentsGridScrollViewer.ScrollableWidth > 0
                ? Visibility.Visible
                : Visibility.Collapsed;

            _syncingHorizontalScroll = false;
        }

        private void StudentsHorizontalScrollBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_syncingHorizontalScroll || _studentsGridScrollViewer == null)
            {
                return;
            }

            _studentsGridScrollViewer.ScrollToHorizontalOffset(e.NewValue);
            SyncStudentsHorizontalScrollBar();
        }   
    }
}
