using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using UserInterface.Models;
using UserInterface.Services;

namespace UserInterface.ViewModels
{
    public class RemindersViewModel : BaseViewModel
    {
        private readonly StudentsViewModel _studentsViewModel;
        private readonly AppPropertiesService _appPropertiesService = new AppPropertiesService();

        public RemindersViewModel(StudentsViewModel studentsViewModel)
        {
            _studentsViewModel = studentsViewModel;
            ReminderItems = new ObservableCollection<ReminderItem>();
            _studentsViewModel.Students.CollectionChanged += Students_CollectionChanged;
            RebuildReminders();
        }

        public ObservableCollection<ReminderItem> ReminderItems { get; }

        private void Students_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildReminders();
        }

        private void RebuildReminders()
        {
            ReminderItems.Clear();
            int resendDays = _appPropertiesService.GetInt("reminder.registration.resendAfterDays", 7);

            foreach (var student in _studentsViewModel.Students)
            {
                bool ahaRegistered = IsYes(student.AhaRegistration);
                bool acuityRegistered = IsYes(student.AcuityRegistration);
                bool hasReminder = !string.IsNullOrWhiteSpace(student.ReminderEmailSent);

                if (!ahaRegistered || acuityRegistered)
                    continue;

                ReminderItems.Add(new ReminderItem
                {
                    Type = "Registration",
                    StudentName = student.FullName,
                    Email = student.Email,
                    DateText = string.IsNullOrWhiteSpace(student.Date) ? "—" : student.Date,
                    DueText = BuildDueText(student.Date, resendDays),
                    Status = hasReminder ? "Sent " + student.ReminderEmailSent : "Pending"
                });
            }
        }

        private static string BuildDueText(string dateText, int daysBefore)
        {
            if (!TryParseDate(dateText, out var date))
                return "Check manually";

            return date.AddDays(-Math.Abs(daysBefore)).ToString("M/d/yyyy", CultureInfo.InvariantCulture);
        }

        private static bool TryParseDate(string value, out DateTime date)
        {
            string[] formats = { "M/d/yyyy", "MM/dd/yyyy", "M-d-yyyy", "MM-dd-yyyy", "yyyy-MM-dd" };
            return DateTime.TryParseExact(value?.Trim() ?? string.Empty, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out date) ||
                   DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
        }

        private static bool IsYes(string value)
        {
            return string.Equals(value?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
