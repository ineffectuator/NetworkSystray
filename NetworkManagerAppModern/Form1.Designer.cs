namespace NetworkManagerAppModern
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.notifyIcon1 = new System.Windows.Forms.NotifyIcon(this.components);
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.showHideMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            //
            // notifyIcon1
            //
            this.notifyIcon1.ContextMenuStrip = this.contextMenuStrip1;
            this.notifyIcon1.Text = "Network Manager (.NET)";
            this.notifyIcon1.Visible = true;
            // Icon will be set in Form1.cs constructor
            //
            // contextMenuStrip1
            //
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.showHideMenuItem,
            this.exitMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(134, 48); // Adjusted size for typical menu items
            //
            // showHideMenuItem
            //
            this.showHideMenuItem.Name = "showHideMenuItem";
            this.showHideMenuItem.Size = new System.Drawing.Size(133, 22);
            this.showHideMenuItem.Text = "&Show/Hide";
            //
            // exitMenuItem
            //
            this.exitMenuItem.Name = "exitMenuItem";
            this.exitMenuItem.Size = new System.Drawing.Size(133, 22);
            this.exitMenuItem.Text = "E&xit";
            //
            // listViewNetworkInterfaces
            //
            this.listViewNetworkInterfaces = new System.Windows.Forms.ListView();
            this.columnHeaderName = new System.Windows.Forms.ColumnHeader();
            this.columnHeaderDescription = new System.Windows.Forms.ColumnHeader();
            this.columnHeaderStatus = new System.Windows.Forms.ColumnHeader();
            this.btnRefreshList = new System.Windows.Forms.Button();
            this.btnEnableSelected = new System.Windows.Forms.Button();
            this.btnDisableSelected = new System.Windows.Forms.Button();
            this.btnSelectColumns = new System.Windows.Forms.Button();
            //
            // Form1
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.listViewNetworkInterfaces);
            this.Controls.Add(this.btnRefreshList);
            this.Controls.Add(this.btnEnableSelected);
            this.Controls.Add(this.btnDisableSelected);
            this.Controls.Add(this.btnSelectColumns);
            this.Name = "Form1";
            this.Text = "Network Device Manager (.NET)";
            // Layout for ListView
            this.listViewNetworkInterfaces.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
            | System.Windows.Forms.AnchorStyles.Left)
            | System.Windows.Forms.AnchorStyles.Right)));
            this.listViewNetworkInterfaces.Location = new System.Drawing.Point(12, 12);
            this.listViewNetworkInterfaces.Size = new System.Drawing.Size(776, 380);
            this.listViewNetworkInterfaces.View = System.Windows.Forms.View.Details;
            this.listViewNetworkInterfaces.FullRowSelect = true;
            this.listViewNetworkInterfaces.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeaderName,
            this.columnHeaderDescription,
            this.columnHeaderStatus});
            this.columnHeaderName.Text = "Name";
            this.columnHeaderName.Width = 150;
            this.columnHeaderDescription.Text = "Description";
            this.columnHeaderDescription.Width = 250;
            this.columnHeaderStatus.Text = "Status";
            this.columnHeaderStatus.Width = 100;
            // Layout for Buttons (simple flow layout from bottom-left)
            this.btnRefreshList.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnRefreshList.Location = new System.Drawing.Point(12, 405);
            this.btnRefreshList.Size = new System.Drawing.Size(100, 23);
            this.btnRefreshList.Text = "Refresh List";
            this.btnRefreshList.UseVisualStyleBackColor = true;

            this.btnEnableSelected.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnEnableSelected.Location = new System.Drawing.Point(118, 405);
            this.btnEnableSelected.Size = new System.Drawing.Size(110, 23); // Slightly wider for text
            this.btnEnableSelected.Text = "Enable Selected";
            this.btnEnableSelected.UseVisualStyleBackColor = true;

            this.btnDisableSelected.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnDisableSelected.Location = new System.Drawing.Point(234, 405);
            this.btnDisableSelected.Size = new System.Drawing.Size(110, 23); // Slightly wider for text
            this.btnDisableSelected.Text = "Disable Selected";
            this.btnDisableSelected.UseVisualStyleBackColor = true;

            //
            // btnConnectSelected
            //
            this.btnConnectSelected = new System.Windows.Forms.Button();
            this.btnConnectSelected.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnConnectSelected.Location = new System.Drawing.Point(350, 405); // Adjusted location
            this.btnConnectSelected.Size = new System.Drawing.Size(110, 23);
            this.btnConnectSelected.Text = "Connect Selected";
            this.btnConnectSelected.UseVisualStyleBackColor = true;
            //
            // btnDisconnectSelected
            //
            this.btnDisconnectSelected = new System.Windows.Forms.Button();
            this.btnDisconnectSelected.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnDisconnectSelected.Location = new System.Drawing.Point(466, 405); // Adjusted location
            this.btnDisconnectSelected.Size = new System.Drawing.Size(120, 23); // Slightly wider for "Disconnect"
            this.btnDisconnectSelected.Text = "Disconnect Selected";
            this.btnDisconnectSelected.UseVisualStyleBackColor = true;

            this.btnSelectColumns.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnSelectColumns.Location = new System.Drawing.Point(688, 405);
            this.btnSelectColumns.Size = new System.Drawing.Size(100, 23);
            this.btnSelectColumns.Text = "Select Columns";
            this.btnSelectColumns.UseVisualStyleBackColor = true;

            // Add new buttons to Controls collection
            this.Controls.Add(this.btnConnectSelected);
            this.Controls.Add(this.btnDisconnectSelected);

            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.NotifyIcon notifyIcon1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem showHideMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exitMenuItem;
        private System.Windows.Forms.ListView listViewNetworkInterfaces;
        private System.Windows.Forms.ColumnHeader columnHeaderName;
        private System.Windows.Forms.ColumnHeader columnHeaderDescription;
        private System.Windows.Forms.ColumnHeader columnHeaderStatus;
        private System.Windows.Forms.Button btnRefreshList;
        private System.Windows.Forms.Button btnEnableSelected;
        private System.Windows.Forms.Button btnDisableSelected;
        private System.Windows.Forms.Button btnConnectSelected; // Declaration
        private System.Windows.Forms.Button btnDisconnectSelected; // Declaration
        private System.Windows.Forms.Button btnSelectColumns;
    }
}
