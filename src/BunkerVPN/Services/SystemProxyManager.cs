using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace BunkerVPN.Services
{
    public static class SystemProxyManager
    {
        [DllImport("wininet.dll")]
        private static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        private const int INTERNET_OPTION_REFRESH = 37;

        private const string UserRoot = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Internet Settings";

        public static void SetSystemProxy(string host = "127.0.0.1", int port = 2080)
        {
            try
            {
                var proxyServer = $"{host}:{port}";
                var proxyOverride = "<local>;localhost;127.*;10.*;172.16.*;192.168.*";

                Registry.SetValue(UserRoot, "ProxyEnable", 1, RegistryValueKind.DWord);
                Registry.SetValue(UserRoot, "ProxyServer", proxyServer, RegistryValueKind.String);
                Registry.SetValue(UserRoot, "ProxyOverride", proxyOverride, RegistryValueKind.String);

                NotifyWindows();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to set system proxy: {ex.Message}");
            }
        }

        public static void ClearSystemProxy()
        {
            try
            {
                Registry.SetValue(UserRoot, "ProxyEnable", 0, RegistryValueKind.DWord);
                NotifyWindows();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to clear system proxy: {ex.Message}");
            }
        }

        private static void NotifyWindows()
        {
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
        }
    }
}
