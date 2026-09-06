using System;
using Microsoft.Win32;

namespace WAHU.Platform
{
    public static class StartupRegistrationService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "WAHU Kids Learn";

        public static bool IsEnabled(string appExePath)
        {
            if (string.IsNullOrWhiteSpace(appExePath)) return false;
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null) return false;
                    var value = key.GetValue(ValueName) as string;
                    return string.Equals(value, BuildCommand(appExePath), StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { return false; }
        }

        public static void SetEnabled(string appExePath, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(appExePath)) throw new ArgumentException("appExePath");
            using (var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, RegistryKeyPermissionCheck.ReadWriteSubTree))
            {
                if (key == null) throw new InvalidOperationException("Không mở được HKCU Run key.");
                if (enabled)
                    key.SetValue(ValueName, BuildCommand(appExePath), RegistryValueKind.String);
                else
                    key.DeleteValue(ValueName, false);
            }
        }

        public static string BuildCommand(string appExePath)
        {
            if (string.IsNullOrWhiteSpace(appExePath)) throw new ArgumentException("appExePath");
            var full = System.IO.Path.GetFullPath(appExePath);
            return "\"" + full.Replace("\"", "\\\"") + "\" --startup";
        }
    }
}
