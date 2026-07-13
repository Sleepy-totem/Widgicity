using System;
using System.Reflection;

namespace Widgicity
{
    public static class AppInfo
    {
        public static string VersionString
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);
                return $"v{version.Major}.{version.Minor}.{version.Build}";
            }
        }

        public static string DisplayTitle => $"Widgicity — {VersionString}";
    }
}