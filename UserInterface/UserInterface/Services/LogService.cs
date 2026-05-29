using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using UserInterface.Models;

namespace UserInterface.Services
{
    public class LogService : IDisposable
    {
        private readonly AppSettings _settings;
        private readonly string _logFolderPath;
        private readonly string _moduleLogFolderPath;
        private readonly DispatcherTimer _pruneTimer;
        private bool _disposed;

        public LogService(AppSettings settings, string settingsFolderPath)
        {
            _settings = settings;
            _logFolderPath = Path.Combine(settingsFolderPath, "logs");
            _moduleLogFolderPath = Path.Combine(_logFolderPath, "modules");
            Directory.CreateDirectory(_logFolderPath);
            Directory.CreateDirectory(_moduleLogFolderPath);

            Entries = new ObservableCollection<LogEntry>();

            if (_settings.RetainVisibleLogsBetweenSessions)
                LoadVisibleLogsFromDisk();

            PruneVisibleEntries();

            _pruneTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(15)
            };
            _pruneTimer.Tick += (_, _) => PruneVisibleEntries();
            _pruneTimer.Start();
        }

        public ObservableCollection<LogEntry> Entries { get; }

        public string LogFolderPath => _logFolderPath;

        public void Add(string source, string message, string level = "Info")
        {
            var now = DateTime.Now;
            var entry = new LogEntry
            {
                Timestamp = now,
                TimeText = now.ToString("MM/dd/yyyy hh:mm:ss tt"),
                Source = NormalizeSource(source),
                Message = CleanMessage(message),
                Level = level
            };

            AppendToDisk(entry);

            void AddEntry()
            {
                Entries.Add(entry);
                PruneVisibleEntries();
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
                dispatcher.BeginInvoke((Action)AddEntry);
            else
                AddEntry();
        }

        public void AppendModuleOutput(string source, string line, string level = "Info")
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            try
            {
                Directory.CreateDirectory(_moduleLogFolderPath);
                string safeSource = MakeSafeFileName(source);
                string sourceFolder = Path.Combine(_moduleLogFolderPath, safeSource);
                Directory.CreateDirectory(sourceFolder);
                string path = Path.Combine(sourceFolder, $"{DateTime.Now:yyyy-MM-dd}.log");
                string text = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {line}";
                File.AppendAllText(path, text + Environment.NewLine);
            }
            catch
            {
                // Stop logging from crashing UI
            }
        }

        public void ClearVisible()
        {
            Entries.Clear();
        }

        public void OpenLogFolder()
        {
            Directory.CreateDirectory(_logFolderPath);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _logFolderPath,
                UseShellExecute = true
            });
        }

        private void LoadVisibleLogsFromDisk()
        {
            foreach (var entry in ReadRecentLogEntries())
                Entries.Add(entry);
        }

        private IEnumerable<LogEntry> ReadRecentLogEntries()
        {
            var cutoff = DateTime.Now.AddHours(-Math.Max(1, _settings.VisibleLogRetentionHours));
            var files = Directory.GetFiles(_logFolderPath, "vitals-*.log")
                .OrderBy(x => x)
                .TakeLast(14)
                .ToList();

            var entries = new List<LogEntry>();
            foreach (var file in files)
            {
                foreach (var line in File.ReadLines(file))
                {
                    var entry = TryParseLogLine(line);
                    if (entry == null || entry.Timestamp < cutoff)
                        continue;

                    entries.Add(entry);
                }
            }

            return entries.OrderBy(x => x.Timestamp).TakeLast(300);
        }

        private void PruneVisibleEntries()
        {
            var cutoff = DateTime.Now.AddHours(-Math.Max(1, _settings.VisibleLogRetentionHours));

            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (Entries[i].Timestamp < cutoff)
                    Entries.RemoveAt(i);
            }

            while (Entries.Count > 300)
                Entries.RemoveAt(0);
        }

        private void AppendToDisk(LogEntry entry)
        {
            try
            {
                Directory.CreateDirectory(_logFolderPath);
                string path = Path.Combine(_logFolderPath, $"vitals-{entry.Timestamp:yyyy-MM-dd}.log");
                string line = string.Join("\t", new[]
                {
                    entry.Timestamp.ToString("O", CultureInfo.InvariantCulture),
                    Escape(entry.Level),
                    Escape(entry.Source),
                    Escape(entry.Message)
                });

                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch
            {
                // Prevent crashes
            }
        }

        private static LogEntry? TryParseLogLine(string line)
        {
            var parts = line.Split('\t');
            if (parts.Length < 4)
                return null;

            if (!DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var timestamp))
                return null;

            return new LogEntry
            {
                Timestamp = timestamp.ToLocalTime(),
                TimeText = timestamp.ToLocalTime().ToString("MM/dd/yyyy hh:mm:ss tt"),
                Level = Unescape(parts[1]),
                Source = Unescape(parts[2]),
                Message = Unescape(string.Join("\t", parts.Skip(3)))
            };
        }

        private static string NormalizeSource(string source)
        {
            return string.Equals(source, "Automation", StringComparison.OrdinalIgnoreCase)
                ? "Application"
                : (source ?? string.Empty);
        }

        private static string CleanMessage(string message)
        {
            string text = (message ?? string.Empty).Trim();

            while (text.EndsWith("...", StringComparison.Ordinal))
                text = text.Substring(0, text.Length - 3).TrimEnd();

            while (text.EndsWith(".", StringComparison.Ordinal))
                text = text.Substring(0, text.Length - 1).TrimEnd();

            return text;
        }

        private static string MakeSafeFileName(string value)
        {
            string safe = string.IsNullOrWhiteSpace(value) ? "module" : value.Trim();
            foreach (char ch in Path.GetInvalidFileNameChars())
                safe = safe.Replace(ch, '_');

            return safe.Replace(' ', '-').ToLowerInvariant();
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\t", "\\t")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private static string Unescape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\n", "\n")
                .Replace("\\r", "\r")
                .Replace("\\t", "\t")
                .Replace("\\\\", "\\");
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            _pruneTimer.Stop();
        }
    }
}
