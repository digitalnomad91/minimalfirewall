using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MinimalFirewall.TypedObjects;
using System;
using System.Text.RegularExpressions;

namespace MinimalFirewall.Dialogs
{
    /// <summary>
    /// Advanced rule creation/editing dialog.
    /// </summary>
    public sealed class CreateAdvancedRuleDialog : ContentDialog
    {
        private readonly FirewallActionsService _actionsService;
        private readonly AdvancedRuleViewModel? _ruleToEdit;
        private readonly AppSettings _appSettings;

        // Form controls
        private TextBox _nameBox = null!;
        private TextBox _programBox = null!;
        private TextBox _serviceBox = null!;
        private ComboBox _actionCombo = null!;
        private ComboBox _directionCombo = null!;
        private ComboBox _protocolCombo = null!;
        private TextBox _localPortsBox = null!;
        private TextBox _remotePortsBox = null!;
        private TextBox _localAddressBox = null!;
        private TextBox _remoteAddressBox = null!;
        private ComboBox _profileCombo = null!;
        private TextBox _groupBox = null!;
        private TextBox _descBox = null!;

        public AdvancedRuleViewModel? RuleVm { get; private set; }

        public CreateAdvancedRuleDialog(FirewallActionsService actionsService, AdvancedRuleViewModel? ruleToEdit, AppSettings appSettings)
        {
            _actionsService = actionsService;
            _ruleToEdit = ruleToEdit;
            _appSettings = appSettings;

            Title = ruleToEdit != null ? "Edit Rule" : "Create Advanced Rule";
            PrimaryButtonText = ruleToEdit != null ? "Save" : "Create";
            CloseButtonText = "Cancel";

            Content = BuildContent();

            if (ruleToEdit != null)
            {
                _nameBox.Text = ruleToEdit.Name ?? "";
                _programBox.Text = ruleToEdit.ApplicationName ?? "";
                _serviceBox.Text = ruleToEdit.ServiceName ?? "";
                _actionCombo.SelectedItem = ruleToEdit.Status ?? "Allow";
                _localPortsBox.Text = ruleToEdit.LocalPorts ?? "";
                _remotePortsBox.Text = ruleToEdit.RemotePorts ?? "";
                _localAddressBox.Text = ruleToEdit.LocalAddresses ?? "";
                _remoteAddressBox.Text = ruleToEdit.RemoteAddresses ?? "";
                _groupBox.Text = ruleToEdit.Grouping ?? "";
                _descBox.Text = ruleToEdit.Description ?? "";
            }

            PrimaryButtonClick += OnPrimaryClick;
        }

        private StackPanel BuildContent()
        {
            var panel = new StackPanel { Spacing = 10, Width = 480 };

            _nameBox = AddRow(panel, "Rule Name:", new TextBox { PlaceholderText = "Enter rule name…" });
            _programBox = AddRow(panel, "Program Path:", new TextBox { PlaceholderText = "e.g. C:\\…\\app.exe or leave blank" });
            _serviceBox = AddRow(panel, "Service Name:", new TextBox { PlaceholderText = "Exact service name (optional)" });

            _actionCombo = AddRow(panel, "Action:", new ComboBox
            {
                ItemsSource = new[] { "Allow", "Block" },
                SelectedIndex = 0, Width = 200
            });
            _directionCombo = AddRow(panel, "Direction:", new ComboBox
            {
                ItemsSource = new[] { "Outbound", "Inbound", "Both" },
                SelectedIndex = 0, Width = 200
            });
            _protocolCombo = AddRow(panel, "Protocol:", new ComboBox
            {
                ItemsSource = new[] { "Any", "TCP", "UDP", "ICMPv4", "ICMPv6" },
                SelectedIndex = 0, Width = 200
            });
            _localPortsBox = AddRow(panel, "Local Ports:", new TextBox { PlaceholderText = "e.g. 80,443,8000-8080" });
            _remotePortsBox = AddRow(panel, "Remote Ports:", new TextBox { PlaceholderText = "e.g. 80,443" });
            _localAddressBox = AddRow(panel, "Local Addresses:", new TextBox { PlaceholderText = "IP, subnet, range, or Any" });
            _remoteAddressBox = AddRow(panel, "Remote Addresses:", new TextBox { PlaceholderText = "IP, subnet, range, or Any" });
            _profileCombo = AddRow(panel, "Profiles:", new ComboBox
            {
                ItemsSource = new[] { "All", "Domain", "Private", "Public" },
                SelectedIndex = 0, Width = 200
            });
            _groupBox = AddRow(panel, "Group:", new TextBox { PlaceholderText = "Optional group name" });
            _descBox = AddRow(panel, "Description:", new TextBox { PlaceholderText = "Optional description" });

            return panel;
        }

        private static T AddRow<T>(StackPanel panel, string label, T control) where T : FrameworkElement
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lbl = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lbl, 0);
            Grid.SetColumn(control, 1);
            row.Children.Add(lbl);
            row.Children.Add(control);
            panel.Children.Add(row);
            return control;
        }

        private void PopulateFromRule(AdvancedRuleViewModel rule)
        {
            _nameBox.Text = rule.Name ?? "";
            _programBox.Text = rule.ApplicationName ?? "";
            _serviceBox.Text = rule.ServiceName ?? "";
            _actionCombo.SelectedItem = rule.Status ?? "Allow";
            _directionCombo.SelectedItem = rule.Direction.ToString() ?? "Outbound";
            _localPortsBox.Text = rule.LocalPorts ?? "";
            _remotePortsBox.Text = rule.RemotePorts ?? "";
            _localAddressBox.Text = rule.LocalAddresses ?? "";
            _remoteAddressBox.Text = rule.RemoteAddresses ?? "";
            _groupBox.Text = rule.Grouping ?? "";
            _descBox.Text = rule.Description ?? "";
        }

        private void OnPrimaryClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(_nameBox.Text))
            {
                args.Cancel = true;
                return;
            }

            string dir = _directionCombo.SelectedItem?.ToString() ?? "Outbound";
            var direction = dir == "Both" ? (Directions.Incoming | Directions.Outgoing) :
                            dir == "Inbound" ? Directions.Incoming : Directions.Outgoing;

            var vm = new AdvancedRuleViewModel
            {
                Name = _nameBox.Text.Trim(),
                ApplicationName = _programBox.Text.Trim(),
                ServiceName = _serviceBox.Text.Trim(),
                Status = _actionCombo.SelectedItem?.ToString() ?? "Allow",
                Direction = direction,
                ProtocolName = _protocolCombo.SelectedItem?.ToString() ?? "Any",
                LocalPorts = _localPortsBox.Text.Trim(),
                RemotePorts = _remotePortsBox.Text.Trim(),
                LocalAddresses = _localAddressBox.Text.Trim(),
                RemoteAddresses = _remoteAddressBox.Text.Trim(),
                Profiles = _profileCombo.SelectedItem?.ToString() ?? "All",
                Grouping = _groupBox.Text.Trim(),
                Description = _descBox.Text.Trim(),
                InterfaceTypes = "All",
                IcmpTypesAndCodes = "*"
            };

            RuleVm = vm;

            if (_ruleToEdit != null)
            {
                // Delete old rule and create new one
                _actionsService.DeleteAdvancedRules(new System.Collections.Generic.List<string> { _ruleToEdit.Name ?? "" });
                _actionsService.CreateAdvancedRule(vm, "All", "*");
            }
            else
            {
                _actionsService.CreateAdvancedRule(vm, "All", "*");
            }
        }
    }
}
