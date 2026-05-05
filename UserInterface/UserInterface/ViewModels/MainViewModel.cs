using System.Windows.Input;

namespace UserInterface.ViewModels
{
    public class MainViewModel : BaseViewModel
    {
        private BaseViewModel _currentViewModel;
        private string _currentSectionTitle = "Dashboard";
        private bool _isAutomationRunning = true;

        public MainViewModel()
        {
            StudentsViewModel = new StudentsViewModel();
            DashboardViewModel = new DashboardViewModel(StudentsViewModel, this);
            RemindersViewModel = new RemindersViewModel();
            LogsViewModel = new LogsViewModel();
            SettingsViewModel = new SettingsViewModel();

            _currentViewModel = DashboardViewModel;

            ShowDashboardCommand = new RelayCommand(() => SetCurrentView(DashboardViewModel, "Dashboard"));
            ShowStudentsCommand = new RelayCommand(() => SetCurrentView(StudentsViewModel, "Students"));
            ShowRemindersCommand = new RelayCommand(() => SetCurrentView(RemindersViewModel, "Reminders"));
            ShowLogsCommand = new RelayCommand(() => SetCurrentView(LogsViewModel, "Logs"));
            ShowSettingsCommand = new RelayCommand(() => SetCurrentView(SettingsViewModel, "Settings"));

            StartAutomationCommand = new RelayCommand(StartAutomation, () => !IsAutomationRunning);
            StopAutomationCommand = new RelayCommand(StopAutomation, () => IsAutomationRunning);
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

        public bool IsAutomationRunning
        {
            get => _isAutomationRunning;
            set
            {
                _isAutomationRunning = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AutomationStatusText));

                if (StartAutomationCommand is RelayCommand startCmd)
                    startCmd.RaiseCanExecuteChanged();

                if (StopAutomationCommand is RelayCommand stopCmd)
                    stopCmd.RaiseCanExecuteChanged();
            }
        }

        public string AutomationStatusText => IsAutomationRunning ? "Running" : "Stopped";

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
            IsAutomationRunning = true;
        }

        private void StopAutomation()
        {
            IsAutomationRunning = false;
        }
    }
}