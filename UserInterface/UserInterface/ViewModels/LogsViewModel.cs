using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using UserInterface.Models;
using UserInterface.Services;

namespace UserInterface.ViewModels
{
    public class LogsViewModel : BaseViewModel
    {
        private readonly LogService _logService;
        private string _searchText = string.Empty;
        private string _selectedSource = "All";
        private string _selectedLevel = "All";
        private bool _isRefreshingFilterOptions;

        public LogsViewModel(LogService logService)
        {
            _logService = logService;
            LogEntriesView = CollectionViewSource.GetDefaultView(_logService.Entries);
            LogEntriesView.Filter = FilterLog;

            SourceOptions = new ObservableCollection<string> { "All" };
            LevelOptions = new ObservableCollection<string> { "All", "Info", "Warning", "Error", "Login Required" };

            _logService.Entries.CollectionChanged += Entries_CollectionChanged;

            ClearLogsCommand = new RelayCommand(ClearLogs);
            OpenLogFolderCommand = new RelayCommand(_logService.OpenLogFolder);
            RefreshFilterOptions();
        }

        public ObservableCollection<LogEntry> LogEntries => _logService.Entries;
        public ICollectionView LogEntriesView { get; }
        public ObservableCollection<string> SourceOptions { get; }
        public ObservableCollection<string> LevelOptions { get; }
        public string LogFolderPath => _logService.LogFolderPath;

        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value ?? string.Empty;
                OnPropertyChanged();
                LogEntriesView.Refresh();
            }
        }

        public string SelectedSource
        {
            get => _selectedSource;
            set
            {
                if (_isRefreshingFilterOptions)
                    return;

                string next = string.IsNullOrWhiteSpace(value) ? "All" : value;
                if (_selectedSource == next)
                    return;

                _selectedSource = next;
                OnPropertyChanged();
                LogEntriesView.Refresh();
            }
        }

        public string SelectedLevel
        {
            get => _selectedLevel;
            set
            {
                if (_isRefreshingFilterOptions)
                    return;

                string next = string.IsNullOrWhiteSpace(value) ? "All" : value;
                if (_selectedLevel == next)
                    return;

                _selectedLevel = next;
                OnPropertyChanged();
                LogEntriesView.Refresh();
            }
        }

        public ICommand ClearLogsCommand { get; }
        public ICommand OpenLogFolderCommand { get; }

        private void ClearLogs()
        {
            _logService.ClearVisible();
        }

        private void Entries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshFilterOptions();
            LogEntriesView.Refresh();
        }

        private bool FilterLog(object obj)
        {
            if (obj is not LogEntry entry)
                return false;

            if (SelectedSource != "All" && !string.Equals(entry.Source, SelectedSource, StringComparison.OrdinalIgnoreCase))
                return false;

            if (SelectedLevel != "All" && !string.Equals(entry.Level, SelectedLevel, StringComparison.OrdinalIgnoreCase))
                return false;

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                string search = SearchText.Trim();
                return (entry.Message?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                       (entry.Source?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0 ||
                       (entry.Level?.IndexOf(search, StringComparison.OrdinalIgnoreCase) ?? -1) >= 0;
            }

            return true;
        }

        private void RefreshFilterOptions()
        {
            var selectedSource = _selectedSource;
            var selectedLevel = _selectedLevel;
            var sources = _logService.Entries.Select(x => x.Source).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x).ToList();

            _isRefreshingFilterOptions = true;
            try
            {
                SourceOptions.Clear();
                SourceOptions.Add("All");
                foreach (var source in sources)
                    SourceOptions.Add(source);

                if (!SourceOptions.Contains(selectedSource) && selectedSource != "All")
                    SourceOptions.Add(selectedSource);

                _selectedSource = selectedSource;
                _selectedLevel = selectedLevel;
                OnPropertyChanged(nameof(SelectedSource));
                OnPropertyChanged(nameof(SelectedLevel));
            }
            finally
            {
                _isRefreshingFilterOptions = false;
            }
        }
    }
}
