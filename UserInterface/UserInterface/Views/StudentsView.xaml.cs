using System.Windows;
using System.Windows.Controls;

namespace UserInterface.Views
{
    public partial class StudentsView : UserControl
    {
        public StudentsView()
        {
            InitializeComponent();
            UpdateColumnVisibility();
        }

        private void ColumnCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            UpdateColumnVisibility();
        }

        private void UpdateColumnVisibility()
        {
            SetColumnVisibility(FirstNameColumn, FirstNameCheckBox);
            SetColumnVisibility(MiddleNameColumn, MiddleNameCheckBox);
            SetColumnVisibility(LastNameColumn, LastNameCheckBox);
            SetColumnVisibility(EmailColumn, EmailCheckBox);
            SetColumnVisibility(PhoneColumn, PhoneCheckBox);
            SetColumnVisibility(CourseColumn, CourseCheckBox);
            SetColumnVisibility(DateColumn, DateCheckBox);
            SetColumnVisibility(AcuityRegistrationColumn, AcuityRegistrationCheckBox);
            SetColumnVisibility(AhaRegistrationColumn, AhaRegistrationCheckBox);
            SetColumnVisibility(ReminderEmailSentColumn, ReminderEmailSentCheckBox);
            SetColumnVisibility(LocationNameColumn, LocationNameCheckBox);
            SetColumnVisibility(StatusColumn, StatusCheckBox);
            SetColumnVisibility(GroupColumn, GroupCheckBox);
        }

        private void SetColumnVisibility(DataGridColumn column, CheckBox checkBox)
        {
            if (column == null || checkBox == null)
                return;

            column.Visibility = checkBox.IsChecked == true
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}