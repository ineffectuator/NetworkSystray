using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace NetworkManagerAppModern
{
    public partial class ProfileSelectionDialog : Form
    {
        public string SelectedProfile { get; private set; } = string.Empty;

        private ListBox listBoxProfiles;
        private Button btnOK;
        private Button btnCancel;

        public ProfileSelectionDialog(List<string> profiles)
        {
            InitializeComponent();
            listBoxProfiles.DataSource = profiles;
            if (profiles.Count > 0)
            {
                listBoxProfiles.SelectedIndex = 0;
            }
        }

        private void InitializeComponent()
        {
            this.listBoxProfiles = new System.Windows.Forms.ListBox();
            this.btnOK = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            //
            // listBoxProfiles
            //
            this.listBoxProfiles.FormattingEnabled = true;
            this.listBoxProfiles.ItemHeight = 15;
            this.listBoxProfiles.Location = new System.Drawing.Point(12, 12);
            this.listBoxProfiles.Name = "listBoxProfiles";
            this.listBoxProfiles.Size = new System.Drawing.Size(260, 139);
            this.listBoxProfiles.TabIndex = 0;
            //
            // btnOK
            //
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Location = new System.Drawing.Point(116, 157);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 1;
            this.btnOK.Text = "OK";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Click += new System.EventHandler(this.BtnOK_Click);
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(197, 157);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(75, 23);
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            //
            // ProfileSelectionDialog
            //
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(284, 191);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.listBoxProfiles);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ProfileSelectionDialog";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Select Wi-Fi Profile";
            this.ResumeLayout(false);
        }

        private void BtnOK_Click(object? sender, EventArgs e)
        {
            if (listBoxProfiles.SelectedItem != null)
            {
                SelectedProfile = listBoxProfiles.SelectedItem.ToString() ?? string.Empty;
            }
            // DialogResult is already set to OK by button's DialogResult property
        }
    }
}
