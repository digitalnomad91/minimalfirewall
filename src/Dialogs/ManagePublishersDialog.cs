using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Collections.Generic;
using System.Linq;

namespace MinimalFirewall.Dialogs
{
    /// <summary>
    /// Manage trusted publishers whitelist dialog.
    /// </summary>
    public sealed class ManagePublishersDialog : ContentDialog
    {
        private readonly PublisherWhitelistService _whitelistService;
        private List<string> _publishers = new();
        private ListView _listView = null!;

        public ManagePublishersDialog(PublisherWhitelistService whitelistService)
        {
            _whitelistService = whitelistService;
            Title = "Manage Trusted Publishers";
            PrimaryButtonText = "Close";

            Content = BuildContent();
            LoadPublishers();
        }

        private StackPanel BuildContent()
        {
            var panel = new StackPanel { Spacing = 12, Width = 460 };

            panel.Children.Add(new TextBlock
            {
                Text = "Applications from these publishers are automatically allowed.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12
            });

            _listView = new ListView
            {
                Height = 260,
                SelectionMode = ListViewSelectionMode.Single
            };
            panel.Children.Add(_listView);

            var addRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var addBox = new TextBox { PlaceholderText = "Publisher name…", Width = 320 };
            var addBtn = new Button { Content = "Add" };
            addBtn.Click += (_, _) =>
            {
                string name = addBox.Text.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    _whitelistService.Add(name);
                    addBox.Text = "";
                    LoadPublishers();
                }
            };
            addRow.Children.Add(addBox);
            addRow.Children.Add(addBtn);
            panel.Children.Add(addRow);

            var deleteBtn = new Button { Content = "Remove Selected" };
            deleteBtn.Click += (_, _) =>
            {
                if (_listView.SelectedItem is string pub)
                {
                    _whitelistService.Remove(pub);
                    LoadPublishers();
                }
            };
            panel.Children.Add(deleteBtn);

            return panel;
        }

        private void LoadPublishers()
        {
            _publishers = _whitelistService.GetTrustedPublishers().ToList();
            _listView.ItemsSource = null;
            _listView.ItemsSource = _publishers;
        }
    }
}
