using System;
using System.Collections.Generic;
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
                inbounds.Add(new Dictionary<string, object>
                {
                    ["type"] = "tun",
                    ["tag"] = "tun-in",
                    ["interface_name"] = "bunker-tun",
                    ["inet4_address"] = "172.19.0.1/30",
                    ["inet6_address"] = "fdfe:dcba:9876::1/126",
                    ["auto_route"] = true,
                    ["strict_route"] = true,
                    ["stack"] = "system",
                    ["sniff"] = true
                });
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
                },
                new Dictionary<string, object>
                {
                    ["type"] = "dns",
                    ["tag"] = "dns-out"
                }
            };

            var routeRules = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["protocol"] = "dns",
                    ["outbound"] = "dns-out"
                },
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
                    ["geosite"] = new[] { "category-ru", "yandex", "vk", "mailru", "sberbank", "tinkoff", "gosuslugi" },
                    ["outbound"] = "direct"
                });
                routeRules.Add(new Dictionary<string, object>
                {
                    ["geoip"] = new[] { "ru" },
                    ["outbound"] = "direct"
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
                            ["address_resolver"] = "dns-direct"
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
                        new Dictionary<string, object>
                        {
                            ["outbound"] = "any",
                            ["server"] = "dns-direct"
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
            else if (raw.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
            {
                outbound["type"] = "vless";
                outbound["server"] = node.Server;
                outbound["server_port"] = node.Port;

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
                    outbound["tls"] = new Dictionary<string, object>
                    {
                        ["enabled"] = true,
                        ["server_name"] = query["sni"] ?? "",
                        ["insecure"] = query["allowInsecure"] == "1"
                    };
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
