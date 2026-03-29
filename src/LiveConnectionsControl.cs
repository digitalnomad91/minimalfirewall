using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using Firewall.Traffic.ViewModels;
using System.Linq;
using NetFwTypeLib;
using MinimalFirewall.TypedObjects;
using System.Collections.Specialized;
using System;
using System.Collections.Generic;
using DarkModeForms;

namespace MinimalFirewall
{
    public partial class LiveConnectionsControl : UserControl
    {
        private TrafficMonitorViewModel _viewModel = null!;
        private AppSettings _appSettings = null!;
        private IconService _iconService = null!;
        private BackgroundFirewallTaskService _backgroundTaskService = null!;
        private FirewallActionsService _actionsService = null!;
        private DarkModeCS? _dm;

        private SortableBindingList<TcpConnectionViewModel> _sortableList = new();
        private readonly HashSet<TcpConnectionViewModel> _terminatedSet = new(ReferenceEqualityComparer.Instance);
        private bool _keepTerminated = false;

        // Auto-refresh timer
        private System.Windows.Forms.Timer? _autoRefreshTimer;
        private bool _isAutoRefreshing = true;
        private bool _isRefreshing = false;
        private const int AutoRefreshIntervalMs = 3000;

        // Animation constants
        private const int SpinnerAnimationIntervalMs = 150;
        private const int FlashInitialAlpha = 200;
        private const int FlashDecayRate = 15; // alpha per 100ms tick
        private const int FlashDecayIntervalMs = 100;

        // Spinner animation
        private System.Windows.Forms.Timer? _spinnerTimer;
        private int _spinnerFrame = 0;
        private static readonly string[] SpinnerFrames = { "◐", "◓", "◑", "◒" };

        // Row flash tracking
        private readonly Dictionary<string, int> _flashingRows = new();
        private System.Windows.Forms.Timer? _flashDecayTimer;

        // Previous snapshot for change detection
        private readonly HashSet<string> _previousConnectionKeys = new();

        // Cached GDI+ objects 
        private static readonly Brush HoverOverlayBrushLight = new SolidBrush(Color.FromArgb(25, Color.Black));
        private static readonly Brush HoverOverlayBrushDark = new SolidBrush(Color.FromArgb(30, Color.White));

        // Light mode colors
        private static readonly Color EstablishedColorLight = Color.FromArgb(204, 255, 204);
        private static readonly Color ListenColorLight = Color.FromArgb(255, 255, 204);
        private static readonly Color TerminatedColorLight = Color.FromArgb(240, 240, 240);
        private static readonly Color FlashColorLight = Color.FromArgb(180, 220, 255);

        // Dark mode colors
        private static readonly Color EstablishedColorDark = Color.FromArgb(35, 60, 35);
        private static readonly Color ListenColorDark = Color.FromArgb(60, 58, 30);
        private static readonly Color TerminatedColorDark = Color.FromArgb(50, 50, 50);
        private static readonly Color FlashColorDark = Color.FromArgb(30, 50, 80);

        private int _hoveredRowIndex = -1;

        // Callback for refresh — wired by MainForm
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public Func<Task>? RefreshCallback { get; set; }

        public LiveConnectionsControl()
        {
            InitializeComponent();
            this.DoubleBuffered = true;
        }

        public void Initialize(
            TrafficMonitorViewModel viewModel,
            AppSettings appSettings,
            IconService iconService,
            BackgroundFirewallTaskService backgroundTaskService,
            FirewallActionsService actionsService,
            DarkModeCS? dm = null)
        {
            _viewModel = viewModel;
            _appSettings = appSettings;
            _iconService = iconService;
            _backgroundTaskService = backgroundTaskService;
            _actionsService = actionsService;
            _dm = dm;

            typeof(DataGridView).InvokeMember(
               "DoubleBuffered",
               System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.SetProperty,
               null,
               liveConnectionsDataGridView,
               new object[] { true });

            liveConnectionsDataGridView.VirtualMode = true;
            liveConnectionsDataGridView.AutoGenerateColumns = false;
            liveConnectionsDataGridView.DataSource = null;
            liveConnectionsDataGridView.CellValueNeeded += LiveConnectionsDataGridView_CellValueNeeded;

            _sortableList = new SortableBindingList<TcpConnectionViewModel>(_viewModel.ActiveConnections.ToList());

            _viewModel.PropertyChanged += ViewModel_PropertyChanged;

            // Flash decay timer — reduces flash alpha by FlashDecayRate every FlashDecayIntervalMs
            _flashDecayTimer = new System.Windows.Forms.Timer { Interval = FlashDecayIntervalMs };
            _flashDecayTimer.Tick += FlashDecayTimer_Tick;
            _flashDecayTimer.Start();

            UpdateEnabledState();
            UpdateLiveConnectionsView();
        }

        // --- Auto-Refresh ---

        public void StartAutoRefresh()
        {
            if (_autoRefreshTimer != null) return;
            _autoRefreshTimer = new System.Windows.Forms.Timer { Interval = AutoRefreshIntervalMs };
            _autoRefreshTimer.Tick += AutoRefreshTimer_Tick;
            _autoRefreshTimer.Start();
            UpdateRefreshButtonState();
        }

        public void StopAutoRefresh()
        {
            _autoRefreshTimer?.Stop();
            _autoRefreshTimer?.Dispose();
            _autoRefreshTimer = null;
            HideSpinner();
            UpdateRefreshButtonState();
        }

        private async void AutoRefreshTimer_Tick(object? sender, EventArgs e)
        {
            if (_isRefreshing || RefreshCallback == null) return;
            await DoRefreshAsync();
        }

        private async void refreshButton_Click(object sender, EventArgs e)
        {
            if (_isRefreshing || RefreshCallback == null) return;
            await DoRefreshAsync();
        }

        private async Task DoRefreshAsync()
        {
            if (_isRefreshing) return;
            _isRefreshing = true;
            ShowSpinner();

            try
            {
                if (RefreshCallback != null)
                    await RefreshCallback.Invoke();
            }
            catch (Exception)
            {
                // Ignore refresh errors
            }
            finally
            {
                _isRefreshing = false;
                HideSpinner();
            }
        }

        private void autoRefreshCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            _isAutoRefreshing = autoRefreshCheckBox.Checked;
            if (_isAutoRefreshing)
                StartAutoRefresh();
            else
                StopAutoRefresh();
        }

        private void UpdateRefreshButtonState()
        {
            refreshButton.Enabled = !_isAutoRefreshing || !_isRefreshing;
        }

        private void ShowSpinner()
        {
            spinnerLabel.Visible = true;
            _spinnerFrame = 0;
            if (_spinnerTimer == null)
            {
                _spinnerTimer = new System.Windows.Forms.Timer { Interval = SpinnerAnimationIntervalMs };
                _spinnerTimer.Tick += (s, e) =>
                {
                    _spinnerFrame = (_spinnerFrame + 1) % SpinnerFrames.Length;
                    spinnerLabel.Text = SpinnerFrames[_spinnerFrame];
                };
            }
            _spinnerTimer.Start();
        }

        private void HideSpinner()
        {
            _spinnerTimer?.Stop();
            spinnerLabel.Visible = false;
            spinnerLabel.Text = "";
        }

        // --- Flash Decay ---

        private void FlashDecayTimer_Tick(object? sender, EventArgs e)
        {
            if (_flashingRows.Count == 0) return;

            var keysToRemove = new List<string>();
            var keysToDecay = new List<string>(_flashingRows.Keys);

            foreach (var key in keysToDecay)
            {
                int alpha = _flashingRows[key] - FlashDecayRate;
                if (alpha <= 0)
                    keysToRemove.Add(key);
                else
                    _flashingRows[key] = alpha;
            }

            foreach (var key in keysToRemove)
                _flashingRows.Remove(key);

            liveConnectionsDataGridView.Invalidate();
        }

        private void UnsubscribeEvents()
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            UnsubscribeEvents();
            StopAutoRefresh();
            _flashDecayTimer?.Stop();
            _flashDecayTimer?.Dispose();
            _spinnerTimer?.Stop();
            _spinnerTimer?.Dispose();
            base.OnHandleDestroyed(e);
        }

        public void OnTabDeselected()
        {
            UnsubscribeEvents();
            StopAutoRefresh();
            if (_viewModel != null) _viewModel.StopMonitoring();
            UpdateEnabledState();
        }

        public void OnTabSelected()
        {
            if (_isAutoRefreshing && _appSettings?.IsTrafficMonitorEnabled == true)
            {
                StartAutoRefresh();
            }
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TrafficMonitorViewModel.ActiveConnections))
            {
                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(UpdateLiveConnectionsView));
                }
                else
                {
                    UpdateLiveConnectionsView();
                }
            }
        }

        public void UpdateEnabledState()
        {
            if (_appSettings == null) return;
            bool isEnabled = _appSettings.IsTrafficMonitorEnabled;
            liveConnectionsDataGridView.Visible = isEnabled;
            topToolbarPanel.Visible = isEnabled;
            disabledPanel.Visible = !isEnabled;
        }

        public void UpdateLiveConnectionsView()
        {
            if (_viewModel == null) return;

            // Update last-refreshed indicator
            lastRefreshedLabel.Text = $"Last updated: {DateTime.Now:HH:mm:ss}";

            string? selectedIdentifier = null;
            if (TryGetSelectedConnection(out var currentConn) && currentConn != null)
            {
                selectedIdentifier = currentConn.ProcessPath + currentConn.RemotePort;
            }

            var newActiveList = _viewModel.ActiveConnections.ToList();

            // Detect new connections for flash effect
            var currentKeys = new HashSet<string>(newActiveList.Select(ConnectionKey));
            foreach (var conn in newActiveList)
            {
                string key = ConnectionKey(conn);
                if (!_previousConnectionKeys.Contains(key))
                {
                    _flashingRows[key] = FlashInitialAlpha;
                }
            }
            _previousConnectionKeys.Clear();
            foreach (var key in currentKeys)
                _previousConnectionKeys.Add(key);

            List<TcpConnectionViewModel> displayList;

            if (_keepTerminated)
            {
                // Build a lookup set of currently active connections by composite key
                var activeKeys = new HashSet<string>(newActiveList.Select(ConnectionKey));

                // Find connections that just disappeared (terminated)
                foreach (var prev in _sortableList)
                {
                    if (!activeKeys.Contains(ConnectionKey(prev)))
                    {
                        _terminatedSet.Add(prev);
                    }
                }

                // Remove any that have reappeared
                _terminatedSet.RemoveWhere(t => activeKeys.Contains(ConnectionKey(t)));

                // Build display list: active + terminated
                displayList = new List<TcpConnectionViewModel>(newActiveList);
                displayList.AddRange(_terminatedSet);
            }
            else
            {
                _terminatedSet.Clear();
                displayList = newActiveList;
            }

            // apply sorting
            if (_appSettings != null)
            {
                int sortCol = _appSettings.LiveConnectionsSortColumn;
                int sortOrd = _appSettings.LiveConnectionsSortOrder;

                if (sortCol > -1 && sortCol < liveConnectionsDataGridView.Columns.Count)
                {
                    var col = liveConnectionsDataGridView.Columns[sortCol];
                    if (!string.IsNullOrEmpty(col.DataPropertyName))
                    {
                        _sortableList = new SortableBindingList<TcpConnectionViewModel>(displayList);
                        var dir = sortOrd == (int)SortOrder.Ascending ? ListSortDirection.Ascending : ListSortDirection.Descending;
                        _sortableList.Sort(col.DataPropertyName, dir);
                        col.HeaderCell.SortGlyphDirection = (SortOrder)sortOrd;
                    }
                    else
                    {
                        _sortableList = new SortableBindingList<TcpConnectionViewModel>(displayList);
                    }
                }
                else
                {
                    _sortableList = new SortableBindingList<TcpConnectionViewModel>(displayList);
                }
            }
            else
            {
                _sortableList = new SortableBindingList<TcpConnectionViewModel>(displayList);
            }

            liveConnectionsDataGridView.RowCount = _sortableList.Count;
            liveConnectionsDataGridView.Invalidate();

            if (selectedIdentifier != null)
            {
                var itemToSelect = _sortableList.FirstOrDefault(x => (x.ProcessPath + x.RemotePort) == selectedIdentifier);
                if (itemToSelect != null)
                {
                    int newIndex = _sortableList.IndexOf(itemToSelect);
                    if (newIndex >= 0 && newIndex < liveConnectionsDataGridView.RowCount)
                    {
                        liveConnectionsDataGridView.ClearSelection();
                        liveConnectionsDataGridView.Rows[newIndex].Selected = true;
                    }
                }
            }

            UpdateEnabledState();
        }

        private void LiveConnectionsDataGridView_CellValueNeeded(object? sender, DataGridViewCellValueEventArgs e)
        {
            if (e.RowIndex >= _sortableList.Count || e.RowIndex < 0) return;
            var conn = _sortableList[e.RowIndex];

            switch (liveConnectionsDataGridView.Columns[e.ColumnIndex].Name)
            {
                case "connIconColumn":
                    if (_appSettings != null && _appSettings.ShowAppIcons && !string.IsNullOrEmpty(conn.ProcessPath))
                    {
                        int iconIndex = _iconService.GetIconIndex(conn.ProcessPath);
                        if (iconIndex != -1 && _iconService.ImageList != null)
                        {
                            e.Value = _iconService.ImageList.Images[iconIndex];
                        }
                    }
                    break;
                case "connNameColumn": e.Value = conn.DisplayName; break;
                case "connStateColumn": e.Value = conn.State; break;
                case "connLocalAddrColumn": e.Value = conn.LocalAddress; break;
                case "connLocalPortColumn": e.Value = conn.LocalPort; break;
                case "connRemoteAddrColumn": e.Value = conn.RemoteAddress; break;
                case "connRemotePortColumn": e.Value = conn.RemotePort; break;
                case "connPathColumn": e.Value = conn.ProcessPath; break;
            }
        }

        public void UpdateIconColumnVisibility()
        {
            if (connIconColumn != null && _appSettings != null)
            {
                connIconColumn.Visible = _appSettings.ShowAppIcons;
                liveConnectionsDataGridView.InvalidateColumn(connIconColumn.Index);
            }
        }

        private void liveConnectionsDataGridView_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _sortableList.Count) return;
            var conn = _sortableList[e.RowIndex];

            bool isDark = _dm != null && _dm.IsDarkMode;
            bool isTerminated = _keepTerminated && _terminatedSet.Contains(conn);
            string connKey = ConnectionKey(conn);

            Color establishedBack = isDark ? EstablishedColorDark : EstablishedColorLight;
            Color listenBack = isDark ? ListenColorDark : ListenColorLight;
            Color terminatedBack = isDark ? TerminatedColorDark : TerminatedColorLight;
            Color flashBase = isDark ? FlashColorDark : FlashColorLight;
            Color textColor = isDark ? Color.FromArgb(240, 240, 240) : Color.Black;
            Color terminatedText = isDark ? Color.FromArgb(130, 130, 130) : Color.Gray;

            // Flash effect for new/changed rows
            if (_flashingRows.TryGetValue(connKey, out int flashAlpha) && flashAlpha > 0)
            {
                e.CellStyle.BackColor = Color.FromArgb(flashAlpha, flashBase.R, flashBase.G, flashBase.B);
                e.CellStyle.ForeColor = textColor;
            }
            else if (isTerminated)
            {
                e.CellStyle.BackColor = terminatedBack;
                e.CellStyle.ForeColor = terminatedText;
            }
            // Color code rows based on connection state
            else if (conn.State != null)
            {
                if (conn.State.Equals("Established", StringComparison.OrdinalIgnoreCase))
                {
                    e.CellStyle.BackColor = establishedBack;
                    e.CellStyle.ForeColor = textColor;
                }
                else if (conn.State.Equals("Listen", StringComparison.OrdinalIgnoreCase))
                {
                    e.CellStyle.BackColor = listenBack;
                    e.CellStyle.ForeColor = textColor;
                }
                else
                {
                    // Other states (TimeWait, CloseWait, etc.)
                    if (isDark)
                    {
                        e.CellStyle.BackColor = _dm!.OScolors.Surface;
                        e.CellStyle.ForeColor = _dm.OScolors.TextActive;
                    }
                }
            }
            else
            {
                // No state — ensure dark mode defaults are applied
                if (isDark)
                {
                    e.CellStyle.BackColor = _dm!.OScolors.Surface;
                    e.CellStyle.ForeColor = _dm.OScolors.TextActive;
                }
            }

            if (liveConnectionsDataGridView.Rows[e.RowIndex].Selected)
            {
                e.CellStyle.SelectionBackColor = isDark
                    ? Color.FromArgb(50, 80, 120)
                    : SystemColors.Highlight;
                e.CellStyle.SelectionForeColor = isDark
                    ? Color.White
                    : SystemColors.HighlightText;
            }
            else
            {
                e.CellStyle.SelectionBackColor = e.CellStyle.BackColor;
                e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
            }
        }

        private void keepTerminatedCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            _keepTerminated = keepTerminatedCheckBox.Checked;
            if (!_keepTerminated)
            {
                _terminatedSet.Clear();
                UpdateLiveConnectionsView();
            }
        }

        private static string ConnectionKey(TcpConnectionViewModel c)
            => $"{c.ProcessPath}|{c.LocalPort}|{c.RemotePort}|{c.RemoteAddress}";

        // --- Mouse & Selection Handling ---

        private void liveConnectionsDataGridView_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
            {
                var grid = (DataGridView)sender;
                if (!grid.Rows[e.RowIndex].Selected)
                {
                    grid.ClearSelection();
                    grid.Rows[e.RowIndex].Selected = true;
                }
            }
        }

        private void liveConnectionsDataGridView_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                _hoveredRowIndex = e.RowIndex;
                liveConnectionsDataGridView.InvalidateRow(e.RowIndex);
            }
        }

        private void liveConnectionsDataGridView_CellMouseLeave(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                _hoveredRowIndex = -1;
                liveConnectionsDataGridView.InvalidateRow(e.RowIndex);
            }
        }

        private void liveConnectionsDataGridView_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            if (!liveConnectionsDataGridView.Rows[e.RowIndex].Selected && e.RowIndex == _hoveredRowIndex)
            {
                bool isDark = _dm != null && _dm.IsDarkMode;
                e.Graphics.FillRectangle(isDark ? HoverOverlayBrushDark : HoverOverlayBrushLight, e.RowBounds);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            liveConnectionsDataGridView.ColumnHeaderMouseClick += (s, ev) =>
            {
                if (_appSettings != null && ev.ColumnIndex >= 0)
                {
                    int currentSort;

                    if (_appSettings.LiveConnectionsSortColumn == ev.ColumnIndex)
                    {
                        // Toggle direction
                        currentSort = _appSettings.LiveConnectionsSortOrder == (int)SortOrder.Ascending
                            ? (int)SortOrder.Descending
                            : (int)SortOrder.Ascending;
                    }
                    else
                    {
                        // New column, reset to Ascending
                        currentSort = (int)SortOrder.Ascending;
                    }

                    _appSettings.LiveConnectionsSortColumn = ev.ColumnIndex;
                    _appSettings.LiveConnectionsSortOrder = currentSort;
                    _appSettings.Save();

                    UpdateLiveConnectionsView();
                }
            };
        }

        // --- Context Menu Actions ---

        private bool TryGetSelectedConnection(out TcpConnectionViewModel? connection)
        {
            connection = null;
            if (liveConnectionsDataGridView.SelectedRows.Count == 0) return false;

            int index = liveConnectionsDataGridView.SelectedRows[0].Index;
            if (index >= 0 && index < _sortableList.Count)
            {
                connection = _sortableList[index];
                return true;
            }
            return false;
        }

        private void liveConnectionsContextMenu_Opening(object sender, CancelEventArgs e)
        {
            if (!TryGetSelectedConnection(out var connection) || connection == null)
            {
                e.Cancel = true;
                return;
            }

            killProcessToolStripMenuItem.Enabled = connection.KillProcessCommand.CanExecute(null);
            blockRemoteIPToolStripMenuItem.Enabled = connection.BlockRemoteIpCommand.CanExecute(null);
            bool pathExists = !string.IsNullOrEmpty(connection.ProcessPath) && File.Exists(connection.ProcessPath);
            openFileLocationToolStripMenuItem.Enabled = pathExists;
            createAdvancedRuleToolStripMenuItem.Enabled = pathExists;
        }

        private void killProcessToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (TryGetSelectedConnection(out var connection) && connection != null && connection.KillProcessCommand.CanExecute(null))
            {
                connection.KillProcessCommand.Execute(null);
            }
        }

        private void blockRemoteIPToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (TryGetSelectedConnection(out var connection) && connection != null && connection.BlockRemoteIpCommand.CanExecute(null))
            {
                connection.BlockRemoteIpCommand.Execute(null);
            }
        }

        private void createAdvancedRuleToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (TryGetSelectedConnection(out var connection) && connection != null && !string.IsNullOrEmpty(connection.ProcessPath))
            {
                using var dialog = new CreateAdvancedRuleForm(_actionsService, connection.ProcessPath, "", _appSettings);
                if (dialog.ShowDialog(this.FindForm()) == DialogResult.OK)
                {
                    if (dialog.RuleVm != null)
                    {
                        var payload = new CreateAdvancedRulePayload { ViewModel = dialog.RuleVm, InterfaceTypes = dialog.RuleVm.InterfaceTypes, IcmpTypesAndCodes = dialog.RuleVm.IcmpTypesAndCodes };
                        _backgroundTaskService.EnqueueTask(new FirewallTask(FirewallTaskType.CreateAdvancedRule, payload));
                    }
                }
            }
        }

        private void openFileLocationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (TryGetSelectedConnection(out var connection) && connection != null && !string.IsNullOrEmpty(connection.ProcessPath))
            {
                if (!File.Exists(connection.ProcessPath) && !Directory.Exists(connection.ProcessPath))
                {
                    DarkModeForms.Messenger.MessageBox("The path for this item is no longer valid or does not exist.", "Path Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    Process.Start("explorer.exe", $"/select, \"{connection.ProcessPath}\"");
                }
                catch (Exception ex) when (ex is Win32Exception or FileNotFoundException)
                {
                    DarkModeForms.Messenger.MessageBox($"Could not open file location.\n\nError: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                DarkModeForms.Messenger.MessageBox("The path for this item is not available or does not exist.", "Path Not Found", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void copyDetailsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (TryGetSelectedConnection(out var connection) && connection != null)
            {
                var details = new System.Text.StringBuilder();
                details.AppendLine($"Type: Live Connection");
                details.AppendLine($"Application: {connection.DisplayName}");
                details.AppendLine($"Path: {connection.ProcessPath}");
                details.AppendLine($"State: {connection.State}");
                details.AppendLine($"Local: {connection.LocalAddress}:{connection.LocalPort}");
                details.AppendLine($"Remote: {connection.RemoteAddress}:{connection.RemotePort}");
                Clipboard.SetText(details.ToString());
            }
        }
    }
}