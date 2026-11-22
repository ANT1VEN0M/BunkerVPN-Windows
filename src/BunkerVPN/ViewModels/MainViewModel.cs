using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Threading;
using BunkerVPN.Models;
using BunkerVPN.Services;

namespace BunkerVPN.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly CoreManager _coreManager = new();
        private readonly SubscriptionService _subService = new();
        private readonly UpdateService _updateService = new();
        private readonly DispatcherTimer _durationTimer = new();
        private DateTime _connectionStartTime;

        public AppSettings Settings { get; set; } = AppSettings.Load();

        private bool _isUpdateAvailable;
        public bool IsUpdateAvailable
        {
            get => _isUpdateAvailable;
            set { _isUpdateAvailable = value; OnPropertyChanged(); }
        }

        private string _updateVersion = "";
        public string UpdateVersion
        {
            get => _updateVersion;
            set { _updateVersion = value; OnPropertyChanged(); }
        }

        private string _updateDownloadUrl = "";
        public string UpdateDownloadUrl
        {
            get => _updateDownloadUrl;
            set { _updateDownloadUrl = value; OnPropertyChanged(); }
        }

        private int _updateDownloadProgress;
        public int UpdateDownloadProgress
        {
            get => _updateDownloadProgress;
            set { _updateDownloadProgress = value; OnPropertyChanged(); }
        }

        private bool _isDownloadingUpdate;
        public bool IsDownloadingUpdate
        {
            get => _isDownloadingUpdate;
            set { _isDownloadingUpdate = value; OnPropertyChanged(); }
        }

        private string _updateCheckStatus = $"Текущая версия: v{UpdateService.CurrentVersion}";
        public string UpdateCheckStatus
        {
            get => _updateCheckStatus;
            set { _updateCheckStatus = value; OnPropertyChanged(); }
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get => _isConnected;
            set { _isConnected = value; OnPropertyChanged(); OnPropertyChanged(nameof(ConnectionButtonText)); OnPropertyChanged(nameof(ConnectionButtonBackground)); }
        }

        private bool _isConnecting;
        public bool IsConnecting
        {
            get => _isConnecting;
            set { _isConnecting = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); }
        }

        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set { _isRefreshing = value; OnPropertyChanged(); }
        }

        private string _statusText = "ОТКЛЮЧЕНО";
        public string StatusText
        {
            get
            {
                if (IsConnecting) return "ПОДКЛЮЧЕНИЕ...";
                if (IsConnected) return "ЗАЩИЩЕНО";
                return "ОТКЛЮЧЕНО";
            }
        }

        public string StatusColor
        {
            get
            {
                if (IsConnecting) return "#FFB300";
                if (IsConnected) return "#00E676";
                return "#8E95A5";
            }
        }

        public string ConnectionButtonText => IsConnected ? "ОТКЛЮЧИТЬ" : (IsConnecting ? "ОТМЕНА" : "ПОДКЛЮЧИТЬ");

        public string ConnectionButtonBackground => IsConnected ? "#FF5252" : "#FF6D00";

        private string _durationText = "00:00:00";
        public string DurationText
        {
            get => _durationText;
            set { _durationText = value; OnPropertyChanged(); }
        }

        private ServerNode? _selectedServer;
        public ServerNode? SelectedServer
        {
            get => _selectedServer;
            set
            {
                _selectedServer = value;
                OnPropertyChanged();
                if (value != null)
                {
                    Settings.LastSelectedServerId = value.Id;
                    Settings.Save();
                }
            }
        }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                FilterServers();
            }
        }

        public ObservableCollection<ServerNode> Servers { get; } = new();
        public ObservableCollection<ServerNode> FilteredServers { get; } = new();
        public ObservableCollection<BunkerFeedItem> Feeds { get; } = new(SubscriptionService.DefaultFeeds);

        public ObservableCollection<string> Logs { get; } = new();

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainViewModel()
        {
            _durationTimer.Interval = TimeSpan.FromSeconds(1);
            _durationTimer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - _connectionStartTime;
                DurationText = elapsed.ToString(@"hh\:mm\:ss");
            };

            _coreManager.LogReceived += log =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    Logs.Add($"[{DateTime.Now:HH:mm:ss}] {log}");
                    if (Logs.Count > 200) Logs.RemoveAt(0);
                });
            };

            _coreManager.CoreExited += () =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    if (IsConnected)
                    {
                        Disconnect();
                    }
                });
            };
        }

        public async Task InitializeAsync()
        {
            // 1. Load cached servers
            var cached = _subService.LoadCache();
            if (cached.Count > 0)
            {
                SetServers(cached);
            }

            // 2. Refresh servers in background
            await RefreshServersAsync();

            // 3. Auto-connect if enabled
            if (Settings.AutoConnectOnStartup && SelectedServer != null)
            {
                await ConnectAsync();
            }

            // 4. Background check for updates if enabled
            if (Settings.AutoCheckUpdates)
            {
                _ = Task.Run(() => CheckForUpdatesAsync(silent: true));
            }
        }

        public async Task RefreshServersAsync()
        {
            if (IsRefreshing) return;
            IsRefreshing = true;

            try
            {
                var targetUrl = !string.IsNullOrWhiteSpace(Settings.CustomSubscriptionUrl)
                    ? Settings.CustomSubscriptionUrl
                    : Settings.SelectedFeedUrl;

                var nodes = await _subService.FetchSubscriptionAsync(targetUrl);
                if (nodes.Count > 0)
                {
                    SetServers(nodes);
                }
            }
            catch (Exception ex)
            {
                Logs.Add($"[-] Subscription update failed: {ex.Message}");
            }
            finally
            {
                IsRefreshing = false;
            }
        }

        private void SetServers(System.Collections.Generic.List<ServerNode> nodes)
        {
            Servers.Clear();
            foreach (var n in nodes) Servers.Add(n);

            FilterServers();

            if (SelectedServer == null && Servers.Count > 0)
            {
                var saved = Servers.FirstOrDefault(s => s.Id == Settings.LastSelectedServerId);
                SelectedServer = saved ?? Servers[0];
            }
        }

        public void FilterServers()
        {
            FilteredServers.Clear();
            var q = SearchText.Trim().ToLowerInvariant();

            foreach (var s in Servers)
            {
                if (string.IsNullOrEmpty(q) ||
                    s.Name.ToLowerInvariant().Contains(q) ||
                    s.Protocol.ToLowerInvariant().Contains(q) ||
                    s.CountryCode.ToLowerInvariant().Contains(q))
                {
                    FilteredServers.Add(s);
                }
            }
        }

        public async Task ToggleConnectionAsync()
        {
            if (IsConnected || IsConnecting)
            {
                Disconnect();
            }
            else
            {
                await ConnectAsync();
            }
        }

        public async Task ConnectAsync()
        {
            if (SelectedServer == null)
            {
                Logs.Add("[-] No server selected to connect.");
                return;
            }

            IsConnecting = true;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusColor));

            var success = await _coreManager.StartAsync(SelectedServer, Settings);

            IsConnecting = false;
            if (success)
            {
                IsConnected = true;
                _connectionStartTime = DateTime.Now;
                _durationTimer.Start();
                (System.Windows.Application.Current as App)?.UpdateTrayIcon(true, SelectedServer.Name);
                (System.Windows.Application.Current as App)?.ShowNotification("Бункер Кота VPN", $"Успешно подключено к {SelectedServer.Name}");
            }
            else
            {
                IsConnected = false;
                (System.Windows.Application.Current as App)?.UpdateTrayIcon(false, "");
            }

            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusColor));
        }

        public void Disconnect()
        {
            _coreManager.Stop();
            _durationTimer.Stop();
            IsConnected = false;
            IsConnecting = false;
            DurationText = "00:00:00";
            (System.Windows.Application.Current as App)?.UpdateTrayIcon(false, "");
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusColor));
        }

        public async Task PingAllServersAsync()
        {
            await PingService.TestAllNodesAsync(Servers, () =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(FilterServers);
            });
        }

        public async Task CheckForUpdatesAsync(bool silent = true)
        {
            UpdateCheckStatus = "Проверка обновлений...";
            try
            {
                var update = await _updateService.CheckForUpdatesAsync();
                if (update.HasUpdate)
                {
                    IsUpdateAvailable = true;
                    UpdateVersion = update.LatestVersion;
                    UpdateDownloadUrl = update.DownloadUrl;
                    UpdateCheckStatus = $"Доступно обновление: {update.LatestVersion}!";
                    (System.Windows.Application.Current as App)?.ShowNotification("BunkerVPN Обновление", $"Доступна новая версия {update.LatestVersion}!");
                }
                else
                {
                    IsUpdateAvailable = false;
                    UpdateCheckStatus = $"У вас установлена последняя версия (v{UpdateService.CurrentVersion}).";
                }
            }
            catch (Exception ex)
            {
                if (!silent) UpdateCheckStatus = $"Ошибка проверки: {ex.Message}";
            }
        }

        public async Task DownloadAndApplyUpdateAsync()
        {
            if (string.IsNullOrEmpty(UpdateDownloadUrl) || IsDownloadingUpdate) return;

            IsDownloadingUpdate = true;
            UpdateCheckStatus = "Загрузка обновления...";

            try
            {
                var progress = new Progress<int>(p =>
                {
                    UpdateDownloadProgress = p;
                    UpdateCheckStatus = $"Загрузка обновления: {p}%...";
                });

                var installerPath = await _updateService.DownloadUpdateAsync(UpdateDownloadUrl, progress);
                UpdateCheckStatus = "Запуск установщика...";
                _updateService.LaunchInstallerAndExit(installerPath);
            }
            catch (Exception ex)
            {
                IsDownloadingUpdate = false;
                UpdateCheckStatus = $"Ошибка скачивания: {ex.Message}";
            }
        }

        public void CleanUp()
        {
            Disconnect();
            Settings.Save();
        }

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
