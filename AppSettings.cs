using Microsoft.Win32;
using System;
using System.Diagnostics;

namespace Widgicity
{
    public class AppSettings
    {
        public bool LaunchAtStartup { get; set; } = false;
        public bool StartMinimizedToTray { get; set; } = false;
        public int ProcessCheckIntervalSeconds { get; set; } = 2;

        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "Widgicity";

        public static void ApplyLaunchAtStartup(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key == null) return;

            if (enable)
            {
                string? exePath = Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(RunValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(RunValueName, throwOnMissingValue: false);
            }
        }
    }
}