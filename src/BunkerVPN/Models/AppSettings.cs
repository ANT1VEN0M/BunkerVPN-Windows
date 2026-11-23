using System;
using System.IO;
using System.Text.Json;

namespace BunkerVPN.Models
{
    public class AppSettings
    {
        public string SelectedFeedUrl { get; set; } = "https://raw.githubusercontent.com/ANT1VEN0M/Bunker-Auto-Engine/main/sub/mix.txt";
        public string CustomSubscriptionUrl { get; set; } = "";
        public string VpnMode { get; set; } = "TUN"; // "TUN" or "SystemProxy"
        public bool BypassRussianSites { get; set; } = true;
        public bool GameBoosterMode { get; set; } = true; // Enabled by default for Discord and Gaming stability
        public bool AutoConnectOnStartup { get; set; } = false;
        public bool StartWithWindows { get; set; } = false;
        public bool MinimizeToTrayOnClose { get; set; } = true;
        public bool AutoCheckUpdates { get; set; } = true;
        public bool AutoSortByPing { get; set; } = true;
        public string LastSelectedServerId { get; set; } = "";
        public string PrimaryDns { get; set; } = "1.1.1.1";
        public string SecondaryDns { get; set; } = "77.88.8.8";

        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BunkerVPN",
            "settings.json"
        );

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch
            {
                // Fallback to defaults
            }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Ignore save errors
            }
        }
    }
}
