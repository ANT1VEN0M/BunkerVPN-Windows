using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using BunkerVPN.Models;

namespace BunkerVPN.Services
{
    public class PingService
    {
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

        public static async Task TestAllNodesAsync(IEnumerable<ServerNode> nodes, Action? onProgress = null)
        {
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 20
            };

            await Parallel.ForEachAsync(nodes, parallelOptions, async (node, ct) =>
            {
                if (!string.IsNullOrEmpty(node.Server) && node.Port > 0)
                {
                    node.PingMs = await TestTcpPingAsync(node.Server, node.Port);
                    onProgress?.Invoke();
                }
            });
        }
    }
}
