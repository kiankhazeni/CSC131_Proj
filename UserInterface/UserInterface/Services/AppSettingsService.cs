using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using UserInterface.Models;

namespace UserInterface.Services
{
    public class AppSettingsService
    {
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        public string SettingsFolderPath { get; }
        public string SettingsFilePath { get; }
        public string UserDefaultsFilePath { get; }

        public AppSettingsService()
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            SettingsFolderPath = Path.Combine(localAppData, "VitalsAutomation");

            string oldSettingsFolderPath = Path.Combine(localAppData, "VitalsEmailAutomation");

            if (!Directory.Exists(SettingsFolderPath) && Directory.Exists(oldSettingsFolderPath))
            {
                try
                {
                    Directory.Move(oldSettingsFolderPath, SettingsFolderPath);
                }
                catch
                {
                    Directory.CreateDirectory(SettingsFolderPath);
                }
            }

            SettingsFilePath = Path.Combine(SettingsFolderPath, "settings.json");
            UserDefaultsFilePath = Path.Combine(SettingsFolderPath, "default-settings.json");
        }

        public AppSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    AppSettings defaults = LoadResetDefaults();
                    Save(defaults);
                    return defaults;
                }

                string json = File.ReadAllText(SettingsFilePath);
                AppSettings settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions)
                                       ?? LoadResetDefaults();

                EnsureValidSettings(settings);
                Save(settings);

                return settings;
            }
            catch
            {
                return LoadResetDefaults();
            }
        }

        public void Save(AppSettings settings)
        {
            EnsureValidSettings(settings);

            Directory.CreateDirectory(SettingsFolderPath);

            string json = JsonSerializer.Serialize(settings, _jsonOptions);
            File.WriteAllText(SettingsFilePath, json);
        }

        public void SaveCurrentAsResetDefaults(AppSettings settings)
        {
            EnsureValidSettings(settings);

            Directory.CreateDirectory(SettingsFolderPath);

            AppSettings copy = Clone(settings);
            string json = JsonSerializer.Serialize(copy, _jsonOptions);
            File.WriteAllText(UserDefaultsFilePath, json);
        }

        public void ReplaceWithDefaults(AppSettings settings)
        {
            AppSettings defaults = LoadResetDefaults();

            settings.SettingsVersion = defaults.SettingsVersion;
            settings.RqiCsvPath = defaults.RqiCsvPath;
            settings.AhaCsvPath = defaults.AhaCsvPath;
            settings.RetainVisibleLogsBetweenSessions = defaults.RetainVisibleLogsBetweenSessions;
            settings.VisibleLogRetentionHours = defaults.VisibleLogRetentionHours;

            settings.AutomationModules.Clear();
            foreach (AutomationModule module in defaults.AutomationModules)
            {
                settings.AutomationModules.Add(module);
            }

            settings.StudentColumns.Clear();
            foreach (StudentColumnSetting column in defaults.StudentColumns)
            {
                settings.StudentColumns.Add(column);
            }

            Save(settings);
        }

        public void ResetStudentColumnsToBuiltInDefaults(AppSettings settings)
        {
            settings.StudentColumns.Clear();

            foreach (StudentColumnSetting column in AppSettings.CreateDefaultStudentColumns())
            {
                settings.StudentColumns.Add(column);
            }

            Save(settings);
        }

        private AppSettings LoadResetDefaults()
        {
            try
            {
                if (File.Exists(UserDefaultsFilePath))
                {
                    string json = File.ReadAllText(UserDefaultsFilePath);
                    AppSettings? defaults = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);

                    if (defaults != null)
                    {
                        EnsureValidSettings(defaults);
                        return defaults;
                    }
                }
            }
            catch
            {
                // Fall back to built-in defaults below.
            }

            return AppSettings.CreateDefault();
        }

        private AppSettings Clone(AppSettings settings)
        {
            string json = JsonSerializer.Serialize(settings, _jsonOptions);
            AppSettings copy = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions)
                               ?? AppSettings.CreateDefault();

            EnsureValidSettings(copy);

            return copy;
        }

        private static void EnsureValidSettings(AppSettings settings)
        {
            settings.SettingsVersion = AppSettings.CurrentSettingsVersion;

            if (string.IsNullOrWhiteSpace(settings.RqiCsvPath))
            {
                settings.RqiCsvPath = @"resources\preprod_cl.csv";
            }

            if (string.IsNullOrWhiteSpace(settings.AhaCsvPath))
            {
                settings.AhaCsvPath = @"resources\aha.csv";
            }

            if (settings.VisibleLogRetentionHours <= 0)
            {
                settings.VisibleLogRetentionHours = 24;
            }

            settings.AutomationModules ??= new ObservableCollection<AutomationModule>();
            settings.StudentColumns ??= new ObservableCollection<StudentColumnSetting>();

            NormalizeOldAutomationModuleNames(settings.AutomationModules);
            RemoveEmptyAutomationModules(settings.AutomationModules);
            MergeMissingAutomationModules(settings.AutomationModules);

            RemoveBadStudentColumns(settings.StudentColumns);
            MergeMissingStudentColumns(settings.StudentColumns);
        }

        private static void NormalizeOldAutomationModuleNames(ObservableCollection<AutomationModule> modules)
        {
            ApplyDefaultModuleForAliases(modules, "RQI Uploader & Email Scraper", new[]
            {
                "RQI and Email Scraper Module",
                "RQI Email Utility",
                "RQI Email Import",
                "RQI / Email Utility",
                "Email Parser"
            });

            ApplyDefaultModuleForAliases(modules, "AHA Automation", new[]
            {
                "AHA Student Scraper",
                "AHA Atlas Automation",
                "Student Scraper"
            });

            ApplyDefaultModuleForAliases(modules, "Outlook Event Creator", new[]
            {
                "Enrollment Calendar Watcher",
                "Calendar Event Creator",
                "Enrollment Watcher"
            });

            ApplyDefaultModuleForAliases(modules, "Reminders", new[]
            {
                "Registration Reminder",
                "Reminder Module",
                "Payment Reminder"
            });
        }

        private static void ApplyDefaultModuleForAliases(
            ObservableCollection<AutomationModule> modules,
            string defaultModuleName,
            string[] aliases)
        {
            AutomationModule? defaultModule = AppSettings.CreateDefaultModules()
                .FirstOrDefault(x => x.Name == defaultModuleName);

            if (defaultModule == null)
            {
                return;
            }

            var matches = modules
                .Where(x => x != null && aliases.Any(alias => string.Equals(x.Name, alias, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (matches.Count == 0)
            {
                return;
            }

            AutomationModule target = matches[0];

            target.Name = defaultModule.Name;
            target.WorkingDirectory = defaultModule.WorkingDirectory;
            target.Command = defaultModule.Command;
            target.Arguments = defaultModule.Arguments;
            target.IsEnabled = defaultModule.IsEnabled;

            for (int i = 1; i < matches.Count; i++)
            {
                modules.Remove(matches[i]);
            }
        }

        private static void RemoveEmptyAutomationModules(ObservableCollection<AutomationModule> modules)
        {
            for (int i = modules.Count - 1; i >= 0; i--)
            {
                AutomationModule module = modules[i];

                if (module == null ||
                    string.IsNullOrWhiteSpace(module.Name) &&
                    string.IsNullOrWhiteSpace(module.WorkingDirectory) &&
                    string.IsNullOrWhiteSpace(module.Command) &&
                    string.IsNullOrWhiteSpace(module.Arguments))
                {
                    modules.RemoveAt(i);
                }
            }
        }

        private static void MergeMissingAutomationModules(ObservableCollection<AutomationModule> modules)
        {
            foreach (AutomationModule defaultModule in AppSettings.CreateDefaultModules())
            {
                AutomationModule? existing = modules.FirstOrDefault(x => x.Name == defaultModule.Name);

                if (existing == null)
                {
                    modules.Add(defaultModule);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(existing.WorkingDirectory))
                {
                    existing.WorkingDirectory = defaultModule.WorkingDirectory;
                }

                if (string.IsNullOrWhiteSpace(existing.Command))
                {
                    existing.Command = defaultModule.Command;
                }

                if (string.IsNullOrWhiteSpace(existing.Arguments))
                {
                    existing.Arguments = defaultModule.Arguments;
                }
            }
        }

        private static void RemoveBadStudentColumns(ObservableCollection<StudentColumnSetting> columns)
        {
            for (int i = columns.Count - 1; i >= 0; i--)
            {
                StudentColumnSetting column = columns[i];

                if (column == null || string.IsNullOrWhiteSpace(column.Key))
                {
                    columns.RemoveAt(i);
                }
            }
        }

        private static void MergeMissingStudentColumns(ObservableCollection<StudentColumnSetting> columns)
        {
            ObservableCollection<StudentColumnSetting> defaults = AppSettings.CreateDefaultStudentColumns();

            foreach (StudentColumnSetting defaultColumn in defaults)
            {
                if (columns.Any(x => x.Key == defaultColumn.Key))
                {
                    continue;
                }

                columns.Add(defaultColumn);
            }

            int index = 0;

            foreach (StudentColumnSetting column in columns.OrderBy(x => x.DisplayIndex).ToList())
            {
                if (column.DisplayIndex < 0 || columns.Count(x => x.DisplayIndex == column.DisplayIndex) > 1)
                {
                    column.DisplayIndex = index;
                }

                if (string.IsNullOrWhiteSpace(column.Header))
                {
                    column.Header = defaults.FirstOrDefault(x => x.Key == column.Key)?.Header ?? column.Key;
                }

                if (column.Width <= 0)
                {
                    column.Width = defaults.FirstOrDefault(x => x.Key == column.Key)?.Width ?? 120;
                }

                index++;
            }
        }
    }
}