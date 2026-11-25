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
        private readonly SpeedMonitorService _speedMonitor = new();
        private readonly DispatcherTimer _durationTimer = new();
        private DateTime _connectionStartTime;
        private string? _customTunnelHealthStatus;
        private bool _isTestingInternet;

        public AppSettings Settings { get; set; } = AppSettings.Load();

        public string AppVersionDisplay => $"PC v{UpdateService.CurrentVersion}";

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
            set
            {
                _isConnected = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConnectionButtonText));
                OnPropertyChanged(nameof(ConnectionButtonBackground));
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(TunnelHealthStatus));
            }
        }

        private bool _isConnecting;
        public bool IsConnecting
        {
            get => _isConnecting;
            set
            {
                _isConnecting = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(ConnectionButtonText));
                OnPropertyChanged(nameof(ConnectionButtonBackground));
                OnPropertyChanged(nameof(CanToggleConnection));
            }
        }

        private bool _isDisconnecting;
        public bool IsDisconnecting
        {
            get => _isDisconnecting;
            set
            {
                _isDisconnecting = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(ConnectionButtonText));
                OnPropertyChanged(nameof(ConnectionButtonBackground));
                OnPropertyChanged(nameof(CanToggleConnection));
            }
        }

        public bool CanToggleConnection => !IsDisconnecting;

        private bool _isRefreshing;
        public bool IsRefreshing
        {
            get => _isRefreshing;
            set { _isRefreshing = value; OnPropertyChanged(); }
        }

        private bool _isPinging;
        public bool IsPinging
        {
            get => _isPinging;
            set { _isPinging = value; OnPropertyChanged(); OnPropertyChanged(nameof(PingButtonText)); }
        }

        // Real-time Speed Monitoring
        public string DownloadSpeedText => _speedMonitor.DownloadSpeedText;
        public string UploadSpeedText => _speedMonitor.UploadSpeedText;
        public Visibility SpeedVisibility => IsConnected ? Visibility.Visible : Visibility.Collapsed;

        // Internet Access Diagnostics
        public bool IsTestingInternet
        {
            get => _isTestingInternet;
            set
            {
                _isTestingInternet = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(InternetTestButtonText));
            }
        }
        public string InternetTestButtonText => IsTestingInternet ? "⏳ Тест..." : "🩺 Тест сети";

        // Services Unblock Status (Android-style)
        public string TunnelHealthStatus
        {
            get => _customTunnelHealthStatus ?? (IsConnected ? "🟢 Защита активна • Smart Bypass" : "⚪ Ожидание подключения");
            set
            {
                _customTunnelHealthStatus = value;
                OnPropertyChanged();
            }
        }

        public string YtStatusText => IsConnected ? "⚡ 4K 60fps • Разблокирован" : "Заблокирован ТСПУ";
        public string YtStatusColor => IsConnected ? "#00E676" : "#FF5252";

        public string DiscordStatusText => IsConnected ? (GameBoosterMode ? "🎮 Discord & Brawl • Turbo UDP" : "🎮 Voice & Chat • Full-Cone") : "Ожидание VPN";
        public string DiscordStatusColor => IsConnected ? "#00E676" : "#8B949E";

        public string TgStatusText => IsConnected ? "✈️ Мгновенно • Direct SOS" : "Ожидание VPN";
        public string TgStatusColor => IsConnected ? "#00E676" : "#8B949E";

        public string AiStatusText => IsConnected ? "🤖 ChatGPT • Gemini OK" : "Ограничено в РФ";
        public string AiStatusColor => IsConnected ? "#00E676" : "#FF5252";

        public string WebStatusText => IsConnected ? "🛡️ В обход ТСПУ • Доступен" : "Заблокировано";
        public string WebStatusColor => IsConnected ? "#00E676" : "#FF5252";

        public bool GameBoosterMode
        {
            get => Settings.GameBoosterMode;
            set
            {
                if (Settings.GameBoosterMode != value)
                {
                    Settings.GameBoosterMode = value;
                    Settings.Save();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GameBoosterBadgeText));
                    OnPropertyChanged(nameof(GameBoosterBadgeBackground));
                    OnPropertyChanged(nameof(GameBoosterBadgeBorder));
                    OnPropertyChanged(nameof(GameBoosterBadgeForeground));
                    OnPropertyChanged(nameof(GameBoosterDescText));
                    OnPropertyChanged(nameof(DiscordStatusText));
                    AddLog(value ? "🎮 Game Booster: ВКЛЮЧЕН (Full-Cone UDP • MTU 1340 • Bypass CS2/Dota/Steam)" : "🎮 Game Booster: ВЫКЛЮЧЕН");
                }
            }
        }

        public string GameBoosterBadgeText => Settings.GameBoosterMode ? "🎮 GAME BOOSTER: ВКЛ" : "🎮 GAME BOOSTER: ВЫКЛ";
        public string GameBoosterBadgeBackground => Settings.GameBoosterMode ? "#132E22" : "#1A212E";
        public string GameBoosterBadgeBorder => Settings.GameBoosterMode ? "#00E676" : "#2E384D";
        public string GameBoosterBadgeForeground => Settings.GameBoosterMode ? "#00E676" : "#8B949E";
        public string GameBoosterDescText => Settings.GameBoosterMode
            ? "⚡ Ускорение активно: Discord & Brawl Stars в туннеле, CS2/Dota и закачки Steam напрямую с мин. пингом"
            : "Нажмите для оптимизации сетевого стека под Discord Voice и онлайн-игры без потери пинга";

        public void ToggleGameBooster()
        {
            GameBoosterMode = !GameBoosterMode;
        }

        public bool DisableCopilotAutostart
        {
            get => Settings.DisableCopilotAutostart;
            set
            {
                if (Settings.DisableCopilotAutostart != value)
                {
                    Settings.DisableCopilotAutostart = value;
                    Settings.Save();
                    OnPropertyChanged();
                    ApplyCopilotOptimization(value);
                }
            }
        }

        public void ApplyCopilotOptimization(bool disable)
        {
            try
            {
                if (disable)
                {
                    bool ok = CopilotOptimizer.DisableCopilotAutostart();
                    if (ok)
                    {
                        AddLog("[+] [Оптимизация] Автозапуск Microsoft Copilot и AI заблокирован (DisabledByPolicy)");
                    }
                    else
                    {
                        AddLog("[!] [Оптимизация] Не удалось изменить автозапуск Copilot (проверьте права администратора)");
                    }
                }
                else
                {
                    bool ok = CopilotOptimizer.RestoreCopilotAutostart();
                    if (ok)
                    {
                        AddLog("[-] [Оптимизация] Настройки автозапуска Copilot возвращены по умолчанию");
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"[!] [Оптимизация] Ошибка: {ex.Message}");
            }
        }

        private string _urlTestResult = "URL Тест";
        public string UrlTestResult
        {
            get => _urlTestResult;
            set { _urlTestResult = value; OnPropertyChanged(); }
        }

        private bool _isTestingUrl;
        public bool IsTestingUrl
        {
            get => _isTestingUrl;
            set { _isTestingUrl = value; OnPropertyChanged(); }
        }

        public void NotifyServiceStatusChanged()
        {
            OnPropertyChanged(nameof(TunnelHealthStatus));
            OnPropertyChanged(nameof(YtStatusText));
            OnPropertyChanged(nameof(YtStatusColor));
            OnPropertyChanged(nameof(DiscordStatusText));
            OnPropertyChanged(nameof(DiscordStatusColor));
            OnPropertyChanged(nameof(TgStatusText));
            OnPropertyChanged(nameof(TgStatusColor));
            OnPropertyChanged(nameof(AiStatusText));
            OnPropertyChanged(nameof(AiStatusColor));
            OnPropertyChanged(nameof(WebStatusText));
            OnPropertyChanged(nameof(WebStatusColor));
            OnPropertyChanged(nameof(SpeedVisibility));
        }

        public string PingButtonText => IsPinging ? "Замер..." : "Пинг";

        private string _statusText = "ОТКЛЮЧЕНО";
        public string StatusText
        {
            get
            {
                if (IsDisconnecting) return "ОТКЛЮЧЕНИЕ...";
                if (IsConnecting) return "ПОДКЛЮЧЕНИЕ...";
                if (IsConnected) return "ЗАЩИЩЕНО";
                return "ОТКЛЮЧЕНО";
            }
        }

        public string StatusColor
        {
            get
            {
                if (IsDisconnecting) return "#64748B";
                if (IsConnecting) return "#00E5FF";
                if (IsConnected) return "#00E676";
                return "#8E95A5";
            }
        }

        public string ConnectionButtonText
        {
            get
            {
                if (IsDisconnecting) return "ОТКЛЮЧЕНИЕ...";
                if (IsConnecting) return "ОТМЕНА";
                if (IsConnected) return "ОТКЛЮЧИТЬ";
                return "ПОДКЛЮЧИТЬ";
            }
        }

        public string ConnectionButtonBackground
        {
            get
            {
                if (IsDisconnecting) return "#475569";
                if (IsConnecting) return "#0284C7";
                if (IsConnected) return "#E11D48";
                return "#FF6D00";
            }
        }

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

        public void AddLog(string message)
        {
            LogService.Log(message);
            try
            {
                var app = System.Windows.Application.Current;
                if (app != null)
                {
                    app.Dispatcher.Invoke(() =>
                    {
                        Logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
                        if (Logs.Count > 300) Logs.RemoveAt(0);
                    });
                }
            }
            catch
            {
                // Ignore UI dispatcher exceptions on shutdown
            }
        }

        public MainViewModel()
        {
            AddLog($"[+] BunkerVPN PC v{UpdateService.CurrentVersion} started");

            _durationTimer.Interval = TimeSpan.FromSeconds(1);
            _durationTimer.Tick += (s, e) =>
            {
                var elapsed = DateTime.Now - _connectionStartTime;
                DurationText = elapsed.ToString(@"hh\:mm\:ss");
                if (IsConnected)
                {
                    _speedMonitor.UpdateSample();
                }
            };

            _speedMonitor.SpeedUpdated += (down, up) =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                {
                    OnPropertyChanged(nameof(DownloadSpeedText));
                    OnPropertyChanged(nameof(UploadSpeedText));
                });
            };

            _coreManager.LogReceived += log =>
            {
                AddLog(log);
            };

            _coreManager.CoreExited += () =>
            {
                System.Windows.Application.Current?.Dispatcher.Invoke(async () =>
                {
                    if (IsConnected && !IsDisconnecting)
                    {
                        await DisconnectAsync();
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

            // 3. Auto-ping and auto-sort servers if enabled
            if (Settings.AutoSortByPing)
            {
                _ = Task.Run(PingAllServersAsync);
            }

            // 4. Auto-connect if enabled
            if (Settings.AutoConnectOnStartup && SelectedServer != null)
            {
                await ConnectAsync();
            }

            // 5. Background check for updates if enabled
            if (Settings.AutoCheckUpdates)
            {
                _ = Task.Run(() => CheckForUpdatesAsync(silent: true));
            }

            // 6. Sync Copilot optimization status with system
            try
            {
                bool isCopilotBlocked = CopilotOptimizer.IsCopilotAutostartDisabled();
                if (Settings.DisableCopilotAutostart != isCopilotBlocked)
                {
                    Settings.DisableCopilotAutostart = isCopilotBlocked;
                    OnPropertyChanged(nameof(DisableCopilotAutostart));
                }
            }
            catch { }
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
                    if (Settings.AutoSortByPing)
                    {
                        _ = Task.Run(PingAllServersAsync);
                    }
                }
            }
            catch (Exception ex)
            {
                AddLog($"[-] Subscription update failed: {ex.Message}");
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

            var query = Servers.Where(s =>
                string.IsNullOrEmpty(q) ||
                s.Name.ToLowerInvariant().Contains(q) ||
                s.Protocol.ToLowerInvariant().Contains(q) ||
                s.CountryCode.ToLowerInvariant().Contains(q));

            if (Settings.AutoSortByPing)
            {
                query = query.OrderBy(s => s.PingMs > 0 ? s.PingMs : 999999);
            }

            foreach (var s in query)
            {
                FilteredServers.Add(s);
            }
        }

        public void SortServersByPing()
        {
            var currentSelectedId = SelectedServer?.Id;

            var sorted = Servers
                .OrderBy(s => s.PingMs > 0 ? s.PingMs : 999999)
                .ToList();

            Servers.Clear();
            foreach (var s in sorted)
            {
                Servers.Add(s);
            }

            FilterServers();

            if (!string.IsNullOrEmpty(currentSelectedId))
            {
                SelectedServer = Servers.FirstOrDefault(s => s.Id == currentSelectedId) ?? Servers.FirstOrDefault();
            }
            else if (Servers.Count > 0)
            {
                SelectedServer = Servers[0];
            }
        }

        public async Task ToggleConnectionAsync()
        {
            if (IsDisconnecting) return;

            if (IsConnected || IsConnecting)
            {
                await DisconnectAsync();
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
                AddLog("[-] Сервер для подключения не выбран.");
                return;
            }

            IsConnecting = true;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(ConnectionButtonText));
            OnPropertyChanged(nameof(ConnectionButtonBackground));

            var success = await _coreManager.StartAsync(SelectedServer, Settings);

            IsConnecting = false;
            if (success)
            {
                IsConnected = true;
                _connectionStartTime = DateTime.Now;
                _speedMonitor.Reset();
                _durationTimer.Start();
                (System.Windows.Application.Current as App)?.UpdateTrayIcon(true, SelectedServer.CleanName);
                (System.Windows.Application.Current as App)?.ShowNotification("Бункер Кота VPN", $"Успешно подключено к {SelectedServer.CleanName}");
                NotifyServiceStatusChanged();
                OnPropertyChanged(nameof(SpeedVisibility));
                _ = Task.Run(RunUrlTestAsync);
            }
            else
            {
                IsConnected = false;
                (System.Windows.Application.Current as App)?.UpdateTrayIcon(false, "");
                NotifyServiceStatusChanged();
            }

            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(ConnectionButtonText));
            OnPropertyChanged(nameof(ConnectionButtonBackground));
        }

        public async Task DisconnectAsync()
        {
            if (IsDisconnecting) return;
            IsDisconnecting = true;
            IsConnecting = false;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(StatusColor));
            OnPropertyChanged(nameof(ConnectionButtonText));
            OnPropertyChanged(nameof(ConnectionButtonBackground));

            try
            {
                await _coreManager.StopAsync();
            }
            catch (Exception ex)
            {
                AddLog($"[-] Ошибка при отключении: {ex.Message}");
            }
            finally
            {
                _durationTimer.Stop();
                _speedMonitor.Reset();
                _customTunnelHealthStatus = null;
                IsConnected = false;
                IsDisconnecting = false;
                DurationText = "00:00:00";
                UrlTestResult = "URL Тест";
                (System.Windows.Application.Current as App)?.UpdateTrayIcon(false, "");
                NotifyServiceStatusChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(ConnectionButtonText));
                OnPropertyChanged(nameof(ConnectionButtonBackground));
                OnPropertyChanged(nameof(SpeedVisibility));
                OnPropertyChanged(nameof(DownloadSpeedText));
                OnPropertyChanged(nameof(UploadSpeedText));
                OnPropertyChanged(nameof(TunnelHealthStatus));
            }
        }

        public void Disconnect()
        {
            _ = Task.Run(async () => await DisconnectAsync());
        }

        public async Task CheckInternetAccessAsync()
        {
            if (IsTestingInternet) return;
            IsTestingInternet = true;
            TunnelHealthStatus = "🟡 Замер доступа в сеть...";
            AddLog("[*] [Диагностика] Проверка реального доступа в сеть через туннель...");

            try
            {
                var latency = await PingService.TestUrlLatencyAsync("https://cp.cloudflare.com/generate_204", 3000);
                if (latency <= 0)
                {
                    latency = await PingService.TestUrlLatencyAsync("https://www.google.com/generate_204", 3000);
                }

                if (latency > 0)
                {
                    TunnelHealthStatus = $"🟢 Доступ в сеть: Есть ({latency} мс)";
                    AddLog($"[+] [Диагностика] Интернет доступен! Шлюз ответил за {latency} мс");
                }
                else
                {
                    TunnelHealthStatus = "🔴 Нет ответа (проверьте узел)";
                    AddLog("[-] [Диагностика] Ошибка: внешний шлюз не ответил. Проверьте сервер или смените узел.");
                }
            }
            catch (Exception ex)
            {
                TunnelHealthStatus = "🔴 Ошибка проверки";
                AddLog($"[-] [Диагностика] Ошибка: {ex.Message}");
            }
            finally
            {
                IsTestingInternet = false;
            }
        }

        public async Task RunUrlTestAsync()
        {
            if (IsTestingUrl) return;
            IsTestingUrl = true;
            UrlTestResult = "Замер...";

            try
            {
                var latency = await PingService.TestUrlLatencyAsync("https://cp.cloudflare.com/generate_204");
                if (latency > 0)
                {
                    UrlTestResult = $"{latency} ms (204)";
                    AddLog($"[+] URL Test 204: {latency} ms (Cloudflare)");
                }
                else
                {
                    var fallback = await PingService.TestUrlLatencyAsync("https://www.google.com/generate_204");
                    if (fallback > 0)
                    {
                        UrlTestResult = $"{fallback} ms (Google)";
                        AddLog($"[+] URL Test 204: {fallback} ms (Google)");
                    }
                    else
                    {
                        UrlTestResult = "Таймаут";
                        AddLog("[-] URL Test: таймаут / трафик не проходит. Возможно, данный узел временно перегружен или заблокирован ТСПУ. Попробуйте другой сервер из списка!");
                    }
                }
            }
            catch (Exception ex)
            {
                UrlTestResult = "Ошибка";
                AddLog($"[-] URL Test error: {ex.Message}");
            }
            finally
            {
                IsTestingUrl = false;
            }
        }

        public async Task PingAllServersAsync()
        {
            if (IsPinging || Servers.Count == 0) return;
            IsPinging = true;

            try
            {
                await PingService.TestAllNodesAsync(Servers, () =>
                {
                    if (Settings.AutoSortByPing)
                    {
                        System.Windows.Application.Current?.Dispatcher.Invoke(FilterServers);
                    }
                });

                if (Settings.AutoSortByPing)
                {
                    System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                    {
                        SortServersByPing();

                        // If not currently connected, select lowest ping LIVE server
                        var liveServers = FilteredServers.Where(s => s.PingMs > 0).ToList();
                        if (!IsConnected && liveServers.Count > 0)
                        {
                            SelectedServer = liveServers[0];
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                AddLog($"[-] Ping test error: {ex.Message}");
            }
            finally
            {
                IsPinging = false;
            }
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
