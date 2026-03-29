using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MinimalFirewall.TypedObjects;
using System;

namespace MinimalFirewall.Dialogs
{
    /// <summary>
    /// Wildcard rule creation/editing dialog.
    /// </summary>
    public sealed class WildcardCreatorDialog : ContentDialog
    {
        private readonly WildcardRuleService _wildcardRuleService;
        private readonly AppSettings _appSettings;
        private readonly WildcardRule? _existingRule;

        private TextBox _folderBox = null!;
        private TextBox _exeBox = null!;
        private ComboBox _actionCombo = null!;
        private ComboBox _protocolCombo = null!;
        private TextBox _remotePortsBox = null!;

        public WildcardRule? NewRule { get; private set; }

        public WildcardCreatorDialog(
            WildcardRuleService wildcardRuleService,
            AppSettings appSettings,
            WildcardRule? existingRule = null)
        {
            _wildcardRuleService = wildcardRuleService;
            _appSettings = appSettings;
            _existingRule = existingRule;

            Title = existingRule != null ? "Edit Wildcard Rule" : "Create Wildcard Rule";
            PrimaryButtonText = existingRule != null ? "Save" : "Create";
            CloseButtonText = "Cancel";

            Content = BuildContent();
            if (existingRule != null)
                PopulateFromRule(existingRule);

            PrimaryButtonClick += OnPrimaryClick;
        }

        // Overload accepting just a path string (from NotifierDialog)
        public WildcardCreatorDialog(
            WildcardRuleService wildcardRuleService,
            string initialPath,
            AppSettings appSettings)
            : this(wildcardRuleService, appSettings)
        {
            _folderBox.Text = System.IO.Path.GetDirectoryName(initialPath) ?? "";
            _exeBox.Text = System.IO.Path.GetFileName(initialPath);
        }

        private StackPanel BuildContent()
        {
            var panel = new StackPanel { Spacing = 12, Width = 420 };

            panel.Children.Add(new TextBlock
            {
                Text = "Create a wildcard rule that matches multiple executables in a folder.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });

            _folderBox = new TextBox { PlaceholderText = "e.g. C:\\Program Files\\MyApp" };
            AddRow(panel, "Folder:", _folderBox);

            _exeBox = new TextBox { PlaceholderText = "e.g. *.exe (wildcard supported)" };
            AddRow(panel, "Exe Name:", _exeBox);

            _actionCombo = new ComboBox { ItemsSource = new[] { "Allow", "Block" }, SelectedIndex = 0, Width = 180 };
            AddRow(panel, "Action:", _actionCombo);

            _protocolCombo = new ComboBox { ItemsSource = new[] { "Any", "TCP", "UDP" }, SelectedIndex = 0, Width = 180 };
            AddRow(panel, "Protocol:", _protocolCombo);

            _remotePortsBox = new TextBox { PlaceholderText = "e.g. 80,443 (leave blank for Any)" };
            AddRow(panel, "Remote Ports:", _remotePortsBox);

            return panel;
        }

        private static void AddRow(StackPanel panel, string label, FrameworkElement control)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            row.Children.Add(new TextBlock { Text = label, Width = 100, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(control);
            panel.Children.Add(row);
        }

        private void PopulateFromRule(WildcardRule rule)
        {
            _folderBox.Text = rule.FolderPath ?? "";
            _exeBox.Text = rule.ExeName ?? "";
            _actionCombo.SelectedItem = rule.Action ?? "Allow";
            int proto = rule.Protocol;
            _protocolCombo.SelectedItem = proto == 6 ? "TCP" : proto == 17 ? "UDP" : "Any";
            _remotePortsBox.Text = rule.RemotePorts == "*" ? "" : rule.RemotePorts;
        }

        private void OnPrimaryClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if (string.IsNullOrWhiteSpace(_folderBox.Text))
            {
                args.Cancel = true;
                return;
            }

            int protocol = _protocolCombo.SelectedItem?.ToString() switch
            {
                "TCP" => 6,
                "UDP" => 17,
                _ => 256
            };

            string remotePorts = string.IsNullOrWhiteSpace(_remotePortsBox.Text) ? "*" : _remotePortsBox.Text.Trim();

            NewRule = new WildcardRule
            {
                FolderPath = _folderBox.Text.Trim(),
                ExeName = _exeBox.Text.Trim(),
                Action = _actionCombo.SelectedItem?.ToString() ?? "Allow",
                Protocol = protocol,
                RemotePorts = remotePorts
            };
        }
    }
}
