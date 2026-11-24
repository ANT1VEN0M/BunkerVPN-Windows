using System;
using System.IO;
using System.Text;

namespace BunkerVPN.Services
{
    public static class LogService
    {
        private static readonly object LockObj = new();
        private static readonly string LogsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BunkerVPN",
            "logs"
        );
        private static readonly string CurrentLogFile = Path.Combine(LogsFolder, "bunker_vpn.log");

        static LogService()
        {
            try
            {
                if (!Directory.Exists(LogsFolder))
                {
                    Directory.CreateDirectory(LogsFolder);
                }

                // Rotate if log file > 5 MB
                if (File.Exists(CurrentLogFile) && new FileInfo(CurrentLogFile).Length > 5 * 1024 * 1024)
                {
                    var backup = Path.Combine(LogsFolder, $"bunker_vpn_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
                    File.Move(CurrentLogFile, backup);
                }

                // Write session startup diagnostics
                var header = new StringBuilder();
                header.AppendLine();
                header.AppendLine("================================================================================");
                header.AppendLine($"[SESSION START] {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                header.AppendLine($"App: BunkerVPN for Windows PC v{UpdateService.CurrentVersion}");
                header.AppendLine($"OS: {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})");
                header.AppendLine($"CLR Runtime: {Environment.Version}");
                header.AppendLine($"Machine: {Environment.MachineName} | User: {Environment.UserName}");
                header.AppendLine("================================================================================");

                File.AppendAllText(CurrentLogFile, header.ToString());
            }
            catch
            {
                // Ignore startup logger errors
            }
        }

        public static void Log(string message)
        {
            try
            {
                var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
                lock (LockObj)
                {
                    File.AppendAllText(CurrentLogFile, entry + Environment.NewLine);
                }
            }
            catch
            {
                // Never crash the app on log writing
            }
        }

        public static string GetLogsFolder()
        {
            if (!Directory.Exists(LogsFolder))
            {
                Directory.CreateDirectory(LogsFolder);
            }
            return LogsFolder;
        }

        public static string GetAllLogsText()
        {
            try
            {
                lock (LockObj)
                {
                    if (File.Exists(CurrentLogFile))
                    {
                        return File.ReadAllText(CurrentLogFile);
                    }
                }
            }
            catch (Exception ex)
            {
                return $"[Error reading logs: {ex.Message}]";
            }
            return "Журнал логов пуст.";
        }

        public static void ExportLogs(string destinationPath)
        {
            try
            {
                var fullLog = GetAllLogsText();
                File.WriteAllText(destinationPath, fullLog, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new Exception($"Не удалось сохранить файл: {ex.Message}", ex);
            }
        }
    }
}
