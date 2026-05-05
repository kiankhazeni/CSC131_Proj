namespace UserInterface.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private int _studentCheckMinutes = 5;
        private int _inboxCheckMinutes = 3;
        private int _renewalReminderDays = 30;
        private int _followUpReminderDays = 2;
        private string _senderEmail = "info@CPRLifeline.net";

        public int StudentCheckMinutes
        {
            get => _studentCheckMinutes;
            set
            {
                _studentCheckMinutes = value;
                OnPropertyChanged();
            }
        }

        public int InboxCheckMinutes
        {
            get => _inboxCheckMinutes;
            set
            {
                _inboxCheckMinutes = value;
                OnPropertyChanged();
            }
        }

        public int RenewalReminderDays
        {
            get => _renewalReminderDays;
            set
            {
                _renewalReminderDays = value;
                OnPropertyChanged();
            }
        }

        public int FollowUpReminderDays
        {
            get => _followUpReminderDays;
            set
            {
                _followUpReminderDays = value;
                OnPropertyChanged();
            }
        }

        public string SenderEmail
        {
            get => _senderEmail;
            set
            {
                _senderEmail = value;
                OnPropertyChanged();
            }
        }
    }
}