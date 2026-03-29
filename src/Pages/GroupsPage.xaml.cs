using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MinimalFirewall.Groups;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MinimalFirewall.Pages
{
    public sealed partial class GroupsPage : Page
    {
        private readonly FirewallGroupManager _groupManager;
        private readonly BackgroundFirewallTaskService _backgroundTaskService;

        public GroupsPage(
            FirewallGroupManager groupManager,
            BackgroundFirewallTaskService backgroundTaskService)
        {
            _groupManager = groupManager;
            _backgroundTaskService = backgroundTaskService;
            InitializeComponent();
        }

        public async Task OnTabSelectedAsync()
        {
            GroupsStatusText.Text = "Loading…";
            try
            {
                var groups = await Task.Run(() => _groupManager.GetAllGroups());
                GroupsDataGrid.ItemsSource = new SortableBindingList<FirewallGroup>(groups);
                GroupsStatusText.Text = $"{groups.Count} group(s)";
            }
            catch (System.Exception ex)
            {
                GroupsStatusText.Text = $"Error: {ex.Message}";
            }
        }

        public void ClearGroups()
        {
            GroupsDataGrid.ItemsSource = null;
        }

        private void GroupToggle_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleSwitch toggle && toggle.DataContext is FirewallGroup group)
            {
                var taskType = toggle.IsOn
                    ? TypedObjects.FirewallTaskType.EnableGroup
                    : TypedObjects.FirewallTaskType.DisableGroup;
                _backgroundTaskService.EnqueueTask(new TypedObjects.FirewallTask(taskType, group.Name));
            }
        }
    }
}
