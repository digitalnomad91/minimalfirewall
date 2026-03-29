using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.ServiceProcess;

namespace MinimalFirewall.Dialogs
{
    /// <summary>
    /// Browse Windows services dialog.
    /// </summary>
    public sealed class BrowseServicesDialog : ContentDialog
    {
        public string? SelectedServiceName { get; private set; }

        public BrowseServicesDialog()
        {
            Title = "Browse Services";
            PrimaryButtonText = "Select";
            CloseButtonText = "Cancel";

            var panel = new StackPanel { Spacing = 12, Width = 500 };

            var searchBox = new AutoSuggestBox
            {
                PlaceholderText = "Search services…",
                QueryIcon = new SymbolIcon(Symbol.Find)
            };

            var listView = new ListView
            {
                Height = 350,
                SelectionMode = ListViewSelectionMode.Single
            };

            // Load services
            var services = new List<ServiceItem>();
            try
            {
                foreach (var svc in System.ServiceProcess.ServiceController.GetServices())
                {
                    services.Add(new ServiceItem
                    {
                        DisplayName = svc.DisplayName,
                        ServiceName = svc.ServiceName
                    });
                }
            }
            catch { }

            services.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
            listView.ItemsSource = services;

            // Use a simple data template via ItemTemplateSelector workaround  
            listView.ItemTemplate = BuildItemTemplate();

            searchBox.TextChanged += (s, _) =>
            {
                string q = s.Text.Trim();
                listView.ItemsSource = string.IsNullOrEmpty(q)
                    ? services
                    : services.FindAll(svc =>
                        svc.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                        svc.ServiceName.Contains(q, StringComparison.OrdinalIgnoreCase));
            };

            panel.Children.Add(searchBox);
            panel.Children.Add(listView);
            Content = panel;

            PrimaryButtonClick += (_, args) =>
            {
                if (listView.SelectedItem is ServiceItem item)
                    SelectedServiceName = item.ServiceName;
                else
                    args.Cancel = true;
            };
        }

        private static DataTemplate BuildItemTemplate()
        {
            // Create template programmatically since XamlReader is not available in unpackaged WinUI 3
            return (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(
                @"<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>
                    <StackPanel Margin='0,4'>
                        <TextBlock Text='{Binding DisplayName}' FontWeight='SemiBold' />
                        <TextBlock Text='{Binding ServiceName}' FontSize='11' />
                    </StackPanel>
                  </DataTemplate>");
        }

        private sealed class ServiceItem
        {
            public string DisplayName { get; set; } = "";
            public string ServiceName { get; set; } = "";
        }
    }
}
