using System.Collections.ObjectModel;

namespace UserInterface.Models
{
    public class AppSettings
    {
        public const int CurrentSettingsVersion = 1;
        public int SettingsVersion { get; set; } = CurrentSettingsVersion;
        public string RqiCsvPath { get; set; } = @"resources\preprod_cl.csv";
        public string AhaCsvPath { get; set; } = @"resources\aha.csv";
        public bool RetainVisibleLogsBetweenSessions { get; set; } = false;
        public int VisibleLogRetentionHours { get; set; } = 24;
        public ObservableCollection<AutomationModule> AutomationModules { get; set; } = CreateDefaultModules();
        public ObservableCollection<StudentColumnSetting> StudentColumns { get; set; } = CreateDefaultStudentColumns();

        public static AppSettings CreateDefault()
        {
            return new AppSettings
            {
                SettingsVersion = CurrentSettingsVersion,
                RqiCsvPath = @"resources\preprod_cl.csv",
                AhaCsvPath = @"resources\aha.csv",
                RetainVisibleLogsBetweenSessions = false,
                VisibleLogRetentionHours = 24,
                AutomationModules = CreateDefaultModules(),
                StudentColumns = CreateDefaultStudentColumns()
            };
        }

        public static ObservableCollection<AutomationModule> CreateDefaultModules()
        {
            return new ObservableCollection<AutomationModule>
            {
                new AutomationModule
                {
                    Name = "RQI Uploader & Email Scraper",
                    WorkingDirectory = string.Empty,
                    Command = "java",
                    Arguments = "--enable-native-access=ALL-UNNAMED \"-Dapp.config=config/app.properties\" -jar \"modules/rqi-and-email-scraper-module.jar\"",
                    IsEnabled = true
                },
                new AutomationModule
                {
                    Name = "AHA Automation",
                    WorkingDirectory = string.Empty,
                    Command = "java",
                    Arguments = "--enable-native-access=ALL-UNNAMED \"-Dapp.config=config/app.properties\" -jar \"modules/aha-automation.jar\"",
                    IsEnabled = true
                },
                new AutomationModule
                {
                    Name = "Outlook Event Creator",
                    WorkingDirectory = string.Empty,
                    Command = "java",
                    Arguments = "--enable-native-access=ALL-UNNAMED \"-Dapp.config=config/app.properties\" -jar \"modules/outlook-event-creator.jar\"",
                    IsEnabled = true
                },
                new AutomationModule
                {
                    Name = "Reminders",
                    WorkingDirectory = string.Empty,
                    Command = "java",
                    Arguments = "--enable-native-access=ALL-UNNAMED \"-Dapp.config=config/app.properties\" -jar \"modules/reminder-module.jar\"",
                    IsEnabled = true
                }
            };
        }

        public static ObservableCollection<StudentColumnSetting> CreateDefaultStudentColumns()
        {
            return new ObservableCollection<StudentColumnSetting>
            {
                new StudentColumnSetting { Key = "FirstName", Header = "First Name", IsVisible = true, DisplayIndex = 0, Width = 127 },
                new StudentColumnSetting { Key = "MiddleName", Header = "Middle Name", IsVisible = false, DisplayIndex = 1, Width = 120 },
                new StudentColumnSetting { Key = "LastName", Header = "Last Name", IsVisible = true, DisplayIndex = 2, Width = 125 },
                new StudentColumnSetting { Key = "Email", Header = "Email", IsVisible = true, DisplayIndex = 3, Width = 220 },
                new StudentColumnSetting { Key = "Phone", Header = "Phone", IsVisible = false, DisplayIndex = 4, Width = 130 },
                new StudentColumnSetting { Key = "Course", Header = "Course", IsVisible = true, DisplayIndex = 5, Width = 100 },
                new StudentColumnSetting { Key = "Date", Header = "Date", IsVisible = true, DisplayIndex = 6, Width = 110 },
                new StudentColumnSetting { Key = "LocationName", Header = "Location", IsVisible = false, DisplayIndex = 7, Width = 120 },
                new StudentColumnSetting { Key = "Group", Header = "Group", IsVisible = false, DisplayIndex = 8, Width = 130 },
                new StudentColumnSetting { Key = "Status", Header = "Status", IsVisible = false, DisplayIndex = 9, Width = 100 },
                new StudentColumnSetting { Key = "AcuityRegistration", Header = "Acuity Registration", IsVisible = true, DisplayIndex = 10, Width = 140 },
                new StudentColumnSetting { Key = "AhaRegistration", Header = "AHA Registration", IsVisible = true, DisplayIndex = 11, Width = 140 },
                new StudentColumnSetting { Key = "ReminderEmailSent", Header = "Reminder Sent", IsVisible = true, DisplayIndex = 12, Width = 130 }
            };
        }
    }
}
