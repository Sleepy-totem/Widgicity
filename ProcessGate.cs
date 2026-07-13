using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Widgicity
{
    public static class ProcessGate
    {
        /// <summary>
        /// Returns true if a process with the given name is currently running.
        /// Only checks process existence — never reads process memory or window content.
        /// </summary>
        public static bool IsProcessRunning(string? processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return false;

            string trimmed = processName.Trim();
            if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed[..^4];

            try
            {
                var matches = Process.GetProcessesByName(trimmed);
                bool found = matches.Length > 0;
                foreach (var p in matches) p.Dispose();
                return found;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Lists currently running apps that have a visible main window, for the picker UI.
        /// Only reads process name + window title (both public, non-sensitive OS metadata).
        /// </summary>
        public static List<(string DisplayTitle, string ProcessName)> GetRunningApps()
        {
            var results = new List<(string, string)>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(p.MainWindowTitle))
                        results.Add(($"{p.MainWindowTitle} ({p.ProcessName}.exe)", p.ProcessName));
                }
                catch { /* some processes deny access; skip them */ }
                finally { p.Dispose(); }
            }
            results.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase));
            return results;
        }
    }
}