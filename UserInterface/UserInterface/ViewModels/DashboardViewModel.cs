using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Input;
using UserInterface.Models;

namespace UserInterface.ViewModels
{
    public class DashboardViewModel : BaseViewModel
    {
        private readonly StudentsViewModel _studentsViewModel;
        private readonly MainViewModel _mainViewModel;

        public DashboardViewModel(StudentsViewModel studentsViewModel, MainViewModel mainViewModel)
        {
            _studentsViewModel = studentsViewModel;
            _mainViewModel = mainViewModel;

            _studentsViewModel.Students.CollectionChanged += Students_CollectionChanged;
            _mainViewModel.PropertyChanged += MainViewModel_PropertyChanged;

            RecentLogs = new ObservableCollection<LogEntry>
            {
                new LogEntry
                {
                    TimeText = "04/23/2026 08:00 AM",
                    Source = "ReminderService",
                    Message = "Renewal reminders sent successfully.",
                    Level = "Info"
                },
                new LogEntry
                {
                    TimeText = "04/23/2026 08:05 AM",
                    Source = "InboxReader",
                    Message = "3 student records processed.",
                    Level = "Info"
                },
                new LogEntry
                {
                    TimeText = "04/23/2026 08:07 AM",
                    Source = "StudentAutomation",
                    Message = "1 student acceptance failed. Retry needed.",
                    Level = "Warning"
                }
            };
        }

        public int ActiveStudentsCount =>
            _studentsViewModel.Students.Count(x =>
                string.Equals(
                    x.AcuityRegistration?.Trim(),
                    "YES",
                    System.StringComparison.OrdinalIgnoreCase));

        public int PendingStudentsCount =>
            _studentsViewModel.Students.Count(x =>
                !string.Equals(
                    x.AcuityRegistration?.Trim(),
                    "YES",
                    System.StringComparison.OrdinalIgnoreCase));

        public int ErrorCount => 1;

        public string LastStudentCheck => "5 minutes ago";
        public string LastInboxCheck => "2 minutes ago";
        public string LastReminderRun => "Today 8:00 AM";
        public string NextReminderRun => "Today 12:00 PM";

        public string AutomationStatusText => _mainViewModel.AutomationStatusText;
        public bool IsAutomationRunning => _mainViewModel.IsAutomationRunning;

        public ICommand StartAutomationCommand => _mainViewModel.StartAutomationCommand;
        public ICommand StopAutomationCommand => _mainViewModel.StopAutomationCommand;

        public ObservableCollection<LogEntry> RecentLogs { get; }

        private void Students_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(ActiveStudentsCount));
            OnPropertyChanged(nameof(PendingStudentsCount));
        }

        private void MainViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.AutomationStatusText) ||
                e.PropertyName == nameof(MainViewModel.IsAutomationRunning))
            {
                OnPropertyChanged(nameof(AutomationStatusText));
                OnPropertyChanged(nameof(IsAutomationRunning));
            }
        }
    }
}