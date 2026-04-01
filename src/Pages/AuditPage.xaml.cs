using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MinimalFirewall.TypedObjects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace MinimalFirewall.Pages
{
    public sealed partial class AuditPage : Page
    {
        private readonly MainViewModel _mainViewModel;
        private readonly ForeignRuleTracker _foreignRuleTracker;
        private readonly FirewallSentryService _firewallSentryService;
        private readonly AppSettings _appSettings;

        private List<FirewallRuleChange> _allChanges = new();
        private List<FirewallRuleChange> _filteredChanges = new();
        private System.Threading.Timer? _searchDebounce;

        public AuditPage(
            MainViewModel mainViewModel,
            ForeignRuleTracker foreignRuleTracker,
            FirewallSentryService firewallSentryService,
            AppSettings appSettings)
        {
            _mainViewModel = mainViewModel;
            _foreignRuleTracker = foreignRuleTracker;
            _firewallSentryService = firewallSentryService;
            _appSettings = appSettings;
            InitializeComponent();

            QuarantineCheckBox.IsChecked = _appSettings.QuarantineMode;
            AuditSearchBox.Text = _appSettings.AuditSearchText;

            _mainViewModel.SystemChangesUpdated += OnSystemChangesUpdated;
            _mainViewModel.StatusTextChanged += text => DispatcherQueue.TryEnqueue(() => AuditStatusText.Text = text);
        }

        private void OnSystemChangesUpdated()
        {
            DispatcherQueue.TryEnqueue(ApplySearchFilter);
        }

        public void ApplySearchFilter()
        {
            _allChanges = _mainViewModel.SystemChanges;
            string search = AuditSearchBox.Text?.Trim() ?? "";

            _filteredChanges = string.IsNullOrEmpty(search)
                ? _allChanges
                : _allChanges.Where(c =>
                    (c.Rule.Name?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (c.Rule.ApplicationName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (c.Publisher?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                ).ToList();

            AuditDataGrid.ItemsSource = _filteredChanges;
            AuditStatusText.Text = $"{_filteredChanges.Count} change(s)";
        }

        private void AuditDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AuditDataGrid.SelectedItem is not FirewallRuleChange change)
            {
                DiffTextBlock.Text = "";
                return;
            }

            var sb = new StringBuilder();
            var r = change.Rule;
            sb.AppendLine($"Name:         {r.Name}");
            sb.AppendLine($"Status:       {change.Type}");
            sb.AppendLine($"Action:       {r.Status}");
            sb.AppendLine($"Direction:    {r.Direction}");
            sb.AppendLine($"Protocol:     {r.ProtocolName}");
            sb.AppendLine($"Program:      {r.ApplicationName}");
            sb.AppendLine($"Service:      {r.ServiceName}");
            sb.AppendLine($"Group:        {r.Grouping}");
            sb.AppendLine($"Profiles:     {r.Profiles}");
            sb.AppendLine($"Local Ports:  {r.LocalPorts}");
            sb.AppendLine($"Remote Ports: {r.RemotePorts}");
            sb.AppendLine($"Publisher:    {change.Publisher}");
            if (!string.IsNullOrEmpty(r.Description))
                sb.AppendLine($"Description:  {r.Description}");
            DiffTextBlock.Text = sb.ToString();
        }

        private void AuditDataGrid_Sorting(object? sender, DataGridColumnEventArgs e)
        {
            bool asc = e.Column.SortDirection != DataGridSortDirection.Ascending;
            e.Column.SortDirection = asc ? DataGridSortDirection.Ascending : DataGridSortDirection.Descending;

            _filteredChanges.Sort((a, b) =>
            {
                // Simple sort by timestamp for now
                int cmp = a.Timestamp.CompareTo(b.Timestamp);
                return asc ? cmp : -cmp;
            });
            AuditDataGrid.ItemsSource = null;
            AuditDataGrid.ItemsSource = _filteredChanges;
        }

        private void AuditSearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            _searchDebounce?.Dispose();
            _searchDebounce = new System.Threading.Timer(_ =>
            {
                _appSettings.AuditSearchText = sender.Text;
                DispatcherQueue.TryEnqueue(ApplySearchFilter);
            }, null, 300, Timeout.Infinite);
        }

        private void QuarantineCheckBox_Click(object sender, RoutedEventArgs e)
        {
            _appSettings.QuarantineMode = QuarantineCheckBox.IsChecked == true;
        }

        private async void RebuildBaselineButton_Click(object sender, RoutedEventArgs e)
        {
            await _mainViewModel.RebuildBaselineAsync();
        }

        public void ApplyThemeFixes() { /* WinUI 3 handles theming automatically */ }
    }
}
