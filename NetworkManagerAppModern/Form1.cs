using System;
using System.Drawing; // For SystemIcons
using System.Windows.Forms;
using System.Net.NetworkInformation; // Required for network interface access
using System.Diagnostics; // Required for Process
using System.Collections.Generic; // For List<T>
using System.Linq; // For LINQ operations

using System.Text.RegularExpressions; // For parsing netsh output

namespace NetworkManagerAppModern
{
    // Helper class for storing info parsed from netsh
    public class SimpleNetInterfaceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string AdminState { get; set; } = string.Empty; // e.g., "Enabled", "Disabled"
        public string OperationalState { get; set; } = string.Empty; // e.g., "Connected", "Disconnected"
        public string? Description { get; set; } // Optional: from NetworkInterface object
        public string? Type { get; set; } // Optional
        public string? Id { get; set; } // Optional

        // We will primarily rely on Name for enabling/disabling via netsh
    }

    // Helper structure for defining columns
    public struct ColumnDefinition
    {
        public string Key { get; }
        public string HeaderText { get; }
        public int DefaultWidth { get; }
        public Func<SimpleNetInterfaceInfo, string> ValueGetter { get; } // Changed type here

        public ColumnDefinition(string key, string headerText, int defaultWidth, Func<SimpleNetInterfaceInfo, string> valueGetter)
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
        private List<string> _visibleColumnKeys;

        public Form1()
        {
            InitializeComponent();
            InitializeColumnData();

            // Icon Loading
            try
            {
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

            PopulateNetworkInterfaces();
        }

        private void InitializeColumnData()
        {
            // Columns will now be based on SimpleNetInterfaceInfo primarily
            _allColumnDefinitions = new List<ColumnDefinition>
            {
                // We use a Func<SimpleNetInterfaceInfo, string> for ValueGetter
                new ColumnDefinition("Name", "Name", 150, info => info.Name),
                new ColumnDefinition("AdminState", "Admin State", 100, info => info.AdminState),
                new ColumnDefinition("OperationalState", "Op. Status", 120, info => info.OperationalState),
                new ColumnDefinition("Description", "Description", 250, info => info.Description ?? ""),
                // Optional: Add more if we decide to merge with NetworkInterface objects later
                new ColumnDefinition("Type", "Type", 100, info => info.Type ?? ""),
                new ColumnDefinition("ID", "ID", 200, info => info.Id ?? ""),
            };
            // Default visible columns adjusted
            _visibleColumnKeys = new List<string> { "Name", "Description", "AdminState", "OperationalState" };
        }

        private void SetupListViewColumns()
        {
            listViewNetworkInterfaces.Columns.Clear();
            if (_visibleColumnKeys == null || _allColumnDefinitions == null) return;

            foreach (string key in _visibleColumnKeys)
            {
                ColumnDefinition colDef = _allColumnDefinitions.FirstOrDefault(cd => cd.Key == key);
                if (!string.IsNullOrEmpty(colDef.Key))
                {
                    // Temporarily change the ValueGetter type in ColumnDefinition for this to compile
                    // This part of ColumnDefinition needs to be generic or use object.
                    // For now, this will cause a compile error which I'll fix by adjusting ColumnDefinition.
                    listViewNetworkInterfaces.Columns.Add(colDef.HeaderText, colDef.DefaultWidth);
                }
            }
        }

        private List<SimpleNetInterfaceInfo> FetchInterfacesViaNetsh()
        {
            var interfaces = new List<SimpleNetInterfaceInfo>();
            ProcessStartInfo psi = new ProcessStartInfo("netsh", "interface show interface")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.Default // Or UTF8, check netsh output encoding
            };

            try
            {
                using (Process? process = Process.Start(psi))
                {
                    if (process == null) return interfaces;

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

                    // Basic parser for netsh output
                    // Example output:
                    // Admin State    State          Type             Interface Name
                    // -------------------------------------------------------------------------
                    // Enabled        Connected      Dedicated        Ethernet
                    // Disabled       Disconnected   Dedicated        Wi-Fi 2
                    // Enabled        Disconnected   Dedicated        Bluetooth Network Connection

                    var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    bool dataStarted = false;
                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        if (line.Trim().StartsWith("---"))
                        {
                            dataStarted = true;
                            continue;
                        }
                        if (!dataStarted || line.Trim().StartsWith("Admin State")) continue; // Skip header line itself

                        // This regex is a bit fragile and assumes consistent spacing / column order.
                        // Columns: Admin State, State, Type, Interface Name
                        // A more robust parser would look for fixed column start/end or use more specific regex.
                        var match = Regex.Match(line, @"^(?<admin>\S+)\s+(?<state>\S+)\s+(?<type>\S+)\s+(?<name>.+)$");
                        if (match.Success)
                        {
                            interfaces.Add(new SimpleNetInterfaceInfo
                            {
                                AdminState = match.Groups["admin"].Value.Trim(),
                                OperationalState = match.Groups["state"].Value.Trim(),
                                // Type = match.Groups["type"].Value.Trim(), // 'Type' from this command is less useful than NetworkInterfaceType
                                Name = match.Groups["name"].Value.Trim()
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fetching interfaces via netsh: {ex.Message}", "Netsh Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return interfaces;
        }


        private void PopulateNetworkInterfaces()
        {
            SetupListViewColumns();
            listViewNetworkInterfaces.Items.Clear();

            List<SimpleNetInterfaceInfo> netshInterfaces = FetchInterfacesViaNetsh();

            // Optional: Get richer data from NetworkInterface.GetAllNetworkInterfaces() and merge
            // For simplicity now, we'll primarily use what netsh gives for consistent listing
            Dictionary<string, NetworkInterface> systemInterfaces = new Dictionary<string, NetworkInterface>();
            try
            {
                 systemInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                                           .Where(ni => !string.IsNullOrEmpty(ni.Name))
                                           .GroupBy(ni => ni.Name) // Handle potential duplicate names, take first
                                           .ToDictionary(g => g.Key, g => g.First());
            }
            catch (Exception ex) {
                 System.Diagnostics.Debug.WriteLine($"Error getting system interfaces: {ex.Message}");
            }


            if (_visibleColumnKeys == null || !_visibleColumnKeys.Any() || _allColumnDefinitions == null) return;

            foreach (var netshInfo in netshInterfaces)
            {
                // Try to find matching system interface for richer details
                if (systemInterfaces.TryGetValue(netshInfo.Name, out NetworkInterface? systemAdapter))
                {
                    netshInfo.Description = systemAdapter.Description;
                    netshInfo.Type = systemAdapter.NetworkInterfaceType.ToString();
                    netshInfo.Id = systemAdapter.Id;
                }

                ListViewItem item;
                ColumnDefinition firstColDef = _allColumnDefinitions.First(cd => cd.Key == _visibleColumnKeys.First());
                item = new ListViewItem(firstColDef.ValueGetter(netshInfo));

                for (int i = 1; i < _visibleColumnKeys.Count; i++)
                {
                    ColumnDefinition colDef = _allColumnDefinitions.First(cd => cd.Key == _visibleColumnKeys[i]);
                    item.SubItems.Add(colDef.ValueGetter(netshInfo));
                }

                item.Tag = netshInfo.Name; // Store the NAME from netsh, as this is what 'set interface' uses
                listViewNetworkInterfaces.Items.Add(item);
            }
        }

        private void BtnSelectColumns_Click(object? sender, EventArgs e)
        {
            if (_allColumnDefinitions == null || _visibleColumnKeys == null) return;

            // The ValueGetter in ColumnDefinition now expects SimpleNetInterfaceInfo
            // So, the Select method for ColumnSelectionForm needs to reflect this.
            // For now, we pass keys as before.
            List<string> allColumnKeys = _allColumnDefinitions.Select(cd => cd.Key).ToList();
            List<string> currentlyVisibleKeys = new List<string>(_visibleColumnKeys);

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
                // The Tag now stores the interface name (string) as recognized by netsh
                if (selectedItem.Tag is string interfaceName && !string.IsNullOrEmpty(interfaceName))
                {
                    System.Diagnostics.Debug.WriteLine($"Attempting to {(enable ? "enable" : "disable")} interface using Name from Tag: '{interfaceName}'");
                    ExecuteNetshCommandByName(interfaceName, enable);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Skipping item with invalid Tag: {selectedItem.Tag}");
                    // Optionally, inform the user if an item was unexpectedly untaggable or had wrong tag type
                }
            }
            // Refresh the list once after all operations are attempted
            PopulateNetworkInterfaces();
        }

        // Reverted to use interface name, with enhanced logging
        private void ExecuteNetshCommandByName(string interfaceName, bool enable)
        {
            System.Diagnostics.Debug.WriteLine($"ExecuteNetshCommandByName: Raw interfaceName received = '{interfaceName}'");

            // For safety, if a name itself contains a quote, this basic approach might need more advanced escaping.
            // However, standard interface names obtained from NetworkInterface.Name usually don't.
            string processedName = interfaceName;

            string arguments = $"interface set interface name=\"{processedName}\" admin={(enable ? "enable" : "disable")}";
            System.Diagnostics.Debug.WriteLine($"ExecuteNetshCommandByName: Executing 'netsh.exe' with arguments: \"{arguments}\"");

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
                        MessageBox.Show($"Failed to start netsh process for interface '{processedName}'.", "Process Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        string errorMsg = $"Netsh command failed for interface '{processedName}' with exit code: {process.ExitCode}.";
                        MessageBox.Show(errorMsg, "Netsh Command Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                string detailedError = $"Operation for interface '{processedName}' failed.\nWin32 Error Code: {ex.NativeErrorCode}\nMessage: {ex.Message}";
                if (ex.NativeErrorCode == 1223) // UAC Denied
                {
                    detailedError = $"Operation for interface '{processedName}' was canceled by the user (UAC prompt denied).";
                }
                MessageBox.Show(detailedError, "Operation Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error occurred while trying to modify interface '{processedName}': {ex.Message}", "Unexpected Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
