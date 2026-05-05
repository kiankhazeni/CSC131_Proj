using System.Collections.ObjectModel;
using UserInterface.Models;

namespace UserInterface.ViewModels
{
    public class RemindersViewModel : BaseViewModel
    {
        public RemindersViewModel()
        {
            ReminderItems = new ObservableCollection<ReminderItem>
            {
                new ReminderItem
                {
                    Type = "Renewal",
                    StudentName = "Kevin Brown",
                    Contact = "kevin@email.com",
                    DueText = "Apr 25, 2026",
                    Status = "Queued"
                },
                new ReminderItem
                {
                    Type = "Unpaid Registration",
                    StudentName = "Sarah Chen",
                    Contact = "sarah@email.com",
                    DueText = "Today",
                    Status = "Queued"
                },
                new ReminderItem
                {
                    Type = "No Appointment Yet",
                    StudentName = "Luis Ramirez",
                    Contact = "luis@email.com",
                    DueText = "Tomorrow",
                    Status = "Pending"
                }
            };
        }

        public ObservableCollection<ReminderItem> ReminderItems { get; }
    }
}