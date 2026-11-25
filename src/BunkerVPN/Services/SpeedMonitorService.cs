using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;

namespace BunkerVPN.Services
{
    /// <summary>
    /// Monitors real-time download and upload network throughput (B/s, KB/s, MB/s)
    /// primarily inspecting the active WinTUN tunnel adapter or active network interfaces.
    /// </summary>
    public class SpeedMonitorService
    {
        private long _lastBytesReceived = -1;
        private long _lastBytesSent = -1;
        private DateTime _lastSampleTime = DateTime.MinValue;

        public event Action<string, string>? SpeedUpdated;

        public string DownloadSpeedText { get; private set; } = "0.0 KB/s";
        public string UploadSpeedText { get; private set; } = "0.0 KB/s";

        public void Reset()
        {
            _lastBytesReceived = -1;
            _lastBytesSent = -1;
            _lastSampleTime = DateTime.MinValue;
            DownloadSpeedText = "0.0 KB/s";
            UploadSpeedText = "0.0 KB/s";
            SpeedUpdated?.Invoke(DownloadSpeedText, UploadSpeedText);
        }

        public void UpdateSample()
        {
            try
            {
                var nics = NetworkInterface.GetAllNetworkInterfaces();

                // 1. Prefer specific active tunnel adapter: bunker-tun or wintun
                NetworkInterface? targetNic = nics.FirstOrDefault(n =>
                    n.OperationalStatus == OperationalStatus.Up &&
                    (n.Name.IndexOf("bunker-tun", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     n.Description.IndexOf("bunker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     n.Description.IndexOf("wintun", StringComparison.OrdinalIgnoreCase) >= 0));

                long totalIn = 0;
                long totalOut = 0;

                if (targetNic != null)
                {
                    var stats = targetNic.GetIPStatistics();
                    totalIn = stats.BytesReceived;
                    totalOut = stats.BytesSent;
                }
                else
                {
                    // Fallback to active interfaces with operational status
                    foreach (var nic in nics)
                    {
                        if (nic.OperationalStatus != OperationalStatus.Up ||
                            nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        {
                            continue;
                        }

                        try
                        {
                            var stats = nic.GetIPStatistics();
                            totalIn += stats.BytesReceived;
                            totalOut += stats.BytesSent;
                        }
                        catch
                        {
                            // Skip interfaces that refuse statistics
                        }
                    }
                }

                var now = DateTime.Now;
                if (_lastBytesReceived >= 0 && _lastSampleTime != DateTime.MinValue)
                {
                    double elapsedSec = (now - _lastSampleTime).TotalSeconds;
                    if (elapsedSec > 0.2)
                    {
                        long deltaIn = totalIn - _lastBytesReceived;
                        long deltaOut = totalOut - _lastBytesSent;

                        if (deltaIn < 0) deltaIn = 0;
                        if (deltaOut < 0) deltaOut = 0;

                        double bytesInPerSec = deltaIn / elapsedSec;
                        double bytesOutPerSec = deltaOut / elapsedSec;

                        DownloadSpeedText = FormatSpeed(bytesInPerSec);
                        UploadSpeedText = FormatSpeed(bytesOutPerSec);

                        SpeedUpdated?.Invoke(DownloadSpeedText, UploadSpeedText);
                    }
                }

                _lastBytesReceived = totalIn;
                _lastBytesSent = totalOut;
                _lastSampleTime = now;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SpeedMonitorService] Error: {ex.Message}");
            }
        }

        public static string FormatSpeed(double bytesPerSec)
        {
            if (bytesPerSec < 1024)
            {
                return $"{bytesPerSec:0} B/s";
            }
            if (bytesPerSec < 1024 * 1024)
            {
                return $"{(bytesPerSec / 1024.0):F1} KB/s";
            }
            return $"{(bytesPerSec / (1024.0 * 1024.0)):F1} MB/s";
        }
    }
}
