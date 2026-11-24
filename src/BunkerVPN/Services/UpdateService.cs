using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace BunkerVPN.Services
{
    public class UpdateInfo
    {
        public bool HasUpdate { get; set; }
        public string LatestVersion { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string ReleaseNotes { get; set; } = "";
    }

    public class UpdateService
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        private static readonly HttpClient DownloadClient = new()
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        static UpdateService()
        {
            if (!HttpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                HttpClient.DefaultRequestHeaders.Add("User-Agent", "BunkerVPN-Windows");
            }
            if (!DownloadClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                DownloadClient.DefaultRequestHeaders.Add("User-Agent", "BunkerVPN-Windows");
            }
        }

        public static string CurrentVersion => "1.0.11";

        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            var info = new UpdateInfo();

            var apiUrls = new[]
            {
                "https://api.github.com/repos/ANT1VEN0M/BunkerVPN-Windows/releases/latest",
                "https://ghfast.top/https://api.github.com/repos/ANT1VEN0M/BunkerVPN-Windows/releases/latest"
            };

            foreach (var url in apiUrls)
            {
                try
                {
                    var json = await HttpClient.GetStringAsync(url);
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    var tagName = root.GetProperty("tag_name").GetString() ?? "";
                    var cleanTag = tagName.TrimStart('v', 'V');
                    var body = root.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "";

                    if (IsNewerVersion(cleanTag, CurrentVersion))
                    {
                        info.HasUpdate = true;
                        info.LatestVersion = tagName;
                        info.ReleaseNotes = body;

                        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var a in assets.EnumerateArray())
                            {
                                var name = a.GetProperty("name").GetString() ?? "";
                                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                                {
                                    info.DownloadUrl = a.GetProperty("browser_download_url").GetString() ?? "";
                                    break;
                                }
                            }
                        }

                        if (!string.IsNullOrEmpty(info.DownloadUrl))
                        {
                            return info;
                        }
                    }
                }
                catch
                {
                    // Fallback to next mirror
                }
            }

            return info;
        }

        public async Task<string> DownloadUpdateAsync(string downloadUrl, IProgress<int>? progress = null)
        {
            var tempSetup = Path.Combine(Path.GetTempPath(), "BunkerVPN_Setup_Update.exe");

            var targetUrl = downloadUrl.StartsWith("https://github.com", StringComparison.OrdinalIgnoreCase)
                ? $"https://ghfast.top/{downloadUrl}"
                : downloadUrl;

            using var resp = await DownloadClient.GetAsync(targetUrl, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();

            var totalBytes = resp.Content.Headers.ContentLength ?? -1L;
            using var stream = await resp.Content.ReadAsStreamAsync();
            using var fs = new FileStream(tempSetup, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long totalRead = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fs.WriteAsync(buffer, 0, read);
                totalRead += read;
                if (totalBytes > 0 && progress != null)
                {
                    var pct = (int)((totalRead * 100) / totalBytes);
                    progress.Report(pct);
                }
            }

            return tempSetup;
        }

        public void LaunchInstallerAndExit(string installerPath)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = installerPath,
                Arguments = "/SILENT /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS",
                UseShellExecute = true
            });

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                (System.Windows.Application.Current as App)?.ExitApplication();
            });
        }

        private static bool IsNewerVersion(string newVer, string curVer)
        {
            try
            {
                var vNew = new Version(newVer);
                var vCur = new Version(curVer);
                return vNew > vCur;
            }
            catch
            {
                return string.Compare(newVer, curVer, StringComparison.OrdinalIgnoreCase) > 0;
            }
        }
    }
}
