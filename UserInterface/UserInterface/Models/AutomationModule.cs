using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace UserInterface.Models
{
    public class AutomationModule : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _workingDirectory = string.Empty;
        private string _command = string.Empty;
        private string _arguments = string.Empty;
        private bool _isEnabled;
        private bool _isRunning;
        private string _status = "Stopped";
        private int? _lastExitCode;
        private string _lastRunText = "Not run";
        private string _nextRunText = "—";

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name
        {
            get => _name;
            set => SetField(ref _name, value ?? string.Empty);
        }

        public string WorkingDirectory
        {
            get => _workingDirectory;
            set => SetField(ref _workingDirectory, value ?? string.Empty);
        }

        public string Command
        {
            get => _command;
            set => SetField(ref _command, value ?? string.Empty);
        }

        public string Arguments
        {
            get => _arguments;
            set => SetField(ref _arguments, value ?? string.Empty);
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetField(ref _isEnabled, value);
        }

        [JsonIgnore]
        public bool IsRunning
        {
            get => _isRunning;
            set => SetField(ref _isRunning, value);
        }

        [JsonIgnore]
        public string Status
        {
            get => _status;
            set => SetField(ref _status, value ?? string.Empty);
        }

        [JsonIgnore]
        public int? LastExitCode
        {
            get => _lastExitCode;
            set => SetField(ref _lastExitCode, value);
        }

        [JsonIgnore]
        public string LastRunText
        {
            get => _lastRunText;
            set => SetField(ref _lastRunText, value ?? string.Empty);
        }

        [JsonIgnore]
        public string NextRunText
        {
            get => _nextRunText;
            set => SetField(ref _nextRunText, value ?? string.Empty);
        }

        private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
