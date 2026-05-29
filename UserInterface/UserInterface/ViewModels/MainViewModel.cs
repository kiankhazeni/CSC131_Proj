using System;
using System.ComponentModel;
using System.Windows.Input;
using UserInterface.Models;
using UserInterface.Services;

namespace UserInterface.ViewModels
{
    public class MainViewModel : BaseViewModel, IDisposable
    {
        private readonly AppSettings _settings;
        private readonly AppSettingsService _settingsService;
        private readonly LogService _logService;
        private readonly AutomationService _automationService;
        private BaseViewModel _currentViewModel;
        private string _currentSectionTitle = "Dashboard";

        public MainViewModel()
        {
            _settingsService = new AppSettingsService();
            _settings = _settingsService.Load();
            _logService = new LogService(_settings, _settingsService.SettingsFolderPath);
            _automationService = new AutomationService(_settings, _logService);
            _automationService.PropertyChanged += AutomationService_PropertyChanged;

            StudentsViewModel = new StudentsViewModel(_settings, _settingsService, _logService);
            LogsViewModel = new LogsViewModel(_logService);
            RemindersViewModel = new RemindersViewModel(StudentsViewModel);
            SettingsViewModel = new SettingsViewModel(_settings, _settingsService, _logService);
            DashboardViewModel = new DashboardViewModel(StudentsViewModel, this, _logService, _automationService, _settings, _settingsService);

            _currentViewModel = DashboardViewModel;

            ShowDashboardCommand = new RelayCommand(() => SetCurrentView(DashboardViewModel, "Dashboard"));
            ShowStudentsCommand = new RelayCommand(() => SetCurrentView(StudentsViewModel, "Students"));
            ShowRemindersCommand = new RelayCommand(() => SetCurrentView(RemindersViewModel, "Reminders"));
            ShowLogsCommand = new RelayCommand(() => SetCurrentView(LogsViewModel, "Logs"));
            ShowSettingsCommand = new RelayCommand(() => SetCurrentView(SettingsViewModel, "Settings"));

            StartAutomationCommand = new RelayCommand(StartAutomation, () => !IsAutomationRunning);
            StopAutomationCommand = new RelayCommand(StopAutomation, () => IsAutomationRunning);

            _logService.Add("Application", "Vitals UI loaded.");
        }

        public DashboardViewModel DashboardViewModel { get; }
        public StudentsViewModel StudentsViewModel { get; }
        public RemindersViewModel RemindersViewModel { get; }
        public LogsViewModel LogsViewModel { get; }
        public SettingsViewModel SettingsViewModel { get; }

        public BaseViewModel CurrentViewModel
        {
            get => _currentViewModel;
            set
            {
                _currentViewModel = value;
                OnPropertyChanged();
            }
        }

        public string CurrentSectionTitle
        {
            get => _currentSectionTitle;
            set
            {
                _currentSectionTitle = value;
                OnPropertyChanged();
            }
        }

        public bool IsAutomationRunning => _automationService.IsRunning;

        public string AutomationStatusText => _automationService.StatusText;

        public ICommand ShowDashboardCommand { get; }
        public ICommand ShowStudentsCommand { get; }
        public ICommand ShowRemindersCommand { get; }
        public ICommand ShowLogsCommand { get; }
        public ICommand ShowSettingsCommand { get; }

        public ICommand StartAutomationCommand { get; }
        public ICommand StopAutomationCommand { get; }

        private void SetCurrentView(BaseViewModel viewModel, string title)
        {
            CurrentViewModel = viewModel;
            CurrentSectionTitle = title;
        }

        private void StartAutomation()
        {
            _settingsService.Save(_settings);
            _automationService.StartAll();
        }

        private void StopAutomation()
        {
            _automationService.StopAll();
        }

        private void AutomationService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AutomationService.IsRunning) ||
                e.PropertyName == nameof(AutomationService.StatusText))
            {
                OnPropertyChanged(nameof(IsAutomationRunning));
                OnPropertyChanged(nameof(AutomationStatusText));

                if (StartAutomationCommand is RelayCommand startCmd)
                    startCmd.RaiseCanExecuteChanged();

                if (StopAutomationCommand is RelayCommand stopCmd)
                    stopCmd.RaiseCanExecuteChanged();
            }
        }

        public void Dispose()
        {
            _automationService.Dispose();
            StudentsViewModel.Dispose();

            if (!_settings.RetainVisibleLogsBetweenSessions)
                _logService.ClearVisible();

            _logService.Dispose();
        }
    }
}
