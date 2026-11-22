using System;
using System.Text.RegularExpressions;
using System.Web;

namespace BunkerVPN.Models
{
    public class ServerNode
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "Bunker Server";
        public string Protocol { get; set; } = "VLESS";
        public string Server { get; set; } = "";
        public int Port { get; set; } = 443;
        public int PingMs { get; set; } = -1;
        public string CountryCode { get; set; } = "UN";
        public string FlagEmoji { get; set; } = "🌐";
        public string RawUri { get; set; } = "";
        public bool IsWarp { get; set; } = false;

        public string PingDisplay => PingMs >= 0 ? $"{PingMs} ms" : "—";
        public string PingColor => PingMs switch
        {
            < 0 => "#6E7681",
            < 100 => "#00E676",
            < 250 => "#FFB300",
            _ => "#FF5252"
        };

        public string ProtocolBadgeColor => Protocol.ToUpperInvariant() switch
        {
            "VLESS" => "#00E5FF",
            "WIREGUARD" or "WARP" => "#FF6D00",
            "MASQUE" => "#D500F9",
            "TROJAN" => "#00E676",
            "HY2" or "HYSTERIA2" => "#FFD600",
            _ => "#7C4DFF"
        };

        public static ServerNode? FromUri(string rawUri)
        {
            if (string.IsNullOrWhiteSpace(rawUri)) return null;
            rawUri = rawUri.Trim();

            try
            {
                var node = new ServerNode { RawUri = rawUri };

                if (rawUri.StartsWith("wireguard://", StringComparison.OrdinalIgnoreCase))
                {
                    node.Protocol = "WireGuard";
                    node.IsWarp = rawUri.Contains("WARP", StringComparison.OrdinalIgnoreCase) || rawUri.Contains(":880");
                    ExtractHostPortName(rawUri, node);
                    return node;
                }
                else if (rawUri.StartsWith("warp://", StringComparison.OrdinalIgnoreCase))
                {
                    // Note: Official sing-box core does not support Hiddify-specific warp:// MASQUE scheme.
                    // Cloudflare WARP on Windows PC is fully provided via native wireguard:// on port 880.
                    return null;
                }
                else if (rawUri.StartsWith("vless://", StringComparison.OrdinalIgnoreCase))
                {
                    node.Protocol = "VLESS";
                    ExtractHostPortName(rawUri, node);
                    return node;
                }
                else if (rawUri.StartsWith("trojan://", StringComparison.OrdinalIgnoreCase))
                {
                    node.Protocol = "Trojan";
                    ExtractHostPortName(rawUri, node);
                    return node;
                }
                else if (rawUri.StartsWith("ss://", StringComparison.OrdinalIgnoreCase))
                {
                    node.Protocol = "Shadowsocks";
                    ExtractHostPortName(rawUri, node);
                    return node;
                }
                else if (rawUri.StartsWith("hy2://", StringComparison.OrdinalIgnoreCase) || rawUri.StartsWith("hysteria2://", StringComparison.OrdinalIgnoreCase))
                {
                    node.Protocol = "Hysteria 2";
                    ExtractHostPortName(rawUri, node);
                    return node;
                }
                else if (rawUri.StartsWith("vmess://", StringComparison.OrdinalIgnoreCase))
                {
                    node.Protocol = "VMess";
                    node.Name = "VMess Node";
                    return node;
                }
            }
            catch
            {
                // Ignore malformed URIs
            }

            return null;
        }

        private static void ExtractHostPortName(string uri, ServerNode node)
        {
            var hashIdx = uri.IndexOf('#');
            if (hashIdx >= 0)
            {
                var remark = uri[(hashIdx + 1)..];
                node.Name = HttpUtility.UrlDecode(remark);
            }

            var atIdx = uri.IndexOf('@');
            if (atIdx >= 0)
            {
                var rest = uri[(atIdx + 1)..];
                var qIdx = rest.IndexOfAny(new[] { '?', '#' });
                var hostPort = qIdx >= 0 ? rest[..qIdx] : rest;

                var colonIdx = hostPort.LastIndexOf(':');
                if (colonIdx >= 0)
                {
                    node.Server = hostPort[..colonIdx];
                    if (int.TryParse(hostPort[(colonIdx + 1)..], out int port))
                    {
                        node.Port = port;
                    }
                }
                else
                {
                    node.Server = hostPort;
                }
            }

            // Extract Country / Flag from Name
            if (node.Name.Contains("🇷🇺") || node.Name.Contains("[RU]")) { node.FlagEmoji = "🇷🇺"; node.CountryCode = "RU"; }
            else if (node.Name.Contains("🇩🇪") || node.Name.Contains("[DE]")) { node.FlagEmoji = "🇩🇪"; node.CountryCode = "DE"; }
            else if (node.Name.Contains("🇳🇱") || node.Name.Contains("[NL]")) { node.FlagEmoji = "🇳🇱"; node.CountryCode = "NL"; }
            else if (node.Name.Contains("🇺🇸") || node.Name.Contains("[US]")) { node.FlagEmoji = "🇺🇸"; node.CountryCode = "US"; }
            else if (node.Name.Contains("🇫🇮") || node.Name.Contains("[FI]")) { node.FlagEmoji = "🇫🇮"; node.CountryCode = "FI"; }
            else if (node.Name.Contains("🇸🇪") || node.Name.Contains("[SE]")) { node.FlagEmoji = "🇸🇪"; node.CountryCode = "SE"; }
            else if (node.Name.Contains("🇬🇧") || node.Name.Contains("[GB]")) { node.FlagEmoji = "🇬🇧"; node.CountryCode = "GB"; }
            else if (node.Name.Contains("🇹🇷") || node.Name.Contains("[TR]")) { node.FlagEmoji = "🇹🇷"; node.CountryCode = "TR"; }
            else if (node.Name.Contains("🇰🇿") || node.Name.Contains("[KZ]")) { node.FlagEmoji = "🇰🇿"; node.CountryCode = "KZ"; }
            else if (node.IsWarp) { node.FlagEmoji = "⚡"; node.CountryCode = "CF"; }
        }
    }
}
