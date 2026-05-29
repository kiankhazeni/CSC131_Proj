using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using UserInterface.Models;
using UserInterface.Views;

namespace UserInterface.Services
{
    public class AutomationService : INotifyPropertyChanged, IDisposable
    {
        private readonly AppSettings _settings;
        private readonly LogService _logService;
        private readonly Dictionary<AutomationModule, Process> _runningProcesses = new Dictionary<AutomationModule, Process>();
        private readonly Dictionary<AutomationModule, CancellationTokenSource> _waitingRestarts = new Dictionary<AutomationModule, CancellationTokenSource>();
        private readonly HashSet<AutomationModule> _manualStopModules = new HashSet<AutomationModule>();
        private readonly HashSet<AutomationModule> _stopRequestedModules = new HashSet<AutomationModule>();
        private readonly HashSet<string> _openedAuthPrompts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<AutomationModule, AuthPromptWindow> _authPrompts = new Dictionary<AutomationModule, AuthPromptWindow>();
        private readonly Dictionary<AutomationModule, RqiSummaryState> _rqiStates = new Dictionary<AutomationModule, RqiSummaryState>();
        private readonly Dictionary<AutomationModule, string> _pendingCalendarInstructor = new Dictionary<AutomationModule, string>();
        private readonly Dictionary<AutomationModule, string> _pendingCalendarStart = new Dictionary<AutomationModule, string>();
        private readonly Dictionary<AutomationModule, ReminderSummaryState> _reminderStates = new Dictionary<AutomationModule, ReminderSummaryState>();
        private readonly HashSet<AutomationModule> _moduleErrorLogged = new HashSet<AutomationModule>();
        private readonly object _gate = new object();
        private string _lastStartText = "Not started";
        private string _lastStopText = "Not stopped";
        private bool _disposed;

        public AutomationService(AppSettings settings, LogService logService)
        {
            _settings = settings;
            _logService = logService;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<AutomationModule> Modules => _settings.AutomationModules;

        public bool IsRunning
        {
            get
            {
                lock (_gate)
                    return _runningProcesses.Count > 0 || _waitingRestarts.Count > 0;
            }
        }

        public string StatusText => IsRunning ? "Running" : "Stopped";

        public string LastStartText
        {
            get => _lastStartText;
            private set
            {
                if (_lastStartText == value)
                    return;

                _lastStartText = value;
                OnPropertyChanged();
            }
        }

        public string LastStopText
        {
            get => _lastStopText;
            private set
            {
                if (_lastStopText == value)
                    return;

                _lastStopText = value;
                OnPropertyChanged();
            }
        }

        public void StartAll()
        {
            if (IsRunning)
            {
                _logService.Add("Application", "Start requested, but automation is already running.", "Warning");
                return;
            }

            int started = 0;

            foreach (var module in Modules.Where(x => x.IsEnabled).ToList())
            {
                if (StartModule(module))
                    started++;
            }

            if (started == 0)
            {
                _logService.Add(
                    "Application",
                    "No automation modules started. Enable a module in Settings or check the logged errors.",
                    "Warning");
            }
            else
            {
                LastStartText = DateTime.Now.ToString("hh:mm:ss tt");
            }

            NotifyStatusChanged();
        }

        public void StopAll()
        {
            List<KeyValuePair<AutomationModule, Process>> running;
            List<KeyValuePair<AutomationModule, CancellationTokenSource>> waiting;

            lock (_gate)
            {
                foreach (var module in Modules)
                    _manualStopModules.Add(module);

                running = _runningProcesses.ToList();
                waiting = _waitingRestarts.ToList();
            }

            if (running.Count == 0 && waiting.Count == 0)
            {
                _logService.Add("Application", "Stop requested, but no modules are running or waiting.", "Warning");
                NotifyStatusChanged();
                return;
            }

            foreach (var pair in waiting)
                CancelWaitingModule(pair.Key, pair.Value);

            foreach (var pair in running)
            {
                lock (_gate)
                    _manualStopModules.Add(pair.Key);

                StopModule(pair.Key, pair.Value);
            }

            LastStopText = DateTime.Now.ToString("hh:mm:ss tt");
            NotifyStatusChanged();
        }

        private bool StartModule(AutomationModule module)
        {
            lock (_gate)
            {
                if (_runningProcesses.ContainsKey(module))
                {
                    _logService.Add(module.Name, "Module is already running.", "Warning");
                    return false;
                }

                if (_waitingRestarts.TryGetValue(module, out var waiting))
                    CancelWaitingModule(module, waiting);

                _manualStopModules.Remove(module);
                _stopRequestedModules.Remove(module);
            }

            string workingDirectory = PathResolver.ResolveDirectory(module.WorkingDirectory);
            if (!Directory.Exists(workingDirectory))
            {
                module.Status = "Missing folder";
                _logService.Add(module.Name, $"Working directory not found: {workingDirectory}", "Error");
                return false;
            }

            if (string.IsNullOrWhiteSpace(module.Command))
            {
                module.Status = "Missing command";
                _logService.Add(module.Name, "Command is empty. Edit it in Settings.", "Error");
                return false;
            }

            string command = module.Command.Trim();
            string arguments = module.Arguments ?? string.Empty;

            ApplyPackagedModuleFallbacks(module, ref workingDirectory, ref command, ref arguments);

            if (!CheckRuntimeRequirements(module, workingDirectory, command, arguments, out var runtime))
                return false;

            try
            {
                PrepareWorkingDirectory(workingDirectory);
                EnsureRuntimeFiles(workingDirectory);

                string resolvedCommand = ResolveCommand(command, workingDirectory, runtime);

                var startInfo = new ProcessStartInfo
                {
                    FileName = resolvedCommand,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                RuntimeLocator.ApplyTo(startInfo, runtime);

                var process = new Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true
                };

                process.OutputDataReceived += (_, e) => LogProcessOutput(module, e.Data, "Info");
                process.ErrorDataReceived += (_, e) => LogProcessOutput(module, e.Data, "Error");
                process.Exited += (_, _) => ProcessExited(module, process);

                if (!process.Start())
                {
                    module.Status = "Failed to start";
                    _logService.Add(module.Name, "Process did not start.", "Error");
                    return false;
                }

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                lock (_gate)
                {
                    _runningProcesses[module] = process;
                    _moduleErrorLogged.Remove(module);
                }

                module.IsRunning = true;
                module.Status = "Running";
                module.LastExitCode = null;
                module.LastRunText = DateTime.Now.ToString("hh:mm:ss tt");
                module.NextRunText = EstimateNextRunText(module);

                _logService.Add(module.Name, "Started process.");
                NotifyStatusChanged();
                return true;
            }
            catch (Exception ex)
            {
                module.IsRunning = false;
                module.Status = "Start failed";
                _logService.Add(module.Name, "Start failed: " + ex.Message, "Error");
                NotifyStatusChanged();
                return false;
            }
        }

        private static void ApplyPackagedModuleFallbacks(AutomationModule module, ref string workingDirectory, ref string command, ref string arguments)
        {
            string moduleName = module.Name ?? string.Empty;

            if (moduleName.Contains("RQI", StringComparison.OrdinalIgnoreCase) ||
                moduleName.Contains("Email", StringComparison.OrdinalIgnoreCase))
            {
                if (TryUsePackagedJar(ref workingDirectory, ref command, ref arguments, "rqi-and-email-scraper-module.jar"))
                    return;
            }

            if (moduleName.Contains("AHA", StringComparison.OrdinalIgnoreCase))
            {
                if (TryUsePackagedJar(ref workingDirectory, ref command, ref arguments, "aha-automation.jar"))
                    return;
            }

            if (moduleName.Contains("Outlook", StringComparison.OrdinalIgnoreCase) ||
                moduleName.Contains("Calendar", StringComparison.OrdinalIgnoreCase) ||
                moduleName.Contains("Enrollment", StringComparison.OrdinalIgnoreCase))
            {
                if (TryUsePackagedJar(ref workingDirectory, ref command, ref arguments, "outlook-event-creator.jar"))
                    return;
            }

            if (moduleName.Contains("Reminder", StringComparison.OrdinalIgnoreCase) ||
                moduleName.Contains("Reminders", StringComparison.OrdinalIgnoreCase))
            {
                TryUsePackagedJar(ref workingDirectory, ref command, ref arguments, "reminder-module.jar");
            }
        }

        private static bool TryUsePackagedJar(ref string workingDirectory, ref string command, ref string arguments, string jarName)
        {
            string appRoot = AppContext.BaseDirectory;
            string jarPath = Path.Combine(appRoot, "modules", jarName);

            if (!File.Exists(jarPath))
                return false;

            workingDirectory = appRoot;
            command = "java";
            arguments = "--enable-native-access=ALL-UNNAMED \"-Dapp.config=config/app.properties\" -jar \"modules/" + jarName + "\"";
            return true;
        }

        private static void PrepareWorkingDirectory(string workingDirectory)
        {
            Directory.CreateDirectory(Path.Combine(workingDirectory, "config"));
            Directory.CreateDirectory(Path.Combine(workingDirectory, "config", "auth_cache"));
            Directory.CreateDirectory(Path.Combine(workingDirectory, "resources"));
            Directory.CreateDirectory(Path.Combine(workingDirectory, "resources", "email_templates"));
        }

        private static void EnsureRuntimeFiles(string workingDirectory)
        {
            EnsureCsv(
                Path.Combine(workingDirectory, "resources", "preprod_cl.csv"),
                "LocationID,LocationName,UserID,FirstName,MiddleName,LastName,Email,JobCode,JobName,HireDate,Status,DateOfBirth,Gender,YearsofExperiences,ActiveDate,InactiveDate,Group");

            EnsureCsv(
                Path.Combine(workingDirectory, "resources", "aha.csv"),
                "EMAIL,First Name,M,Last Name,Phone,Course,Date,Acuity Regist.,AHA Regist.,Reminder email sent");
        }

        private static void EnsureCsv(string path, string header)
        {
            if (File.Exists(path))
                return;

            string? parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(parent))
                Directory.CreateDirectory(parent);

            File.WriteAllText(path, header + Environment.NewLine);
        }

        private bool CheckRuntimeRequirements(
            AutomationModule module,
            string workingDirectory,
            string command,
            string arguments,
            out RuntimeInfo runtime)
        {
            string commandLine = $"{command} {arguments}".ToLowerInvariant();

            bool needsJava =
                commandLine.Contains("java") ||
                commandLine.Contains(".jar");

            runtime = needsJava
                ? RuntimeLocator.Locate()
                : new RuntimeInfo();

            if (needsJava && !runtime.HasJava)
            {
                module.Status = "Missing Java";
                _logService.Add(
                    module.Name,
                    "Java was not found. Install Java 21 or place a portable JDK/JRE under tools\\jdk.",
                    "Error");

                NotifyStatusChanged();
                return false;
            }

            string? jarFile = GetArgumentAfter(arguments, "-jar");

            if (!string.IsNullOrWhiteSpace(jarFile))
            {
                string jarPath = Path.IsPathRooted(jarFile)
                    ? jarFile
                    : Path.Combine(workingDirectory, jarFile);

                if (!File.Exists(jarPath))
                {
                    module.Status = "Missing jar";
                    _logService.Add(module.Name, $"File not found: {jarPath}", "Error");
                    NotifyStatusChanged();
                    return false;
                }
            }

            return true;
        }

        private static string ResolveCommand(string command, string workingDirectory, RuntimeInfo runtime)
        {
            if (string.Equals(command, "java", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(command, "java.exe", StringComparison.OrdinalIgnoreCase))
            {
                string? detectedJava = RuntimeLocator.GetJavaCommandPath(runtime);

                if (!string.IsNullOrWhiteSpace(detectedJava))
                {
                    return detectedJava;
                }
            }

            if (!Path.IsPathRooted(command) && (command.Contains('\\') || command.Contains('/')))
            {
                string fromWorkingDirectory = Path.GetFullPath(Path.Combine(workingDirectory, command));

                if (File.Exists(fromWorkingDirectory))
                {
                    return fromWorkingDirectory;
                }

                string fromProject = PathResolver.ResolveFile(command);

                if (File.Exists(fromProject))
                {
                    return fromProject;
                }
            }

            return command;
        }

        private static string? GetArgumentAfter(string arguments, string option)
        {
            var tokens = SplitCommandLine(arguments);
            for (int i = 0; i < tokens.Count - 1; i++)
            {
                if (string.Equals(tokens[i], option, StringComparison.OrdinalIgnoreCase))
                    return tokens[i + 1];
            }

            return null;
        }

        private static List<string> SplitCommandLine(string commandLine)
        {
            var tokens = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            foreach (char ch in commandLine)
            {
                if (ch == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (char.IsWhiteSpace(ch) && !inQuotes)
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }
                    continue;
                }

                current.Append(ch);
            }

            if (current.Length > 0)
                tokens.Add(current.ToString());

            return tokens;
        }

        private void StopModule(AutomationModule module, Process process)
        {
            try
            {
                lock (_gate)
                    _stopRequestedModules.Add(module);

                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);

                module.Status = "Stopping";
                module.NextRunText = "—";
            }
            catch (Exception ex)
            {
                _logService.Add(module.Name, "Stop failed: " + ex.Message, "Error");
            }
        }

        private void CancelWaitingModule(AutomationModule module, CancellationTokenSource cts)
        {
            try
            {
                cts.Cancel();
                cts.Dispose();
            }
            catch
            {
                // Ignore.
            }

            lock (_gate)
                _waitingRestarts.Remove(module);

            module.IsRunning = false;
            module.Status = "Stopped";
            module.NextRunText = "—";
            _logService.Add(module.Name, "Waiting run cancelled.");
        }

        private void ProcessExited(AutomationModule module, Process process)
        {
            RunOnUiThread(() =>
            {
                int? exitCode = null;
                try
                {
                    exitCode = process.ExitCode;
                }
                catch
                {
                    // Ignore processes that exit before the code is available.
                }

                lock (_gate)
                    _runningProcesses.Remove(module);

                module.IsRunning = false;
                module.LastExitCode = exitCode;
                module.Status = exitCode == 0 ? "Stopped" : $"Exited ({exitCode})";
                module.NextRunText = "—";

                bool restarting = ShouldRestartAha(module);
                bool stopRequested;
                lock (_gate)
                {
                    stopRequested = _stopRequestedModules.Remove(module);
                }

                string stoppedMessage;
                if (stopRequested)
                    stoppedMessage = exitCode == 0 ? "Stop requested. Process stopped." : $"Stop requested. Process exited with code {exitCode}.";
                else
                    stoppedMessage = exitCode == 0 ? "Process stopped." : $"Process exited with code {exitCode}.";

                if (restarting)
                {
                    int delaySeconds = Math.Max(1, GetIntAppProperty("aha.runInterval", 300));
                    stoppedMessage += $" Next AHA Automation run in {delaySeconds} seconds.";
                }

                _logService.Add(
                    module.Name,
                    stoppedMessage,
                    exitCode == 0 ? "Info" : "Warning");

                process.Dispose();
                NotifyStatusChanged();

                if (restarting)
                    _ = RestartAhaAfterDelayAsync(module);
            });
        }

        private bool ShouldRestartAha(AutomationModule module)
        {
            if (_disposed || !module.IsEnabled || !module.Name.Contains("AHA", StringComparison.OrdinalIgnoreCase))
                return false;

            lock (_gate)
            {
                if (_manualStopModules.Contains(module))
                    return false;
            }

            return GetBooleanAppProperty("aha.runContinuously", false);
        }

        private async Task RestartAhaAfterDelayAsync(AutomationModule module)
        {
            int delaySeconds = Math.Max(1, GetIntAppProperty("aha.runInterval", 300));
            var cts = new CancellationTokenSource();

            lock (_gate)
                _waitingRestarts[module] = cts;

            module.IsRunning = true;
            module.Status = "Waiting";
            module.NextRunText = DateTime.Now.AddSeconds(delaySeconds).ToString("hh:mm:ss tt");
            NotifyStatusChanged();

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cts.Token);
            }
            catch (TaskCanceledException)
            {
                return;
            }
            finally
            {
                lock (_gate)
                    _waitingRestarts.Remove(module);
                cts.Dispose();
            }

            module.IsRunning = false;

            if (_disposed || !module.IsEnabled)
            {
                module.Status = "Stopped";
                module.NextRunText = "—";
                NotifyStatusChanged();
                return;
            }

            lock (_gate)
            {
                if (_manualStopModules.Contains(module) || _runningProcesses.ContainsKey(module))
                {
                    module.Status = "Stopped";
                    module.NextRunText = "—";
                    NotifyStatusChanged();
                    return;
                }
            }

            RunOnUiThread(() => StartModule(module));
        }

        private bool GetBooleanAppProperty(string key, bool defaultValue)
        {
            string value = GetAppProperty(key);
            return string.IsNullOrWhiteSpace(value) ? defaultValue : bool.TryParse(value, out bool parsed) ? parsed : defaultValue;
        }

        private int GetIntAppProperty(string key, int defaultValue)
        {
            string value = GetAppProperty(key);
            return string.IsNullOrWhiteSpace(value) ? defaultValue : int.TryParse(value, out int parsed) ? parsed : defaultValue;
        }

        private static string GetAppProperty(string key)
        {
            try
            {
                string path = PathResolver.ResolveFile("config/app.properties");
                if (!File.Exists(path))
                    return string.Empty;

                foreach (string line in File.ReadLines(path))
                {
                    string trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith("#") || !trimmed.Contains('='))
                        continue;

                    int index = trimmed.IndexOf('=');
                    string currentKey = trimmed.Substring(0, index).Trim();
                    if (string.Equals(currentKey, key, StringComparison.OrdinalIgnoreCase))
                        return trimmed.Substring(index + 1).Trim();
                }
            }
            catch
            {
                // Use default.
            }

            return string.Empty;
        }

        private string EstimateNextRunText(AutomationModule module)
        {
            string name = module.Name ?? string.Empty;
            int seconds = 0;

            if (name.Contains("RQI", StringComparison.OrdinalIgnoreCase) || name.Contains("Email", StringComparison.OrdinalIgnoreCase))
                seconds = GetIntAppProperty("app.runInterval", 0);
            else if (name.Contains("Outlook", StringComparison.OrdinalIgnoreCase) || name.Contains("Calendar", StringComparison.OrdinalIgnoreCase))
                seconds = GetIntAppProperty("calendar.runInterval", 0);
            else if (name.Contains("Reminder", StringComparison.OrdinalIgnoreCase) || name.Contains("Reminders", StringComparison.OrdinalIgnoreCase))
                seconds = GetIntAppProperty("reminder.runInterval", 0);
            else if (name.Contains("AHA", StringComparison.OrdinalIgnoreCase))
                seconds = GetIntAppProperty("aha.runInterval", 0);

            return seconds > 0 ? DateTime.Now.AddSeconds(seconds).ToString("hh:mm:ss tt") : "—";
        }

        private void LogProcessOutput(AutomationModule module, string? data, string level)
        {
            if (string.IsNullOrWhiteSpace(data))
                return;

            string line = data.Trim();
            string normalizedLevel = NormalizeProcessOutputLevel(line, level);

            _logService.AppendModuleOutput(module.Name, line, normalizedLevel);

            HandleDeviceCodePrompt(module, line);
            CloseAuthPromptIfAuthenticated(module, line);
            UpdateModuleActivityFromOutput(module, line);

            var summary = ToVisibleSummary(module, line, normalizedLevel);
            if (summary == null)
                return;

            _logService.Add(module.Name, summary.Value.Message, summary.Value.Level);
        }

        private static string NormalizeProcessOutputLevel(string line, string rawLevel)
        {
            if (IsInfoConsoleLine(line))
                return "Info";

            if (IsWarningConsoleLine(line))
                return "Warning";

            if (IsErrorConsoleLine(line))
                return "Error";

            if (string.Equals(rawLevel, "Error", StringComparison.OrdinalIgnoreCase))
                return "Info";

            return string.IsNullOrWhiteSpace(rawLevel) ? "Info" : rawLevel;
        }

        private static bool IsInfoConsoleLine(string line)
        {
            return Regex.IsMatch(line, @"(^|\s|\])INFO(\s|$)", RegexOptions.IgnoreCase) ||
                   line.Contains("Access token refreshed successfully", StringComparison.OrdinalIgnoreCase) ||
                   line.Contains("Signed in successfully", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsWarningConsoleLine(string line)
        {
            return Regex.IsMatch(line, @"(^|\s|\])WARN(?:ING)?(\s|:|$)", RegexOptions.IgnoreCase) ||
                   line.StartsWith("SLF4J:", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsErrorConsoleLine(string line)
        {
            return Regex.IsMatch(line, @"(^|\s|\])ERROR(\s|:|$)", RegexOptions.IgnoreCase) ||
                   line.Contains("exception", StringComparison.OrdinalIgnoreCase) ||
                   line.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                   line.Contains("fatal", StringComparison.OrdinalIgnoreCase) ||
                   line.Contains("Caused by:", StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateModuleActivityFromOutput(AutomationModule module, string line)
        {
            var nextMatch = Regex.Match(
                line,
                @"Next (?:check|reminder check) in\s+(\d+)\s+seconds",
                RegexOptions.IgnoreCase);

            if (nextMatch.Success &&
                int.TryParse(nextMatch.Groups[1].Value, out int nextSeconds) &&
                nextSeconds > 0)
            {
                MarkModuleLoopComplete(module, nextSeconds);
                return;
            }

            if (IsRqiModule(module) &&
                (line.Contains("No new appointments found", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("No new appointment rows found", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("RQI upload successful", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("RQI Upload is disabled", StringComparison.OrdinalIgnoreCase)))
            {
                MarkModuleLoopComplete(module, GetIntAppProperty("app.runInterval", 0));
                return;
            }

            if (IsOutlookEventCreatorModule(module) &&
                (line.Contains("No new enrollment emails", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("Calendar event created", StringComparison.OrdinalIgnoreCase)))
            {
                MarkModuleLoopComplete(module, GetIntAppProperty("calendar.runInterval", 0));
                return;
            }

            if (IsReminderModule(module) &&
                (line.StartsWith("Registration reminder complete", StringComparison.OrdinalIgnoreCase) ||
                 line.StartsWith("Skipped:", StringComparison.OrdinalIgnoreCase)))
            {
                MarkModuleLoopComplete(module, GetIntAppProperty("reminder.runInterval", 0));
                return;
            }

            if (module.Name.Contains("AHA", StringComparison.OrdinalIgnoreCase) &&
                (line.Contains("Finished processing all visible class results", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("aha.csv update complete", StringComparison.OrdinalIgnoreCase)))
            {
                MarkModuleLoopComplete(module, GetIntAppProperty("aha.runInterval", 0));
            }
        }

        private void MarkModuleLoopComplete(AutomationModule module, int nextRunSeconds)
        {
            RunOnUiThread(() =>
            {
                module.LastRunText = DateTime.Now.ToString("hh:mm:ss tt");

                module.NextRunText = nextRunSeconds > 0
                    ? DateTime.Now.AddSeconds(nextRunSeconds).ToString("hh:mm:ss tt")
                    : "—";

                NotifyStatusChanged();
            });
        }

        private static bool IsOutlookEventCreatorModule(AutomationModule module)
        {
            string name = module.Name ?? string.Empty;

            return name.Contains("Outlook", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Calendar", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Enrollment", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsReminderModule(AutomationModule module)
        {
            string name = module.Name ?? string.Empty;

            return name.Contains("Reminder", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Reminders", StringComparison.OrdinalIgnoreCase);
        }

        private void HandleDeviceCodePrompt(AutomationModule module, string line)
        {
            if (!line.Contains("login.microsoft.com/device", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("microsoft.com/devicelogin", StringComparison.OrdinalIgnoreCase))
                return;

            string url = ExtractFirstUrl(line) ?? "https://login.microsoft.com/device";
            string code = ExtractDeviceCode(line);
            string key = module.Name + "|" + url + "|" + code;

            lock (_gate)
            {
                if (!_openedAuthPrompts.Add(key))
                    return;
            }

            RunOnUiThread(() =>
            {
                try
                {
                    if (_authPrompts.TryGetValue(module, out var existingPrompt) && existingPrompt.IsVisible)
                    {
                        existingPrompt.Activate();
                        return;
                    }

                    var prompt = new AuthPromptWindow(module.Name, url, code)
                    {
                        Owner = GetActiveWindow()
                    };
                    prompt.Closed += (_, _) => _authPrompts.Remove(module);
                    _authPrompts[module] = prompt;
                    prompt.Show();
                }
                catch
                {
                    // The log entry below still gives the user the code.
                }
            });

            string message = string.IsNullOrWhiteSpace(code)
                ? "Microsoft sign-in required. Open the sign-in link and follow the instructions"
                : "Microsoft sign-in required. Open the sign-in link and enter code " + code;
            _logService.Add(module.Name, message, "Login Required");
        }

        private void CloseAuthPromptIfAuthenticated(AutomationModule module, string line)
        {
            if (!line.Contains("Saved Microsoft authentication record", StringComparison.OrdinalIgnoreCase) &&
                !line.Contains("Signed in successfully", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("Processing ", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("Checking inbox", StringComparison.OrdinalIgnoreCase) &&
                !line.StartsWith("Watching inbox", StringComparison.OrdinalIgnoreCase))
                return;

            RunOnUiThread(() =>
            {
                if (_authPrompts.TryGetValue(module, out var prompt))
                {
                    prompt.Close();
                    _authPrompts.Remove(module);
                }
            });
        }

        private static Window? GetActiveWindow()
        {
            var app = Application.Current;
            if (app == null)
                return null;

            return app.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive) ?? app.MainWindow;
        }

        private static string? ExtractFirstUrl(string text)
        {
            var match = Regex.Match(text, @"https?://[^\s]+", RegexOptions.IgnoreCase);
            return match.Success ? match.Value.TrimEnd('.', ',', ';') : null;
        }

        private static string ExtractDeviceCode(string text)
        {
            var match = Regex.Match(text, @"code\s+([A-Z0-9]{6,})", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        private static string FormatAhaCsvUpdate(string line)
        {
            var match = Regex.Match(line, @"Updated:\s*(\d+)\s*,\s*appended:\s*(\d+)", RegexOptions.IgnoreCase);
            if (match.Success)
                return $"Finished updating aha.csv: updated {match.Groups[1].Value}, appended {match.Groups[2].Value}";

            return line;
        }

        private (string Message, string Level)? ToVisibleSummary(AutomationModule module, string line, string rawLevel)
        {
            if (IsNoisy(line))
                return null;

            if (line.Contains("selenium", StringComparison.OrdinalIgnoreCase) &&
                line.Contains("datepicker", StringComparison.OrdinalIgnoreCase))
            {
                return ("Failed to select date", "Error");
            }

            string level = rawLevel;
            if (line.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("exception", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("failed", StringComparison.OrdinalIgnoreCase))
            {
                level = "Error";
            }
            else if (line.Contains("warning", StringComparison.OrdinalIgnoreCase) ||
                     line.StartsWith("SLF4J:", StringComparison.OrdinalIgnoreCase))
            {
                level = "Warning";
            }

            if (level == "Error")
            {
                lock (_gate)
                {
                    if (!_moduleErrorLogged.Add(module))
                        return null;
                }
                return (Condense(line), "Error");
            }



            // For copy past: if (line.Contains("", StringComparison.OrdinalIgnoreCase))
            if (line.StartsWith("Processing class result", StringComparison.OrdinalIgnoreCase))
                return (Condense(line), "Info");

            if (line.Contains("Saved Microsoft authentication record", StringComparison.OrdinalIgnoreCase))
                return (Condense(line), "Info");

            if (IsRqiModule(module))
            {
                var rqiSummary = TryBuildRqiSummary(module, line, level);
                if (rqiSummary != null)
                    return rqiSummary;
            }

            if (line.StartsWith("Instructor", StringComparison.OrdinalIgnoreCase) || line.Contains("Instructor :", StringComparison.OrdinalIgnoreCase))
            {
                _pendingCalendarInstructor[module] = line.Split(':').Last().Trim();
                return null;
            }

            if (line.StartsWith("Start", StringComparison.OrdinalIgnoreCase) || line.Contains("Start      :", StringComparison.OrdinalIgnoreCase))
            {
                int separatorIndex = line.IndexOf(':');
                _pendingCalendarStart[module] = separatorIndex >= 0 ? line.Substring(separatorIndex + 1).Trim() : line.Trim();
                return null;
            }

            if (line.Contains("Calendar event created", StringComparison.OrdinalIgnoreCase))
            {
                _pendingCalendarInstructor.TryGetValue(module, out var instructor);
                _pendingCalendarStart.TryGetValue(module, out var start);
                if (!string.IsNullOrWhiteSpace(instructor) || !string.IsNullOrWhiteSpace(start))
                    return ($"Calendar event created: {start}, Instructor {instructor}".Trim().TrimEnd(','), "Info");
                return (Condense(line.Trim()), "Info");
            }

            if (IsOutlookEventCreatorModule(module) &&
                line.Contains("No new enrollment emails", StringComparison.OrdinalIgnoreCase))
            {
                return ("Checked inbox: no new enrollment emails", "Info");
            }

            var reminderSummary = TryBuildReminderSummary(module, line);
            if (reminderSummary != null)
                return reminderSummary;

            if (ShouldHideVisibleLine(line))
                return null;

            if (line.StartsWith("Scraped ", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Finished processing all visible class results", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("aha.csv update complete", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("File not found", StringComparison.OrdinalIgnoreCase))
            {
                if (line.Contains("aha.csv update complete", StringComparison.OrdinalIgnoreCase))
                    return (Condense(FormatAhaCsvUpdate(line)), level);

                return (Condense(line), level);
            }

            return null;
        }

        private (string Message, string Level)? TryBuildRqiSummary(AutomationModule module, string line, string level)
        {
            if (!_rqiStates.TryGetValue(module, out var state))
            {
                state = new RqiSummaryState();
                _rqiStates[module] = state;
            }

            if (line.StartsWith("Checking inbox at", StringComparison.OrdinalIgnoreCase))
            {
                FlushPendingRqiCheck(module, state);
                state.PendingInboxCheck = true;
                state.ProcessedMessages = null;
                return null;
            }

            var processingMatch = Regex.Match(line, @"Processing\s+(\d+)\s+messages", RegexOptions.IgnoreCase);
            if (processingMatch.Success)
            {
                if (int.TryParse(processingMatch.Groups[1].Value, out int count))
                    state.ProcessedMessages = count;
                return null;
            }

            if (line.Contains("No new appointments found", StringComparison.OrdinalIgnoreCase))
            {
                string message = state.ProcessedMessages.HasValue
                    ? $"Checked inbox: processed {state.ProcessedMessages.Value} messages, no new appointments"
                    : "Checked inbox: no new appointments";
                state.ClearInbox();
                return (message, "Info");
            }

            if (line.StartsWith("Initialized Google Sheet", StringComparison.OrdinalIgnoreCase))
            {
                return (Condense(line), "Info");
            }

            if (line.StartsWith("Updated ", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Synced ", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Saved Microsoft authentication record", StringComparison.OrdinalIgnoreCase))
            {
                FlushPendingRqiCheck(module, state);
                return (Condense(line), level);
            }

            if (line.Contains("Attempting to upload to RQI", StringComparison.OrdinalIgnoreCase))
            {
                FlushPendingRqiCheck(module, state);
                state.PendingRqiUpload = true;
                return null;
            }

            if (line.Contains("RQI upload successful", StringComparison.OrdinalIgnoreCase))
            {
                state.PendingRqiUpload = false;
                return ("Attempted to upload to RQI. Upload successful", "Info");
            }

            if (state.PendingRqiUpload && (line.Contains("failed", StringComparison.OrdinalIgnoreCase) || line.Contains("error", StringComparison.OrdinalIgnoreCase)))
            {
                state.PendingRqiUpload = false;
                return ("Attempted to upload to RQI. Upload failed: " + Condense(line), "Error");
            }

            return null;
        }

        private void FlushPendingRqiCheck(AutomationModule module, RqiSummaryState state)
        {
            if (!state.PendingInboxCheck)
                return;

            string message = state.ProcessedMessages.HasValue
                ? $"Checked inbox: processed {state.ProcessedMessages.Value} messages"
                : "Checked inbox";
            state.ClearInbox();
            _logService.Add(module.Name, message, "Info");
        }

        private static bool IsRqiModule(AutomationModule module)
        {
            string name = module.Name ?? string.Empty;
            return name.Contains("RQI", StringComparison.OrdinalIgnoreCase) ||
                   name.Contains("Email", StringComparison.OrdinalIgnoreCase);
        }

        private (string Message, string Level)? TryBuildReminderSummary(AutomationModule module, string line)
        {
            if (!module.Name.Contains("Reminder", StringComparison.OrdinalIgnoreCase) && !module.Name.Contains("Reminders", StringComparison.OrdinalIgnoreCase))
                return null;

            if (!_reminderStates.TryGetValue(module, out var state))
            {
                state = new ReminderSummaryState();
                _reminderStates[module] = state;
            }

            if (line.StartsWith("Dry run:", StringComparison.OrdinalIgnoreCase))
            {
                state.DryRun = line.Contains("true", StringComparison.OrdinalIgnoreCase);
                return null;
            }

            if (line.StartsWith("Would send:", StringComparison.OrdinalIgnoreCase))
            {
                state.WouldSend = ExtractTrailingInt(line);
                return null;
            }

            if (line.StartsWith("Sent:", StringComparison.OrdinalIgnoreCase))
            {
                state.Sent = ExtractTrailingInt(line);
                return null;
            }

            if (line.StartsWith("Skipped:", StringComparison.OrdinalIgnoreCase))
            {
                state.Skipped = ExtractTrailingInt(line);
                string prefix = state.DryRun ? "[DRY RUN] " : string.Empty;
                string message = $"{prefix}Registration reminder complete: would send {state.WouldSend}, sent {state.Sent}, skipped {state.Skipped}";
                _reminderStates.Remove(module);
                return (message, "Info");
            }

            if (line.StartsWith("Registration reminder complete", StringComparison.OrdinalIgnoreCase))
                return null;

            return null;
        }

        private static int ExtractTrailingInt(string line)
        {
            var match = Regex.Match(line, @"(\d+)\s*$");
            return match.Success && int.TryParse(match.Groups[1].Value, out int value) ? value : 0;
        }

        private static bool ShouldHideVisibleLine(string line)
        {
            return line.StartsWith("Checking inbox", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("Processing ", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("Found ", StringComparison.OrdinalIgnoreCase) ||
                   line.Contains("No new enrollment emails", StringComparison.OrdinalIgnoreCase) ||
                   line.Contains("No new appointments found", StringComparison.OrdinalIgnoreCase) ||
                   line.Contains("SCRAPED STUDENTS", StringComparison.OrdinalIgnoreCase) ||
                   line.Contains("Updated aha.csv row for", StringComparison.OrdinalIgnoreCase) ||
                   line.Contains("AHA sheet update complete", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("Appended ", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("No new", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("Connected to RQI", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("Would send", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("Sent registration reminder", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("Dry run:", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("Sent:", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("Skipped:", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("=====", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNoisy(string line)
        {
            return line.StartsWith("WARNING: A terminally deprecated", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("WARNING: sun.misc.Unsafe", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("WARNING: Please consider", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("WARNING: Restricted methods", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("SLF4J:", StringComparison.OrdinalIgnoreCase) ||
                   line.Contains("Netty versions were found", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("at ", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("Caused by:", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("Build info:", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("System info:", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("Driver info:", StringComparison.OrdinalIgnoreCase) ||
                   line.StartsWith("Session ID:", StringComparison.OrdinalIgnoreCase);
        }

        private static string Condense(string line)
        {
            if (line.Length <= 220)
                return line;

            return line.Substring(0, 217) + "...";
        }

        private void NotifyStatusChanged()
        {
            OnPropertyChanged(nameof(IsRunning));
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(Modules));
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static void RunOnUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
                dispatcher.BeginInvoke(action);
            else
                action();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            StopAll();
        }

        private class RqiSummaryState
        {
            public bool PendingInboxCheck { get; set; }
            public int? ProcessedMessages { get; set; }
            public bool PendingRqiUpload { get; set; }

            public void ClearInbox()
            {
                PendingInboxCheck = false;
                ProcessedMessages = null;
            }
        }

        private class ReminderSummaryState
        {
            public bool DryRun { get; set; }
            public int WouldSend { get; set; }
            public int Sent { get; set; }
            public int Skipped { get; set; }
        }
    }
}