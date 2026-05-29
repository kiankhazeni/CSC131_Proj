using System;
using System.IO;

namespace UserInterface.Services
{
    public static class PathResolver
    {
        public static string ResolveDirectory(string directory)
        {
            if (string.IsNullOrWhiteSpace(directory))
                return AppContext.BaseDirectory;

            string expanded = Environment.ExpandEnvironmentVariables(directory.Trim());

            if (Path.IsPathRooted(expanded))
                return Path.GetFullPath(expanded);

            string fromAppBase = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, expanded));
            if (Directory.Exists(fromAppBase))
                return fromAppBase;

            string? current = AppContext.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(current))
            {
                string candidate = Path.GetFullPath(Path.Combine(current, expanded));
                if (Directory.Exists(candidate))
                    return candidate;

                current = Directory.GetParent(current)?.FullName;
            }

            return fromAppBase;
        }

        public static string ResolveFile(string file)
        {
            if (string.IsNullOrWhiteSpace(file))
                return string.Empty;

            string expanded = Environment.ExpandEnvironmentVariables(file.Trim());

            if (Path.IsPathRooted(expanded))
                return Path.GetFullPath(expanded);

            string fromAppBase = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, expanded));
            if (File.Exists(fromAppBase))
                return fromAppBase;

            string? current = AppContext.BaseDirectory;
            while (!string.IsNullOrWhiteSpace(current))
            {
                string candidate = Path.GetFullPath(Path.Combine(current, expanded));
                if (File.Exists(candidate))
                    return candidate;

                current = Directory.GetParent(current)?.FullName;
            }

            return fromAppBase;
        }
    }
}
