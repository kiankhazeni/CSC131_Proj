using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using UserInterface.Models;
using UserInterface.Services;

namespace UserInterface.ViewModels
{
    public class DashboardViewModel : BaseViewModel
    {
        private readonly StudentsViewModel _studentsViewModel;
        private readonly MainViewModel _mainViewModel;
        private readonly LogService _logService;
        private readonly AutomationService _automationService;
        private readonly AppSettings _settings;
        private readonly AppSettingsService _settingsService;

        public DashboardViewModel(
            StudentsViewModel studentsViewModel,
            MainViewModel mainViewModel,
            LogService logService,
            AutomationService automationService,
            AppSettings settings,
            AppSettingsService settingsService)
        {
            _studentsViewModel = studentsViewModel;
            _mainViewModel = mainViewModel;
            _logService = logService;
            _automationService = automationService;
            _settings = settings;
            _settingsService = settingsService;

            foreach (var module in _automationService.Modules)
                module.PropertyChanged += Module_PropertyChanged;

            _studentsViewModel.Students.CollectionChanged += Students_CollectionChanged;
            _mainViewModel.PropertyChanged += MainViewModel_PropertyChanged;
            _logService.Entries.CollectionChanged += Logs_CollectionChanged;
            _automationService.PropertyChanged += AutomationService_PropertyChanged;
        }

        public int ActiveStudentsCount =>
            _studentsViewModel.Students.Count(x => IsYes(x.AcuityRegistration));

        public int PendingStudentsCount =>
            _studentsViewModel.Students.Count(x => !IsYes(x.AcuityRegistration) && string.IsNullOrWhiteSpace(x.ReminderEmailSent));

        public int ErrorCount => _logService.Entries.Count(x => string.Equals(x.Level, "Error", StringComparison.OrdinalIgnoreCase));

        public string LastStudentCheck => _studentsViewModel.LastLoadTimeText;
        public string LastInboxCheck => _automationService.LastStartText;
        public string LastReminderRun => _automationService.LastStopText;
        public string NextReminderRun => IsAutomationRunning ? "Controlled by running modules" : "Start automation to resume";

        public string AutomationStatusText => _mainViewModel.AutomationStatusText;
        public bool IsAutomationRunning => _mainViewModel.IsAutomationRunning;

        public ICommand StartAutomationCommand => _mainViewModel.StartAutomationCommand;
        public ICommand StopAutomationCommand => _mainViewModel.StopAutomationCommand;

        public IEnumerable<LogEntry> RecentLogs => _logService.Entries.Reverse().Take(3).Reverse();
        public ObservableCollection<AutomationModule> ModuleStatuses => _automationService.Modules;

        private void Students_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(ActiveStudentsCount));
            OnPropertyChanged(nameof(PendingStudentsCount));
            OnPropertyChanged(nameof(LastStudentCheck));
        }

        private void Logs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(ErrorCount));
            OnPropertyChanged(nameof(RecentLogs));
        }

        private void MainViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.AutomationStatusText) ||
                e.PropertyName == nameof(MainViewModel.IsAutomationRunning))
            {
                OnPropertyChanged(nameof(AutomationStatusText));
                OnPropertyChanged(nameof(IsAutomationRunning));
                OnPropertyChanged(nameof(NextReminderRun));
            }
        }

        private void AutomationService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            foreach (var module in _automationService.Modules)
                module.PropertyChanged -= Module_PropertyChanged;

            foreach (var module in _automationService.Modules)
                module.PropertyChanged += Module_PropertyChanged;

            OnPropertyChanged(nameof(LastInboxCheck));
            OnPropertyChanged(nameof(LastReminderRun));
            OnPropertyChanged(nameof(ModuleStatuses));
        }



        private void Module_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AutomationModule.IsEnabled))
                _settingsService.Save(_settings);

            OnPropertyChanged(nameof(ModuleStatuses));
        }

        private static bool IsYes(string value)
        {
            return string.Equals(value?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
