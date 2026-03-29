using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MinimalFirewall.TypedObjects;
using System;
using System.Collections.Specialized;
using System.Threading.Tasks;

namespace MinimalFirewall.Pages
{
    public sealed partial class DashboardPage : Page
    {
        private readonly MainViewModel _viewModel;
        private readonly AppSettings _appSettings;
        private readonly IconService _iconService;
        private readonly WildcardRuleService _wildcardRuleService;
        private readonly FirewallActionsService _actionsService;
        private readonly BackgroundFirewallTaskService _backgroundTaskService;

        private bool _isActive;

        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                LandingGrid.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
                ActiveGrid.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public DashboardPage(
            MainViewModel viewModel,
            AppSettings appSettings,
            IconService iconService,
            WildcardRuleService wildcardRuleService,
            FirewallActionsService actionsService,
            BackgroundFirewallTaskService backgroundTaskService)
        {
            _viewModel = viewModel;
            _appSettings = appSettings;
            _iconService = iconService;
            _wildcardRuleService = wildcardRuleService;
            _actionsService = actionsService;
            _backgroundTaskService = backgroundTaskService;

            InitializeComponent();

            // Bind list
            PendingListView.ItemsSource = _viewModel.PendingConnections;
            _viewModel.PendingConnections.CollectionChanged += PendingConnections_Changed;

            // Initial state
            IsActive = _viewModel.IsLockedDown;
        }

        private void PendingConnections_Changed(object? sender, NotifyCollectionChangedEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                StatusText.Text = _viewModel.PendingConnections.Count > 0
                    ? $"{_viewModel.PendingConnections.Count} pending connection(s)"
                    : "";
            });
        }

        private void AllowButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is PendingConnectionViewModel pending)
                _viewModel.ProcessDashboardAction(pending, "Allow");
        }

        private void BlockButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is PendingConnectionViewModel pending)
                _viewModel.ProcessDashboardAction(pending, "Block");
        }

        private void IgnoreButton_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is PendingConnectionViewModel pending)
                _viewModel.ProcessDashboardAction(pending, "Ignore");
        }

        private void AllowAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var pending in _viewModel.PendingConnections.ToList())
                _viewModel.ProcessDashboardAction(pending, "Allow");
        }

        private void BlockAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var pending in _viewModel.PendingConnections.ToList())
                _viewModel.ProcessDashboardAction(pending, "Block");
        }

        private async void PendingListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PendingListView.SelectedItem is not PendingConnectionViewModel pending)
            {
                DetailsTextBlock.Text = "";
                return;
            }

            await ShowDetailsAsync(pending);
        }

        private async Task ShowDetailsAsync(PendingConnectionViewModel pending)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Application:  {pending.FileName}");
            sb.AppendLine($"Path:         {pending.AppPath}");
            sb.AppendLine($"PID:          {pending.ProcessId}");
            if (!string.IsNullOrEmpty(pending.ProcessOwner))
                sb.AppendLine($"Owner:        {pending.ProcessOwner}");
            if (!string.IsNullOrEmpty(pending.ParentProcessId))
            {
                string parentDisplay = string.IsNullOrEmpty(pending.ParentProcessName)
                    ? pending.ParentProcessId
                    : $"{pending.ParentProcessName} (PID: {pending.ParentProcessId})";
                sb.AppendLine($"Parent:       {parentDisplay}");
            }
            sb.AppendLine($"Service:      {(string.IsNullOrEmpty(pending.ServiceName) ? "N/A" : pending.ServiceName)}");
            sb.AppendLine($"Direction:    {pending.Direction}");
            string remote = string.IsNullOrEmpty(pending.RemoteAddress) ? "N/A" : $"{pending.RemoteAddress}:{pending.RemotePort}";
            sb.AppendLine($"Remote:       {remote}");
            sb.AppendLine($"Protocol:     {pending.Protocol}");
            if (!string.IsNullOrEmpty(pending.CommandLine))
                sb.AppendLine($"CMD:          {pending.CommandLine}");

            DetailsTextBlock.Text = sb.ToString();

            // Async publisher lookup
            if (!string.IsNullOrEmpty(pending.AppPath))
            {
                string? publisher = await Task.Run(() =>
                {
                    SignatureValidationService.GetPublisherInfo(pending.AppPath, out string name);
                    return name;
                });
                if (!string.IsNullOrEmpty(publisher) && PendingListView.SelectedItem == pending)
                {
                    DetailsTextBlock.Text += $"Publisher:    {publisher}\n";
                }
            }
        }
    }
}
