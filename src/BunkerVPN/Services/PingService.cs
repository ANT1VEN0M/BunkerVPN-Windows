using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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

        public static async Task<int> TestNodePingAsync(ServerNode node, int timeoutMs = 1500)
        {
            if (string.IsNullOrWhiteSpace(node.Server)) return -1;

            // WireGuard / Cloudflare WARP uses UDP. ICMP ping is fastest and accurate for measuring latency.
            if (node.Protocol.Equals("WireGuard", StringComparison.OrdinalIgnoreCase) || node.IsWarp)
            {
                var icmp = await TestIcmpPingAsync(node.Server, timeoutMs);
                if (icmp > 0) return icmp;
            }

            // TCP connect test for VLESS, Trojan, Shadowsocks, Hysteria2
            if (node.Port > 0)
            {
                var tcp = await TestTcpPingAsync(node.Server, node.Port, timeoutMs);
                if (tcp > 0) return tcp;
            }

            // Fallback to ICMP ping if TCP connection was dropped
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
