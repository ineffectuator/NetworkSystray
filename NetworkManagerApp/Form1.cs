using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Net.NetworkInformation; // Required for network interface access
using System.Diagnostics; // Required for Process
using System.Linq; // For LINQ operations like OfType

namespace NetworkManagerApp
{
    // Helper structure for defining columns
    public struct ColumnDefinition
    {
        public string Key { get; } // Unique key, can be same as HeaderText or different
        public string HeaderText { get; }
        public int DefaultWidth { get; }
        public Func<NetworkInterface, string> ValueGetter { get; }

        public ColumnDefinition(string key, string headerText, int defaultWidth, Func<NetworkInterface, string> valueGetter)
        {
            Key = key;
            HeaderText = headerText;
            DefaultWidth = defaultWidth;
            ValueGetter = valueGetter;
        }
    }

    public partial class Form1 : Form
    {
        private List<ColumnDefinition> _allColumnDefinitions;
        private List<string> _visibleColumnKeys; // Stores keys of visible columns

        public Form1()
        {
            InitializeComponent();
            InitializeColumnDefinitions();

            // Default visible columns
            _visibleColumnKeys = new List<string> { "Name", "Description", "Status" };

            // Wire up event handlers for NotifyIcon
            this.notifyIcon1.DoubleClick += new System.EventHandler(this.NotifyIcon1_DoubleClick);
            this.showHideMenuItem.Click += new System.EventHandler(this.ShowHideMenuItem_Click);
            this.exitMenuItem.Click += new System.EventHandler(this.ExitMenuItem_Click);

            // Wire up event handlers for main buttons
            this.btnRefreshList.Click += new System.EventHandler(this.BtnRefreshList_Click);
            this.btnEnableSelected.Click += new System.EventHandler(this.BtnEnableSelected_Click);
            this.btnDisableSelected.Click += new System.EventHandler(this.BtnDisableSelected_Click);
            this.btnSelectColumns.Click += new System.EventHandler(this.BtnSelectColumns_Click);

            // Set the NotifyIcon's icon
            // PLEASE REPLACE 'appicon.ico' with your actual icon file added to resources.
            // Example: If you add 'appicon.ico' to a 'Resources' folder and set its build action to 'Embedded Resource',
            // and your project's default namespace is NetworkManagerApp, it might be accessible via:
            // this.notifyIcon1.Icon = new System.Drawing.Icon(GetType(), "Resources.appicon.ico");
            // Or, if added to project resources directly (Project > Properties > Resources > Add Existing File):
            // this.notifyIcon1.Icon = NetworkManagerApp.Properties.Resources.appicon;
            // For now, this is a placeholder. The application will run but the tray icon might be invisible or default.
            try
            {
                // This is a common way if you add an icon to your project's Resources.resx
                // 1. Go to Project > Properties > Resources.
                // 2. If no Resources.resx, click "This project does not contain a default resources file. Click here to create one."
                // 3. Select "Icons" from the dropdown, then "Add Resource" > "Add Existing File..." and pick your .ico file.
                //    Let's assume you named it 'appicon' in the resources.
                // this.notifyIcon1.Icon = Properties.Resources.appicon;
                // If you don't have an icon yet, the program will work but the tray icon may not be ideal.
                // For testing without an icon, you can temporarily comment out the line above.
                // Visual Studio usually generates a default icon for the .exe itself, which notifyIcon might pick up if no icon is set.
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Icon loading failed: " + ex.Message);
                // Optionally, inform the user or log this if an icon is critical.
            }

            // Populate the list on startup (this will now also set up columns)
            PopulateNetworkInterfaces();
        }

        private void InitializeColumnDefinitions()
        {
            _allColumnDefinitions = new List<ColumnDefinition>
            {
                new ColumnDefinition("Name", "Name", 150, ni => ni.Name),
                new ColumnDefinition("Description", "Description", 250, ni => ni.Description),
                new ColumnDefinition("Status", "Status", 100, ni => ni.OperationalStatus.ToString()),
                new ColumnDefinition("ID", "ID", 200, ni => ni.Id),
                new ColumnDefinition("Type", "Type", 100, ni => ni.NetworkInterfaceType.ToString()),
                new ColumnDefinition("Speed", "Speed (Mbps)", 100, ni => (ni.Speed / 1000000).ToString()),
                new ColumnDefinition("MAC", "MAC Address", 120, ni => ni.GetPhysicalAddress().ToString()),
                new ColumnDefinition("IsReceiveOnly", "Receive Only", 80, ni => ni.IsReceiveOnly.ToString()),
                new ColumnDefinition("SupportsMulticast", "Multicast", 80, ni => ni.SupportsMulticast.ToString())
            };
        }

        private void SetupListViewColumns()
        {
            listViewNetworkInterfaces.Columns.Clear();
            foreach (string key in _visibleColumnKeys)
            {
                ColumnDefinition colDef = _allColumnDefinitions.First(cd => cd.Key == key);
                listViewNetworkInterfaces.Columns.Add(colDef.HeaderText, colDef.DefaultWidth);
            }
        }

        private void PopulateNetworkInterfaces()
        {
            // Ensure columns are set up based on _visibleColumnKeys
            SetupListViewColumns();

            listViewNetworkInterfaces.Items.Clear();

            try
            {
                NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
                foreach (NetworkInterface adapter in adapters)
                {
                    // Create ListViewItem with the first visible column's value
                    ListViewItem item = null;
                    if (_visibleColumnKeys.Any())
                    {
                        ColumnDefinition firstColDef = _allColumnDefinitions.First(cd => cd.Key == _visibleColumnKeys.First());
                        item = new ListViewItem(firstColDef.ValueGetter(adapter));
                    }
                    else // Should not happen if we always have default columns, but as a fallback
                    {
                        item = new ListViewItem("N/A");
                    }

                    // Add sub-items for the rest of the visible columns
                    for (int i = 1; i < _visibleColumnKeys.Count; i++)
                    {
                        ColumnDefinition colDef = _allColumnDefinitions.First(cd => cd.Key == _visibleColumnKeys[i]);
                        item.SubItems.Add(colDef.ValueGetter(adapter));
                    }

                    item.Tag = adapter;
                    listViewNetworkInterfaces.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fetching network interfaces: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSelectColumns_Click(object sender, EventArgs e)
        {
            List<string> allColumnDisplayNames = _allColumnDefinitions.Select(cd => cd.Key).ToList();
            // Pass the *keys* of currently visible columns
            List<string> currentlyVisibleKeys = new List<string>(_visibleColumnKeys);

            using (ColumnSelectionForm colForm = new ColumnSelectionForm(allColumnDisplayNames, currentlyVisibleKeys))
            {
                if (colForm.ShowDialog() == DialogResult.OK)
                {
                    _visibleColumnKeys = new List<string>(colForm.SelectedColumns);
                    // Refresh columns and data
                    PopulateNetworkInterfaces();
                }
            }
        }

        private void BtnRefreshList_Click(object sender, EventArgs e)
        {
            PopulateNetworkInterfaces();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Hide the main window on startup, ShowInTaskbar = false prevents it from appearing then disappearing
            this.Visible = false;
            this.ShowInTaskbar = false;
        }

        private void BtnEnableSelected_Click(object sender, EventArgs e)
        {
            UpdateSelectedInterfaces(true);
        }

        private void BtnDisableSelected_Click(object sender, EventArgs e)
        {
            UpdateSelectedInterfaces(false);
        }

        private void UpdateSelectedInterfaces(bool enable)
        {
            if (listViewNetworkInterfaces.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select one or more network interfaces.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string action = enable ? "enable" : "disable";
            foreach (ListViewItem selectedItem in listViewNetworkInterfaces.SelectedItems)
            {
                if (selectedItem.Tag is NetworkInterface adapter)
                {
                    // Call SetInterfaceState but don't let it refresh the list individually.
                    // The refresh will happen once after all selected items are processed.
                    ExecuteNetshCommand(adapter.Name, enable);
                }
            }
            // Refresh the list once after all operations are attempted
            PopulateNetworkInterfaces();
        }

        // Renamed from SetInterfaceState to avoid confusion, as PopulateNetworkInterfaces is called by the caller
        private void ExecuteNetshCommand(string interfaceName, bool enable)
        {
            string arguments = $"interface set interface name=\"{interfaceName}\" admin={(enable ? "enable" : "disable")}";
            ProcessStartInfo psi = new ProcessStartInfo("netsh", arguments)
            {
                Verb = "runas", // Request administrator privileges
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = true // Must be true for Verb to work
            };

            try
            {
                Process process = Process.Start(psi);
                process.WaitForExit(); // Wait for the command to complete
                // Optionally, check process.ExitCode here if needed, though UAC cancellation also results in non-zero
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // This exception can occur if the user cancels the UAC prompt
                MessageBox.Show($"Operation to {(enable ? "enable" : "disable")} interface '{interfaceName}' was cancelled or failed.\nError: {ex.Message}", "Operation Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error {(enable ? "enabling" : "disabling")} interface '{interfaceName}': {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void NotifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            ToggleFormVisibility();
        }

        private void ShowHideMenuItem_Click(object sender, EventArgs e)
        {
            ToggleFormVisibility();
        }

        private void ExitMenuItem_Click(object sender, EventArgs e)
        {
            // Ensure the icon is disposed before exiting, otherwise it might linger.
            if (notifyIcon1 != null)
            {
                notifyIcon1.Visible = false;
                notifyIcon1.Dispose();
            }
            Application.Exit();
        }

        private void ToggleFormVisibility()
        {
            if (this.Visible)
            {
                this.Hide();
                // Optionally, keep ShowInTaskbar false if you always want it hidden from taskbar when not active
                // this.ShowInTaskbar = false;
            }
            else
            {
                this.Show();
                this.ShowInTaskbar = true; // Make it appear in taskbar when shown
                this.WindowState = FormWindowState.Normal;
                this.Activate(); // Bring to front
            }
        }
    }
}
