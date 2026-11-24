using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using BunkerVPN.Models;

namespace BunkerVPN.Services
{
    public class PingService
    {
        private static readonly HttpClient HttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(4)
        };

        public static async Task<int> TestUrlLatencyAsync(string url = "https://cp.cloudflare.com/generate_204", int timeoutMs = 2500)
        {
            try
            {
                var sw = Stopwatch.StartNew();
                using var cts = new CancellationTokenSource(timeoutMs);
                using var resp = await HttpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                sw.Stop();
                if ((int)resp.StatusCode >= 200 && (int)resp.StatusCode < 400)
                {
                    return (int)sw.ElapsedMilliseconds;
                }
            }
            catch
            {
                // Timeout or network error
            }
            return -1;
        }

        public static async Task<int> TestTlsHandshakePingAsync(string host, int port, string? sni = null, int timeoutMs = 1800)
        {
            if (string.IsNullOrWhiteSpace(host) || port <= 0) return -1;

            var sw = Stopwatch.StartNew();
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(timeoutMs);

            try
            {
                await client.ConnectAsync(host, port, cts.Token);
                using var ssl = new SslStream(client.GetStream(), false, (sender, cert, chain, errors) => true);
                var targetHost = !string.IsNullOrWhiteSpace(sni) ? sni : host;
                var sslOptions = new SslClientAuthenticationOptions
                {
                    TargetHost = targetHost,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                };
                await ssl.AuthenticateAsClientAsync(sslOptions, cts.Token);
                sw.Stop();
                return (int)sw.ElapsedMilliseconds;
            }
            catch
            {
                return -1;
            }
        }

        public static async Task<int> TestTcpPingAsync(string host, int port, int timeoutMs = 1500)
        {
            if (string.IsNullOrWhiteSpace(host) || port <= 0) return -1;

            var sw = Stopwatch.StartNew();
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(timeoutMs);

            try
            {
                await client.ConnectAsync(host, port, cts.Token);
                sw.Stop();
                return (int)sw.ElapsedMilliseconds;
            }
            catch
            {
                return -1;
            }
        }

        public static async Task<int> TestIcmpPingAsync(string host, int timeoutMs = 1200)
        {
            if (string.IsNullOrWhiteSpace(host)) return -1;

            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = await ping.SendPingAsync(host, timeoutMs);
                if (reply.Status == IPStatus.Success)
                {
                    return (int)reply.RoundtripTime;
                }
            }
            catch
            {
                // Ignore ICMP errors
            }
            return -1;
        }

        public static async Task<int> TestNodePingAsync(ServerNode node, int timeoutMs = 1800)
        {
            if (string.IsNullOrWhiteSpace(node.Server)) return -1;

            // WireGuard / Cloudflare WARP uses UDP
            if (node.Protocol.Equals("WireGuard", StringComparison.OrdinalIgnoreCase) || node.IsWarp)
            {
                var icmp = await TestIcmpPingAsync(node.Server, timeoutMs);
                if (icmp > 0) return icmp;
                return await TestTcpPingAsync(node.Server, node.Port > 0 ? node.Port : 880, timeoutMs);
            }

            // Real TLS Handshake check for VLESS, Trojan, Reality, Hysteria 2
            // This eliminates fake TCP pings on dead servers where port 443 is open but proxy doesn't work
            if (node.Port > 0)
            {
                var tlsPing = await TestTlsHandshakePingAsync(node.Server, node.Port, node.Sni, timeoutMs);
                if (tlsPing > 0) return tlsPing;

                // For Shadowsocks, fall back to TCP ping
                if (node.Protocol.Equals("Shadowsocks", StringComparison.OrdinalIgnoreCase))
                {
                    return await TestTcpPingAsync(node.Server, node.Port, timeoutMs);
                }

                // If TLS handshake failed on a TLS/Reality node, it is dead
                return -1;
            }

            return await TestIcmpPingAsync(node.Server, timeoutMs);
        }

        public static async Task TestAllNodesAsync(IEnumerable<ServerNode> nodes, Action? onProgress = null)
        {
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 20
            };

            await Parallel.ForEachAsync(nodes, parallelOptions, async (node, ct) =>
            {
                node.PingMs = await TestNodePingAsync(node);
                onProgress?.Invoke();
            });
        }
    }
}
