using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MinimalFirewall.Dialogs;
using MinimalFirewall.TypedObjects;
using System.Collections.Generic;
using System.ComponentModel;

namespace MinimalFirewall.Pages
{
    public sealed partial class WildcardRulesPage : Page
    {
        private readonly WildcardRuleService _wildcardRuleService;
        private readonly BackgroundFirewallTaskService _backgroundTaskService;
        private readonly AppSettings _appSettings;

        private BindingList<WildcardRule> _rules = new();

        public WildcardRulesPage(
            WildcardRuleService wildcardRuleService,
            BackgroundFirewallTaskService backgroundTaskService,
            AppSettings appSettings)
        {
            _wildcardRuleService = wildcardRuleService;
            _backgroundTaskService = backgroundTaskService;
            _appSettings = appSettings;
            InitializeComponent();
        }

        public void LoadRules()
        {
            _rules = new BindingList<WildcardRule>(_wildcardRuleService.GetRules());
            WildcardDataGrid.ItemsSource = _rules;
            StatusText.Text = $"{_rules.Count} wildcard rule(s)";
        }

        public void ClearRules()
        {
            _rules.Clear();
            WildcardDataGrid.ItemsSource = null;
        }

        private async void AddRuleButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new WildcardCreatorDialog(_wildcardRuleService, _appSettings);
            dialog.XamlRoot = XamlRoot;
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && dialog.NewRule != null)
            {
                _backgroundTaskService.EnqueueTask(new FirewallTask(FirewallTaskType.AddWildcardRule, dialog.NewRule));
                _rules.Add(dialog.NewRule);
                StatusText.Text = $"{_rules.Count} wildcard rule(s)";
            }
        }

        private async void EditRuleButton_Click(object sender, RoutedEventArgs e)
        {
            if (WildcardDataGrid.SelectedItem is not WildcardRule selectedRule) return;

            var dialog = new WildcardCreatorDialog(_wildcardRuleService, _appSettings, selectedRule);
            dialog.XamlRoot = XamlRoot;
            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary && dialog.NewRule != null)
            {
                var payload = new UpdateWildcardRulePayload { OldRule = selectedRule, NewRule = dialog.NewRule };
                _backgroundTaskService.EnqueueTask(new FirewallTask(FirewallTaskType.UpdateWildcardRule, payload));

                int idx = _rules.IndexOf(selectedRule);
                if (idx >= 0) _rules[idx] = dialog.NewRule;
            }
        }

        private async void DeleteRuleButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = new List<WildcardRule>();
            if (WildcardDataGrid.SelectedItems != null)
                foreach (var item in WildcardDataGrid.SelectedItems)
                    if (item is WildcardRule r) selected.Add(r);

            if (selected.Count == 0) return;

            var confirm = new ContentDialog
            {
                Title = "Confirm Deletion",
                Content = $"Delete {selected.Count} wildcard rule(s)? Firewall rules they created will also be removed.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                XamlRoot = XamlRoot
            };
            if (await confirm.ShowAsync() != ContentDialogResult.Primary) return;

            foreach (var rule in selected)
            {
                _backgroundTaskService.EnqueueTask(new FirewallTask(FirewallTaskType.RemoveWildcardRule,
                    new DeleteWildcardRulePayload { Wildcard = rule }));
                _rules.Remove(rule);
            }
            StatusText.Text = $"{_rules.Count} wildcard rule(s)";
        }
    }
}
