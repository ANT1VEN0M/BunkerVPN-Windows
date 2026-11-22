using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using Forms = System.Windows.Forms;
using BunkerVPN.Services;

namespace BunkerVPN
{
    public partial class App : System.Windows.Application
    {
        private static Mutex? _mutex;
        private Forms.NotifyIcon? _notifyIcon;
        private MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            const string mutexName = "BunkerVPN_SingleInstance_Mutex_KotAntidot";
            _mutex = new Mutex(true, mutexName, out bool isNewInstance);

            if (!isNewInstance)
            {
                // App is already running
                Shutdown();
                return;
            }

            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            {
                Debug.WriteLine($"Unhandled exception: {args.ExceptionObject}");
            };

            SetupTrayIcon();
        }

        private void SetupTrayIcon()
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Text = "Бункер Кота VPN",
                Visible = true
            };

            try
            {
                var iconStream = GetResourceStream(new Uri("pack://application:,,,/Resources/Icons/tray_disconnected.ico"))?.Stream;
                if (iconStream != null)
                {
                    _notifyIcon.Icon = new Icon(iconStream);
                }
                else
                {
                    _notifyIcon.Icon = SystemIcons.Shield;
                }
            }
            catch
            {
                _notifyIcon.Icon = SystemIcons.Shield;
            }

            var contextMenu = new Forms.ContextMenuStrip();
            contextMenu.Items.Add("🐾 Открыть Бункер", null, (s, e) => ShowMainWindow());
            contextMenu.Items.Add(new Forms.ToolStripSeparator());
            contextMenu.Items.Add("⚡ Подключить", null, (s, e) => _mainWindow?.ToggleConnection());
            contextMenu.Items.Add(new Forms.ToolStripSeparator());
            contextMenu.Items.Add("❌ Выход", null, (s, e) => ExitApplication());

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => ShowMainWindow();
        }

        public void RegisterMainWindow(MainWindow window)
        {
            _mainWindow = window;
        }

        public void UpdateTrayIcon(bool connected, string serverName)
        {
            if (_notifyIcon == null) return;

            try
            {
                var iconName = connected ? "tray_connected.ico" : "tray_disconnected.ico";
                var iconStream = GetResourceStream(new Uri($"pack://application:,,,/Resources/Icons/{iconName}"))?.Stream;
                if (iconStream != null)
                {
                    _notifyIcon.Icon = new Icon(iconStream);
                }

                _notifyIcon.Text = connected ? $"Бункер: {serverName}" : "Бункер Кота VPN (Отключено)";
            }
            catch
            {
                // Ignore icon update errors
            }
        }

        public void ShowNotification(string title, string message)
        {
            _notifyIcon?.ShowBalloonTip(3000, title, message, Forms.ToolTipIcon.Info);
        }

        public void ShowMainWindow()
        {
            if (_mainWindow != null)
            {
                _mainWindow.Show();
                _mainWindow.WindowState = WindowState.Normal;
                _mainWindow.Activate();
            }
        }

        public void ExitApplication()
        {
            _mainWindow?.CleanUp();
            _notifyIcon?.Dispose();
            _mutex?.ReleaseMutex();
            Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mainWindow?.CleanUp();
            _notifyIcon?.Dispose();
            _mutex?.ReleaseMutex();
            base.OnExit(e);
        }
    }
}
