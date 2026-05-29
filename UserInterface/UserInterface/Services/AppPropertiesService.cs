using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using UserInterface.Models;

namespace UserInterface.Services
{
    public class AppPropertiesService
    {
        public static event Action? PropertiesChanged;

        public string AppPropertiesPath { get; }
        public string DefaultAppPropertiesPath { get; }

        public AppPropertiesService()
        {
            AppPropertiesPath = PathResolver.ResolveFile("config/app.properties");
            DefaultAppPropertiesPath = PathResolver.ResolveFile("config/default-app.properties");
        }

        public string Load()
        {
            EnsureExists();
            return File.Exists(AppPropertiesPath) ? File.ReadAllText(AppPropertiesPath) : string.Empty;
        }

        public void Save(string text)
        {
            string? parent = Path.GetDirectoryName(AppPropertiesPath);

            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.WriteAllText(AppPropertiesPath, text ?? string.Empty);
            PropertiesChanged?.Invoke();
        }

        public string RestoreDefault()
        {
            if (!File.Exists(DefaultAppPropertiesPath))
            {
                throw new FileNotFoundException(
                    "Default app.properties file was not found.",
                    DefaultAppPropertiesPath
                );
            }

            string text = File.ReadAllText(DefaultAppPropertiesPath);
            Save(text);
            return text;
        }

        public ObservableCollection<AppPropertyItem> LoadItems()
        {
            Dictionary<string, string> values = LoadValues();
            var items = new ObservableCollection<AppPropertyItem>();

            foreach (DisplayInfo display in DisplayLayout.OrderBy(x => CategoryOrder(x.Category)).ThenBy(x => x.Order))
            {
                if (!values.TryGetValue(display.Key, out string? value))
                {
                    continue;
                }

                items.Add(CreateItem(display, value));
            }

            HashSet<string> knownKeys = new HashSet<string>(
                DisplayLayout.Select(x => x.Key),
                StringComparer.OrdinalIgnoreCase
            );

            foreach (var pair in values.OrderBy(x => x.Key))
            {
                if (knownKeys.Contains(pair.Key))
                {
                    continue;
                }

                DisplayInfo fallback = Info(
                    pair.Key,
                    "Other",
                    "General",
                    MakeFallbackLabel(pair.Key),
                    pair.Key,
                    9999
                );

                items.Add(CreateItem(fallback, pair.Value));
            }

            return items;
        }

        public void SaveItems(IEnumerable<AppPropertyItem> items)
        {
            Dictionary<string, string> keyToValue = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (AppPropertyItem item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Key))
                {
                    continue;
                }

                keyToValue[item.Key] = item.Value ?? string.Empty;
            }

            var lines = new List<string>();
            string currentCategory = string.Empty;
            string currentSection = string.Empty;

            foreach (string key in GetSaveOrder(keyToValue.Keys))
            {
                DisplayInfo display = FirstDisplayForKey(key);

                if (!string.Equals(currentCategory, display.Category, StringComparison.OrdinalIgnoreCase))
                {
                    currentCategory = display.Category;
                    currentSection = string.Empty;

                    if (lines.Count > 0)
                    {
                        lines.Add(string.Empty);
                    }

                    lines.Add("# ----- " + currentCategory + " -----");
                }

                if (!string.Equals(currentSection, display.Section, StringComparison.OrdinalIgnoreCase))
                {
                    currentSection = display.Section;

                    if (!string.IsNullOrWhiteSpace(currentSection))
                    {
                        lines.Add("# " + currentSection);
                    }
                }

                lines.Add(key + "=" + keyToValue[key]);
            }

            Save(string.Join(Environment.NewLine, lines) + Environment.NewLine);
        }

        public string GetValue(string key, string defaultValue = "")
        {
            Dictionary<string, string> values = LoadValues();

            return values.TryGetValue(key, out string? value)
                ? value
                : defaultValue;
        }

        public int GetInt(string key, int defaultValue)
        {
            return int.TryParse(GetValue(key), out int value) ? value : defaultValue;
        }

        public void SetValue(string key, string value)
        {
            string newValue = value ?? string.Empty;

            string text = Load();
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
            bool replaced = false;
            bool changed = false;

            for (int i = 0; i < lines.Count; i++)
            {
                string trimmed = lines[i].Trim();

                if (trimmed.StartsWith("#") || !trimmed.Contains('='))
                {
                    continue;
                }

                int index = trimmed.IndexOf('=');
                string currentKey = trimmed.Substring(0, index).Trim();

                if (!string.Equals(currentKey, key, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string currentValue = trimmed.Substring(index + 1).Trim();

                if (string.Equals(currentValue, newValue, StringComparison.Ordinal))
                {
                    return;
                }

                lines[i] = key + "=" + newValue;
                replaced = true;
                changed = true;
                break;
            }

            if (!replaced)
            {
                lines.Add(key + "=" + newValue);
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            Save(string.Join(Environment.NewLine, lines));
        }

        private Dictionary<string, string> LoadValues()
        {
            string text = Load();
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string rawLine in text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None))
            {
                string line = rawLine.Trim();

                if (line.Length == 0 || line.StartsWith("#") || !line.Contains('='))
                {
                    continue;
                }

                int index = line.IndexOf('=');
                string key = line.Substring(0, index).Trim();
                string value = line.Substring(index + 1).Trim();

                if (!string.IsNullOrWhiteSpace(key))
                {
                    values[key] = value;
                }
            }

            return values;
        }

        private static AppPropertyItem CreateItem(DisplayInfo display, string value)
        {
            return new AppPropertyItem
            {
                DisplayId = display.DisplayId,
                DisplayOrder = display.Order,
                Key = display.Key,
                Category = display.Category,
                Section = display.Section,
                Label = display.Label,
                Description = display.Description,
                Value = value,
                IsSensitive = IsSensitive(display.Key),
                IsBoolean = IsBooleanKey(display.Key, value)
            };
        }

        private void EnsureExists()
        {
            if (File.Exists(AppPropertiesPath))
            {
                return;
            }

            if (File.Exists(DefaultAppPropertiesPath))
            {
                string? parent = Path.GetDirectoryName(AppPropertiesPath);

                if (!string.IsNullOrWhiteSpace(parent))
                {
                    Directory.CreateDirectory(parent);
                }

                File.Copy(DefaultAppPropertiesPath, AppPropertiesPath, overwrite: false);
            }
        }

        private static IEnumerable<string> GetSaveOrder(IEnumerable<string> keys)
        {
            return keys
                .OrderBy(key => CategoryOrder(FirstDisplayForKey(key).Category))
                .ThenBy(key => FirstDisplayForKey(key).Order)
                .ThenBy(key => key);
        }

        private static DisplayInfo FirstDisplayForKey(string key)
        {
            DisplayInfo? display = DisplayLayout.FirstOrDefault(
                x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase)
            );

            if (display != null)
            {
                return display;
            }

            return Info(
                key,
                "Other",
                "General",
                MakeFallbackLabel(key),
                key,
                9999
            );
        }

        private static string MakeFallbackLabel(string key)
        {
            int index = key.LastIndexOf('.');
            string fallback = index >= 0 ? key.Substring(index + 1) : key;

            return string.Concat(
                fallback.Select((ch, i) => i > 0 && char.IsUpper(ch) ? " " + ch : ch.ToString())
            );
        }

        private static int CategoryOrder(string category)
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

        private static bool IsSensitive(string key)
        {
            string lower = key.ToLowerInvariant();

            return lower.Contains("password") ||
                   lower.Contains("secret") ||
                   lower.Contains("clientid") ||
                   lower.Contains("credentials") ||
                   lower.Contains("token") ||
                   lower.Contains("cachefile");
        }

        private static bool IsBooleanKey(string key, string value)
        {
            return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "false", StringComparison.OrdinalIgnoreCase) ||
                   key.EndsWith(".enabled", StringComparison.OrdinalIgnoreCase) ||
                   key.EndsWith(".runContinuously", StringComparison.OrdinalIgnoreCase) ||
                   key.EndsWith("dryRun", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class DisplayInfo
        {
            public string DisplayId { get; init; } = string.Empty;
            public string Key { get; init; } = string.Empty;
            public string Category { get; init; } = string.Empty;
            public string Section { get; init; } = string.Empty;
            public string Label { get; init; } = string.Empty;
            public string Description { get; init; } = string.Empty;
            public int Order { get; init; }
        }

        private static DisplayInfo Info(
            string key,
            string category,
            string section,
            string label,
            string description,
            int order
        )
        {
            return new DisplayInfo
            {
                DisplayId = category + "|" + section + "|" + key,
                Key = key,
                Category = category,
                Section = section,
                Label = label,
                Description = description,
                Order = order
            };
        }

        private static readonly List<DisplayInfo> DisplayLayout = new()
        {
            // =========================================================
            // Application
            // =========================================================
            Info(
                "outlook.clientId",
                "Application",
                "Microsoft Sign-In",
                "Azure Application Client ID",
                "Find this on Azure application Overview page labeled \"Application (client) ID\"",
                10
            ),

            Info(
                "outlook.tenantId",
                "Application",
                "Microsoft Sign-In",
                "Azure Tenant ID",
                "Use \"common\" for personal/work accounts, or specific tenant if required",
                20
            ),

            Info(
                "outlook.graphScopes",
                "Application",
                "Microsoft Sign-In",
                "Microsoft Permissions",
                "Microsoft Graph Permissions requested by the app",
                30
            ),

            // =========================================================
            // AHA Automation
            // =========================================================
            Info(
                "aha.headlessMode",
                "AHA Automation",
                "Module Run Options",
                "Headless Mode",
                "Runs automation in the background. If Disabled, AHA Altas window will open in the foreground",
                999
            ),

            Info(
                "aha.runContinuously",
                "AHA Automation",
                "Module Run Options",
                "Continuous Run",
                "Logs into AHA, accepts students, and scrapes student info. Repeats until stopped. On error, moves to next run. If Disabled, runs once and ends process",
                1000
            ),

            Info(
                "aha.runInterval",
                "AHA Automation",
                "Module Run Options",
                "Run Interval",
                "Seconds between AHA Automation runs",
                1005
            ),

            Info(
                "aha.email",
                "AHA Automation",
                "AHA Login",
                "Email",
                "Email used to sign in to AHA Atlas",
                1010
            ),

            Info(
                "aha.password",
                "AHA Automation",
                "AHA Login",
                "Password",
                "Password used to sign in to AHA Atlas",
                1020
            ),

            Info(
                "aha.organization",
                "AHA Automation",
                "AHA Filters",
                "Organization Filter",
                "Organization selected on AHA class search page",
                1030
            ),

            Info(
                "aha.instructor",
                "AHA Automation",
                "AHA Filters",
                "Instructor Filter",
                "Instructor selected on the AHA class search page. Acceptable values: \"All\", Instructor Name (1)",
                1040
            ),

            Info(
                "aha.startOffsetDays",
                "AHA Automation",
                "AHA Filters",
                "Start Date Offset",
                "Start date for search date selector \n(Yesterday = -1, Today = 0, Tomorrow = 1)",
                1050
            ),

            Info(
                "aha.endOffsetDays",
                "AHA Automation",
                "AHA Filters",
                "End Date Offset",
                "End date for search date selector \n(Yesterday = -1, Today = 0, Tomorrow = 1)",
                1060
            ),

            Info(
                "file.ahaCsv",
                "AHA Automation",
                "Student Data",
                "AHA CSV File",
                "Local CSV file used for AHA records",
                1100
            ),
            // =========================================================
            // Outlook Event Creator
            // =========================================================
            Info(
                "calendar.runContinuously",
                "Outlook Event Creator",
                "Module Runtime",
                "Continuous Run",
                "Checks Outlook for enrollment emails. Repeats until stopped. If Disabled, runs once and ends process",
                2010
            ),

            Info(
                "calendar.runInterval",
                "Outlook Event Creator",
                "Module Runtime",
                "Run Interval",
                "Seconds between Outlook Event Creator runs",
                2020
            ),

            Info(
                "email.maxCount",
                "Outlook Event Creator",
                "Email Watcher",
                "Max Emails Per Check",
                "Maximum number of emails to scan per check",
                2025
            ),

            Info(
                "calendar.timeZone",
                "Outlook Event Creator",
                "Calendar",
                "Calendar Time Zone",
                "Time zone used when creating Outlook calendar events",
                2030
            ),

            Info(
                "calendarFile.seenIds",
                "Outlook Event Creator",
                "Local Files",
                "Processed Email IDs File",
                "Tracks which enrollment emails have already been processed",
                2040
            ),

            Info(
                "calendar.tokenCacheFile",
                "Outlook Event Creator",
                "Sign-in Cache",
                "Calendar Sign-in Cache",
                "Local file that stores Microsoft sign-in",
                2070
            ),

            // =========================================================
            // Reminders
            // =========================================================
            Info(
                "reminder.dryRun",
                "Reminders",
                "Module Runtime",
                "Dry Run Mode",
                "Preview reminder emails without actually sending them (no login required)",
                3002
            ),

            Info(
                "reminder.runContinuously",
                "Reminders",
                "Module Runtime",
                "Continuous Run",
                "Runs reminder checks. Repeats until stopped. If Disabled, runs once and ends process",
                3005
            ),

            Info(
                "reminder.runInterval",
                "Reminders",
                "Module Runtime",
                "Run Interval",
                "Seconds between reminder checks",
                3010
            ),

            Info(
                "reminder.registration.subject",
                "Reminders",
                "Registration Reminders",
                "Reminder Email Subject Line",
                "Subject line for registration reminder emails",
                3080
            ),

            Info(
                "reminder.registration.template",
                "Reminders",
                "Registration Reminders",
                "Reminder Email Template",
                "Path to the registration reminder email template",
                3090
            ),

            Info(
                "reminder.registration.enabled",
                "Reminders",
                "Registration Reminders",
                "Send Registration Reminders",
                "Sends reminders to students who still need to complete registration (registered in AHA but not Acuity)",
                3100
            ),

            Info(
                "reminder.registration.resendAfterDays",
                "Reminders",
                "Registration Reminders",
                "Reminder Schedule",
                "Days before appointment date to send registration reminders",
                3110
            ),

            //Info(
            //    "reminder.renewal.enabled",
            //    "Reminders",
            //    "Renewal Reminders",
            //    "Send Renewal Reminders",
            //    "Renewal reminders are not currently implemented",
            //    3200
            //),

            //Info(
            //    "reminder.certificationValidMonths",
            //    "Reminders",
            //    "Renewal Reminders",
            //    "Certification Validity Period",
            //    "Months a certification is valid for before expiring",
            //    3210
            //),

            //Info(
            //    "reminder.renewalOffsetsDays",
            //    "Reminders",
            //    "Renewal Reminders",
            //    "Renewal Email Schedule",
            //    "Comma-separated days before expiration to send renewal reminders",
            //    3220
            //),

            Info(
                "file.ahaCsv",
                "Reminders",
                "Student Data",
                "AHA CSV File",
                "Local CSV file used for AHA records",
                3250
            ),

            Info(
                "reminder.tokenCacheFile",
                "Reminders",
                "Sign-in Cache",
                "Reminder Sign-in Cache",
                "Local file that stores Microsoft sign-in",
                3300
            ),

            // =========================================================
            // RQI Uploader & Email Scraper
            // =========================================================
            Info(
                "app.runInterval",
                "RQI Uploader & Email Scraper",
                "Module Runtime",
                "Run Interval",
                "Seconds between RQI Uploader & Email Scraper runs",
                3900
            ),

            Info(
                "app.runContinuously",
                "RQI Uploader & Email Scraper",
                "Module Runtime",
                "Continuous Run",
                "Scrapes Outlook inbox, syncs to Google Sheets, and uploads to RQI. Repeats until stopped. If Disabled, runs once and ends process",
                3910
            ),

            Info(
                "file.msgIds",
                "RQI Uploader & Email Scraper",
                "Email Scraper",
                "Processed Email IDs File",
                "Tracks which appointment emails have already been processed",
                4000
            ),

            Info(
                "file.emailDump",
                "RQI Uploader & Email Scraper",
                "Email Scraper",
                "Processed Emails File",
                "File where parsed email text is saved",
                4010
            ),

            Info(
                "file.rqiCsv",
                "RQI Uploader & Email Scraper",
                "Email Scraper",
                "RQI CSV File",
                "Local CSV file used for RQI records",
                4020
            ),

            Info(
                "file.ahaCsv",
                "RQI Uploader & Email Scraper",
                "Email Scraper",
                "AHA CSV File",
                "Local CSV file used for AHA records",
                4030
            ),

            Info(
                "google.spreadsheetAha",
                "RQI Uploader & Email Scraper",
                "Google Sheets",
                "AHA Spreadsheet ID",
                "Find spreadsheet ID: docs.google.com/spreadsheets/d/YOUR_SPREADSHEET_ID/edit?gid=0#gid=0",
                4040
            ),

            Info(
                "google.spreadsheetRqi",
                "RQI Uploader & Email Scraper",
                "Google Sheets",
                "RQI Spreadsheet ID",
                "Find spreadsheet ID: docs.google.com/spreadsheets/d/YOUR_SPREADSHEET_ID/edit?gid=0#gid=0",
                4050
            ),

            Info(
                "google.sheetNameAha",
                "RQI Uploader & Email Scraper",
                "Google Sheets",
                "AHA Sheet Tab Name",
                "Name of the tab the AHA CSV file will upload to",
                4060
            ),

            Info(
                "google.sheetNameRqi",
                "RQI Uploader & Email Scraper",
                "Google Sheets",
                "RQI Sheet Tab Name",
                "Name of the tab the RQI CSV file will upload to",
                4070
            ),

            Info(
                "rqi.enabled",
                "RQI Uploader & Email Scraper",
                "RQI Uploader",
                "Upload to RQI",
                "Turns RQI SFTP upload on or off",
                4100
            ),

            Info(
                "email.maxCount",
                "RQI Uploader & Email Scraper",
                "Email Scraper",
                "Max Emails Per Check",
                "Caps number of emails scanned per check",
                4110
            ),

            Info(
                "email.maxAge",
                "RQI Uploader & Email Scraper",
                "Email Scraper",
                "Email Search Range",
                "Days back to search for Outlook emails",
                4120
            ),

            Info(
                "outlook.tokenCacheName",
                "RQI Uploader & Email Scraper",
                "Sign-in Cache",
                "Microsoft Token Cache Name",
                "Internal cache name for Microsoft sign-in",
                4150
            ),

            Info(
                "outlook.tokenCacheFile",
                "RQI Uploader & Email Scraper",
                "Sign-in Cache",
                "Microsoft Sign-in Cache",
                "Local file that stores Microsoft sign-in",
                4160
            ),

            Info(
                "rqi.host",
                "RQI Uploader & Email Scraper",
                "RQI Uploader",
                "SFTP Host",
                "SFTP host for RQI uploads",
                4180
            ),

            Info(
                "rqi.username",
                "RQI Uploader & Email Scraper",
                "RQI Uploader",
                "SFTP Username",
                "Username for RQI uploads",
                4190
            ),

            Info(
                "rqi.password",
                "RQI Uploader & Email Scraper",
                "RQI Uploader",
                "SFTP Password",
                "Password for RQI uploads",
                4200
            ),

            Info(
                "rqi.remoteDir",
                "RQI Uploader & Email Scraper",
                "RQI Uploader",
                "SFTP Directory",
                "Remote upload directory on the RQI SFTP server",
                4210
            ),

            Info(
                "rqi.filename",
                "RQI Uploader & Email Scraper",
                "RQI Uploader",
                "SFTP Upload File Name",
                "Required upload file name for RQI",
                4220
            ),

            Info(
                "rqi.port",
                "RQI Uploader & Email Scraper",
                "RQI Uploader",
                "SFTP Port",
                "SFTP port for RQI uploads",
                4230
            ),

            Info(
                "google.credentialsFile",
                "RQI Uploader & Email Scraper",
                "Google Sheets",
                "Google Credentials",
                "Necessary to upload CSVs to Google Sheets. If needed, acquire new Google Service Account key at console.cloud.google.com",
                4240
            )
        };
    }
}