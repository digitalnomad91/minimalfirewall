// File: GroupsControl.Designer.cs
namespace MinimalFirewall
{
    partial class GroupsControl
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.ContextMenuStrip groupsContextMenu;
        private System.Windows.Forms.ToolStripMenuItem deleteGroupToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem renameGroupToolStripMenuItem;
        private System.Windows.Forms.DataGridView groupsDataGridView;
        private System.Windows.Forms.DataGridViewTextBoxColumn groupNameColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn groupEnabledColumn;
        private System.Windows.Forms.Panel topPanel;
        private System.Windows.Forms.Button addGroupButton;
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.groupsContextMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.renameGroupToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.deleteGroupToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.groupsDataGridView = new System.Windows.Forms.DataGridView();
            this.groupNameColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.groupEnabledColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.topPanel = new System.Windows.Forms.Panel();
            this.addGroupButton = new System.Windows.Forms.Button();
            this.groupsContextMenu.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.groupsDataGridView)).BeginInit();
            this.topPanel.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupsContextMenu
            // 
            this.groupsContextMenu.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.groupsContextMenu.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.renameGroupToolStripMenuItem,
            this.deleteGroupToolStripMenuItem});
            this.groupsContextMenu.Name = "groupsContextMenu";
            this.groupsContextMenu.Size = new System.Drawing.Size(177, 52);
            // 
            // renameGroupToolStripMenuItem
            // 
            this.renameGroupToolStripMenuItem.Name = "renameGroupToolStripMenuItem";
            this.renameGroupToolStripMenuItem.Size = new System.Drawing.Size(176, 24);
            this.renameGroupToolStripMenuItem.Text = "Rename Group...";
            this.renameGroupToolStripMenuItem.Click += new System.EventHandler(this.renameGroupToolStripMenuItem_Click);
            // 
            // deleteGroupToolStripMenuItem
            // 
            this.deleteGroupToolStripMenuItem.Name = "deleteGroupToolStripMenuItem";
            this.deleteGroupToolStripMenuItem.Size = new System.Drawing.Size(176, 24);
            this.deleteGroupToolStripMenuItem.Text = "Delete Group...";
            this.deleteGroupToolStripMenuItem.Click += new System.EventHandler(this.deleteGroupToolStripMenuItem_Click);
            // 
            // topPanel
            // 
            this.topPanel.Controls.Add(this.addGroupButton);
            this.topPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.topPanel.Height = 40;
            this.topPanel.Name = "topPanel";
            this.topPanel.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            // 
            // addGroupButton
            // 
            this.addGroupButton.Location = new System.Drawing.Point(4, 4);
            this.addGroupButton.Name = "addGroupButton";
            this.addGroupButton.Size = new System.Drawing.Size(130, 30);
            this.addGroupButton.TabIndex = 0;
            this.addGroupButton.Text = "+ Add Group";
            this.addGroupButton.UseVisualStyleBackColor = true;
            this.addGroupButton.Click += new System.EventHandler(this.addGroupButton_Click);
            // 
            // groupsDataGridView
            // 
            this.groupsDataGridView.AllowUserToAddRows = false;
            this.groupsDataGridView.AllowUserToDeleteRows = false;
            this.groupsDataGridView.AllowUserToResizeRows = false;
            this.groupsDataGridView.AllowUserToOrderColumns = true;
            this.groupsDataGridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.None;
            this.groupsDataGridView.BackgroundColor = System.Drawing.SystemColors.Control;
            this.groupsDataGridView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.groupsDataGridView.CellBorderStyle = System.Windows.Forms.DataGridViewCellBorderStyle.None;
            this.groupsDataGridView.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.None;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Segoe UI", 12F);
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.groupsDataGridView.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.groupsDataGridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.groupsDataGridView.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.groupNameColumn,
            this.groupEnabledColumn});
            this.groupsDataGridView.ContextMenuStrip = this.groupsContextMenu;
            this.groupsDataGridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.groupsDataGridView.EnableHeadersVisualStyles = false;
            this.groupsDataGridView.GridColor = System.Drawing.SystemColors.Control;
            this.groupsDataGridView.Location = new System.Drawing.Point(0, 40);
            this.groupsDataGridView.Name = "groupsDataGridView";
            this.groupsDataGridView.ReadOnly = true;
            this.groupsDataGridView.RowHeadersVisible = false;
            this.groupsDataGridView.RowTemplate.Height = 35;
            this.groupsDataGridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.groupsDataGridView.ShowCellToolTips = true;
            this.groupsDataGridView.Size = new System.Drawing.Size(800, 560);
            this.groupsDataGridView.TabIndex = 1;
            this.groupsDataGridView.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.groupsDataGridView_CellClick);
            this.groupsDataGridView.CellMouseDown += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.groupsDataGridView_CellMouseDown);
            this.groupsDataGridView.CellPainting += new System.Windows.Forms.DataGridViewCellPaintingEventHandler(this.groupsDataGridView_CellPainting);
            this.groupsDataGridView.ColumnHeaderMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler(this.groupsDataGridView_ColumnHeaderMouseClick);
            // 
            // groupNameColumn
            // 
            this.groupNameColumn.DataPropertyName = "Name";
            this.groupNameColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill; 
            this.groupNameColumn.HeaderText = "Group Name";
            this.groupNameColumn.Name = "groupNameColumn";
            this.groupNameColumn.ReadOnly = true;
            // 
            // groupEnabledColumn
            // 
            this.groupEnabledColumn.DataPropertyName = "IsEnabled";
            this.groupEnabledColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.groupEnabledColumn.HeaderText = "Enabled";
            this.groupEnabledColumn.Name = "groupEnabledColumn";
            this.groupEnabledColumn.ReadOnly = true;
            // 
            // GroupsControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.groupsDataGridView);
            this.Controls.Add(this.topPanel);
            this.Name = "GroupsControl";
            this.Size = new System.Drawing.Size(800, 600);
            this.groupsContextMenu.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.groupsDataGridView)).EndInit();
            this.topPanel.ResumeLayout(false);
            this.ResumeLayout(false);
        }
        #endregion
    }
}