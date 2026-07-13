using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Widgicity
{
    public static class ProcessGate
    {
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

        public static bool IsProcessFocused(string? processName)
        {
            if (string.IsNullOrWhiteSpace(processName)) return false;

            string trimmed = processName.Trim();
            if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed[..^4];

            IntPtr hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return false;

            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid == 0) return false;

            try
            {
                using var proc = Process.GetProcessById((int)pid);
                return string.Equals(proc.ProcessName, trimmed, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // Process may have exited between the calls above; treat as not focused
                return false;
            }
        }
    }
}