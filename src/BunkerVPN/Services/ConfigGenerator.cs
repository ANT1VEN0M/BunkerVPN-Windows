using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Web;
using BunkerVPN.Models;

namespace BunkerVPN.Services
{
    public static class ConfigGenerator
    {
        public static string GenerateSingBoxConfig(ServerNode node, AppSettings settings)
        {
            var isTun = settings.VpnMode.Equals("TUN", StringComparison.OrdinalIgnoreCase);

            var inbounds = new List<object>();

            if (isTun)
            {
                var tunDict = new Dictionary<string, object>
                {
                    ["type"] = "tun",
                    ["tag"] = "tun-in",
                    ["interface_name"] = "bunker-tun",
                    ["address"] = new[] { "172.19.0.1/30", "fdfe:dcba:9876::1/126" },
                    ["auto_route"] = true,
                    ["strict_route"] = false,
                    ["stack"] = "gvisor", // gvisor gives 100% reliable userspace network stack on Windows without WFP drops
                    ["mtu"] = settings.GameBoosterMode ? 1340 : 1360,
                    ["sniff"] = true
                };

                // Game Booster: MTU 1340 avoids UDP fragmentation on LTE/GPON, Full-Cone NAT for Discord Voice & P2P
                if (settings.GameBoosterMode)
                {
                    tunDict["endpoint_independent_nat"] = true;
                    tunDict["udp_timeout"] = 300;
                }

                inbounds.Add(tunDict);
            }

            // Always add mixed inbound for local tools / fallback proxy
            inbounds.Add(new Dictionary<string, object>
            {
                ["type"] = "mixed",
                ["tag"] = "mixed-in",
                ["listen"] = "127.0.0.1",
                ["listen_port"] = 2080,
                ["sniff"] = true
            });

            var proxyOutbound = BuildOutboundFromNode(node);

            var outbounds = new List<object>
            {
                proxyOutbound,
                new Dictionary<string, object>
                {
                    ["type"] = "direct",
                    ["tag"] = "direct"
                }
            };

            var routeRules = new List<object>
            {
                // 1. Hijack DNS (both port 53 and protocol dns for instant interception)
                new Dictionary<string, object>
                {
                    ["port"] = new[] { 53 },
                    ["action"] = "hijack-dns"
                },
                new Dictionary<string, object>
                {
                    ["protocol"] = "dns",
                    ["action"] = "hijack-dns"
                },
                // 2. Direct route for Windows NCSI connectivity checks (Fixes Windows "Без доступа к Интернету")
                new Dictionary<string, object>
                {
                    ["domain"] = new[]
                    {
                        "msftconnecttest.com",
                        "www.msftconnecttest.com",
                        "msftncsi.com",
                        "www.msftncsi.com",
                        "ipv6.msftconnecttest.com"
                    },
                    ["outbound"] = "direct"
                },
                // 3. Private IP ranges direct
                new Dictionary<string, object>
                {
                    ["ip_is_private"] = true,
                    ["outbound"] = "direct"
                }
            };

            if (settings.BypassRussianSites)
            {
                routeRules.Add(new Dictionary<string, object>
                {
                    ["domain_suffix"] = new[]
                    {
                        ".ru",
                        ".su",
                        ".xn--p1ai",
                        "yandex.ru",
                        "yandex.net",
                        "ya.ru",
                        "vk.com",
                        "vk.me",
                        "mail.ru",
                        "sberbank.ru",
                        "sber.ru",
                        "tinkoff.ru",
                        "tbank.ru",
                        "gosuslugi.ru",
                        "mos.ru",
                        "kinopoisk.ru",
                        "ozon.ru",
                        "wildberries.ru",
                        "avito.ru",
                        "rutube.ru",
                        "dzen.ru",
                        "2gis.ru"
                    },
                    ["outbound"] = "direct"
                });
            }

            if (settings.GameBoosterMode)
            {
                // 1. Direct routes for low-latency game servers & gigabit CDNs (Zero Ping & Max Download)
                routeRules.Add(new Dictionary<string, object>
                {
                    ["domain_suffix"] = new[]
                    {
                        "steamcontent.com",
                        "steamserver.net",
                        "valve.net",
                        "valvesoftware.com",
                        "epicgames.com",
                        "ea.com",
                        "origin.com",
                        "blizzard.com",
                        "battle.net",
                        "riotgames.com",
                        "leagueoflegends.com",
                        "pvp.net",
                        "ubisoft.com",
                        "ubi.com",
                        "wargaming.net",
                        "lesta.ru",
                        "tanki.su",
                        "korabli.su",
                        "vkplay.ru"
                    },
                    ["outbound"] = "direct"
                });

                // 2. Proxy routes for blocked voice, game auth, and game servers
                // Discord Voice & RTC (UDP high ports + domains)
                routeRules.Add(new Dictionary<string, object>
                {
                    ["network"] = "udp",
                    ["port_range"] = new[] { "50000:65535" },
                    ["outbound"] = "proxy"
                });

                // Supercell (Brawl Stars, Clash of Clans), Roblox, Discord, Steam Community
                routeRules.Add(new Dictionary<string, object>
                {
                    ["domain_suffix"] = new[]
                    {
                        "discord.com",
                        "discord.gg",
                        "discordapp.com",
                        "discordapp.net",
                        "discordstatus.com",
                        "brawlstarsgame.com",
                        "supercell.com",
                        "supercellid.com",
                        "roblox.com",
                        "rbxcdn.com",
                        "steamcommunity.com",
                        "steam-chat.com"
                    },
                    ["outbound"] = "proxy"
                });
            }

            var config = new Dictionary<string, object>
            {
                ["log"] = new Dictionary<string, object>
                {
                    ["level"] = "info",
                    ["timestamp"] = true
                },
                ["dns"] = new Dictionary<string, object>
                {
                    ["servers"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["tag"] = "dns-remote",
                            ["address"] = $"https://{settings.PrimaryDns}/dns-query",
                            ["address_resolver"] = "dns-direct",
                            ["detour"] = "proxy"
                        },
                        new Dictionary<string, object>
                        {
                            ["tag"] = "dns-remote-fallback",
                            ["address"] = "https://8.8.8.8/dns-query",
                            ["address_resolver"] = "dns-direct",
                            ["detour"] = "proxy"
                        },
                        new Dictionary<string, object>
                        {
                            ["tag"] = "dns-direct",
                            ["address"] = settings.SecondaryDns,
                            ["detour"] = "direct"
                        }
                    },
                    ["rules"] = new object[]
                    {
                        // 1. Windows NCSI connectivity checks always resolve directly
                        new Dictionary<string, object>
                        {
                            ["domain"] = new[]
                            {
                                "msftconnecttest.com",
                                "www.msftconnecttest.com",
                                "msftncsi.com",
                                "www.msftncsi.com",
                                "ipv6.msftconnecttest.com"
                            },
                            ["server"] = "dns-direct"
                        },
                        // 2. Russian sites direct resolution
                        new Dictionary<string, object>
                        {
                            ["domain_suffix"] = new[]
                            {
                                ".ru", ".su", ".xn--p1ai",
                                "yandex.ru", "yandex.net", "ya.ru",
                                "vk.com", "vk.me", "mail.ru",
                                "sberbank.ru", "sber.ru", "tinkoff.ru", "tbank.ru",
                                "gosuslugi.ru", "mos.ru", "kinopoisk.ru", "ozon.ru", "wildberries.ru",
                                "avito.ru", "rutube.ru", "dzen.ru", "2gis.ru"
                            },
                            ["server"] = "dns-direct"
                        },
                        // 3. Fallback: all foreign & proxy traffic to remote DoH
                        new Dictionary<string, object>
                        {
                            ["outbound"] = "any",
                            ["server"] = "dns-remote"
                        }
                    },
                    ["strategy"] = "prefer_ipv4"
                },
                ["inbounds"] = inbounds,
                ["outbounds"] = outbounds,
                ["route"] = new Dictionary<string, object>
                {
                    ["rules"] = routeRules,
                    ["auto_detect_interface"] = true,
                    ["final"] = "proxy"
                }
            };

            return JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        }

        private static Dictionary<string, object> BuildOutboundFromNode(ServerNode node)
        {
            var raw = node.RawUri;
            var outbound = new Dictionary<string, object>
            {
                ["tag"] = "proxy"
            };

            if (raw.StartsWith("wireguard://", StringComparison.OrdinalIgnoreCase))
            {
                outbound["type"] = "wireguard";
                outbound["server"] = node.Server;
                outbound["server_port"] = node.Port;
                outbound["system_interface"] = false;

                var uri = new Uri(raw);
                var query = HttpUtility.ParseQueryString(uri.Query);

                var privKey = HttpUtility.UrlDecode(uri.UserInfo);
                var pubKey = query["publickey"] ?? query["public_key"] ?? "";
                var address = query["address"] ?? "172.16.0.2/32";
                var reservedStr = query["reserved"] ?? "";

                outbound["private_key"] = privKey;
                outbound["peer_public_key"] = pubKey;
                outbound["local_address"] = address.Split(',');
                outbound["mtu"] = 1280;

                if (!string.IsNullOrEmpty(reservedStr))
                {
                    var parts = reservedStr.Split(',');
                    var reservedBytes = new List<int>();
                    foreach (var p in parts)
                    {
                        if (int.TryParse(p.Trim(), out int b)) reservedBytes.Add(b);
                    }
                    if (reservedBytes.Count == 3)
                    {
                        outbound["reserved"] = reservedBytes;
                    }
                }
                return outbound;
            }
            else if (raw.StartsWith("ss://", StringComparison.OrdinalIgnoreCase))
            {
                outbound["type"] = "shadowsocks";
                outbound["server"] = node.Server;
                outbound["server_port"] = node.Port;
                outbound["method"] = "aes-256-gcm";
                outbound["password"] = "password";

                try
                {
                    var atIdx = raw.IndexOf('@');
                    if (atIdx > 5)
                    {
                        var userPart = raw[5..atIdx];
                        // If base64 encoded method:password
                        try
                        {
                            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(userPart));
                            var colon = decoded.IndexOf(':');
                            if (colon > 0)
                            {
                                outbound["method"] = decoded[..colon];
                                outbound["password"] = decoded[(colon + 1)..];
                            }
                        }
                        catch
                        {
                            var colon = userPart.IndexOf(':');
                            if (colon > 0)
                            {
                                outbound["method"] = userPart[..colon];
                                outbound["password"] = userPart[(colon + 1)..];
                            }
                        }
                    }
                }
                catch
                {
                    // Use fallback defaults
                }

                return outbound;
            }
            else if (raw.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
            {
                outbound["type"] = "vless";
                outbound["server"] = node.Server;
                outbound["server_port"] = node.Port;
                outbound["packet_encoding"] = "xudp"; // CRITICAL: multiplexes UDP over TCP for DNS, Discord Voice & Gaming

                var uri = new Uri(raw);
                var query = HttpUtility.ParseQueryString(uri.Query);

                outbound["uuid"] = uri.UserInfo;
                var flow = query["flow"] ?? "";
                if (!string.IsNullOrEmpty(flow)) outbound["flow"] = flow;

                var security = query["security"] ?? "none";
                if (security.Equals("reality", StringComparison.OrdinalIgnoreCase))
                {
                    outbound["tls"] = new Dictionary<string, object>
                    {
                        ["enabled"] = true,
                        ["server_name"] = query["sni"] ?? "",
                        ["reality"] = new Dictionary<string, object>
                        {
                            ["enabled"] = true,
                            ["public_key"] = query["pbk"] ?? "",
                            ["short_id"] = query["sid"] ?? ""
                        },
                        ["utls"] = new Dictionary<string, object>
                        {
                            ["enabled"] = true,
                            ["fingerprint"] = query["fp"] ?? "chrome"
                        }
                    };
                }
                else if (security.Equals("tls", StringComparison.OrdinalIgnoreCase))
                {
                    var tlsDict = new Dictionary<string, object>
                    {
                        ["enabled"] = true,
                        ["server_name"] = query["sni"] ?? "",
                        ["insecure"] = query["allowInsecure"] == "1"
                    };
                    var fp = query["fp"] ?? "chrome";
                    if (!string.IsNullOrEmpty(fp))
                    {
                        tlsDict["utls"] = new Dictionary<string, object>
                        {
                            ["enabled"] = true,
                            ["fingerprint"] = fp
                        };
                    }
                    outbound["tls"] = tlsDict;
                }

                var netType = query["type"] ?? "tcp";
                if (netType.Equals("ws", StringComparison.OrdinalIgnoreCase))
                {
                    outbound["transport"] = new Dictionary<string, object>
                    {
                        ["type"] = "ws",
                        ["path"] = query["path"] ?? "/"
                    };
                }
                else if (netType.Equals("grpc", StringComparison.OrdinalIgnoreCase))
                {
                    outbound["transport"] = new Dictionary<string, object>
                    {
                        ["type"] = "grpc",
                        ["service_name"] = query["serviceName"] ?? ""
                    };
                }
                else if (netType.Equals("xhttp", StringComparison.OrdinalIgnoreCase) || netType.Equals("httpupgrade", StringComparison.OrdinalIgnoreCase))
                {
                    outbound["transport"] = new Dictionary<string, object>
                    {
                        ["type"] = "httpupgrade",
                        ["host"] = query["host"] ?? query["sni"] ?? node.Server,
                        ["path"] = query["path"] ?? "/"
                    };
                }

                return outbound;
            }
            else if (raw.StartsWith("trojan://", StringComparison.OrdinalIgnoreCase))
            {
                outbound["type"] = "trojan";
                outbound["server"] = node.Server;
                outbound["server_port"] = node.Port;

                var uri = new Uri(raw);
                var query = HttpUtility.ParseQueryString(uri.Query);

                outbound["password"] = uri.UserInfo;
                outbound["tls"] = new Dictionary<string, object>
                {
                    ["enabled"] = true,
                    ["server_name"] = query["sni"] ?? node.Server,
                    ["insecure"] = query["allowInsecure"] == "1"
                };

                return outbound;
            }
            else if (raw.StartsWith("hy2://", StringComparison.OrdinalIgnoreCase) || raw.StartsWith("hysteria2://", StringComparison.OrdinalIgnoreCase))
            {
                outbound["type"] = "hysteria2";
                outbound["server"] = node.Server;
                outbound["server_port"] = node.Port;

                var uri = new Uri(raw);
                var query = HttpUtility.ParseQueryString(uri.Query);

                outbound["password"] = uri.UserInfo;
                outbound["tls"] = new Dictionary<string, object>
                {
                    ["enabled"] = true,
                    ["server_name"] = query["sni"] ?? node.Server,
                    ["insecure"] = query["insecure"] == "1"
                };

                return outbound;
            }

            // Fallback: direct
            return new Dictionary<string, object>
            {
                ["type"] = "direct",
                ["tag"] = "proxy"
            };
        }
    }
}
