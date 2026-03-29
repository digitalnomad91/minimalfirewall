using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MinimalFirewall.TypedObjects;
using System;

namespace MinimalFirewall.Dialogs
{
    /// <summary>
    /// Rule creation wizard dialog that guides the user through creating common firewall rules.
    /// </summary>
    public sealed class RuleWizardDialog : ContentDialog
    {
        private readonly FirewallActionsService _actionsService;
        private readonly WildcardRuleService _wildcardRuleService;
        private readonly BackgroundFirewallTaskService _backgroundTaskService;
        private readonly AppSettings _appSettings;

        // Wizard state
        private StackPanel _currentPanel = null!;
        private TextBlock _headerText = null!;
        private Grid _contentArea = null!;

        private string _selectedTemplate = "";
        private string _wizardAppPath = "";
        private string _wizardPorts = "";
        private string _wizardAction = "Allow";
        private string _wizardDirection = "Outbound";
        private string _wizardRuleName = "";
        private int _wizardProtocol = 256; // Any

        public RuleWizardDialog(
            FirewallActionsService actionsService,
            WildcardRuleService wildcardRuleService,
            BackgroundFirewallTaskService backgroundTaskService,
            AppSettings appSettings)
        {
            _actionsService = actionsService;
            _wildcardRuleService = wildcardRuleService;
            _backgroundTaskService = backgroundTaskService;
            _appSettings = appSettings;

            Title = "New Firewall Rule";
            PrimaryButtonText = "Close";
            CloseButtonText = "";

            Content = BuildWizard();
            ShowTemplateSelection();
        }

        private StackPanel BuildWizard()
        {
            var panel = new StackPanel { Spacing = 16, Width = 480 };

            _headerText = new TextBlock
            {
                Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"],
                TextWrapping = TextWrapping.Wrap
            };
            panel.Children.Add(_headerText);

            _contentArea = new Grid();
            panel.Children.Add(_contentArea);

            return panel;
        }

        private void ShowTemplateSelection()
        {
            _headerText.Text = "Select a rule type to create:";
            _contentArea.Children.Clear();

            var list = new StackPanel { Spacing = 8 };

            AddTemplateButton(list, "Program Rule", "Control access for a specific application.", () => ShowProgramRule());
            AddTemplateButton(list, "Port Rule", "Allow or block a specific TCP/UDP port.", () => ShowPortRule());
            AddTemplateButton(list, "Block Service", "Block a Windows service from networking.", () => ShowBlockService());
            AddTemplateButton(list, "Advanced Rule", "Create a fully customized rule.", () => ShowAdvancedRule());

            _contentArea.Children.Add(list);
        }

        private void AddTemplateButton(StackPanel parent, string title, string description, Action onClick)
        {
            var btn = new Button { HorizontalAlignment = HorizontalAlignment.Stretch };
            var inner = new StackPanel { Spacing = 2 };
            inner.Children.Add(new TextBlock { Text = title, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
            inner.Children.Add(new TextBlock
            {
                Text = description,
                FontSize = 12,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });
            btn.Content = inner;
            btn.Click += (_, _) => onClick();
            parent.Children.Add(btn);
        }

        private void ShowProgramRule()
        {
            _headerText.Text = "Program Rule — Select application";
            _contentArea.Children.Clear();

            var panel = new StackPanel { Spacing = 12 };
            var pathBox = new TextBox { PlaceholderText = "Path to .exe (e.g. C:\\…\\app.exe)" };
            var browseBtn = new Button { Content = "Browse…" };
            browseBtn.Click += async (_, _) =>
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                WinRT.Interop.InitializeWithWindow.Initialize(picker,
                    WinRT.Interop.WindowNative.GetWindowHandle(App.Instance.MainWindow!));
                picker.FileTypeFilter.Add(".exe");
                var file = await picker.PickSingleFileAsync();
                if (file != null) pathBox.Text = file.Path;
            };

            var actionCombo = new ComboBox { ItemsSource = new[] { "Allow", "Block" }, SelectedIndex = 0 };
            var dirCombo = new ComboBox { ItemsSource = new[] { "Outbound", "Inbound", "Both" }, SelectedIndex = 0 };
            var createBtn = new Button { Content = "Create Rule", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };

            createBtn.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(pathBox.Text)) return;
                string action = $"{actionCombo.SelectedItem} ({dirCombo.SelectedItem})";
                _actionsService.ApplyApplicationRuleChange(new System.Collections.Generic.List<string> { pathBox.Text }, action);
                ShowCompleted("Program rule created.");
            };

            panel.Children.Add(new TextBlock { Text = "Application path:" });
            var pathRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            pathRow.Children.Add(pathBox);
            pathRow.Children.Add(browseBtn);
            panel.Children.Add(pathRow);
            panel.Children.Add(new TextBlock { Text = "Action:" });
            panel.Children.Add(actionCombo);
            panel.Children.Add(new TextBlock { Text = "Direction:" });
            panel.Children.Add(dirCombo);
            panel.Children.Add(createBtn);

            _contentArea.Children.Add(panel);
        }

        private void ShowPortRule()
        {
            _headerText.Text = "Port Rule — Configure port/protocol";
            _contentArea.Children.Clear();

            var panel = new StackPanel { Spacing = 12 };
            var portBox = new TextBox { PlaceholderText = "Port(s), e.g. 80,443,8000-8080" };
            var protocolCombo = new ComboBox { ItemsSource = new[] { "TCP", "UDP" }, SelectedIndex = 0 };
            var actionCombo = new ComboBox { ItemsSource = new[] { "Allow", "Block" }, SelectedIndex = 0 };
            var dirCombo = new ComboBox { ItemsSource = new[] { "Outbound", "Inbound", "Both" }, SelectedIndex = 0 };
            var nameBox = new TextBox { PlaceholderText = "Rule name (optional)" };
            var createBtn = new Button { Content = "Create Rule", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };

            createBtn.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(portBox.Text)) return;
                string name = string.IsNullOrWhiteSpace(nameBox.Text)
                    ? $"MFW {actionCombo.SelectedItem} {protocolCombo.SelectedItem} {portBox.Text}"
                    : nameBox.Text;

                int protocol = protocolCombo.SelectedItem?.ToString() == "TCP" ? 6 : 17;
                string direction = dirCombo.SelectedItem?.ToString() ?? "Outbound";
                var vm = new AdvancedRuleViewModel
                {
                    Name = name,
                    Grouping = MFWConstants.MainRuleGroup,
                    IsEnabled = true,
                    Status = actionCombo.SelectedItem?.ToString() ?? "Allow",
                    Direction = direction.Contains("Both") ? (Directions.Incoming | Directions.Outgoing) :
                                direction.Contains("Inbound") ? Directions.Incoming : Directions.Outgoing,
                    ProtocolName = protocolCombo.SelectedItem?.ToString() ?? "TCP",
                    RemotePorts = portBox.Text,
                    LocalAddresses = "*",
                    RemoteAddresses = "*",
                    Profiles = "All",
                    Type = TypedObjects.RuleType.Advanced,
                    InterfaceTypes = "All"
                };
                _actionsService.CreateAdvancedRule(vm, "All", "*");
                ShowCompleted("Port rule created.");
            };

            panel.Children.Add(new TextBlock { Text = "Port(s):" });
            panel.Children.Add(portBox);
            panel.Children.Add(new TextBlock { Text = "Protocol:" });
            panel.Children.Add(protocolCombo);
            panel.Children.Add(new TextBlock { Text = "Action:" });
            panel.Children.Add(actionCombo);
            panel.Children.Add(new TextBlock { Text = "Direction:" });
            panel.Children.Add(dirCombo);
            panel.Children.Add(new TextBlock { Text = "Rule name:" });
            panel.Children.Add(nameBox);
            panel.Children.Add(createBtn);

            _contentArea.Children.Add(panel);
        }

        private void ShowBlockService()
        {
            _headerText.Text = "Block Service — Enter service name";
            _contentArea.Children.Clear();

            var panel = new StackPanel { Spacing = 12 };
            var serviceBox = new TextBox { PlaceholderText = "Exact service name (e.g. wuauserv)" };
            var dirCombo = new ComboBox { ItemsSource = new[] { "Outbound", "Inbound", "Both" }, SelectedIndex = 0 };
            var createBtn = new Button { Content = "Create Block Rule", Style = (Style)Application.Current.Resources["AccentButtonStyle"] };

            createBtn.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(serviceBox.Text)) return;
                _actionsService.ApplyServiceRuleChange(serviceBox.Text, 
                    $"Block ({dirCombo.SelectedItem ?? "Outbound"})");
                ShowCompleted("Service block rule created.");
            };

            panel.Children.Add(new TextBlock { Text = "Service name:" });
            panel.Children.Add(serviceBox);
            panel.Children.Add(new TextBlock { Text = "Direction:" });
            panel.Children.Add(dirCombo);
            panel.Children.Add(createBtn);
            _contentArea.Children.Add(panel);
        }

        private async void ShowAdvancedRule()
        {
            Hide();
            var dialog = new CreateAdvancedRuleDialog(_actionsService, null, _appSettings);
            dialog.XamlRoot = App.Instance.MainWindow?.Content?.XamlRoot;
            await dialog.ShowAsync();
        }

        private void ShowCompleted(string message)
        {
            _headerText.Text = "Done!";
            _contentArea.Children.Clear();
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(new TextBlock { Text = message });
            var backBtn = new Button { Content = "Create Another Rule" };
            backBtn.Click += (_, _) => ShowTemplateSelection();
            panel.Children.Add(backBtn);
            _contentArea.Children.Add(panel);
        }
    }
}
