using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MinimalFirewall.Dialogs;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace MinimalFirewall.Pages
{
    public sealed partial class SettingsPage : Page
    {
        private readonly AppSettings _appSettings;
        private readonly StartupService _startupService;
        private readonly PublisherWhitelistService _whitelistService;
        private readonly FirewallActionsService _actionsService;
        private readonly UserActivityLogger _activityLogger;
        private readonly MainViewModel _mainViewModel;
        private readonly string _version;
        private readonly Action _onThemeChanged;
        private readonly Action _onAutoRefreshChanged;

        private bool _loading; // suppress change events during load

        public SettingsPage(
            AppSettings appSettings,
            StartupService startupService,
            PublisherWhitelistService whitelistService,
            FirewallActionsService actionsService,
            UserActivityLogger activityLogger,
            MainViewModel mainViewModel,
            string version,
            Action onThemeChanged,
            Action onAutoRefreshChanged)
        {
            _appSettings = appSettings;
            _startupService = startupService;
            _whitelistService = whitelistService;
            _actionsService = actionsService;
            _activityLogger = activityLogger;
            _mainViewModel = mainViewModel;
            _version = version;
            _onThemeChanged = onThemeChanged;
            _onAutoRefreshChanged = onAutoRefreshChanged;
            InitializeComponent();
        }

        public void LoadSettingsToUI()
        {
            _loading = true;
            AutoThemeSwitch.IsOn = _appSettings.Theme == "Auto";
            DarkModeSwitch.IsOn = _appSettings.Theme == "Dark" ||
                (_appSettings.Theme == "Auto" && IsSystemDarkMode());
            DarkModeSwitch.IsEnabled = !AutoThemeSwitch.IsOn;
            ShowAppIconsSwitch.IsOn = _appSettings.ShowAppIcons;
            CloseToTraySwitch.IsOn = _appSettings.CloseToTray;
            StartOnStartupSwitch.IsOn = _appSettings.StartOnSystemStartup;
            PopupsSwitch.IsOn = _appSettings.IsPopupsEnabled;
            LoggingSwitch.IsOn = _appSettings.IsLoggingEnabled;
            AutoAllowSystemTrustedSwitch.IsOn = _appSettings.AutoAllowSystemTrusted;
            AuditAlertsSwitch.IsOn = _appSettings.AlertOnForeignRules;
            TrafficMonitorSwitch.IsOn = _appSettings.IsTrafficMonitorEnabled;
            AutoRefreshBox.Value = _appSettings.AutoRefreshIntervalMinutes;
            VersionLabel.Text = _version;
            _loading = false;
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

        private void AutoThemeSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            DarkModeSwitch.IsEnabled = !AutoThemeSwitch.IsOn;
            _appSettings.Theme = AutoThemeSwitch.IsOn ? "Auto" : (DarkModeSwitch.IsOn ? "Dark" : "Light");
            _onThemeChanged();
        }

        private void DarkModeSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading || AutoThemeSwitch.IsOn) return;
            _appSettings.Theme = DarkModeSwitch.IsOn ? "Dark" : "Light";
            _onThemeChanged();
        }

        private void ShowAppIconsSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _appSettings.ShowAppIcons = ShowAppIconsSwitch.IsOn;
        }

        private void Save_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _appSettings.CloseToTray = CloseToTraySwitch.IsOn;
            _appSettings.IsPopupsEnabled = PopupsSwitch.IsOn;
            _appSettings.AutoAllowSystemTrusted = AutoAllowSystemTrustedSwitch.IsOn;
            _appSettings.AlertOnForeignRules = AuditAlertsSwitch.IsOn;
            _appSettings.Save();
        }

        private void StartOnStartupSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _appSettings.StartOnSystemStartup = StartOnStartupSwitch.IsOn;
            _startupService.SetStartup(_appSettings.StartOnSystemStartup);
        }

        private void LoggingSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _appSettings.IsLoggingEnabled = LoggingSwitch.IsOn;
            _activityLogger.IsEnabled = LoggingSwitch.IsOn;
        }

        private void TrafficMonitorSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (_loading) return;
            _appSettings.IsTrafficMonitorEnabled = TrafficMonitorSwitch.IsOn;
            if (!TrafficMonitorSwitch.IsOn)
                _mainViewModel.TrafficMonitorViewModel.StopMonitoring();
        }

        private void AutoRefreshBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (_loading) return;
            int val = (int)args.NewValue;
            if (val >= 1)
            {
                _appSettings.AutoRefreshIntervalMinutes = val;
                _onAutoRefreshChanged();
            }
        }

        private async void ManagePublishersButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ManagePublishersDialog(_whitelistService);
            dialog.XamlRoot = XamlRoot;
            await dialog.ShowAsync();
        }

        private void CleanUpOrphanedButton_Click(object sender, RoutedEventArgs e)
        {
            _ = _mainViewModel.CleanUpOrphanedRulesAsync();
        }

        private async void ExportRulesButton_Click(object sender, RoutedEventArgs e)
        {
            string json = await _actionsService.ExportAllMfwRulesAsync();
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker,
                WinRT.Interop.WindowNative.GetWindowHandle(App.Instance.MainWindow!));
            picker.SuggestedFileName = "MinimalFirewall_rules.json";
            picker.FileTypeChoices.Add("JSON", new[] { ".json" });
            var file = await picker.PickSaveFileAsync();
            if (file == null) return;
            await Windows.Storage.FileIO.WriteTextAsync(file, json);
        }

        private async void ImportMergeButton_Click(object sender, RoutedEventArgs e)
            => await ImportRulesAsync(replace: false);

        private async void ImportReplaceButton_Click(object sender, RoutedEventArgs e)
            => await ImportRulesAsync(replace: true);

        private async Task ImportRulesAsync(bool replace)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker,
                WinRT.Interop.WindowNative.GetWindowHandle(App.Instance.MainWindow!));
            picker.FileTypeFilter.Add(".json");
            var file = await picker.PickSingleFileAsync();
            if (file == null) return;
            string json = await Windows.Storage.FileIO.ReadTextAsync(file);
            await _actionsService.ImportRulesAsync(json, replace);
        }

        private async void DeleteAllRulesButton_Click(object sender, RoutedEventArgs e)
        {
            var confirm = new ContentDialog
            {
                Title = "Delete All MFW Rules",
                Content = "This will delete ALL Minimal Firewall rules. Are you sure?",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                XamlRoot = XamlRoot
            };
            if (await confirm.ShowAsync() == ContentDialogResult.Primary)
                _actionsService.DeleteAllMfwRules();
        }

        private async void RevertFirewallButton_Click(object sender, RoutedEventArgs e)
        {
            var confirm = new ContentDialog
            {
                Title = "Revert Firewall",
                Content = "Revert Windows Firewall to factory defaults? This removes ALL custom rules.",
                PrimaryButtonText = "Revert",
                CloseButtonText = "Cancel",
                XamlRoot = XamlRoot
            };
            if (await confirm.ShowAsync() == ContentDialogResult.Primary)
            {
                // Revert via netsh
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "netsh.exe",
                    Arguments = "advfirewall reset",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
        }

        private void OpenFirewallButton_Click(object sender, RoutedEventArgs e)
            => Process.Start("control.exe", "firewall.cpl");

        private void OpenAppDataButton_Click(object sender, RoutedEventArgs e)
            => Process.Start("explorer.exe", AppDomain.CurrentDomain.BaseDirectory);

        private void CheckUpdatesButton_Click(object sender, RoutedEventArgs e)
            => Process.Start(new ProcessStartInfo("https://github.com/deminimis/minimalfirewall/releases") { UseShellExecute = true });

        private async void ExportDiagnosticButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            WinRT.Interop.InitializeWithWindow.Initialize(picker,
                WinRT.Interop.WindowNative.GetWindowHandle(App.Instance.MainWindow!));
            picker.SuggestedFileName = "MFW_Rules_Export.json";
            picker.FileTypeChoices.Add("JSON", new[] { ".json" });
            var file = await picker.PickSaveFileAsync();
            if (file == null) return;
            string json = await _actionsService.ExportAllMfwRulesAsync();
            await Windows.Storage.FileIO.WriteTextAsync(file, json);
        }
    }
}
