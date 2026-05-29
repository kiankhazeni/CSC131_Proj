using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;
using UserInterface.Models;
using UserInterface.Services;

namespace UserInterface.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private readonly AppSettings _settings;
        private readonly AppSettingsService _settingsService;
        private readonly AppPropertiesService _appPropertiesService;
        private readonly LogService _logService;

        private string _settingsStatusMessage = string.Empty;
        private string _moduleConfigStatusMessage = string.Empty;
        private string _authStatusMessage = string.Empty;
        private string _selectedConfigCategory = string.Empty;

        private bool _suppressExternalReload;
        private bool _suppressUiSettingStatus;
        private bool _suppressConfigItemSync;

        private AuthCacheItem? _selectedAuthCacheItem;

        public SettingsViewModel(AppSettings settings, AppSettingsService settingsService, LogService logService)
        {
            _settings = settings;
            _settingsService = settingsService;
            _logService = logService;
            _appPropertiesService = new AppPropertiesService();

            ConfigItems = _appPropertiesService.LoadItems();
            SubscribeToConfigItemChanges(ConfigItems);

            ConfigCategories = new ObservableCollection<string>();
            RefreshConfigCategories();

            ConfigItemsView = CollectionViewSource.GetDefaultView(ConfigItems);
            ConfigItemsView.SortDescriptions.Clear();
            ConfigItemsView.SortDescriptions.Add(new SortDescription(nameof(AppPropertyItem.DisplayOrder), ListSortDirection.Ascending));
            ConfigItemsView.GroupDescriptions.Clear();
            ConfigItemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(AppPropertyItem.Section)));
            ConfigItemsView.Filter = FilterConfigItem;

            AuthCacheItems = new ObservableCollection<AuthCacheItem>();
            RefreshAuthCacheItems();
            SelectedAuthCacheItem = AuthCacheItems.FirstOrDefault();

            SettingsStatusMessage = "No changes made";
            ModuleConfigStatusMessage = "No changes made";
            AuthStatusMessage = "No changes made";

            SaveSettingsCommand = new RelayCommand(SaveSettings);
            ResetSettingsCommand = new RelayCommand(ResetSettings);
            SaveCurrentAsDefaultCommand = new RelayCommand(SaveCurrentAsDefault);
            SaveAppPropertiesCommand = new RelayCommand(SaveAppProperties);
            ReloadAppPropertiesCommand = new RelayCommand(ReloadAppProperties);
            RestoreDefaultAppPropertiesCommand = new RelayCommand(RestoreDefaultAppProperties);
            ToggleConfigBooleanCommand = new RelayCommand<AppPropertyItem>(ToggleConfigBoolean);
            ClearSelectedAuthCacheCommand = new RelayCommand(ClearSelectedAuthCache);
            ClearAllAuthCachesCommand = new RelayCommand(ClearAllAuthCaches);

            ToggleSecretVisibilityCommand = new RelayCommand<AppPropertyItem>(item =>
            {
                if (item == null)
                    return;

                item.IsSecretVisible = !item.IsSecretVisible;
            });

            SubscribeToModuleChanges();
            AutomationModules.CollectionChanged += AutomationModules_CollectionChanged;
            ConfigItems.CollectionChanged += ConfigItems_CollectionChanged;
            AppPropertiesService.PropertiesChanged += AppPropertiesService_PropertiesChanged;
        }

        public bool RetainVisibleLogsBetweenSessions
        {
            get => _settings.RetainVisibleLogsBetweenSessions;
            set
            {
                if (_settings.RetainVisibleLogsBetweenSessions == value)
                    return;

                _settings.RetainVisibleLogsBetweenSessions = value;
                OnPropertyChanged();
                SaveUiSettingChange("Log display setting updated");
            }
        }

        public int VisibleLogRetentionHours
        {
            get => _settings.VisibleLogRetentionHours;
            set
            {
                if (_settings.VisibleLogRetentionHours == value)
                    return;

                _settings.VisibleLogRetentionHours = value;
                OnPropertyChanged();
                SaveUiSettingChange("Log retention setting updated");
            }
        }

        public ObservableCollection<AutomationModule> AutomationModules => _settings.AutomationModules;
        public ObservableCollection<AppPropertyItem> ConfigItems { get; }
        public ObservableCollection<string> ConfigCategories { get; }
        public ICollectionView ConfigItemsView { get; }
        public ObservableCollection<AuthCacheItem> AuthCacheItems { get; }

        public AuthCacheItem? SelectedAuthCacheItem
        {
            get => _selectedAuthCacheItem;
            set
            {
                _selectedAuthCacheItem = value;
                OnPropertyChanged();
            }
        }

        public string SelectedConfigCategory
        {
            get => _selectedConfigCategory;
            set
            {
                string newValue = value ?? string.Empty;

                if (_selectedConfigCategory == newValue)
                    return;

                _selectedConfigCategory = newValue;
                OnPropertyChanged();

                ConfigItemsView?.Refresh();
            }
        }

        public string SettingsStatusMessage
        {
            get => _settingsStatusMessage;
            set
            {
                _settingsStatusMessage = value;
                OnPropertyChanged();
            }
        }

        public string ModuleConfigStatusMessage
        {
            get => _moduleConfigStatusMessage;
            set
            {
                _moduleConfigStatusMessage = value;
                OnPropertyChanged();
            }
        }

        public string AuthStatusMessage
        {
            get => _authStatusMessage;
            set
            {
                _authStatusMessage = value;
                OnPropertyChanged();
            }
        }

        public string AppPropertiesPath => _appPropertiesService.AppPropertiesPath;
        public string DefaultAppPropertiesPath => _appPropertiesService.DefaultAppPropertiesPath;
        public string SettingsFilePath => _settingsService.SettingsFilePath;

        public ICommand SaveSettingsCommand { get; }
        public ICommand ResetSettingsCommand { get; }
        public ICommand SaveCurrentAsDefaultCommand { get; }
        public ICommand SaveAppPropertiesCommand { get; }
        public ICommand ReloadAppPropertiesCommand { get; }
        public ICommand RestoreDefaultAppPropertiesCommand { get; }
        public ICommand ToggleConfigBooleanCommand { get; }
        public ICommand ClearSelectedAuthCacheCommand { get; }
        public ICommand ClearAllAuthCachesCommand { get; }
        public RelayCommand<AppPropertyItem> ToggleSecretVisibilityCommand { get; }

        private void ToggleConfigBoolean(AppPropertyItem? item)
        {
            if (item == null || !item.IsBoolean)
                return;

            item.BooleanValue = !item.BooleanValue;
            ModuleConfigStatusMessage = item.Label + " set to " + (item.BooleanValue ? "Enabled / True" : "Disabled / False");
        }

        private bool FilterConfigItem(object obj)
        {
            if (obj is not AppPropertyItem item)
                return false;

            if (string.IsNullOrWhiteSpace(SelectedConfigCategory))
                return true;

            return string.Equals(item.Category, SelectedConfigCategory, StringComparison.OrdinalIgnoreCase);
        }

        private void ConfigItems_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (AppPropertyItem item in e.OldItems)
                    item.PropertyChanged -= ConfigItem_PropertyChanged;
            }

            if (e.NewItems != null)
            {
                foreach (AppPropertyItem item in e.NewItems)
                    item.PropertyChanged += ConfigItem_PropertyChanged;
            }
        }

        private void SubscribeToConfigItemChanges(ObservableCollection<AppPropertyItem> items)
        {
            foreach (AppPropertyItem item in items)
            {
                item.PropertyChanged -= ConfigItem_PropertyChanged;
                item.PropertyChanged += ConfigItem_PropertyChanged;
            }
        }

        private void ConfigItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressConfigItemSync)
                return;

            if (e.PropertyName != nameof(AppPropertyItem.Value))
                return;

            if (sender is not AppPropertyItem changedItem)
                return;

            SyncDuplicateConfigValues(changedItem);
            RefreshAuthCacheItems();
        }

        private void SyncDuplicateConfigValues(AppPropertyItem changedItem)
        {
            try
            {
                _suppressConfigItemSync = true;

                foreach (AppPropertyItem item in ConfigItems)
                {
                    if (ReferenceEquals(item, changedItem))
                        continue;

                    if (!string.Equals(item.Key, changedItem.Key, StringComparison.OrdinalIgnoreCase))
                        continue;

                    item.Value = changedItem.Value;
                }
            }
            finally
            {
                _suppressConfigItemSync = false;
            }
        }

        private void RefreshConfigCategories()
        {
            string previous = SelectedConfigCategory;

            ConfigCategories.Clear();

            foreach (string category in ConfigItems
                         .Select(x => x.Category)
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(CategorySortForUi))
            {
                ConfigCategories.Add(category);
            }

            if (ConfigCategories.Count == 0)
            {
                SelectedConfigCategory = string.Empty;
                return;
            }

            SelectedConfigCategory = ConfigCategories.Any(x => string.Equals(x, previous, StringComparison.OrdinalIgnoreCase))
                ? previous
                : ConfigCategories[0];
        }

        private static int CategorySortForUi(string category)
        {
            string[] order =
            {
                "Application",
                "RQI Uploader & Email Scraper",
                "AHA Automation",
                "Outlook Event Creator",
                "Reminders",
                "Other"
            };

            int index = Array.FindIndex(
                order,
                x => string.Equals(x, category, StringComparison.OrdinalIgnoreCase)
            );

            return index < 0 ? 999 : index;
        }

        private void SaveSettings()
        {
            _settingsService.Save(_settings);
            SettingsStatusMessage = "Saved UI settings at " + DateTime.Now.ToString("hh:mm:ss tt");
            _logService.Add("Settings", "UI settings saved");
        }

        private void ResetSettings()
        {
            _suppressUiSettingStatus = true;
            try
            {
                _settingsService.ReplaceWithDefaults(_settings);
            }
            finally
            {
                _suppressUiSettingStatus = false;
            }

            SubscribeToModuleChanges();
            NotifyAllSettingsChanged();
            _settingsService.Save(_settings);
            SettingsStatusMessage = "Restored UI defaults at " + DateTime.Now.ToString("hh:mm:ss tt");
            _logService.Add("Settings", "UI settings reset to defaults", "Warning");
        }

        private void SaveCurrentAsDefault()
        {
            _settingsService.Save(_settings);
            _settingsService.SaveCurrentAsResetDefaults(_settings);
            SettingsStatusMessage = "Saved current UI defaults at " + DateTime.Now.ToString("hh:mm:ss tt");
            _logService.Add("Settings", "Current UI settings saved as reset defaults");
        }

        private void SaveAppProperties()
        {
            _suppressExternalReload = true;
            try
            {
                _appPropertiesService.SaveItems(ConfigItems);
            }
            finally
            {
                _suppressExternalReload = false;
            }

            RefreshAuthCacheItems();
            ModuleConfigStatusMessage = "Saved module config at " + DateTime.Now.ToString("hh:mm:ss tt");
            _logService.Add("Settings", "Module config saved to app.properties");
        }

        private void ReloadAppProperties()
        {
            ReplaceConfigItems(_appPropertiesService.LoadItems());
            RefreshAuthCacheItems();
            ModuleConfigStatusMessage = "Reset changes at " + DateTime.Now.ToString("hh:mm:ss tt");
            _logService.Add("Settings", "Module config reset");
        }

        private void RestoreDefaultAppProperties()
        {
            _appPropertiesService.RestoreDefault();
            ReloadAppProperties();
            ModuleConfigStatusMessage = "Restored defaults at " + DateTime.Now.ToString("hh:mm:ss tt");
            _logService.Add("Settings", "Module config restored from default-app.properties", "Warning");
        }

        private void ReplaceConfigItems(ObservableCollection<AppPropertyItem> newItems)
        {
            string selectedCategory = SelectedConfigCategory;

            ConfigItems.Clear();

            foreach (AppPropertyItem item in newItems)
            {
                ConfigItems.Add(item);
            }

            SubscribeToConfigItemChanges(ConfigItems);
            RefreshConfigCategories();

            if (ConfigCategories.Any(x => string.Equals(x, selectedCategory, StringComparison.OrdinalIgnoreCase)))
            {
                SelectedConfigCategory = selectedCategory;
            }

            ConfigItemsView.Refresh();
            OnPropertyChanged(nameof(ConfigItems));
        }

        private void ClearSelectedAuthCache()
        {
            if (SelectedAuthCacheItem == null)
            {
                AuthStatusMessage = "Select a sign-in cache to clear";
                return;
            }

            int deleted = DeleteAuthCacheFile(SelectedAuthCacheItem.FullPath);
            AuthStatusMessage = deleted > 0
                ? "Cleared " + SelectedAuthCacheItem.DisplayName + " at " + DateTime.Now.ToString("hh:mm:ss tt")
                : "No cache file found for " + SelectedAuthCacheItem.DisplayName;
            _logService.Add("Settings", AuthStatusMessage, deleted > 0 ? "Warning" : "Info");
        }

        private void ClearAllAuthCaches()
        {
            int deleted = 0;
            string cacheDirectory = PathResolver.ResolveDirectory("config/auth_cache");

            if (Directory.Exists(cacheDirectory))
            {
                foreach (var file in Directory.GetFiles(cacheDirectory))
                {
                    if (string.Equals(Path.GetFileName(file), "placeholder.txt", StringComparison.OrdinalIgnoreCase))
                        continue;

                    deleted += DeleteAuthCacheFile(file);
                }
            }

            AuthStatusMessage = "Cleared " + deleted + " sign-in cache file(s) at " + DateTime.Now.ToString("hh:mm:ss tt");
            _logService.Add("Settings", AuthStatusMessage, deleted > 0 ? "Warning" : "Info");
        }

        private static int DeleteAuthCacheFile(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    return 0;

                if (string.Equals(Path.GetFileName(filePath), "placeholder.txt", StringComparison.OrdinalIgnoreCase))
                    return 0;

                File.Delete(filePath);
                return 1;
            }
            catch
            {
                return 0;
            }
        }

        private void RefreshAuthCacheItems()
        {
            string? previousPath = SelectedAuthCacheItem?.RelativePath;
            AuthCacheItems.Clear();

            AddAuthCacheItem("RQI Uploader & Email Scraper", "outlook.tokenCacheFile");
            AddAuthCacheItem("Outlook Event Creator", "calendar.tokenCacheFile");
            AddAuthCacheItem("Reminders", "reminder.tokenCacheFile");

            SelectedAuthCacheItem = AuthCacheItems.FirstOrDefault(x => string.Equals(x.RelativePath, previousPath, StringComparison.OrdinalIgnoreCase))
                                    ?? AuthCacheItems.FirstOrDefault();
        }

        private void AddAuthCacheItem(string displayName, string configKey)
        {
            string relativePath = GetConfigValue(configKey);
            if (string.IsNullOrWhiteSpace(relativePath))
                return;

            if (AuthCacheItems.Any(x => string.Equals(x.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase)))
                return;

            AuthCacheItems.Add(new AuthCacheItem
            {
                DisplayName = displayName,
                ConfigKey = configKey,
                RelativePath = relativePath,
                FullPath = PathResolver.ResolveFile(relativePath)
            });
        }

        private string GetConfigValue(string key)
        {
            var item = ConfigItems.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            return item?.Value ?? _appPropertiesService.GetValue(key);
        }

        private void AppPropertiesService_PropertiesChanged()
        {
            if (_suppressExternalReload)
                return;

            ReplaceConfigItems(_appPropertiesService.LoadItems());
            RefreshAuthCacheItems();
            ModuleConfigStatusMessage = "Module config reloaded at " + DateTime.Now.ToString("hh:mm:ss tt");
        }

        private void AutomationModules_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (AutomationModule module in e.OldItems)
                    module.PropertyChanged -= AutomationModule_PropertyChanged;
            }

            if (e.NewItems != null)
            {
                foreach (AutomationModule module in e.NewItems)
                    module.PropertyChanged += AutomationModule_PropertyChanged;
            }
        }

        private void SubscribeToModuleChanges()
        {
            foreach (var module in AutomationModules)
            {
                module.PropertyChanged -= AutomationModule_PropertyChanged;
                module.PropertyChanged += AutomationModule_PropertyChanged;
            }
        }

        private void AutomationModule_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressUiSettingStatus)
                return;

            if (e.PropertyName == nameof(AutomationModule.IsEnabled) && sender is AutomationModule module)
            {
                _settingsService.Save(_settings);
                SettingsStatusMessage = module.Name + (module.IsEnabled ? " enabled at " : " disabled at ") + DateTime.Now.ToString("hh:mm:ss tt");
                _logService.Add("Settings", module.Name + (module.IsEnabled ? " enabled" : " disabled"));
                return;
            }

            if (e.PropertyName == nameof(AutomationModule.Name) ||
                e.PropertyName == nameof(AutomationModule.Command) ||
                e.PropertyName == nameof(AutomationModule.Arguments))
            {
                _settingsService.Save(_settings);
            }
        }

        private void SaveUiSettingChange(string message)
        {
            if (_suppressUiSettingStatus)
                return;

            _settingsService.Save(_settings);
            SettingsStatusMessage = message + " at " + DateTime.Now.ToString("hh:mm:ss tt");
        }

        private void NotifyAllSettingsChanged()
        {
            OnPropertyChanged(nameof(RetainVisibleLogsBetweenSessions));
            OnPropertyChanged(nameof(VisibleLogRetentionHours));
            OnPropertyChanged(nameof(AutomationModules));
        }
    }
}