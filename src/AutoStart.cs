using System;
using System.Reflection;
using Microsoft.Win32;

namespace DesktopTodo
{
    public static class AutoStart
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "DesktopTodo";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey, false))
                {
                    if (key == null) return false;
                    return key.GetValue(ValueName) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key == null) return;
                    if (enabled)
                    {
                        string exePath = Assembly.GetExecutingAssembly().Location;
                        key.SetValue(ValueName, "\"" + exePath + "\"");
                    }
                    else
                    {
                        key.DeleteValue(ValueName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log("AutoStart.SetEnabled failed: " + ex.Message);
            }
        }
    }
}
