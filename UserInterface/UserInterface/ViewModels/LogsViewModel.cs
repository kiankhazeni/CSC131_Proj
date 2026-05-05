using System.Collections.ObjectModel;
using UserInterface.Models;

namespace UserInterface.ViewModels
{
    public class LogsViewModel : BaseViewModel
    {
        public LogsViewModel()
        {
            LogEntries = new ObservableCollection<LogEntry>
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
                    Message = "3 appointment emails processed.",
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

        public ObservableCollection<LogEntry> LogEntries { get; }
    }
}