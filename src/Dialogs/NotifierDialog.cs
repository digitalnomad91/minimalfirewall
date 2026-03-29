using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using MinimalFirewall.TypedObjects;
using System;

namespace MinimalFirewall.Dialogs
{
    /// <summary>
    /// Connection blocked notification dialog (replaces NotifierForm).
    /// </summary>
    public sealed class NotifierDialog : ContentDialog
    {
        public string UserDecision { get; private set; } = "Ignore";
        public TimeSpan TemporaryDuration { get; private set; }
        public bool TrustPublisher { get; private set; }

        private readonly PendingConnectionViewModel _pending;

        public NotifierDialog(PendingConnectionViewModel pending)
        {
            _pending = pending;

            Title = "Connection Blocked";
            CloseButtonText = "Ignore";

            string appName = string.IsNullOrEmpty(pending.ServiceName)
                ? pending.FileName
                : $"{pending.FileName} ({pending.ServiceName})";

            var panel = new StackPanel { Spacing = 12 };

            panel.Children.Add(new TextBlock
            {
                Text = $"Blocked a {pending.Direction} connection for:",
                TextWrapping = TextWrapping.Wrap
            });
            panel.Children.Add(new TextBlock
            {
                Text = appName,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 15
            });
            panel.Children.Add(new TextBlock
            {
                Text = pending.AppPath,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
            });

            // Remote info
            if (!string.IsNullOrEmpty(pending.RemoteAddress))
            {
                panel.Children.Add(new TextBlock
                {
                    Text = $"Remote: {pending.RemoteAddress}:{pending.RemotePort}",
                    FontSize = 12
                });
            }

            // Trust publisher checkbox
            var trustCheck = new CheckBox { Content = "Trust publisher (auto-allow future connections)", IsChecked = false };
            panel.Children.Add(trustCheck);

            Content = panel;

            // Buttons
            PrimaryButtonText = "Allow";
            SecondaryButtonText = "Block";

            PrimaryButtonClick += (_, _) =>
            {
                UserDecision = "Allow";
                TrustPublisher = trustCheck.IsChecked == true;
            };
            SecondaryButtonClick += (_, _) =>
            {
                UserDecision = "Block";
                TrustPublisher = trustCheck.IsChecked == true;
            };
            CloseButtonClick += (_, _) => UserDecision = "Ignore";
        }
    }
}
