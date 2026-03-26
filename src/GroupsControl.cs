using DarkModeForms;
using MinimalFirewall.Groups;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Linq;
using System.ComponentModel;
using System.Collections.Generic;
using System;
using System.Drawing;
using System.Reflection;

namespace MinimalFirewall
{
    public partial class GroupsControl : UserControl
    {
        private FirewallGroupManager? _groupManager;
        private BackgroundFirewallTaskService? _backgroundTaskService;
        private FirewallActionsService? _actionsService;
        private AppSettings? _appSettings;
        private DarkModeCS? _dm;

        private BindingSource _bindingSource;

        private const int SwitchWidthBase = 50;
        private const int SwitchHeightBase = 25;
        private const int ThumbSizeBase = 21;

        public GroupsControl()
        {
            InitializeComponent();

            _bindingSource = new BindingSource(this.components);

            EnableDoubleBuffering(groupsDataGridView);
        }

        public void Initialize(FirewallGroupManager groupManager, BackgroundFirewallTaskService backgroundTaskService, DarkModeCS dm,
            FirewallActionsService? actionsService = null, AppSettings? appSettings = null)
        {
            _groupManager = groupManager;
            _backgroundTaskService = backgroundTaskService;
            _dm = dm;
            _actionsService = actionsService;
            _appSettings = appSettings;

            groupsDataGridView.AutoGenerateColumns = false;
            groupsDataGridView.DataSource = _bindingSource;
        }

        private void EnableDoubleBuffering(Control control)
        {
            typeof(Control).InvokeMember("DoubleBuffered",
                BindingFlags.SetProperty | BindingFlags.Instance | BindingFlags.NonPublic,
                null, control, new object[] { true });
        }

        public void ClearGroups()
        {
            _bindingSource.DataSource = null;
        }

        public async Task OnTabSelectedAsync()
        {
            await DisplayGroupsAsync();
        }

        private async Task DisplayGroupsAsync()
        {
            if (groupsDataGridView is null || _groupManager is null) return;

            try
            {
                var groups = await Task.Run(() => _groupManager.GetAllGroups());

                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => _bindingSource.DataSource = new SortableBindingList<FirewallGroup>(groups)));
                }
                else
                {
                    _bindingSource.DataSource = new SortableBindingList<FirewallGroup>(groups);
                }
                groupsDataGridView.Refresh();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading groups: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void deleteGroupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (groupsDataGridView.SelectedRows.Count > 0 && _backgroundTaskService != null)
            {
                var result = MessageBox.Show(
                    "Are you sure you want to delete the \nselected group(s) and all associated rules?",
                    "Confirm Deletion",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    // Collect items first to avoid modifying the collection while iterating
                    var itemsToDelete = new List<FirewallGroup>();

                    foreach (DataGridViewRow row in groupsDataGridView.SelectedRows)
                    {
                        if (row.DataBoundItem is FirewallGroup group)
                        {
                            itemsToDelete.Add(group);
                        }
                    }

                    foreach (var group in itemsToDelete)
                    {
                        _backgroundTaskService.EnqueueTask(new FirewallTask(FirewallTaskType.DeleteGroup, group.Name));

                        _bindingSource.Remove(group);
                    }
                }
            }
        }

        private void renameGroupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (groupsDataGridView.SelectedRows.Count == 0 || _backgroundTaskService == null) return;
            if (groupsDataGridView.SelectedRows[0].DataBoundItem is not FirewallGroup group) return;

            string oldName = group.Name;
            string? newName = PromptForGroupName("Rename Group", "Enter new group name:", oldName);

            if (newName == null || string.Equals(newName, oldName, StringComparison.Ordinal)) return;

            var payload = new RenameGroupPayload { OldGroupName = oldName, NewGroupName = newName };
            _backgroundTaskService.EnqueueTask(new FirewallTask(FirewallTaskType.RenameGroup, payload, $"Renaming group '{oldName}'"));

            // Refresh the view after rename
            _ = DisplayGroupsAsync();
        }

        private void addGroupButton_Click(object sender, EventArgs e)
        {
            string? groupName = PromptForGroupName("Add Group", "Enter a name for the new group:", string.Empty);
            if (string.IsNullOrWhiteSpace(groupName)) return;

            if (_actionsService == null || _appSettings == null) return;

            using var dialog = new CreateAdvancedRuleForm(_actionsService, _appSettings, groupName);
            if (dialog.ShowDialog(this.FindForm()) == DialogResult.OK && dialog.RuleVm != null && _backgroundTaskService != null)
            {
                var payload = new CreateAdvancedRulePayload
                {
                    ViewModel = dialog.RuleVm,
                    InterfaceTypes = dialog.RuleVm.InterfaceTypes,
                    IcmpTypesAndCodes = dialog.RuleVm.IcmpTypesAndCodes
                };
                _backgroundTaskService.EnqueueTask(new FirewallTask(FirewallTaskType.CreateAdvancedRule, payload, $"Creating rule in group '{groupName}'"));
                _ = DisplayGroupsAsync();
            }
        }

        private static string? PromptForGroupName(string title, string prompt, string defaultValue)
        {
            using var form = new Form
            {
                Text = title,
                ClientSize = new Size(380, 130),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false
            };

            var label = new Label { Text = prompt, Location = new Point(12, 12), AutoSize = true };
            var textBox = new TextBox { Location = new Point(12, 36), Width = 356, Text = defaultValue };
            var okButton = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Location = new Point(210, 72),
                Width = 76
            };
            var cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(292, 72),
                Width = 76
            };

            form.Controls.AddRange([label, textBox, okButton, cancelButton]);
            form.AcceptButton = okButton;
            form.CancelButton = cancelButton;

            if (form.ShowDialog() == DialogResult.OK)
            {
                string result = textBox.Text.Trim();
                return string.IsNullOrEmpty(result) ? null : result;
            }
            return null;
        }

        private void groupsDataGridView_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == groupsDataGridView.Columns["groupEnabledColumn"].Index)
            {
                if (groupsDataGridView.Rows[e.RowIndex].DataBoundItem is FirewallGroup group && _backgroundTaskService != null)
                {
                    bool newState = !group.IsEnabled;
                    group.SetEnabledState(newState);

                    var payload = new SetGroupEnabledStatePayload { GroupName = group.Name, IsEnabled = newState };
                    _backgroundTaskService.EnqueueTask(new FirewallTask(FirewallTaskType.SetGroupEnabledState, payload));

                    groupsDataGridView.InvalidateCell(e.ColumnIndex, e.RowIndex);
                }
            }
        }

        private void groupsDataGridView_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == groupsDataGridView.Columns["groupEnabledColumn"].Index)
            {
                e.PaintBackground(e.CellBounds, true);
                if (groupsDataGridView.Rows[e.RowIndex].DataBoundItem is FirewallGroup group && e.Graphics != null)
                {
                    DrawToggleSwitch(e.Graphics, e.CellBounds, group.IsEnabled);
                }
                e.Handled = true;
            }
        }

        private void DrawToggleSwitch(Graphics g, Rectangle bounds, bool isChecked)
        {
            if (_dm == null) return;

            // DPI Scaling calculation
            float scaleFactor = g.DpiY / 96f;
            int switchWidth = (int)(SwitchWidthBase * scaleFactor);
            int switchHeight = (int)(SwitchHeightBase * scaleFactor);
            int thumbSize = (int)(ThumbSizeBase * scaleFactor);
            int padding = (int)(2 * scaleFactor);

            Rectangle switchRect = new Rectangle(
                bounds.X + (bounds.Width - switchWidth) / 2,
                bounds.Y + (bounds.Height - switchHeight) / 2,
                switchWidth,
                switchHeight);

            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Color backColor = isChecked ? Color.FromArgb(0, 192, 0) : Color.FromArgb(200, 0, 0);

            using (var path = new System.Drawing.Drawing2D.GraphicsPath())
            {
                path.AddArc(switchRect.X, switchRect.Y, switchRect.Height, switchRect.Height, 90, 180);
                path.AddArc(switchRect.Right - switchRect.Height, switchRect.Y, switchRect.Height, switchRect.Height, 270, 180);
                path.CloseFigure();

                using var brush = new SolidBrush(backColor);
                g.FillPath(brush, path);
            }

            int thumbX = isChecked ?
                switchRect.Right - thumbSize - padding :
                switchRect.X + padding;

            Rectangle thumbRect = new Rectangle(
                thumbX,
                switchRect.Y + (switchRect.Height - thumbSize) / 2,
                thumbSize,
                thumbSize);

            using var thumbBrush = new SolidBrush(_dm.OScolors.TextActive);
            g.FillEllipse(thumbBrush, thumbRect);
        }

        private void groupsDataGridView_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0) return;

            var column = groupsDataGridView.Columns[e.ColumnIndex];
            string propertyName = column.DataPropertyName;

            if (string.IsNullOrEmpty(propertyName)) return;

            var direction = ListSortDirection.Ascending;
            if (column.HeaderCell.SortGlyphDirection == SortOrder.Ascending)
            {
                direction = ListSortDirection.Descending;
            }

            if (_bindingSource.DataSource is SortableBindingList<FirewallGroup> list)
            {
                list.Sort(propertyName, direction);
            }

            foreach (DataGridViewColumn col in groupsDataGridView.Columns)
            {
                if (col != column) col.HeaderCell.SortGlyphDirection = SortOrder.None;
            }

            column.HeaderCell.SortGlyphDirection = direction == ListSortDirection.Ascending ? SortOrder.Ascending : SortOrder.Descending;
        }

        private void groupsDataGridView_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && e.RowIndex >= 0)
            {
                var grid = (DataGridView)sender;
                var clickedRow = grid.Rows[e.RowIndex];

                if (!clickedRow.Selected)
                {
                    grid.ClearSelection();
                    clickedRow.Selected = true;
                }
            }
        }
    }
}