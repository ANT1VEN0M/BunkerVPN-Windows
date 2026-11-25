using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BunkerVPN.Models;

namespace BunkerVPN.Services
{
    public class BunkerFeedItem
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string Url { get; set; } = "";
        public string Icon { get; set; } = "";
    }

    public class SubscriptionService
    {
        private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromSeconds(25) };

        public static readonly List<BunkerFeedItem> DefaultFeeds = new()
        {
            new() { Title = "👑 Золотая Выборка VIP", Description = "Протестированные TOP-серверы VLESS Reality & Hysteria 2 с максимальной скоростью", Url = "https://cdn.jsdelivr.net/gh/ANT1VEN0M/Bunker-Auto-Engine@main/sub/gold.txt", Icon = "👑" },
            new() { Title = "Мировой Микс (YouTube / DPI)", Description = "70 проверенных серверов + WARP для всех заблокированных сайтов", Url = "https://cdn.jsdelivr.net/gh/ANT1VEN0M/Bunker-Auto-Engine@main/sub/mix.txt", Icon = "🌍" },
            new() { Title = "Discord Voice & Gaming", Description = "Full-Cone UDP для мгновенного подключения голосовых каналов Discord", Url = "https://cdn.jsdelivr.net/gh/ANT1VEN0M/Bunker-Auto-Engine@main/sub/discord.txt", Icon = "🎮" },
            new() { Title = "Турбо-серверы (Минимальный пинг)", Description = "Быстрые сервера с минимальной задержкой для игр и стриминга", Url = "https://cdn.jsdelivr.net/gh/ANT1VEN0M/Bunker-Auto-Engine@main/sub/fast.txt", Icon = "⚡" },
            new() { Title = "Белый список (RU / BY)", Description = "Серверы в белых списках хостингов РФ для обхода глухих блокировок", Url = "https://cdn.jsdelivr.net/gh/ANT1VEN0M/Bunker-Auto-Engine@main/sub/bs.txt", Icon = "🤍" },
            new() { Title = "Cloudflare WARP+ (Порт 880)", Description = "Прямой WireGuard туннель Cloudflare с защитой от ТСПУ", Url = "https://cdn.jsdelivr.net/gh/ANT1VEN0M/Bunker-Auto-Engine@main/sub/warp.txt", Icon = "🚀" },
            new() { Title = "Завод / Проходная (443 & 53)", Description = "Для работы в строгих корпоративных сетях предприятий", Url = "https://cdn.jsdelivr.net/gh/ANT1VEN0M/Bunker-Auto-Engine@main/sub/factory.txt", Icon = "🏭" }
        };

        private static readonly string CachePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BunkerVPN",
            "servers_cache.json"
        );

        public async Task<List<ServerNode>> FetchSubscriptionAsync(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return new List<ServerNode>();

            var candidateUrls = new List<string> { url };
            if (url.Contains("raw.githubusercontent.com/ANT1VEN0M/Bunker-Auto-Engine/main/"))
            {
                var jsd = url.Replace("raw.githubusercontent.com/ANT1VEN0M/Bunker-Auto-Engine/main/", "cdn.jsdelivr.net/gh/ANT1VEN0M/Bunker-Auto-Engine@main/");
                candidateUrls.Insert(0, jsd);
            }
            if (url.StartsWith("https://raw.githubusercontent.com/", StringComparison.OrdinalIgnoreCase))
            {
                candidateUrls.Add($"https://ghfast.top/{url}");
            }

            foreach (var targetUrl in candidateUrls)
            {
                try
                {
                    var content = await HttpClient.GetStringAsync(targetUrl);
                    content = content.Trim();

                    // If content is Base64, decode it
                    if (!content.Contains("://") && content.Length > 20)
                    {
                        try
                        {
                            var bytes = Convert.FromBase64String(content);
                            content = Encoding.UTF8.GetString(bytes);
                        }
                        catch
                        {
                            // Not base64, proceed as raw
                        }
                    }

                    var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                    var nodes = new List<ServerNode>();

                    foreach (var line in lines)
                    {
                        var node = ServerNode.FromUri(line);
                        if (node != null)
                        {
                            nodes.Add(node);
                        }
                    }

                    if (nodes.Count > 0)
                    {
                        SaveCache(nodes);
                        return nodes;
                    }
                }
                catch
                {
                    // Fallback to next mirror
                }
            }

            return LoadCache();
        }

        public List<ServerNode> LoadCache()
        {
            try
            {
                if (File.Exists(CachePath))
                {
                    var json = File.ReadAllText(CachePath);
                    return JsonSerializer.Deserialize<List<ServerNode>>(json) ?? new List<ServerNode>();
                }
            }
            catch
            {
                // Ignore load error
            }
            return new List<ServerNode>();
        }

        public void SaveCache(List<ServerNode> nodes)
        {
            try
            {
                var dir = Path.GetDirectoryName(CachePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                var json = JsonSerializer.Serialize(nodes, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(CachePath, json);
            }
            catch
            {
                // Ignore save error
            }
        }
    }
}
