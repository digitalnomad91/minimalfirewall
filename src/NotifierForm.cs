using DarkModeForms;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace MinimalFirewall
{
    public partial class NotifierForm : Form
    {
        // Enums and Properties
        public enum NotifierResult { Ignore, Allow, Block, TemporaryAllow, CreateWildcard }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public NotifierResult Result { get; set; } = NotifierResult.Ignore;
        public PendingConnectionViewModel PendingConnection { get; private set; }
        public TimeSpan TemporaryDuration { get; private set; }
        public bool TrustPublisher { get; private set; } = false;
        private readonly DarkModeCS dm;

        // Slide-in animation
        private int _targetY;
        private System.Windows.Forms.Timer? _slideTimer;

        public NotifierForm(PendingConnectionViewModel pending, bool isDarkMode)
        {
            InitializeComponent();

            PendingConnection = pending;

            // Initialize Dark Mode
            dm = new DarkModeCS(this)
            {
                ColorMode = isDarkMode ? DarkModeCS.DisplayMode.DarkMode : DarkModeCS.DisplayMode.ClearMode
            };
            dm.ApplyTheme(isDarkMode);

            Color bgColor = isDarkMode ? Color.FromArgb(32, 32, 36) : Color.FromArgb(250, 250, 252);
            Color fgColor = isDarkMode ? Color.White : Color.FromArgb(30, 30, 30);
            Color panelBg = isDarkMode ? Color.FromArgb(40, 40, 45) : Color.FromArgb(243, 243, 246);

            this.BackColor = bgColor;
            this.ForeColor = fgColor;

            pathLabel.BackColor = panelBg;
            pathLabel.ForeColor = isDarkMode ? Color.FromArgb(180, 180, 180) : Color.FromArgb(80, 80, 80);
            detailsRichTextBox.BackColor = panelBg;
            detailsRichTextBox.ForeColor = fgColor;

            // Set UI Text
            string appName = string.IsNullOrEmpty(pending.ServiceName) ? pending.FileName : $"{pending.FileName} ({pending.ServiceName})";
            this.Text = "Connection Blocked";
            infoLabel.Text = $"Blocked a {pending.Direction} connection for:";
            appNameLabel.Text = appName;

            pathLabel.Text = pending.AppPath;

            PopulateDetails(pending, isDarkMode);

            this.AcceptButton = this.ignoreButton;

            // Modern button styling
            StyleActionButton(allowButton, Color.FromArgb(46, 160, 67), Color.White);
            StyleActionButton(blockButton, Color.FromArgb(218, 54, 51), Color.White);
            StyleActionButton(ignoreButton, isDarkMode ? Color.FromArgb(55, 55, 60) : Color.FromArgb(220, 220, 225), fgColor);
            StyleActionButton(tempAllowButton, isDarkMode ? Color.FromArgb(55, 55, 60) : Color.FromArgb(220, 220, 225), fgColor);
            StyleActionButton(createWildcardButton, isDarkMode ? Color.FromArgb(55, 55, 60) : Color.FromArgb(220, 220, 225), fgColor);

            SetupTempAllowMenu();
        }

        private static void StyleActionButton(Button btn, Color bgColor, Color fgColor)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = bgColor;
            btn.ForeColor = fgColor;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(bgColor, 0.15f);
            btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(bgColor, 0.1f);
            btn.Cursor = Cursors.Hand;
            btn.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
        }

        private void PopulateDetails(PendingConnectionViewModel pending, bool isDarkMode)
        {
            detailsRichTextBox.Clear();

            Color labelColor = isDarkMode ? Color.FromArgb(100, 180, 255) : Color.FromArgb(0, 100, 200);
            Color valColor = isDarkMode ? Color.White : Color.FromArgb(30, 30, 30);
            using Font boldFont = new Font(detailsRichTextBox.Font, FontStyle.Bold);

            void AppendField(string label, string value)
            {
                detailsRichTextBox.SelectionStart = detailsRichTextBox.TextLength;
                detailsRichTextBox.SelectionLength = 0;
                detailsRichTextBox.SelectionColor = labelColor;
                detailsRichTextBox.SelectionFont = boldFont;
                detailsRichTextBox.AppendText(label.PadRight(12));
                detailsRichTextBox.SelectionColor = valColor;
                detailsRichTextBox.SelectionFont = detailsRichTextBox.Font;
                detailsRichTextBox.AppendText(value + Environment.NewLine);
            }

            // Direction with icon
            string dirIcon = pending.Direction.Equals("Incoming", StringComparison.OrdinalIgnoreCase) ? "↓" : "↑";
            AppendField("Direction", $"{dirIcon} {pending.Direction}");

            // Remote address and port
            string remote = "N/A";
            if (!string.IsNullOrEmpty(pending.RemoteAddress))
            {
                remote = string.IsNullOrEmpty(pending.RemotePort)
                    ? pending.RemoteAddress
                    : $"{pending.RemoteAddress}:{pending.RemotePort}";
            }
            AppendField("Remote", remote);

            // Protocol
            string protoDisplay = pending.Protocol switch
            {
                "6" => "TCP",
                "17" => "UDP",
                "1" => "ICMPv4",
                "58" => "ICMPv6",
                _ => string.IsNullOrEmpty(pending.Protocol) ? "N/A" : $"Protocol {pending.Protocol}"
            };
            AppendField("Protocol", protoDisplay);
        }

        // Position in bottom-right above taskbar with slide-up animation
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Don't be permanently topmost — just bring to front initially
            this.TopMost = true;
            this.Activate();

            // Position in bottom-right corner above taskbar
            var workingArea = Screen.PrimaryScreen?.WorkingArea ?? Screen.AllScreens[0].WorkingArea;
            int margin = 12;
            int startX = workingArea.Right - this.Width - margin;
            int startY = workingArea.Bottom; // start below the screen
            _targetY = workingArea.Bottom - this.Height - margin;

            this.StartPosition = FormStartPosition.Manual;
            this.Location = new Point(startX, startY);
            this.Opacity = 0;

            // Slide-up animation
            _slideTimer = new System.Windows.Forms.Timer { Interval = 12 };
            int step = 0;
            _slideTimer.Tick += (s, ev) =>
            {
                step++;
                // Ease-out cubic over ~20 steps (~240ms)
                double t = Math.Min(step / 20.0, 1.0);
                double ease = 1.0 - Math.Pow(1.0 - t, 3);

                this.Top = startY - (int)((startY - _targetY) * ease);
                this.Opacity = ease;

                if (t >= 1.0)
                {
                    _slideTimer.Stop();
                    _slideTimer.Dispose();
                    _slideTimer = null;
                    this.Top = _targetY;
                    this.Opacity = 1.0;
                }
            };
            _slideTimer.Start();

            // Drop TopMost after a brief delay so it doesn't stay pinned forever
            var topMostTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            topMostTimer.Tick += (s, ev) =>
            {
                this.TopMost = false;
                topMostTimer.Stop();
                topMostTimer.Dispose();
            };
            topMostTimer.Start();
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            LoadPublisherInfoAsync();
        }

        private async void LoadPublisherInfoAsync()
        {
            try
            {
                string publisherName = null;

                bool hasInfo = await Task.Run(() => SignatureValidationService.GetPublisherInfo(PendingConnection.AppPath, out publisherName));

                if (hasInfo && !string.IsNullOrEmpty(publisherName))
                {
                    if (publisherName.Length > 30)
                    {
                        publisherName = publisherName.Substring(0, 30) + "...";
                    }
                    trustPublisherCheckBox.Text = $"Trust: {publisherName}";
                    trustPublisherCheckBox.Visible = true;
                }
                else
                {
                    trustPublisherCheckBox.Visible = false;
                }
            }
            catch
            {
                trustPublisherCheckBox.Visible = false;
            }
        }

        // Window Closing: no longer save position (toast always appears bottom-right)
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _slideTimer?.Stop();
            _slideTimer?.Dispose();
            base.OnFormClosing(e);
        }

        private void SetupTempAllowMenu()
        {
            tempAllowContextMenu.Items.Add("For 2 minutes").Click += (s, e) => SetTemporaryAllow(TimeSpan.FromMinutes(2));
            tempAllowContextMenu.Items.Add("For 5 minutes").Click += (s, e) => SetTemporaryAllow(TimeSpan.FromMinutes(5));
            tempAllowContextMenu.Items.Add("For 15 minutes").Click += (s, e) => SetTemporaryAllow(TimeSpan.FromMinutes(15));
            tempAllowContextMenu.Items.Add("For 1 hour").Click += (s, e) => SetTemporaryAllow(TimeSpan.FromHours(1));
            tempAllowContextMenu.Items.Add("For 3 hours").Click += (s, e) => SetTemporaryAllow(TimeSpan.FromHours(3));
            tempAllowContextMenu.Items.Add("For 8 hours").Click += (s, e) => SetTemporaryAllow(TimeSpan.FromHours(8));
        }

        private void SetTemporaryAllow(TimeSpan duration)
        {
            Result = NotifierResult.TemporaryAllow;
            TemporaryDuration = duration;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void allowButton_Click(object sender, EventArgs e)
        {
            Result = NotifierResult.Allow;
            TrustPublisher = trustPublisherCheckBox.Visible && trustPublisherCheckBox.Checked;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void blockButton_Click(object sender, EventArgs e)
        {
            Result = NotifierResult.Block;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void ignoreButton_Click(object sender, EventArgs e)
        {
            Result = NotifierResult.Ignore;
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void tempAllowButton_Click(object sender, EventArgs e)
        {
            tempAllowContextMenu.Show(tempAllowButton, new Point(0, tempAllowButton.Height));
        }

        private void createWildcardButton_Click(object sender, EventArgs e)
        {
            Result = NotifierResult.CreateWildcard;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private async void copyDetailsButton_Click(object sender, EventArgs e)
        {
            try
            {
                var details = new System.Text.StringBuilder();
                details.AppendLine($"Type: Pending Connection");
                details.AppendLine($"Application: {PendingConnection.FileName}");
                details.AppendLine($"Path: {PendingConnection.AppPath}");
                details.AppendLine($"PID: {PendingConnection.ProcessId}");
                if (!string.IsNullOrEmpty(PendingConnection.ProcessOwner))
                    details.AppendLine($"Owner: {PendingConnection.ProcessOwner}");
                if (!string.IsNullOrEmpty(PendingConnection.ParentProcessId))
                {
                    string parentDisplay = string.IsNullOrEmpty(PendingConnection.ParentProcessName) ? PendingConnection.ParentProcessId : $"{PendingConnection.ParentProcessName} (PID: {PendingConnection.ParentProcessId})";
                    details.AppendLine($"Parent Process: {parentDisplay}");
                }
                details.AppendLine($"Service: {PendingConnection.ServiceName}");
                details.AppendLine($"Direction: {PendingConnection.Direction}");
                if (!string.IsNullOrEmpty(PendingConnection.CommandLine))
                    details.AppendLine($"CMD: {PendingConnection.CommandLine}");

                // clipboard retry logic
                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        Clipboard.SetText(details.ToString());
                        break;
                    }
                    catch (ExternalException)
                    {
                        if (i == 4) throw;
                        await Task.Delay(50);
                    }
                }

                copyDetailsButton.Text = "✓";

                await Task.Delay(2000);

                if (!this.IsDisposed && this.IsHandleCreated)
                {
                    copyDetailsButton.Text = "📋";
                }
            }
            catch
            {
            }
        }
    }
}