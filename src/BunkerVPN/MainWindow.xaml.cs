using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using BunkerVPN.Services;
using BunkerVPN.ViewModels;

namespace BunkerVPN
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            (System.Windows.Application.Current as App)?.RegisterMainWindow(this);

            Loaded += async (s, e) =>
            {
                // Sync radio buttons with settings
                if (ViewModel.Settings.VpnMode.Equals("SystemProxy", StringComparison.OrdinalIgnoreCase))
                {
                    RadioProxyMode.IsChecked = true;
                }
                else
                {
                    RadioTunMode.IsChecked = true;
                }

                await ViewModel.InitializeAsync();
            };
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.Settings.MinimizeToTrayOnClose)
            {
                Hide();
                (System.Windows.Application.Current as App)?.ShowNotification("Бункер Кота VPN", "Приложение продолжает работать в системном трее.");
            }
            else
            {
                (System.Windows.Application.Current as App)?.ExitApplication();
            }
        }

        public async void ToggleConnection()
        {
            await ViewModel.ToggleConnectionAsync();
        }

        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.ToggleConnectionAsync();
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (PanelHome == null) return;

            PanelHome.Visibility = TabHome.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelServers.Visibility = TabServers.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelFeeds.Visibility = TabFeeds.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelSettings.Visibility = TabSettings.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelLogs.Visibility = TabLogs.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SwitchToServers_Click(object sender, RoutedEventArgs e)
        {
            TabServers.IsChecked = true;
        }

        private async void PingAll_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.PingAllServersAsync();
        }

        private async void RefreshServers_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.RefreshServersAsync();
        }

        private async void SelectFeed_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string url)
            {
                ViewModel.Settings.SelectedFeedUrl = url;
                ViewModel.Settings.CustomSubscriptionUrl = "";
                ViewModel.Settings.Save();
                await ViewModel.RefreshServersAsync();
                TabServers.IsChecked = true;
            }
        }

        private async void ApplyCustomUrl_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Settings.Save();
            await ViewModel.RefreshServersAsync();
            TabServers.IsChecked = true;
        }

        private void Mode_Changed(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;
            ViewModel.Settings.VpnMode = RadioTunMode.IsChecked == true ? "TUN" : "SystemProxy";
            ViewModel.Settings.Save();
        }

        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            ViewModel?.Settings.Save();
        }

        private void AutoStart_Changed(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;
            AutoStartManager.SetAutoStart(ViewModel.Settings.StartWithWindows);
            ViewModel.Settings.Save();
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Logs.Clear();
        }

        private void TelegramLink_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "https://t.me/BUNKER_K0TA",
                    UseShellExecute = true
                });
            }
            catch
            {
                // Ignore browser launch errors
            }
        }

        private void SortByPing_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.SortServersByPing();
        }

        private async void CheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.CheckForUpdatesAsync(silent: false);
        }

        private async void ApplyUpdate_Click(object sender, RoutedEventArgs e)
        {
            await ViewModel.DownloadAndApplyUpdateAsync();
        }

        public void CleanUp()
        {
            ViewModel.CleanUp();
        }
    }
}
