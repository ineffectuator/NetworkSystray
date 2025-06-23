using System;
using System.Drawing; // For SystemIcons
using System.Windows.Forms;
using System.Net.NetworkInformation; // Required for network interface access
using System.Diagnostics; // Required for Process
using System.Collections.Generic; // For List<T>
using System.Linq; // For LINQ operations

namespace NetworkManagerAppModern
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
        private List<ColumnDefinition> _allColumnDefinitions; // Keep it nullable or initialize in constructor
        private List<string> _visibleColumnKeys;

        public Form1()
        {
            InitializeComponent();
            InitializeColumnData(); // Call before first PopulateNetworkInterfaces

            // Icon Loading
            try
            {
                // Assumes you have an icon resource named 'appicon' in your project's Properties.Resources
                // Example: this.notifyIcon1.Icon = Properties.Resources.appicon;
                 throw new NotImplementedException("Custom icon resource loading not yet fully implemented. Using fallback.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Custom icon loading failed: {ex.Message}. Using fallback system icon.");
                this.notifyIcon1.Icon = SystemIcons.Application;
            }
            this.notifyIcon1.Visible = true;

            // Wire up event handlers
            this.notifyIcon1.DoubleClick += new System.EventHandler(this.NotifyIcon1_DoubleClick);
            this.showHideMenuItem.Click += new System.EventHandler(this.ShowHideMenuItem_Click);
            this.exitMenuItem.Click += new System.EventHandler(this.ExitMenuItem_Click);

            this.btnRefreshList.Click += new System.EventHandler(this.BtnRefreshList_Click);
            this.btnEnableSelected.Click += new System.EventHandler(this.BtnEnableSelected_Click);
            this.btnDisableSelected.Click += new System.EventHandler(this.BtnDisableSelected_Click);
            this.btnSelectColumns.Click += new System.EventHandler(this.BtnSelectColumns_Click);


            // Populate the list on initial load
            PopulateNetworkInterfaces();
        }

        private void InitializeColumnData()
        {
            _allColumnDefinitions = new List<ColumnDefinition>
            {
                new ColumnDefinition("Name", "Name", 150, ni => ni.Name),
                new ColumnDefinition("Description", "Description", 250, ni => ni.Description),
                new ColumnDefinition("Status", "Status", 100, ni => ni.OperationalStatus.ToString()),
                new ColumnDefinition("ID", "ID", 200, ni => ni.Id),
                new ColumnDefinition("Type", "Type", 100, ni => ni.NetworkInterfaceType.ToString()),
                new ColumnDefinition("Speed", "Speed (Mbps)", 100, ni => (ni.Speed / 1000000).ToString()),
                new ColumnDefinition("MAC", "MAC Address", 120, ni => ni.GetPhysicalAddress()?.ToString() ?? "N/A"),
                new ColumnDefinition("IsReceiveOnly", "Receive Only", 80, ni => ni.IsReceiveOnly.ToString()),
                new ColumnDefinition("SupportsMulticast", "Multicast", 80, ni => ni.SupportsMulticast.ToString())
            };
            // Default visible columns
            _visibleColumnKeys = new List<string> { "Name", "Description", "Status" };
        }

        private void SetupListViewColumns()
        {
            listViewNetworkInterfaces.Columns.Clear();
            if (_visibleColumnKeys == null || _allColumnDefinitions == null) return;

            foreach (string key in _visibleColumnKeys)
            {
                // Find the column definition by key
                ColumnDefinition colDef = _allColumnDefinitions.FirstOrDefault(cd => cd.Key == key);
                // FirstOrDefault returns default(ColumnDefinition) if not found, check Key against null/empty
                if (!string.IsNullOrEmpty(colDef.Key))
                {
                    listViewNetworkInterfaces.Columns.Add(colDef.HeaderText, colDef.DefaultWidth);
                }
            }
        }

        private void PopulateNetworkInterfaces()
        {
            SetupListViewColumns(); // Ensure columns are set up based on _visibleColumnKeys
            listViewNetworkInterfaces.Items.Clear();

            if (_visibleColumnKeys == null || !_visibleColumnKeys.Any() || _allColumnDefinitions == null)
            {
                // No columns to display or definitions are missing
                return;
            }

            try
            {
                NetworkInterface[] adapters = NetworkInterface.GetAllNetworkInterfaces();
                foreach (NetworkInterface adapter in adapters)
                {
                    ListViewItem? item = null;
                    // First visible column's value for the main item text
                    ColumnDefinition firstColDef = _allColumnDefinitions.First(cd => cd.Key == _visibleColumnKeys.First());
                    item = new ListViewItem(firstColDef.ValueGetter(adapter));

                    // Add sub-items for the rest of the visible columns
                    for (int i = 1; i < _visibleColumnKeys.Count; i++)
                    {
                        ColumnDefinition colDef = _allColumnDefinitions.First(cd => cd.Key == _visibleColumnKeys[i]);
                        item.SubItems.Add(colDef.ValueGetter(adapter));
                    }

                    item.Tag = adapter; // Store the full adapter object for later use
                    listViewNetworkInterfaces.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fetching network interfaces: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSelectColumns_Click(object? sender, EventArgs e)
        {
            if (_allColumnDefinitions == null || _visibleColumnKeys == null) return;

            List<string> allColumnKeys = _allColumnDefinitions.Select(cd => cd.Key).ToList();
            List<string> currentlyVisibleKeys = new List<string>(_visibleColumnKeys); // Pass a copy

            using (ColumnSelectionForm colForm = new ColumnSelectionForm(allColumnKeys, currentlyVisibleKeys))
            {
                if (colForm.ShowDialog(this) == DialogResult.OK) // Pass 'this' to center on parent
                {
                    _visibleColumnKeys = new List<string>(colForm.SelectedColumnKeys);
                    PopulateNetworkInterfaces(); // Refresh columns and data
                }
            }
        }

        private void BtnRefreshList_Click(object? sender, EventArgs e)
        {
            PopulateNetworkInterfaces();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Hide the main window on startup
            this.Visible = false;
            this.ShowInTaskbar = false; // Prevents it from flashing in the taskbar
        }

        private void BtnEnableSelected_Click(object? sender, EventArgs e)
        {
            UpdateSelectedInterfaces(true);
        }

        private void BtnDisableSelected_Click(object? sender, EventArgs e)
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

            foreach (ListViewItem selectedItem in listViewNetworkInterfaces.SelectedItems)
            {
                if (selectedItem.Tag is NetworkInterface adapter)
                {
                    // Try using adapter.Description instead of adapter.Name
                    System.Diagnostics.Debug.WriteLine($"Attempting to { (enable ? "enable" : "disable") } interface using description: '{adapter.Description}' (Name was: '{adapter.Name}')");
                    ExecuteNetshCommand(adapter.Description, enable);
                }
            }
            // Refresh the list once after all operations are attempted
            PopulateNetworkInterfaces();
        }

        private void ExecuteNetshCommand(string interfaceName, bool enable)
        {
            string arguments = $"interface set interface name=\"{interfaceName}\" admin={(enable ? "enable" : "disable")}";
            ProcessStartInfo psi = new ProcessStartInfo("netsh", arguments)
            {
                Verb = "runas", // Request administrator privileges
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = true, // Must be true for Verb to work with "runas"
                // For capturing output, UseShellExecute must be false.
                // However, 'runas' verb requires UseShellExecute = true.
                // This means we can't directly capture output when using 'runas' this way.
                // The UAC prompt itself is handled by ShellExecute.
                // If 'runas' fails silently or netsh errors out AFTER elevation, this direct capture won't work.
                // We will primarily rely on ExitCode if the process runs.
                // RedirectStandardOutput = true, // Cannot use with UseShellExecute = true
                // RedirectStandardError = true   // Cannot use with UseShellExecute = true
            };

            try
            {
                using (Process? process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        MessageBox.Show($"Failed to start netsh process for interface '{interfaceName}'.", "Process Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    process.WaitForExit(); // Wait for the UAC-elevated process to complete

                    if (process.ExitCode != 0)
                    {
                        // If UAC was denied, a Win32Exception "Operation canceled by user" (1223) is usually thrown and caught below.
                        // Other non-zero exit codes from netsh indicate failure after successful elevation.
                        string errorMsg = $"Netsh command failed for interface '{interfaceName}' with exit code: {process.ExitCode}.";
                        // Since we can't capture stdout/stderr directly with UseShellExecute=true,
                        // we can't add more details from netsh here.
                        MessageBox.Show(errorMsg, "Netsh Command Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    // else: ExitCode 0 usually means success.
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // Common Win32Exceptions:
                // 1223: The operation was canceled by the user (UAC denial).
                // 2: The system cannot find the file specified (netsh not found - very unlikely).
                // 5: Access is denied (if 'runas' failed for some reason beyond UAC, or UAC was bypassed and permissions still insufficient).
                string detailedError = $"Operation for interface '{interfaceName}' failed.\nWin32 Error Code: {ex.NativeErrorCode}\nMessage: {ex.Message}";
                if (ex.NativeErrorCode == 1223)
                {
                    detailedError = $"Operation for interface '{interfaceName}' was canceled by the user (UAC prompt denied).";
                }
                MessageBox.Show(detailedError, "Operation Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error occurred while trying to modify interface '{interfaceName}': {ex.Message}", "Unexpected Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void NotifyIcon1_DoubleClick(object? sender, EventArgs e)
        {
            ToggleFormVisibility();
        }

        private void ShowHideMenuItem_Click(object? sender, EventArgs e)
        {
            ToggleFormVisibility();
        }

        private void ExitMenuItem_Click(object? sender, EventArgs e)
        {
            if (notifyIcon1 != null)
            {
                notifyIcon1.Visible = false; // Hide icon before disposing
                notifyIcon1.Dispose(); // Release resources used by the icon
            }
            Application.Exit();
        }

        private void ToggleFormVisibility()
        {
            if (this.Visible)
            {
                this.Hide();
                // this.ShowInTaskbar = false; // Optional: remove from taskbar when hidden
            }
            else
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.ShowInTaskbar = true;
                this.Activate();
            }
        }
    }
}
