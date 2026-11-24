using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using BunkerVPN.Models;

namespace BunkerVPN.Services
{
    public class CoreManager
    {
        private Process? _coreProcess;
        public bool IsRunning => _coreProcess != null && !_coreProcess.HasExited;

        public event Action<string>? LogReceived;
        public event Action? CoreExited;

        public static string GetCoreExecutablePath()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var subPath = Path.Combine(baseDir, "core", "sing-box.exe");
            if (File.Exists(subPath)) return subPath;

            var localPath = Path.Combine(baseDir, "sing-box.exe");
            if (File.Exists(localPath)) return localPath;

            return subPath; // Default target
        }

        public async Task<bool> StartAsync(ServerNode node, AppSettings settings)
        {
            Stop();

            var coreExe = GetCoreExecutablePath();
            if (!File.Exists(coreExe))
            {
                LogReceived?.Invoke($"[-] sing-box core not found at: {coreExe}");
                return false;
            }

            var configJson = ConfigGenerator.GenerateSingBoxConfig(node, settings);
            var configPath = Path.Combine(Path.GetTempPath(), "bunker_singbox_config.json");
            await File.WriteAllTextAsync(configPath, configJson);

            LogReceived?.Invoke($"[+] [PC v{UpdateService.CurrentVersion}] Starting core with server: {node.Name} ({node.Protocol}) in {settings.VpnMode} mode...");

            var psi = new ProcessStartInfo
            {
                FileName = coreExe,
                Arguments = $"run -c \"{configPath}\"",
                WorkingDirectory = Path.GetDirectoryName(coreExe),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            psi.EnvironmentVariables["ENABLE_DEPRECATED_TUN_ADDRESS_FIELDS"] = "true";
            psi.EnvironmentVariables["ENABLE_DEPRECATED_SPECIAL_OUTBOUNDS"] = "true";
            psi.EnvironmentVariables["ENABLE_DEPRECATED_GEOIP"] = "true";
            psi.EnvironmentVariables["ENABLE_DEPRECATED_GEOSITE"] = "true";
            psi.EnvironmentVariables["ENABLE_LEGACY_TUN_ADDRESS_FIELDS"] = "1";
            psi.EnvironmentVariables["ENABLE_LEGACY_SPECIAL_OUTBOUNDS"] = "1";
            psi.EnvironmentVariables["ENABLE_LEGACY_GEOIP"] = "1";
            psi.EnvironmentVariables["ENABLE_LEGACY_GEOSITE"] = "1";

            try
            {
                _coreProcess = new Process { StartInfo = psi, EnableRaisingEvents = true };

                _coreProcess.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data)) LogReceived?.Invoke(e.Data);
                };
                _coreProcess.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data)) LogReceived?.Invoke(e.Data);
                };
                _coreProcess.Exited += (s, e) =>
                {
                    LogReceived?.Invoke("[-] sing-box core process exited.");
                    CoreExited?.Invoke();
                };

                var started = _coreProcess.Start();
                if (started)
                {
                    _coreProcess.BeginOutputReadLine();
                    _coreProcess.BeginErrorReadLine();

                    // If System Proxy mode, enable Windows proxy
                    if (settings.VpnMode.Equals("SystemProxy", StringComparison.OrdinalIgnoreCase))
                    {
                        SystemProxyManager.SetSystemProxy("127.0.0.1", 2080);
                        LogReceived?.Invoke("[+] System Proxy configured to 127.0.0.1:2080");
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                LogReceived?.Invoke($"[-] Failed to start core process: {ex.Message}");
            }

            return false;
        }

        public void Stop()
        {
            try
            {
                SystemProxyManager.ClearSystemProxy();

                if (_coreProcess != null && !_coreProcess.HasExited)
                {
                    _coreProcess.Kill(entireProcessTree: true);
                    _coreProcess.WaitForExit(1000);
                    _coreProcess.Dispose();
                }
            }
            catch
            {
                // Ignore kill errors
            }
            finally
            {
                _coreProcess = null;
            }
        }
    }
}
