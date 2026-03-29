using Firewall.Traffic.ViewModels;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using MinimalFirewall.Pages;
using MinimalFirewall.TypedObjects;
using System;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Graphics;
using WinRT.Interop;

namespace MinimalFirewall
{
    public sealed partial class MainWindow : Window
    {
        #region Fields
        private FirewallDataService _dataService = null!;
        private FirewallActionsService _actionsService = null!;
        private FirewallEventListenerService _eventListenerService = null!;
        private FirewallSentryService _firewallSentryService = null!;
        private FirewallRuleService _firewallRuleService = null!;
        private UserActivityLogger _activityLogger = null!;
        private WildcardRuleService _wildcardRuleService = null!;
        private ForeignRuleTracker _foreignRuleTracker = null!;
        private AppSettings _appSettings = null!;
        private StartupService _startupService = null!;
        private FirewallGroupManager _groupManager = null!;
        private IconService _iconService = null!;
        private PublisherWhitelistService _whitelistService = null!;
        private BackgroundFirewallTaskService _backgroundTaskService = null!;
        private MainViewModel _mainViewModel = null!;

        private TrayIconManager? _trayIconManager;
        private System.Threading.Timer? _autoRefreshTimer;
        private System.Threading.Timer? _trayBlinkTimer;
        private bool _trayBlinkState = false;
        private bool _isSentryServiceStarted = false;
        private readonly bool _startMinimized;
        private CancellationTokenSource? _scanCts;

        // Page instances (lazy)
        private DashboardPage? _dashboardPage;
        private RulesPage? _rulesPage;
        private WildcardRulesPage? _wildcardRulesPage;
        private AuditPage? _auditPage;
        private GroupsPage? _groupsPage;
        private LiveConnectionsPage? _liveConnectionsPage;
        private SettingsPage? _settingsPage;
        #endregion

        public MainWindow(bool startMinimized = false)
        {
            _startMinimized = startMinimized;
            InitializeComponent();
            ConfigPathManager.EnsureStorageDirectoryExists();
            _appSettings = AppSettings.Load();

            // Window setup
            Title = "Minimal Firewall";
            var appWindow = GetAppWindow();
            if (appWindow != null)
            {
                appWindow.SetIcon("logo.ico");
                RestoreWindowState(appWindow);
            }

            // Set ExtendsContentIntoTitleBar for custom title bar
            this.ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);

            InitializeServices();
            SetupTrayIcon();

            _mainViewModel.PendingConnections.CollectionChanged += PendingConnections_CollectionChanged;
            _mainViewModel.PopupRequired += OnPopupRequired;
            _mainViewModel.DashboardActionProcessed += OnDashboardActionProcessed;
            _mainViewModel.SystemChangesUpdated += () => DispatcherQueue.TryEnqueue(UpdateUiWithChangesCount);
            _backgroundTaskService.QueueCountChanged += OnQueueCountChanged;
            _backgroundTaskService.WildcardRulesChanged += OnWildcardRulesChanged;
            _mainViewModel.StatusTextChanged += text => DispatcherQueue.TryEnqueue(() => StatusTextBlock.Text = text);

            Microsoft.Win32.SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;

            this.Closed += MainWindow_Closed;
        }

        private AppWindow? GetAppWindow()
        {
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(hwnd);
            return AppWindow.GetFromWindowId(windowId);
        }

        private void InitializeServices()
        {
            _startupService = new StartupService();
            _groupManager = new FirewallGroupManager();
            _iconService = new IconService();
            _whitelistService = new PublisherWhitelistService();
            _firewallRuleService = new FirewallRuleService();
            _activityLogger = new UserActivityLogger { IsEnabled = _appSettings.IsLoggingEnabled };
            _wildcardRuleService = new WildcardRuleService();
            _foreignRuleTracker = new ForeignRuleTracker();

            var uwpService = new UwpService(_firewallRuleService);
            _dataService = new FirewallDataService(_firewallRuleService, _wildcardRuleService, uwpService);
            _firewallSentryService = new FirewallSentryService(_firewallRuleService);
            var trafficMonitorViewModel = new TrafficMonitorViewModel();

            _eventListenerService = new FirewallEventListenerService(
                _dataService, _wildcardRuleService,
                () => _mainViewModel.IsLockedDown,
                msg => _activityLogger.LogDebug(msg),
                _appSettings, _whitelistService, null!);

            _actionsService = new FirewallActionsService(
                _firewallRuleService, _activityLogger, _eventListenerService,
                _foreignRuleTracker, _firewallSentryService, _whitelistService,
                _wildcardRuleService, _dataService);
            _eventListenerService.ActionsService = _actionsService;

            _backgroundTaskService = new BackgroundFirewallTaskService(
                _actionsService, _activityLogger, _wildcardRuleService, _dataService);
            _actionsService.BackgroundTaskService = _backgroundTaskService;

            // Register async dialog callback for ViewModel (replaces StatusForm)
            _mainViewModel = new MainViewModel(
                _firewallRuleService, _wildcardRuleService, _backgroundTaskService,
                _dataService, _firewallSentryService, _foreignRuleTracker,
                trafficMonitorViewModel, _eventListenerService, _appSettings,
                _activityLogger, _actionsService);
        }

        #region Page Navigation

        private void NavView_Loaded(object sender, RoutedEventArgs e)
        {
            // Select Dashboard by default
            NavView.SelectedItem = NavView.MenuItems[0];
        }

        private async void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                await NavigateToPageAsync(item.Tag?.ToString() ?? "Dashboard");
            }
        }

        private async Task NavigateToPageAsync(string tag)
        {
            Page page = tag switch
            {
                "Dashboard" => GetOrCreatePage(ref _dashboardPage,
                    () => new DashboardPage(_mainViewModel, _appSettings, _iconService, _wildcardRuleService, _actionsService, _backgroundTaskService)),
                "Rules" => GetOrCreatePage(ref _rulesPage,
                    () => new RulesPage(_mainViewModel, _actionsService, _wildcardRuleService, _backgroundTaskService, _iconService, _appSettings)),
                "WildcardRules" => GetOrCreatePage(ref _wildcardRulesPage,
                    () => new WildcardRulesPage(_wildcardRuleService, _backgroundTaskService, _appSettings)),
                "Audit" => GetOrCreatePage(ref _auditPage,
                    () => new AuditPage(_mainViewModel, _foreignRuleTracker, _firewallSentryService, _appSettings)),
                "Groups" => GetOrCreatePage(ref _groupsPage,
                    () => new GroupsPage(_groupManager, _backgroundTaskService)),
                "LiveConnections" => GetOrCreatePage(ref _liveConnectionsPage,
                    () => new LiveConnectionsPage(_mainViewModel.TrafficMonitorViewModel, _appSettings, _iconService, _backgroundTaskService, _actionsService)),
                "Settings" => GetOrCreatePage(ref _settingsPage,
                    () => new SettingsPage(_appSettings, _startupService, _whitelistService, _actionsService, _activityLogger, _mainViewModel,
                        "Version " + Assembly.GetExecutingAssembly().GetName()?.Version?.ToString(3),
                        OnThemeChanged, SetupAutoRefreshTimer)),
                _ => GetOrCreatePage(ref _dashboardPage,
                    () => new DashboardPage(_mainViewModel, _appSettings, _iconService, _wildcardRuleService, _actionsService, _backgroundTaskService))
            };

            ContentFrame.Content = page;

            // Call OnNavigatedTo logic
            if (page is DashboardPage dp && tag == "Dashboard")
            {
                // Dashboard loads lazily when its items populate
            }
            else if (page is RulesPage rp && tag == "Rules")
            {
                await rp.OnTabSelectedAsync();
            }
            else if (page is WildcardRulesPage wrp && tag == "WildcardRules")
            {
                wrp.LoadRules();
            }
            else if (page is AuditPage ap && tag == "Audit")
            {
                ap.ApplySearchFilter();
            }
            else if (page is GroupsPage gp && tag == "Groups")
            {
                await gp.OnTabSelectedAsync();
            }
            else if (page is LiveConnectionsPage lcp && tag == "LiveConnections")
            {
                lcp.OnTabSelected(_appSettings);
            }
            else if (page is SettingsPage sp && tag == "Settings")
            {
                sp.LoadSettingsToUI();
            }

            // Update pending badge
            UpdatePendingBadge();
        }

        private static T GetOrCreatePage<T>(ref T? field, Func<T> factory) where T : Page
        {
            return field ??= factory();
        }

        #endregion

        #region Lockdown & Rescan

        private void LockdownButton_Click(object sender, RoutedEventArgs e)
        {
            _mainViewModel.ToggleLockdownMode();
            UpdateLockdownButtonState();
            UpdateTrayStatus();
        }

        private async void RescanButton_Click(object sender, RoutedEventArgs e)
        {
            RescanButton.IsEnabled = false;
            try
            {
                await ForceDataRefreshAsync();
            }
            finally
            {
                RescanButton.IsEnabled = true;
            }
        }

        private void UpdateLockdownButtonState()
        {
            bool locked = _mainViewModel.IsLockedDown;
            LockdownIcon.Glyph = locked ? "\uE72E" : "\uE785"; // lock / unlock
            LockdownButton.Style = locked
                ? (Style)Application.Current.Resources["AccentButtonStyle"]
                : null;
            LockdownButton.ToolTipService_ToolTip(locked ? "Disable Lockdown" : "Enable Lockdown");

            // Show/hide dashboard
            if (_dashboardPage != null)
                _dashboardPage.IsActive = locked;

            UpdatePendingBadge();
        }

        private void UpdatePendingBadge()
        {
            int count = _mainViewModel.PendingConnections.Count;
            PendingCountBadge.Text = count > 0 ? $"{count} pending" : "";
        }

        #endregion

        #region Data Refresh

        private async Task ForceDataRefreshAsync(bool forceUwpScan = false)
        {
            _scanCts?.Cancel();
            _scanCts = new CancellationTokenSource();
            var token = _scanCts.Token;
            StatusTextBlock.Text = "Refreshing…";
            try
            {
                await _mainViewModel.RefreshRulesDataAsync(token);
                if (_rulesPage != null)
                    await _rulesPage.OnTabSelectedAsync();
                StatusTextBlock.Text = "";
            }
            catch (OperationCanceledException) { }
        }

        private void SetupAutoRefreshTimer()
        {
            _autoRefreshTimer?.Dispose();
            if (_appSettings.AutoRefreshIntervalMinutes > 0)
            {
                var interval = TimeSpan.FromMinutes(_appSettings.AutoRefreshIntervalMinutes);
                _autoRefreshTimer = new System.Threading.Timer(_ =>
                {
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        var selected = (NavView.SelectedItem as NavigationViewItem)?.Tag?.ToString();
                        if (selected == "Rules")
                            await ForceDataRefreshAsync();
                    });
                }, null, interval, interval);
            }
        }

        #endregion

        #region Theme

        private void OnThemeChanged()
        {
            bool isDark = _appSettings.Theme == "Dark" ||
                (_appSettings.Theme == "Auto" && IsSystemDarkMode());
            RequestedTheme = isDark ? ElementTheme.Dark : ElementTheme.Light;
        }

        private static bool IsSystemDarkMode()
        {
            try
            {
                var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return (int?)key?.GetValue("AppsUseLightTheme") == 0;
            }
            catch { return false; }
        }

        private void SystemEvents_UserPreferenceChanged(object sender, Microsoft.Win32.UserPreferenceChangedEventArgs e)
        {
            if (e.Category == Microsoft.Win32.UserPreferenceCategory.General && _appSettings.Theme == "Auto")
            {
                DispatcherQueue.TryEnqueue(OnThemeChanged);
            }
        }

        #endregion

        #region Tray Icon

        private void SetupTrayIcon()
        {
            _trayIconManager = new TrayIconManager(
                onShow: () => DispatcherQueue.TryEnqueue(ShowFromTray),
                onToggleLockdown: () => DispatcherQueue.TryEnqueue(() =>
                {
                    _mainViewModel.ToggleLockdownMode();
                    UpdateLockdownButtonState();
                    UpdateTrayStatus();
                }),
                onExit: () => DispatcherQueue.TryEnqueue(ExitApplication));
        }

        private void UpdateTrayStatus()
        {
            bool locked = _mainViewModel.IsLockedDown;

            if (!locked)
            {
                _trayBlinkTimer?.Change(Timeout.Infinite, Timeout.Infinite);
                bool hasAlerts = _appSettings.AlertOnForeignRules && _mainViewModel.UnseenSystemChangesCount > 0;
                _trayIconManager?.SetIcon(hasAlerts ? TrayIconState.Alert : TrayIconState.Unlocked, locked);
            }
            else
            {
                _trayIconManager?.SetIcon(TrayIconState.Locked, locked);
            }
        }

        private void ShowFromTray()
        {
            if (_mainViewModel.IsLockedDown)
                _eventListenerService.Start();
            if (_isSentryServiceStarted)
                _firewallSentryService.Start();

            SetupAutoRefreshTimer();
            this.Activate();
            var appWindow = GetAppWindow();
            appWindow?.Show();
        }

        private void ExitApplication()
        {
            _mainWindow_Cleanup();
            Microsoft.UI.Xaml.Application.Current.Exit();
        }

        #endregion

        #region Pending Connections & Popups

        private void PendingConnections_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                bool locked = _mainViewModel.IsLockedDown;
                int count = _mainViewModel.PendingConnections.Count;

                if (count > 0 && locked)
                {
                    StartTrayBlink();
                }
                else
                {
                    StopTrayBlink();
                    UpdateTrayStatus();
                }
                UpdatePendingBadge();
            });
        }

        private void StartTrayBlink()
        {
            _trayBlinkTimer ??= new System.Threading.Timer(TrayBlinkTick, null, 0, 500);
        }

        private void StopTrayBlink()
        {
            _trayBlinkTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void TrayBlinkTick(object? state)
        {
            _trayBlinkState = !_trayBlinkState;
            _trayIconManager?.SetIcon(_trayBlinkState ? TrayIconState.Alert : TrayIconState.Locked, true);
        }

        private void OnPopupRequired(PendingConnectionViewModel pending)
        {
            DispatcherQueue.TryEnqueue(async () =>
            {
                if (!_appSettings.IsPopupsEnabled) return;
                await ShowNotifierAsync(pending);
            });
        }

        private async Task ShowNotifierAsync(PendingConnectionViewModel pending)
        {
            var dialog = new Dialogs.NotifierDialog(pending);
            dialog.XamlRoot = ContentFrame.XamlRoot;
            var result = await dialog.ShowAsync();

            // Process result
            var decision = dialog.UserDecision;
            var payload = new ProcessPendingConnectionPayload
            {
                PendingConnection = pending,
                Decision = decision,
                Duration = dialog.TemporaryDuration,
                TrustPublisher = dialog.TrustPublisher
            };
            _backgroundTaskService.EnqueueTask(new FirewallTask(FirewallTaskType.ProcessPendingConnection, payload));
            _mainViewModel.PendingConnections.Remove(pending);
        }

        private void OnDashboardActionProcessed(PendingConnectionViewModel processedConnection)
        {
            // No active notifier to close in WinUI 3 (dialogs are awaited)
        }

        #endregion

        #region Window Lifecycle

        private async void MainWindow_Activated(object sender, WindowActivatedEventArgs args)
        {
            if (!_startMinimized)
            {
                await StartupActivationAsync();
            }
            else
            {
                _ = PrepareForTrayAsync();
            }
        }

        private bool _startupDone = false;

        private async Task StartupActivationAsync()
        {
            if (_startupDone) return;
            _startupDone = true;

            OnThemeChanged();
            await NavigateToPageAsync("Dashboard");

            // Fade in using opacity (AppWindow)
            var appWindow = GetAppWindow();
            if (appWindow != null)
                RestoreWindowState(appWindow);

            _actionsService.CleanupTemporaryRulesOnStartup();
            if (_appSettings.StartOnSystemStartup)
                _startupService.VerifyAndCorrectStartupTaskPath();

            if (_mainViewModel.IsLockedDown)
            {
                _eventListenerService.EnableAuditing();
                _eventListenerService.Start();
            }
            else
            {
                _eventListenerService.DisableAuditing();
            }

            UpdateTrayStatus();
            UpdateLockdownButtonState();
            await Task.Run(_actionsService.ReenableMfwRules);

            string versionInfo = "Version " + Assembly.GetExecutingAssembly().GetName()?.Version?.ToString(3);
            _activityLogger.LogDebug("Application Started: " + versionInfo);
            SetupAutoRefreshTimer();
        }

        private void RestoreWindowState(AppWindow appWindow)
        {
            int w = _appSettings.WindowSize.Width;
            int h = _appSettings.WindowSize.Height;
            if (w > 400 && h > 300)
                appWindow.Resize(new SizeInt32(w, h));

            bool maximized = _appSettings.WindowState == 2;
            if (maximized)
            {
                var presenter = appWindow.Presenter as OverlappedPresenter;
                presenter?.Maximize();
            }
        }

        private void SaveWindowState()
        {
            var appWindow = GetAppWindow();
            if (appWindow == null) return;

            bool maximized = appWindow.Presenter is OverlappedPresenter op && op.State == OverlappedPresenterState.Maximized;
            var size = appWindow.Size;
            _appSettings.WindowSize = new System.Drawing.Size(size.Width, size.Height);
            _appSettings.WindowState = maximized ? 2 : 1;
            _appSettings.Save();
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            if (_appSettings.CloseToTray)
            {
                args.Handled = true;
                SaveWindowState();
                var appWindow = GetAppWindow();
                appWindow?.Hide();
                _ = PrepareForTrayAsync();
            }
            else
            {
                _mainWindow_Cleanup();
            }
        }

        private async Task PrepareForTrayAsync()
        {
            _scanCts?.Cancel();
            _firewallSentryService.Stop();
            _mainViewModel.TrafficMonitorViewModel.StopMonitoring();
            _autoRefreshTimer?.Dispose();

            _mainViewModel.ClearRulesData();
            _mainViewModel.PendingConnections.Clear();
            _mainViewModel.SystemChanges.Clear();

            _dataService.ClearCaches();
            _iconService.ClearCache();

            await Task.Run(() =>
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                SetProcessWorkingSetSize(System.Diagnostics.Process.GetCurrentProcess().Handle,
                    new IntPtr(-1), new IntPtr(-1));
            });
        }

        private void _mainWindow_Cleanup()
        {
            Microsoft.Win32.SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
            _mainViewModel.PendingConnections.CollectionChanged -= PendingConnections_CollectionChanged;
            _mainViewModel.PopupRequired -= OnPopupRequired;
            _backgroundTaskService.QueueCountChanged -= OnQueueCountChanged;
            _trayIconManager?.Dispose();
            _trayBlinkTimer?.Dispose();
            _autoRefreshTimer?.Dispose();
            _backgroundTaskService?.Dispose();
            _firewallSentryService?.Dispose();
            _eventListenerService?.Dispose();
        }

        private void OnQueueCountChanged(int count)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (count == 0)
                    _mainViewModel.ClearRulesCache();
            });
        }

        private void OnWildcardRulesChanged()
        {
            DispatcherQueue.TryEnqueue(() => _wildcardRulesPage?.LoadRules());
        }

        private void UpdateUiWithChangesCount()
        {
            int unseen = _mainViewModel.UnseenSystemChangesCount;
            // Update audit nav item badge
            if (NavView.FooterMenuItems.Count > 0)
            {
                // Update status tray
                UpdateTrayStatus();
            }
        }

        #endregion

        #region Native P/Invokes

        [LibraryImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool SetProcessWorkingSetSize(
            IntPtr process, IntPtr minimumWorkingSetSize, IntPtr maximumWorkingSetSize);

        #endregion
    }

    // Helper extension to set tooltip via code
    internal static class ButtonExtensions
    {
        public static void ToolTipService_ToolTip(this Button btn, string tip)
        {
            ToolTipService.SetToolTip(btn, tip);
        }
    }
}
