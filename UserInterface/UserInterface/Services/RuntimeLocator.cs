using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace UserInterface.Services
{
    public class RuntimeInfo
    {
        public string? JavaHome { get; set; }
        public string? JavaBinPath { get; set; }

        public bool HasJava =>
            !string.IsNullOrWhiteSpace(JavaBinPath) &&
            (File.Exists(Path.Combine(JavaBinPath, "java.exe")) ||
             File.Exists(Path.Combine(JavaBinPath, "java")));
    }

    public static class RuntimeLocator
    {
        public static RuntimeInfo Locate()
        {
            var info = new RuntimeInfo
            {
                JavaHome = FindJavaHome()
            };

            info.JavaBinPath = ToJavaBin(info.JavaHome);

            return info;
        }

        public static void ApplyTo(ProcessStartInfo startInfo, RuntimeInfo runtime)
        {
            if (!string.IsNullOrWhiteSpace(runtime.JavaHome))
            {
                startInfo.EnvironmentVariables["JAVA_HOME"] = runtime.JavaHome;
            }

            if (string.IsNullOrWhiteSpace(runtime.JavaBinPath))
            {
                return;
            }

            string currentPath = startInfo.EnvironmentVariables["PATH"] ?? string.Empty;

            startInfo.EnvironmentVariables["PATH"] =
                runtime.JavaBinPath + Path.PathSeparator + currentPath;
        }

        public static string GetRuntimeStatusText()
        {
            try
            {
                RuntimeInfo runtime = Locate();

                return runtime.HasJava
                    ? "Java found: " + runtime.JavaHome
                    : "Java not found";
            }
            catch (Exception ex)
            {
                return "Runtime check unavailable: " + ex.Message;
            }
        }

        public static string? GetJavaCommandPath(RuntimeInfo runtime)
        {
            if (string.IsNullOrWhiteSpace(runtime.JavaBinPath))
            {
                return null;
            }

            string javaExe = Path.Combine(runtime.JavaBinPath, "java.exe");

            if (File.Exists(javaExe))
            {
                return javaExe;
            }

            string java = Path.Combine(runtime.JavaBinPath, "java");

            return File.Exists(java) ? java : null;
        }

        private static string? FindJavaHome()
        {
            foreach (string candidate in GetJavaCandidates())
            {
                string? home = NormalizeJavaHome(candidate);

                if (home != null)
                {
                    return home;
                }
            }

            return null;
        }

        private static IEnumerable<string> GetJavaCandidates()
        {
            string? javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");

            if (!string.IsNullOrWhiteSpace(javaHome))
            {
                yield return javaHome;
            }

            foreach (string candidate in FindToolDirectories("jdk"))
            {
                yield return candidate;
            }

            foreach (string candidate in FindToolDirectories("jre"))
            {
                yield return candidate;
            }

            foreach (string candidate in FindToolDirectories("java"))
            {
                yield return candidate;
            }

            foreach (string candidate in FindProgramFilesJavaDirectories())
            {
                yield return candidate;
            }

            string? whereJava = TryFindOnPath("java.exe");

            if (!string.IsNullOrWhiteSpace(whereJava))
            {
                yield return whereJava;
            }
        }

        private static IEnumerable<string> FindToolDirectories(string namePart)
        {
            var roots = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "tools"),
                PathResolver.ResolveDirectory("tools")
            }
            .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (string root in roots)
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                foreach (string directory in SafeEnumerateDirectories(root, "*" + namePart + "*", true).Take(20))
                {
                    yield return directory;
                }
            }
        }

        private static IEnumerable<string> FindProgramFilesJavaDirectories()
        {
            foreach (string root in GetProgramFilesRoots())
            {
                foreach (string vendor in new[] { "Java", "Eclipse Adoptium", "Microsoft", "Zulu", "Amazon Corretto" })
                {
                    string vendorPath = Path.Combine(root, vendor);

                    if (!Directory.Exists(vendorPath))
                    {
                        continue;
                    }

                    foreach (string directory in SafeEnumerateDirectories(vendorPath, "*jdk*", false).Take(20))
                    {
                        yield return directory;
                    }
                }
            }
        }

        private static IEnumerable<string> SafeEnumerateDirectories(string root, string searchPattern, bool recursive)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return Enumerable.Empty<string>();
            }

            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = recursive,
                ReturnSpecialDirectories = false
            };

            try
            {
                return Directory.GetDirectories(root, searchPattern, options);
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        private static IEnumerable<string> SafeEnumerateFiles(string root, string searchPattern, bool recursive)
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                return Enumerable.Empty<string>();
            }

            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = recursive,
                ReturnSpecialDirectories = false
            };

            try
            {
                return Directory.GetFiles(root, searchPattern, options);
            }
            catch
            {
                return Enumerable.Empty<string>();
            }
        }

        private static IEnumerable<string> GetProgramFilesRoots()
        {
            string? pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string? pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            if (!string.IsNullOrWhiteSpace(pf))
            {
                yield return pf;
            }

            if (!string.IsNullOrWhiteSpace(pf86) &&
                !string.Equals(pf86, pf, StringComparison.OrdinalIgnoreCase))
            {
                yield return pf86;
            }
        }

        private static string? NormalizeJavaHome(string path)
        {
            path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));

            if (File.Exists(path) &&
                Path.GetFileName(path).Equals("java.exe", StringComparison.OrdinalIgnoreCase))
            {
                path = Directory.GetParent(path)?.Parent?.FullName ?? path;
            }

            if (Directory.Exists(Path.Combine(path, "bin")) &&
                File.Exists(Path.Combine(path, "bin", "java.exe")))
            {
                return Path.GetFullPath(path);
            }

            if (Directory.Exists(path))
            {
                string? java = SafeEnumerateFiles(path, "java.exe", true)
                    .FirstOrDefault(x => x.EndsWith(Path.Combine("bin", "java.exe"), StringComparison.OrdinalIgnoreCase));

                if (java != null)
                {
                    return Directory.GetParent(java)?.Parent?.FullName;
                }
            }

            return null;
        }

        private static string? ToJavaBin(string? javaHome)
        {
            if (string.IsNullOrWhiteSpace(javaHome))
            {
                return null;
            }

            string bin = Path.Combine(javaHome, "bin");

            return Directory.Exists(bin) ? bin : null;
        }

        private static string? TryFindOnPath(string command)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c where " + command,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();

                string output = process.StandardOutput.ReadLine() ?? string.Empty;

                process.WaitForExit(3000);

                return process.HasExited && process.ExitCode == 0 && File.Exists(output)
                    ? output
                    : null;
            }
            catch
            {
                return null;
            }
        }
    }
}