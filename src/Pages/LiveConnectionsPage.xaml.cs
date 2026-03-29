using CommunityToolkit.WinUI.UI.Controls;
using Firewall.Traffic.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;
using System.Linq;

namespace MinimalFirewall.Pages
{
    public sealed partial class LiveConnectionsPage : Page
    {
        private readonly TrafficMonitorViewModel _viewModel;
        private readonly AppSettings _appSettings;
        private readonly IconService _iconService;
        private readonly BackgroundFirewallTaskService _backgroundTaskService;
        private readonly FirewallActionsService _actionsService;

        private SortableBindingList<TcpConnectionViewModel> _sortableList = new();

        public LiveConnectionsPage(
            TrafficMonitorViewModel viewModel,
            AppSettings appSettings,
            IconService iconService,
            BackgroundFirewallTaskService backgroundTaskService,
            FirewallActionsService actionsService)
        {
            _viewModel = viewModel;
            _appSettings = appSettings;
            _iconService = iconService;
            _backgroundTaskService = backgroundTaskService;
            _actionsService = actionsService;
            InitializeComponent();
        }

        public void OnTabSelected(AppSettings settings)
        {
            UpdateEnabledState();
            if (settings.IsTrafficMonitorEnabled)
                RefreshConnections();
        }

        public void OnTabDeselected()
        {
            _viewModel.StopMonitoring();
        }

        public void UpdateEnabledState()
        {
            bool enabled = _appSettings.IsTrafficMonitorEnabled;
            DisabledInfoBar.IsOpen = !enabled;
            LiveDataGrid.IsEnabled = enabled;
            RefreshButton.IsEnabled = enabled;
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshSpinner.IsActive = true;
            RefreshButton.IsEnabled = false;
            try
            {
                await Task.Run(RefreshConnections);
                DispatcherQueue.TryEnqueue(RefreshConnections);
            }
            finally
            {
                RefreshSpinner.IsActive = false;
                RefreshButton.IsEnabled = true;
            }
        }

        private void RefreshConnections()
        {
            _viewModel.RefreshConnections();
            _sortableList = new SortableBindingList<TcpConnectionViewModel>(_viewModel.ActiveConnections.ToList());
            LiveDataGrid.ItemsSource = _sortableList;
            LiveStatusText.Text = $"{_sortableList.Count} connection(s)";
        }

        private void LiveDataGrid_Sorting(object? sender, DataGridColumnEventArgs e)
        {
            var propName = (e.Column.Binding as Microsoft.UI.Xaml.Data.Binding)?.Path.Path;
            if (propName == null) return;

            bool asc = e.Column.SortDirection != DataGridSortDirection.Ascending;
            _sortableList.Sort(propName, asc ? ListSortDirection.Ascending : ListSortDirection.Descending);
            e.Column.SortDirection = asc ? DataGridSortDirection.Ascending : DataGridSortDirection.Descending;

            foreach (var col in LiveDataGrid.Columns)
                if (col != e.Column) col.SortDirection = null;

            LiveDataGrid.ItemsSource = null;
            LiveDataGrid.ItemsSource = _sortableList;
        }
    }
}
