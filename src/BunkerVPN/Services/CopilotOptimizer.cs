using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace BunkerVPN.Services
{
    /// <summary>
    /// Service to manage and permanently disable Microsoft Copilot autostart and background bloatware
    /// using Windows AppModel startupTask policies and Group Policy overrides.
    /// Does NOT delete or corrupt Copilot - allows manual launch while preventing auto-boot.
    /// </summary>
    public static class CopilotOptimizer
    {
        private const string AppModelBasePath = @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\SystemAppData";
        private const string PolicyCopilot = @"Software\Policies\Microsoft\Windows\WindowsCopilot";
        private const string PolicyAI = @"Software\Policies\Microsoft\Windows\WindowsAI";

        public enum StartupTaskState
        {
            Disabled = 0,
            DisabledByUser = 1,
            Enabled = 2,
            DisabledByPolicy = 3,
            EnabledByPolicy = 4,
            DisabledByUserAndPolicy = 5,
            NotFound = -1
        }

        /// <summary>
        /// Checks if Copilot autostart is blocked or disabled by policy.
        /// </summary>
        public static bool IsCopilotAutostartDisabled()
        {
            try
            {
                // Check Group Policy
                using (var hkcuKey = Registry.CurrentUser.OpenSubKey(PolicyCopilot, false))
                {
                    if (hkcuKey?.GetValue("TurnOffWindowsCopilot") is int v && v == 1) return true;
                }

                using (var hklmKey = Registry.LocalMachine.OpenSubKey(PolicyCopilot, false))
                {
                    if (hklmKey?.GetValue("TurnOffWindowsCopilot") is int v && v == 1) return true;
                }

                // Check AppModel SystemAppData startupTask state
                using var baseKey = Registry.CurrentUser.OpenSubKey(AppModelBasePath, false);
                if (baseKey != null)
                {
                    foreach (var familyName in baseKey.GetSubKeyNames())
                    {
                        if (familyName.IndexOf("Copilot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            familyName.IndexOf("OfficeHub", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            using var famKey = baseKey.OpenSubKey(familyName, false);
                            if (famKey == null) continue;

                            foreach (var taskId in famKey.GetSubKeyNames())
                            {
                                using var taskKey = famKey.OpenSubKey(taskId, false);
                                if (taskKey?.GetValue("State") is int state && (state == 3 || state == 5 || state == 1 || state == 0))
                                {
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CopilotOptimizer] Check error: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Permanently disables Copilot autostart by enforcing DisabledByPolicy (State=3)
        /// and activating Windows Group Policy TurnOffWindowsCopilot=1.
        /// </summary>
        public static bool DisableCopilotAutostart()
        {
            bool success = false;

            try
            {
                // 1. Set Group Policy overrides (HKCU & HKLM)
                SetPolicy(Registry.CurrentUser, PolicyCopilot, "TurnOffWindowsCopilot", 1);
                SetPolicy(Registry.LocalMachine, PolicyCopilot, "TurnOffWindowsCopilot", 1);
                SetPolicy(Registry.LocalMachine, PolicyAI, "TurnOffWindowsCopilot", 1);

                // 2. Set AppModel startup tasks to DisabledByPolicy (State = 3)
                using var baseKey = Registry.CurrentUser.OpenSubKey(AppModelBasePath, true);
                if (baseKey != null)
                {
                    foreach (var familyName in baseKey.GetSubKeyNames())
                    {
                        if (familyName.IndexOf("Copilot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            familyName.IndexOf("OfficeHub", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            using var famKey = baseKey.OpenSubKey(familyName, true);
                            if (famKey == null) continue;

                            foreach (var taskId in famKey.GetSubKeyNames())
                            {
                                using var taskKey = famKey.OpenSubKey(taskId, true);
                                if (taskKey != null && taskKey.GetValue("State") != null)
                                {
                                    taskKey.SetValue("State", 3, RegistryValueKind.DWord);
                                    taskKey.SetValue("UserEnabledStartupOnce", 0, RegistryValueKind.DWord);
                                    success = true;
                                }
                            }
                        }
                    }
                }

                success = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CopilotOptimizer] Disable failed: {ex.Message}");
            }

            return success;
        }

        /// <summary>
        /// Restores Copilot autostart to default system state.
        /// </summary>
        public static bool RestoreCopilotAutostart()
        {
            try
            {
                // 1. Remove or reset Group Policy
                RemovePolicy(Registry.CurrentUser, PolicyCopilot, "TurnOffWindowsCopilot");
                RemovePolicy(Registry.LocalMachine, PolicyCopilot, "TurnOffWindowsCopilot");
                RemovePolicy(Registry.LocalMachine, PolicyAI, "TurnOffWindowsCopilot");

                // 2. Reset AppModel startup tasks to Enabled (State = 2)
                using var baseKey = Registry.CurrentUser.OpenSubKey(AppModelBasePath, true);
                if (baseKey != null)
                {
                    foreach (var familyName in baseKey.GetSubKeyNames())
                    {
                        if (familyName.IndexOf("Copilot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            familyName.IndexOf("OfficeHub", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            using var famKey = baseKey.OpenSubKey(familyName, true);
                            if (famKey == null) continue;

                            foreach (var taskId in famKey.GetSubKeyNames())
                            {
                                using var taskKey = famKey.OpenSubKey(taskId, true);
                                if (taskKey != null && taskKey.GetValue("State") != null)
                                {
                                    taskKey.SetValue("State", 2, RegistryValueKind.DWord);
                                }
                            }
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CopilotOptimizer] Restore failed: {ex.Message}");
                return false;
            }
        }

        private static void SetPolicy(RegistryKey root, string subKey, string valueName, int value)
        {
            try
            {
                using var key = root.CreateSubKey(subKey, true);
                key?.SetValue(valueName, value, RegistryValueKind.DWord);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CopilotOptimizer] SetPolicy failed on {subKey}: {ex.Message}");
            }
        }

        private static void RemovePolicy(RegistryKey root, string subKey, string valueName)
        {
            try
            {
                using var key = root.OpenSubKey(subKey, true);
                key?.DeleteValue(valueName, false);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[CopilotOptimizer] RemovePolicy failed on {subKey}: {ex.Message}");
            }
        }
    }
}
