using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using UserInterface.Models;
using UserInterface.Services;

namespace UserInterface.ViewModels
{
    public class StudentsViewModel : BaseViewModel
    {
        private readonly CsvStudentService _csvAppointmentService;

        private string _preprodCsvPath = @"Resources\preprod_cl.csv";
        private string _ahaCsvPath = @"Resources\aha.csv";
        private string _statusMessage = "Ready";

        private bool _showFirstNameColumn = true;
        private bool _showLastNameColumn = true;
        private bool _showEmailColumn = true;
        private bool _showPhoneColumn = true;
        private bool _showLocationColumn = true;
        private bool _showGroupColumn = true;
        private bool _showDateColumn = true;
        private bool _showReminderSentColumn = true;

        public StudentsViewModel()
        {
            _csvAppointmentService = new CsvStudentService();
            Students = new ObservableCollection<StudentRecord>();

            LoadCsvCommand = new RelayCommand(LoadStudents);

            LoadStudents();
        }

        public ObservableCollection<StudentRecord> Students { get; }

        public string PreprodCsvPath
        {
            get => _preprodCsvPath;
            set
            {
                _preprodCsvPath = value;
                OnPropertyChanged();
            }
        }

        public string AhaCsvPath
        {
            get => _ahaCsvPath;
            set
            {
                _ahaCsvPath = value;
                OnPropertyChanged();
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        public bool ShowFirstNameColumn
        {
            get => _showFirstNameColumn;
            set
            {
                _showFirstNameColumn = value;
                OnPropertyChanged();
            }
        }

        public bool ShowLastNameColumn
        {
            get => _showLastNameColumn;
            set
            {
                _showLastNameColumn = value;
                OnPropertyChanged();
            }
        }

        public bool ShowEmailColumn
        {
            get => _showEmailColumn;
            set
            {
                _showEmailColumn = value;
                OnPropertyChanged();
            }
        }

        public bool ShowPhoneColumn
        {
            get => _showPhoneColumn;
            set
            {
                _showPhoneColumn = value;
                OnPropertyChanged();
            }
        }

        public bool ShowLocationColumn
        {
            get => _showLocationColumn;
            set
            {
                _showLocationColumn = value;
                OnPropertyChanged();
            }
        }

        public bool ShowGroupColumn
        {
            get => _showGroupColumn;
            set
            {
                _showGroupColumn = value;
                OnPropertyChanged();
            }
        }

        public bool ShowDateColumn
        {
            get => _showDateColumn;
            set
            {
                _showDateColumn = value;
                OnPropertyChanged();
            }
        }

        public bool ShowReminderSentColumn
        {
            get => _showReminderSentColumn;
            set
            {
                _showReminderSentColumn = value;
                OnPropertyChanged();
            }
        }

        public ICommand LoadCsvCommand { get; }

        private void LoadStudents()
        {
            Students.Clear();

            string preprodFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PreprodCsvPath);
            string ahaFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, AhaCsvPath);

            if (!File.Exists(preprodFullPath))
            {
                StatusMessage = $"File not found: {preprodFullPath}";
                return;
            }

            if (!File.Exists(ahaFullPath))
            {
                StatusMessage = $"File not found: {ahaFullPath}";
                return;
            }

            var records = _csvAppointmentService.LoadStudents(preprodFullPath, ahaFullPath);

            foreach (var record in records)
            {
                Students.Add(record);
            }

            StatusMessage = $"{Students.Count} merged CSV files loaded";
        }
    }
}