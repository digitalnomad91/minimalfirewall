using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using MinimalFirewall.Dialogs;
using MinimalFirewall.TypedObjects;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;

namespace MinimalFirewall.Pages
{
    public sealed partial class RulesPage : Page
    {
        private readonly MainViewModel _mainViewModel;
        private readonly FirewallActionsService _actionsService;
        private readonly WildcardRuleService _wildcardRuleService;
        private readonly BackgroundFirewallTaskService _backgroundTaskService;
        private readonly IconService _iconService;
        private readonly AppSettings _appSettings;

        private SortableBindingList<AggregatedRuleViewModel> _currentRules = new();
        private System.Threading.Timer? _searchDebounce;
        private string _currentTag = "";

        public RulesPage(
            MainViewModel mainViewModel,
            FirewallActionsService actionsService,
            WildcardRuleService wildcardRuleService,
            BackgroundFirewallTaskService backgroundTaskService,
            IconService iconService,
            AppSettings appSettings)
        {
            _mainViewModel = mainViewModel;
            _actionsService = actionsService;
            _wildcardRuleService = wildcardRuleService;
            _backgroundTaskService = backgroundTaskService;
            _iconService = iconService;
            _appSettings = appSettings;

            InitializeComponent();

            // Load filter state
            ProgramFilter.IsChecked = _appSettings.FilterPrograms;
            ServiceFilter.IsChecked = _appSettings.FilterServices;
            UwpFilter.IsChecked = _appSettings.FilterUwp;
            WildcardFilter.IsChecked = _appSettings.FilterWildcards;
            SystemFilter.IsChecked = _appSettings.FilterSystem;
            SearchBox.Text = _appSettings.RulesSearchText;

            _mainViewModel.RulesListUpdated += OnRulesListUpdated;
        }

        private void OnRulesListUpdated()
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                _currentRules = _mainViewModel.VirtualRulesData;
                RulesDataGrid.ItemsSource = _currentRules;
                RulesStatusText.Text = $"{_currentRules.Count} rule(s)";
            });
        }

        public async Task OnTabSelectedAsync()
        {
            await _mainViewModel.RefreshRulesDataAsync(CancellationToken.None);
        }

        private void SearchBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            _searchDebounce?.Dispose();
            _searchDebounce = new System.Threading.Timer(_ =>
            {
                _appSettings.RulesSearchText = sender.Text;
                DispatcherQueue.TryEnqueue(async () => await OnTabSelectedAsync());
            }, null, 300, Timeout.Infinite);
        }

        private async void Filter_Changed(object sender, RoutedEventArgs e)
        {
            _appSettings.FilterPrograms = ProgramFilter.IsChecked == true;
            _appSettings.FilterServices = ServiceFilter.IsChecked == true;
            _appSettings.FilterUwp = UwpFilter.IsChecked == true;
            _appSettings.FilterWildcards = WildcardFilter.IsChecked == true;
            _appSettings.FilterSystem = SystemFilter.IsChecked == true;
            await OnTabSelectedAsync();
        }

        private void RulesDataGrid_Sorting(object? sender, DataGridColumnEventArgs e)
        {
            var propName = ((e.Column as DataGridBoundColumn)?.Binding as Microsoft.UI.Xaml.Data.Binding)?.Path?.Path;
            if (propName == null) return;

            bool asc = e.Column.SortDirection != DataGridSortDirection.Ascending;
            var dir = asc ? System.ComponentModel.ListSortDirection.Ascending
                          : System.ComponentModel.ListSortDirection.Descending;
            _currentRules.Sort(propName, dir);
            e.Column.SortDirection = asc ? DataGridSortDirection.Ascending : DataGridSortDirection.Descending;

            // Clear other columns
            foreach (var col in RulesDataGrid.Columns)
                if (col != e.Column) col.SortDirection = null;

            RulesDataGrid.ItemsSource = null;
            RulesDataGrid.ItemsSource = _currentRules;
        }

        private AggregatedRuleViewModel? GetSelectedRule()
            => RulesDataGrid.SelectedItem as AggregatedRuleViewModel;

        private IEnumerable<AggregatedRuleViewModel> GetSelectedRules()
        {
            if (RulesDataGrid.SelectedItems != null)
                foreach (var item in RulesDataGrid.SelectedItems)
                    if (item is AggregatedRuleViewModel r) yield return r;
        }

        private void RulesDataGrid_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            // Context menu shows on right click
            var flyout = FlyoutBase.GetAttachedFlyout(this) as MenuFlyout;
            flyout?.ShowAt(RulesDataGrid, new Microsoft.UI.Xaml.Controls.Primitives.FlyoutShowOptions
            {
                Position = e.GetPosition(RulesDataGrid)
            });
        }

        private async void RulesDataGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            await EditRuleAsync();
        }

        private void ApplyAction(string action)
        {
            foreach (var rule in GetSelectedRules())
                _actionsService.ApplyApplicationRuleChange(new List<string> { rule.ApplicationName ?? "" }, action);
        }

        private void AllowOutbound_Click(object s, RoutedEventArgs e) => ApplyAction("Allow (Outbound)");
        private void AllowInbound_Click(object s, RoutedEventArgs e) => ApplyAction("Allow (Inbound)");
        private void AllowAll_Click(object s, RoutedEventArgs e) => ApplyAction("Allow (Both)");
        private void BlockOutbound_Click(object s, RoutedEventArgs e) => ApplyAction("Block (Outbound)");
        private void BlockInbound_Click(object s, RoutedEventArgs e) => ApplyAction("Block (Inbound)");
        private void BlockAll_Click(object s, RoutedEventArgs e) => ApplyAction("Block (Both)");

        private async void EditRule_Click(object s, RoutedEventArgs e) => await EditRuleAsync();

        private async Task EditRuleAsync()
        {
            var rule = GetSelectedRule();
            if (rule == null) return;

            var dialog = new CreateAdvancedRuleDialog(_actionsService, rule.UnderlyingRules.FirstOrDefault(), _appSettings);
            dialog.XamlRoot = XamlRoot;
            await dialog.ShowAsync();
        }

        private void DeleteRule_Click(object s, RoutedEventArgs e)
        {
            var selected = GetSelectedRules().ToList();
            if (selected.Count == 0) return;
            var names = selected.Select(r => r.Name ?? "").Where(n => !string.IsNullOrEmpty(n)).ToList();
            if (names.Count > 0)
                _actionsService.DeleteAdvancedRules(names);
        }

        private void OpenFileLocation_Click(object s, RoutedEventArgs e)
        {
            var rule = GetSelectedRule();
            if (rule?.ApplicationName != null && File.Exists(rule.ApplicationName))
                Process.Start("explorer.exe", $"/select,\"{rule.ApplicationName}\"");
        }

        private void CopyDetails_Click(object s, RoutedEventArgs e)
        {
            var rule = GetSelectedRule();
            if (rule == null) return;
            var sb = new StringBuilder();
            sb.AppendLine($"Name: {rule.Name}");
            sb.AppendLine($"Inbound: {rule.InboundStatus}");
            sb.AppendLine($"Outbound: {rule.OutboundStatus}");
            sb.AppendLine($"Program: {rule.ApplicationName}");
            sb.AppendLine($"Service: {rule.ServiceName}");

            var dp = new DataPackage();
            dp.SetText(sb.ToString());
            Clipboard.SetContent(dp);
        }

        private async void CreateRuleButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new RuleWizardDialog(_actionsService, _wildcardRuleService, _backgroundTaskService, _appSettings);
            dialog.XamlRoot = XamlRoot;
            await dialog.ShowAsync();
        }

        public void UpdateIconColumnVisibility()
        {
            // Icon column not used in WinUI 3 DataGrid (handled via template columns if needed)
        }
    }
}
