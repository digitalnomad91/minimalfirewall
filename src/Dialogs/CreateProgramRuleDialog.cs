using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MinimalFirewall.TypedObjects;
using System;
using System.Collections.Generic;

namespace MinimalFirewall.Dialogs
{
    /// <summary>
    /// Dialog for creating a simple program-based firewall rule.
    /// </summary>
    public sealed class CreateProgramRuleDialog : ContentDialog
    {
        private readonly string[] _filePaths;
        private readonly FirewallActionsService _actionsService;

        private RadioButton _allowRadio = null!;
        private RadioButton _blockRadio = null!;
        private ComboBox _allowDirectionCombo = null!;
        private ComboBox _blockDirectionCombo = null!;

        public CreateProgramRuleDialog(string[] filePaths, FirewallActionsService actionsService)
        {
            _filePaths = filePaths;
            _actionsService = actionsService;

            Title = "Create Program Rule";
            PrimaryButtonText = "OK";
            CloseButtonText = "Cancel";

            string programDisplay = filePaths.Length == 1
                ? $"Program: {System.IO.Path.GetFileName(filePaths[0])}"
                : $"{filePaths.Length} programs selected.";

            var directions = new[] { "Outbound", "Inbound", "Both" };

            var panel = new StackPanel { Spacing = 12, Width = 360 };
            panel.Children.Add(new TextBlock { Text = programDisplay, TextWrapping = TextWrapping.Wrap });

            _allowRadio = new RadioButton { Content = "Allow", GroupName = "Action", IsChecked = true };
            _blockRadio = new RadioButton { Content = "Block", GroupName = "Action" };
            _allowDirectionCombo = new ComboBox { ItemsSource = directions, SelectedIndex = 0, Width = 180 };
            _blockDirectionCombo = new ComboBox { ItemsSource = directions, SelectedIndex = 0, Width = 180, IsEnabled = false };

            _allowRadio.Checked += (_, _) =>
            {
                _allowDirectionCombo.IsEnabled = true;
                _blockDirectionCombo.IsEnabled = false;
            };
            _blockRadio.Checked += (_, _) =>
            {
                _allowDirectionCombo.IsEnabled = false;
                _blockDirectionCombo.IsEnabled = true;
            };

            var allowRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            allowRow.Children.Add(_allowRadio);
            allowRow.Children.Add(_allowDirectionCombo);

            var blockRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            blockRow.Children.Add(_blockRadio);
            blockRow.Children.Add(_blockDirectionCombo);

            panel.Children.Add(allowRow);
            panel.Children.Add(blockRow);
            Content = panel;

            PrimaryButtonClick += (_, _) =>
            {
                string action = _allowRadio.IsChecked == true ? "Allow" : "Block";
                string direction = _allowRadio.IsChecked == true
                    ? (_allowDirectionCombo.SelectedItem?.ToString() ?? "Outbound")
                    : (_blockDirectionCombo.SelectedItem?.ToString() ?? "Outbound");
                string finalAction = $"{action} ({direction})";
                _actionsService.ApplyApplicationRuleChange(new List<string>(_filePaths), finalAction);
            };
        }
    }
}
