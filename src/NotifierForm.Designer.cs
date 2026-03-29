using System.Drawing;
using System.Windows.Forms;

namespace MinimalFirewall
{
    partial class NotifierForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label infoLabel;
        private System.Windows.Forms.Label appNameLabel;
        private System.Windows.Forms.Label pathLabel;
        private System.Windows.Forms.RichTextBox detailsRichTextBox;
        private System.Windows.Forms.CheckBox trustPublisherCheckBox;
        private System.Windows.Forms.Button createWildcardButton;
        private System.Windows.Forms.Button copyDetailsButton;
        private System.Windows.Forms.Button allowButton;
        private System.Windows.Forms.Button tempAllowButton;
        private System.Windows.Forms.Button blockButton;
        private System.Windows.Forms.Button ignoreButton;
        private System.Windows.Forms.ContextMenuStrip tempAllowContextMenu;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            infoLabel = new Label();
            appNameLabel = new Label();
            pathLabel = new Label();
            detailsRichTextBox = new RichTextBox();
            trustPublisherCheckBox = new CheckBox();
            createWildcardButton = new Button();
            copyDetailsButton = new Button();
            allowButton = new Button();
            tempAllowButton = new Button();
            blockButton = new Button();
            ignoreButton = new Button();
            tempAllowContextMenu = new ContextMenuStrip(components);
            SuspendLayout();
            // 
            // infoLabel
            // 
            infoLabel.AutoSize = true;
            infoLabel.Font = new Font("Segoe UI", 8.5F);
            infoLabel.Location = new Point(16, 12);
            infoLabel.Margin = new Padding(4, 0, 4, 0);
            infoLabel.Name = "infoLabel";
            infoLabel.Size = new Size(142, 15);
            infoLabel.TabIndex = 0;
            infoLabel.Text = "Blocked a connection for:";
            // 
            // appNameLabel
            // 
            appNameLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            appNameLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            appNameLabel.Location = new Point(16, 30);
            appNameLabel.Margin = new Padding(4, 0, 4, 0);
            appNameLabel.Name = "appNameLabel";
            appNameLabel.Size = new Size(348, 24);
            appNameLabel.TabIndex = 1;
            appNameLabel.Text = "Application Name";
            // 
            // pathLabel — now a Label, not a TextBox
            // 
            pathLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            pathLabel.AutoEllipsis = true;
            pathLabel.Font = new Font("Segoe UI", 7.5F);
            pathLabel.Location = new Point(16, 56);
            pathLabel.Margin = new Padding(4, 0, 4, 0);
            pathLabel.Name = "pathLabel";
            pathLabel.Padding = new Padding(6, 4, 6, 4);
            pathLabel.Size = new Size(348, 24);
            pathLabel.TabIndex = 2;
            pathLabel.Text = "C:\\Path\\To\\Application.exe";
            // 
            // detailsRichTextBox
            // 
            detailsRichTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            detailsRichTextBox.BackColor = SystemColors.Control;
            detailsRichTextBox.BorderStyle = BorderStyle.None;
            detailsRichTextBox.Font = new Font("Consolas", 8.5F);
            detailsRichTextBox.Location = new Point(16, 86);
            detailsRichTextBox.Margin = new Padding(4, 3, 4, 3);
            detailsRichTextBox.Name = "detailsRichTextBox";
            detailsRichTextBox.ReadOnly = true;
            detailsRichTextBox.ScrollBars = RichTextBoxScrollBars.None;
            detailsRichTextBox.Size = new Size(348, 58);
            detailsRichTextBox.TabIndex = 10;
            detailsRichTextBox.TabStop = false;
            detailsRichTextBox.Text = "";
            // 
            // trustPublisherCheckBox
            // 
            trustPublisherCheckBox.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            trustPublisherCheckBox.AutoSize = true;
            trustPublisherCheckBox.Font = new Font("Segoe UI", 8F);
            trustPublisherCheckBox.Location = new Point(56, 153);
            trustPublisherCheckBox.Margin = new Padding(4, 3, 4, 3);
            trustPublisherCheckBox.Name = "trustPublisherCheckBox";
            trustPublisherCheckBox.Size = new Size(142, 19);
            trustPublisherCheckBox.TabIndex = 3;
            trustPublisherCheckBox.Text = "Always trust publisher";
            trustPublisherCheckBox.UseVisualStyleBackColor = true;
            trustPublisherCheckBox.Visible = false;
            // 
            // createWildcardButton
            // 
            createWildcardButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            createWildcardButton.Location = new Point(16, 230);
            createWildcardButton.Margin = new Padding(4, 3, 4, 3);
            createWildcardButton.Name = "createWildcardButton";
            createWildcardButton.Size = new Size(152, 30);
            createWildcardButton.TabIndex = 4;
            createWildcardButton.Text = "Create Wildcard Rule…";
            createWildcardButton.UseVisualStyleBackColor = true;
            createWildcardButton.Click += createWildcardButton_Click;
            // 
            // copyDetailsButton
            // 
            copyDetailsButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            copyDetailsButton.Location = new Point(16, 149);
            copyDetailsButton.Margin = new Padding(4, 3, 4, 3);
            copyDetailsButton.Name = "copyDetailsButton";
            copyDetailsButton.Size = new Size(34, 28);
            copyDetailsButton.TabIndex = 5;
            copyDetailsButton.Text = "📋";
            copyDetailsButton.UseVisualStyleBackColor = true;
            copyDetailsButton.Click += copyDetailsButton_Click;
            // 
            // allowButton
            // 
            allowButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            allowButton.FlatStyle = FlatStyle.Flat;
            allowButton.Location = new Point(16, 188);
            allowButton.Margin = new Padding(4, 3, 4, 3);
            allowButton.Name = "allowButton";
            allowButton.Size = new Size(100, 32);
            allowButton.TabIndex = 6;
            allowButton.Text = "✓ Allow";
            allowButton.UseVisualStyleBackColor = true;
            allowButton.Click += allowButton_Click;
            // 
            // tempAllowButton
            // 
            tempAllowButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            tempAllowButton.Location = new Point(200, 230);
            tempAllowButton.Margin = new Padding(4, 3, 4, 3);
            tempAllowButton.Name = "tempAllowButton";
            tempAllowButton.Size = new Size(164, 30);
            tempAllowButton.TabIndex = 7;
            tempAllowButton.Text = "Allow Temporarily ▾";
            tempAllowButton.UseVisualStyleBackColor = true;
            tempAllowButton.Click += tempAllowButton_Click;
            // 
            // blockButton
            // 
            blockButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            blockButton.FlatStyle = FlatStyle.Flat;
            blockButton.Location = new Point(122, 188);
            blockButton.Margin = new Padding(4, 3, 4, 3);
            blockButton.Name = "blockButton";
            blockButton.Size = new Size(100, 32);
            blockButton.TabIndex = 8;
            blockButton.Text = "✕ Block";
            blockButton.UseVisualStyleBackColor = true;
            blockButton.Click += blockButton_Click;
            // 
            // ignoreButton
            // 
            ignoreButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            ignoreButton.Location = new Point(264, 188);
            ignoreButton.Margin = new Padding(4, 3, 4, 3);
            ignoreButton.Name = "ignoreButton";
            ignoreButton.Size = new Size(100, 32);
            ignoreButton.TabIndex = 9;
            ignoreButton.Text = "Ignore";
            ignoreButton.UseVisualStyleBackColor = true;
            ignoreButton.Click += ignoreButton_Click;
            // 
            // tempAllowContextMenu
            // 
            tempAllowContextMenu.Name = "tempAllowContextMenu";
            tempAllowContextMenu.Size = new Size(61, 4);
            // 
            // NotifierForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(380, 275);
            Controls.Add(ignoreButton);
            Controls.Add(blockButton);
            Controls.Add(tempAllowButton);
            Controls.Add(allowButton);
            Controls.Add(copyDetailsButton);
            Controls.Add(createWildcardButton);
            Controls.Add(trustPublisherCheckBox);
            Controls.Add(detailsRichTextBox);
            Controls.Add(pathLabel);
            Controls.Add(appNameLabel);
            Controls.Add(infoLabel);
            FormBorderStyle = FormBorderStyle.FixedToolWindow;
            Margin = new Padding(4, 3, 4, 3);
            MaximizeBox = false;
            MinimizeBox = false;
            MinimumSize = new Size(380, 275);
            Name = "NotifierForm";
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Text = "Connection Blocked";
            ResumeLayout(false);
            PerformLayout();

        }

        #endregion
    }
}