using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UserInterface.Models;
using UserInterface.Services;
using FormsDialogResult = System.Windows.Forms.DialogResult;
using FormsFolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;

namespace UserInterface.ViewModels
{
    public class StudentsViewModel : BaseViewModel, IDisposable
    {
        private readonly CsvStudentService _csvAppointmentService;
        private readonly AppSettings _settings;
        private readonly AppSettingsService _settingsService;
        private readonly LogService _logService;
        private readonly AppPropertiesService _appPropertiesService;
        private FileSystemWatcher? _preprodWatcher;
        private FileSystemWatcher? _ahaWatcher;
        private DateTime _lastAutoReload = DateTime.MinValue;
        private bool _disposed;

        private string _statusMessage = "Ready";
        private string _lastLoadTimeText = "Not loaded";
        private bool _hasLoadedOnce;

        private bool _suppressAppPropertiesReload;

        public StudentsViewModel(AppSettings settings, AppSettingsService settingsService, LogService logService)
        {
            _settings = settings;
            _settingsService = settingsService;
            _logService = logService;
            _appPropertiesService = new AppPropertiesService();
            _csvAppointmentService = new CsvStudentService();
            Students = new ObservableCollection<StudentRecord>();

            LoadCsvCommand = new RelayCommand(() => LoadStudents(logResult: true));
            ExportCsvCommand = new RelayCommand(ExportSourceCsvs);
            RestoreDefaultCsvPathsCommand = new RelayCommand(RestoreDefaultCsvPaths);
            BrowsePreprodCsvCommand = new RelayCommand(BrowsePreprodCsv);
            BrowseAhaCsvCommand = new RelayCommand(BrowseAhaCsv);

            LoadStudents(logResult: false);
            ConfigureWatchers();

            AppPropertiesService.PropertiesChanged += AppPropertiesService_PropertiesChanged;
        }

        public ObservableCollection<StudentRecord> Students { get; }
        public ObservableCollection<StudentColumnSetting> StudentColumns => _settings.StudentColumns;

        public string PreprodCsvPath
        {
            get => _settings.RqiCsvPath;
            set
            {
                if (_settings.RqiCsvPath == (value ?? string.Empty))
                    return;

                _settings.RqiCsvPath = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        public string AhaCsvPath
        {
            get => _settings.AhaCsvPath;
            set
            {
                if (_settings.AhaCsvPath == (value ?? string.Empty))
                    return;

                _settings.AhaCsvPath = value ?? string.Empty;
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

        public string LastLoadTimeText
        {
            get => _lastLoadTimeText;
            private set
            {
                _lastLoadTimeText = value;
                OnPropertyChanged();
            }
        }

        public ICommand LoadCsvCommand { get; }
        public ICommand ExportCsvCommand { get; }
        public ICommand RestoreDefaultCsvPathsCommand { get; }
        public ICommand BrowsePreprodCsvCommand { get; }
        public ICommand BrowseAhaCsvCommand { get; }

        public void SaveColumnSettings()
        {
            _settingsService.Save(_settings);
        }

        public void ResetColumnSettings()
        {
            _settingsService.ResetStudentColumnsToBuiltInDefaults(_settings);
            OnPropertyChanged(nameof(StudentColumns));
            StatusMessage = "Student columns reset to defaults.";
            _logService.Add("Student Records", StatusMessage);
        }

        private void BrowsePreprodCsv()
        {
            BrowseCsvFile("Select RQI CSV", path => PreprodCsvPath = path);
        }

        private void BrowseAhaCsv()
        {
            BrowseCsvFile("Select AHA CSV", path => AhaCsvPath = path);
        }

        private void BrowseCsvFile(string title, Action<string> setPath)
        {
            string resourcesDirectory = PathResolver.ResolveDirectory("resources");
            if (!Directory.Exists(resourcesDirectory))
                Directory.CreateDirectory(resourcesDirectory);

            var dialog = new OpenFileDialog
            {
                Title = title,
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false,
                InitialDirectory = resourcesDirectory
            };

            if (dialog.ShowDialog() == true)
            {
                if (!TryNormalizeAllowedCsvPath(dialog.FileName, out string relativePath))
                    return;

                setPath(relativePath);
                SaveCsvPathSettings();
                ConfigureWatchers();
                StatusMessage = "CSV path updated. app.properties was updated too.";
                LoadStudents(logResult: true);
            }
        }

        private void SaveCsvPathSettings()
        {
            _settingsService.Save(_settings);

            _suppressAppPropertiesReload = true;
            try
            {
                _appPropertiesService.SetValue("file.rqiCsv", ToAppRelativePath(PreprodCsvPath));
                _appPropertiesService.SetValue("file.ahaCsv", ToAppRelativePath(AhaCsvPath));
            }
            finally
            {
                _suppressAppPropertiesReload = false;
            }
        }

        private void AppPropertiesService_PropertiesChanged()
        {
            if (_suppressAppPropertiesReload || _disposed)
            {
                return;
            }

            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_disposed)
                {
                    return;
                }

                string rqiCsv = _appPropertiesService.GetValue("file.rqiCsv", PreprodCsvPath);
                string ahaCsv = _appPropertiesService.GetValue("file.ahaCsv", AhaCsvPath);

                bool changed = false;

                if (!string.IsNullOrWhiteSpace(rqiCsv) &&
                    !string.Equals(PreprodCsvPath, rqiCsv, StringComparison.OrdinalIgnoreCase))
                {
                    PreprodCsvPath = rqiCsv;
                    changed = true;
                }

                if (!string.IsNullOrWhiteSpace(ahaCsv) &&
                    !string.Equals(AhaCsvPath, ahaCsv, StringComparison.OrdinalIgnoreCase))
                {
                    AhaCsvPath = ahaCsv;
                    changed = true;
                }

                if (!changed)
                {
                    return;
                }

                _settingsService.Save(_settings);
                ConfigureWatchers();
                LoadStudents(logResult: true);

                StatusMessage = "CSV paths updated from module config";
            }));
        }

        private void LoadStudents(bool logResult)
        {
            if (!ValidateCurrentCsvPaths())
                return;

            SaveCsvPathSettings();
            ConfigureWatchers();
            Students.Clear();

            string preprodFullPath = ResolveCsvPath(PreprodCsvPath);
            string ahaFullPath = ResolveCsvPath(AhaCsvPath);

            if (!File.Exists(preprodFullPath))
            {
                StatusMessage = $"File not found: {preprodFullPath}";
                if (logResult)
                    _logService.Add("Student Records", StatusMessage, "Error");
                return;
            }

            if (!File.Exists(ahaFullPath))
            {
                StatusMessage = $"File not found: {ahaFullPath}";
                if (logResult)
                    _logService.Add("Student Records", StatusMessage, "Error");
                return;
            }

            var records = _csvAppointmentService.LoadStudents(preprodFullPath, ahaFullPath);

            foreach (var record in records)
                Students.Add(record);

            LastLoadTimeText = DateTime.Now.ToString("hh:mm:ss tt");
            StatusMessage = $"{Students.Count} merged student records found";
            _settingsService.Save(_settings);

            if (logResult || _hasLoadedOnce)
                _logService.Add("Student Records", StatusMessage);

            _hasLoadedOnce = true;
        }


        private void RestoreDefaultCsvPaths()
        {
            PreprodCsvPath = @"resources\preprod_cl.csv";
            AhaCsvPath = @"resources\aha.csv";
            SaveCsvPathSettings();
            ConfigureWatchers();
            StatusMessage = "CSV paths restored to defaults";
            _logService.Add("Student Records", StatusMessage);
            LoadStudents(logResult: true);
        }

        private void ExportSourceCsvs()
        {
            using var dialog = new FormsFolderBrowserDialog
            {
                Description = "Select a folder for exported AHA and RQI CSV files",
                UseDescriptionForTitle = true
            };

            if (dialog.ShowDialog() != FormsDialogResult.OK || string.IsNullOrWhiteSpace(dialog.SelectedPath))
                return;

            try
            {
                string preprodFullPath = ResolveCsvPath(PreprodCsvPath);
                string ahaFullPath = ResolveCsvPath(AhaCsvPath);

                if (!File.Exists(preprodFullPath))
                    throw new FileNotFoundException("RQI CSV was not found.", preprodFullPath);
                if (!File.Exists(ahaFullPath))
                    throw new FileNotFoundException("AHA CSV was not found.", ahaFullPath);

                string preprodDest = Path.Combine(dialog.SelectedPath, Path.GetFileName(preprodFullPath));
                string ahaDest = Path.Combine(dialog.SelectedPath, Path.GetFileName(ahaFullPath));

                File.Copy(preprodFullPath, preprodDest, overwrite: true);
                File.Copy(ahaFullPath, ahaDest, overwrite: true);

                StatusMessage = "Exported AHA and RQI CSVs.";
                _logService.Add("Student Records", StatusMessage);
            }
            catch (Exception ex)
            {
                StatusMessage = "Export failed: " + ex.Message;
                _logService.Add("Student Records", StatusMessage, "Error");
            }
        }

        private void ConfigureWatchers()
        {
            DisposeWatcher(ref _preprodWatcher);
            DisposeWatcher(ref _ahaWatcher);

            _preprodWatcher = CreateWatcher(ResolveCsvPath(PreprodCsvPath));
            _ahaWatcher = CreateWatcher(ResolveCsvPath(AhaCsvPath));
        }

        private FileSystemWatcher? CreateWatcher(string filePath)
        {
            try
            {
                string? directory = Path.GetDirectoryName(filePath);
                string fileName = Path.GetFileName(filePath);

                if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName) || !Directory.Exists(directory))
                    return null;

                var watcher = new FileSystemWatcher(directory, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = true
                };

                watcher.Changed += (_, _) => ReloadFromWatcher();
                watcher.Created += (_, _) => ReloadFromWatcher();
                watcher.Renamed += (_, _) => ReloadFromWatcher();
                return watcher;
            }
            catch
            {
                return null;
            }
        }

        private void ReloadFromWatcher()
        {
            if ((DateTime.Now - _lastAutoReload).TotalMilliseconds < 700)
                return;

            _lastAutoReload = DateTime.Now;
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(new Action(() => LoadStudents(logResult: true)));
        }

        private static void DisposeWatcher(ref FileSystemWatcher? watcher)
        {
            if (watcher == null)
                return;

            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            watcher = null;
        }


        private bool ValidateCurrentCsvPaths()
        {
            if (!TryNormalizeAllowedCsvPath(PreprodCsvPath, out string normalizedPreprod))
                return false;

            if (!TryNormalizeAllowedCsvPath(AhaCsvPath, out string normalizedAha))
                return false;

            if (!string.Equals(PreprodCsvPath, normalizedPreprod, StringComparison.Ordinal))
                PreprodCsvPath = normalizedPreprod;

            if (!string.Equals(AhaCsvPath, normalizedAha, StringComparison.Ordinal))
                AhaCsvPath = normalizedAha;

            return true;
        }

        private bool TryNormalizeAllowedCsvPath(string path, out string normalizedPath)
        {
            normalizedPath = path ?? string.Empty;

            if (string.IsNullOrWhiteSpace(path))
            {
                MessageBox.Show(
                    "Please choose a CSV file inside this app folder, preferably in the resources folder.",
                    "Invalid CSV Path",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            try
            {
                string fullPath = Path.GetFullPath(Path.IsPathRooted(path)
                    ? path
                    : Path.Combine(GetAppRootDirectory(), path));

                string appRoot = GetAppRootDirectory();

                if (!IsInsideDirectory(fullPath, appRoot))
                {
                    MessageBox.Show(
                        "CSV files must stay inside the Vitals app folder so the Java modules can read and update them. Please choose a file in the resources folder.",
                        "CSV Path Outside App Folder",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return false;
                }

                normalizedPath = Path.GetRelativePath(appRoot, fullPath).Replace('/', '\\');

                if (!normalizedPath.StartsWith("resources" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                    !normalizedPath.StartsWith("resources" + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(
                        "This CSV is inside the app folder, but the recommended location is the resources folder. The path will still be saved.",
                        "CSV Path Notice",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not use that CSV path: " + ex.Message, "Invalid CSV Path", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private static bool IsInsideDirectory(string fullPath, string directory)
        {
            string normalizedPath = Path.GetFullPath(fullPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string normalizedDirectory = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return normalizedPath.StartsWith(normalizedDirectory, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetAppRootDirectory()
        {
            string resourcesDirectory = PathResolver.ResolveDirectory("resources");
            string? parent = Directory.GetParent(resourcesDirectory)?.FullName;
            return Path.GetFullPath(parent ?? AppDomain.CurrentDomain.BaseDirectory);
        }

        private static string ResolveCsvPath(string path)
        {
            if (Path.IsPathRooted(path))
                return path;

            string fromOutput = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
            if (File.Exists(fromOutput))
                return fromOutput;

            string? current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(current))
            {
                string candidate = Path.Combine(current, path);
                if (File.Exists(candidate))
                    return candidate;

                current = Directory.GetParent(current)?.FullName;
            }

            return fromOutput;
        }

        private static string ToAppRelativePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                string fullPath = Path.GetFullPath(path);
                string appBase = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory);

                if (fullPath.StartsWith(appBase, StringComparison.OrdinalIgnoreCase))
                    return Path.GetRelativePath(appBase, fullPath).Replace('\\', '/');
            }
            catch
            {
                // Use original
            }

            return path;
        }


        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            AppPropertiesService.PropertiesChanged -= AppPropertiesService_PropertiesChanged;

            DisposeWatcher(ref _preprodWatcher);
            DisposeWatcher(ref _ahaWatcher);
        }
    }
}
