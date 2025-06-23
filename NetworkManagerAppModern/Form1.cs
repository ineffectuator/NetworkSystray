using System;
using System.Drawing; // For SystemIcons
using System.Windows.Forms;
using System.Net.NetworkInformation; // Required for network interface access
using System.Diagnostics; // Required for Process
using System.Collections.Generic; // For List<T>
using System.Linq; // For LINQ operations

using System.Text.RegularExpressions; // For parsing netsh output
using System.Management; // For WMI event watcher

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
        private bool _isExiting = false; // Flag to allow proper exit

        // Timer for delayed refresh after network change event
        private System.Windows.Forms.Timer _initialRefreshDelayTimer;

        // Polling mechanism members
        private System.Windows.Forms.Timer _pollingTimer;
        private Dictionary<string, DateTime> _interfacesToPoll; // Key: Interface Name, Value: Poll Start Time
        private List<SimpleNetInterfaceInfo> _lastKnownInterfaces; // Stores the last complete list
        private const int POLLING_INTERVAL_MS = 500;
        private const int POLLING_TIMEOUT_SECONDS = 5;

        private ManagementEventWatcher? _wmiWatcher;

        public Form1()
        {
            InitializeComponent();

            // Initialize polling-related collections BEFORE first call to PopulateNetworkInterfaces
            _interfacesToPoll = new Dictionary<string, DateTime>();
            _lastKnownInterfaces = new List<SimpleNetInterfaceInfo>();

            InitializeColumnData();

            // Wire up FormClosing event
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.Form1_FormClosing);

            // Icon Loading
            try
            {
                // Attempt to load the custom icon from project resources.
                // Make sure you've added an icon named 'appicon' to your project's Resources.resx
                // (Project > Properties > Resources > Add Resource > Add Existing File...)
                this.notifyIcon1.Icon = Properties.Resources.appicon;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Custom icon 'appicon' failed to load: {ex.Message}. Using fallback system icon.");
                // Fallback to a standard system icon if the custom one isn't found or fails to load
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

            // Register for network change notifications
            NetworkChange.NetworkAddressChanged += new NetworkAddressChangedEventHandler(AddressChangedCallback);

            // Initialize the initial delay timer
            _initialRefreshDelayTimer = new System.Windows.Forms.Timer();
            _initialRefreshDelayTimer.Interval = 250; // Shorter initial delay, polling will follow
            _initialRefreshDelayTimer.Tick += InitialRefreshDelayTimer_Tick;

            // Initialize polling mechanism members
            _interfacesToPoll = new Dictionary<string, DateTime>();
            _lastKnownInterfaces = new List<SimpleNetInterfaceInfo>();
            _pollingTimer = new System.Windows.Forms.Timer();
            _pollingTimer.Interval = POLLING_INTERVAL_MS;
            _pollingTimer.Tick += PollingTimer_Tick;

            InitializeWmiWatcher();
        }

        private void InitializeWmiWatcher()
        {
            try
            {
                WqlEventQuery query = new WqlEventQuery(
                    "SELECT * FROM __InstanceModificationEvent WITHIN 2 " +
                    "WHERE TargetInstance ISA 'Win32_NetworkAdapter' AND " +
                    "TargetInstance.NetConnectionStatus <> PreviousInstance.NetConnectionStatus");

                _wmiWatcher = new ManagementEventWatcher(query);
                _wmiWatcher.EventArrived += new EventArrivedEventHandler(WmiEventHandler);
                _wmiWatcher.Start();
                System.Diagnostics.Debug.WriteLine("WMI Watcher started.");
            }
            catch (ManagementException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize WMI watcher: {ex.Message}");
                // Optionally inform the user, or log this more formally
                // This can happen due to permissions issues or WMI service problems
                _wmiWatcher = null; // Ensure it's null if setup failed
            }
            catch (Exception ex) // Catch other potential exceptions
            {
                System.Diagnostics.Debug.WriteLine($"An unexpected error occurred while initializing WMI watcher: {ex.Message}");
                _wmiWatcher = null;
            }
        }

        private void WmiEventHandler(object sender, EventArrivedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("WMI Network Adapter event arrived.");
            // WMI events arrive on a separate thread. We need to marshal UI updates
            // or operations that affect UI controls (like starting timers that update UI)
            // to the main UI thread.

            // We can extract details from e.NewEvent["TargetInstance"] if needed,
            // for example, to get the name of the adapter that changed.
            // ManagementBaseObject targetInstance = e.NewEvent["TargetInstance"] as ManagementBaseObject;
            // string adapterName = targetInstance?["Name"]?.ToString();
            // System.Diagnostics.Debug.WriteLine($"WMI Event for adapter: {adapterName}");

            // For now, any relevant WMI event will trigger our existing refresh logic,
            // which includes the initial delay timer. This timer helps debounce and
            // ensures that we don't try to access netsh too rapidly if WMI events
            // come in quick succession for a single underlying system change.

            if (this.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(() => {
                    // Perform checks on UI thread
                    if (!this.IsDisposed && this.Handle != IntPtr.Zero && _initialRefreshDelayTimer != null && !_initialRefreshDelayTimer.Enabled)
                    {
                        System.Diagnostics.Debug.WriteLine("WMI Event: Starting initial refresh delay timer.");
                        _initialRefreshDelayTimer.Start();
                    }
                }));
            }
            else
            {
                // Already on UI thread (should not happen for WMI events, but good practice)
                if (!this.IsDisposed && this.Handle != IntPtr.Zero && _initialRefreshDelayTimer != null && !_initialRefreshDelayTimer.Enabled)
                {
                    System.Diagnostics.Debug.WriteLine("WMI Event (already on UI thread): Starting initial refresh delay timer.");
                    _initialRefreshDelayTimer.Start();
                }
            }
        }

        private void InitialRefreshDelayTimer_Tick(object? sender, EventArgs e)
        {
            _initialRefreshDelayTimer.Stop(); // Ensure it only runs once per trigger
            // Instead of directly populating, decide if polling is needed or just refresh.
            // For now, let's assume we always try to populate and then check for polling.
            // This will be refined. The main goal is to trigger PopulateNetworkInterfaces
            // which will then determine if polling is necessary based on state changes.
            PopulateNetworkInterfaces(isManualRefresh: false);
        }

        private void PollingTimer_Tick(object? sender, EventArgs e)
        {
            CheckPolledInterfaces();
        }

        private SimpleNetInterfaceInfo? FetchSingleInterfaceInfo(string interfaceName)
        {
            // Quotes around interfaceName are important if it contains spaces
            ProcessStartInfo psi = new ProcessStartInfo("netsh", $"interface show interface name=\"{interfaceName}\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.Default
            };

            try
            {
                using (Process? process = Process.Start(psi))
                {
                    if (process == null) return null;

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();

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
                        if (!dataStarted || line.Trim().StartsWith("Admin State")) continue;

                        var match = Regex.Match(line, @"^(?<admin>\S+)\s+(?<state>\S+)\s+(?<type>\S+)\s+(?<name_col>.+)$");
                        if (match.Success)
                        {
                            // Ensure the name matches, as "name=" filter might be partial or netsh might return related items.
                            // However, for "show interface name='X'", it should be quite specific.
                            // For safety, we could check match.Groups["name_col"].Value.Trim() == interfaceName if issues arise.
                            return new SimpleNetInterfaceInfo
                            {
                                AdminState = match.Groups["admin"].Value.Trim(),
                                OperationalState = match.Groups["state"].Value.Trim(),
                                Name = match.Groups["name_col"].Value.Trim() // Use the name reported by netsh
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error fetching single interface info for '{interfaceName}' via netsh: {ex.Message}", "Netsh Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return null; // Not found or error
        }

        private void CheckPolledInterfaces()
        {
            if (!_interfacesToPoll.Any())
            {
                _pollingTimer.Stop();
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Checking polled interfaces: {string.Join(", ", _interfacesToPoll.Keys)}");

            List<string> completedPollingInterfaces = new List<string>();
            bool anInterfaceWasPolledThisTick = false;

            foreach (var entry in _interfacesToPoll.ToList()) // ToList to allow modification during iteration
            {
                anInterfaceWasPolledThisTick = true;
                string interfaceName = entry.Key;
                DateTime pollStartTime = entry.Value;

                SimpleNetInterfaceInfo? currentInfo = FetchSingleInterfaceInfo(interfaceName);

                bool stopPollingThisInterface = false;

                if (currentInfo != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Polling {interfaceName}: Current state {currentInfo.AdminState}/{currentInfo.OperationalState}");
                    if (currentInfo.AdminState.Equals("Enabled", StringComparison.OrdinalIgnoreCase) &&
                        (currentInfo.OperationalState.Equals("Connected", StringComparison.OrdinalIgnoreCase) ||
                         currentInfo.OperationalState.Equals("Disconnected", StringComparison.OrdinalIgnoreCase) ||
                         currentInfo.OperationalState.Equals("Non-operational", StringComparison.OrdinalIgnoreCase) || // Add other stable states
                         currentInfo.OperationalState.Equals("Operational", StringComparison.OrdinalIgnoreCase) // Generic for some VPNs
                         ))
                    {
                        System.Diagnostics.Debug.WriteLine($"Interface {interfaceName} confirmed as Enabled and in a stable operational state.");
                        stopPollingThisInterface = true;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Interface {interfaceName} no longer found by netsh during poll.");
                    // Interface disappeared, might have been a transient one or renamed. Stop polling for it.
                    stopPollingThisInterface = true;
                }

                if (!stopPollingThisInterface && (DateTime.UtcNow - pollStartTime).TotalSeconds > POLLING_TIMEOUT_SECONDS)
                {
                    System.Diagnostics.Debug.WriteLine($"Polling for {interfaceName} timed out.");
                    stopPollingThisInterface = true;
                }

                if (stopPollingThisInterface)
                {
                    completedPollingInterfaces.Add(interfaceName);
                    System.Diagnostics.Debug.WriteLine($"Marking {interfaceName} to be removed from polling queue.");
                }
            }

            bool refreshDueToCompletionOrTimeout = false;
            if (completedPollingInterfaces.Any())
            {
                foreach (string interfaceName in completedPollingInterfaces)
                {
                    _interfacesToPoll.Remove(interfaceName);
                    System.Diagnostics.Debug.WriteLine($"Removed {interfaceName} from polling queue.");
                }
                refreshDueToCompletionOrTimeout = true;
            }

            if (!_interfacesToPoll.Any() && anInterfaceWasPolledThisTick) // Check anInterfaceWasPolledThisTick to ensure this isn't a tick where the list was already empty
            {
                _pollingTimer.Stop();
                System.Diagnostics.Debug.WriteLine("All polling finished, stopping polling timer.");
                // If all polling is done (and we actually polled something this tick or just finished the last one),
                // a final refresh is needed.
                refreshDueToCompletionOrTimeout = true;
            }

            if(refreshDueToCompletionOrTimeout)
            {
                System.Diagnostics.Debug.WriteLine("Polling action completed for one or more interfaces, or all polling finished. Calling PopulateNetworkInterfaces.");
                PopulateNetworkInterfaces(isManualRefresh: false);
            }
            else if (anInterfaceWasPolledThisTick)
            {
                System.Diagnostics.Debug.WriteLine("Polling continues for some interfaces, no immediate full refresh from this tick.");
            }
            // If no interfaces were polled this tick (e.g. timer ticked but list was empty and it wasn't stopped last time), do nothing.
        }

        private void AddressChangedCallback(object? sender, EventArgs e)
        {
            // This event can be raised on a different thread.
            // Instead of direct population, start the timer to introduce a delay.
            // This helps ensure that netsh has time to report the updated state.

            // We need to marshal the check and the timer start to the UI thread.
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(() => {
                    // Perform checks on UI thread
                    if (!this.IsDisposed && this.Handle != IntPtr.Zero && _initialRefreshDelayTimer != null && !_initialRefreshDelayTimer.Enabled)
                    {
                        System.Diagnostics.Debug.WriteLine("NetworkAddressChanged Event: Starting initial refresh delay timer.");
                        _initialRefreshDelayTimer.Start();
                    }
                }));
            }
            else
            {
                // Already on UI thread, perform checks directly
                if (!this.IsDisposed && this.Handle != IntPtr.Zero && _initialRefreshDelayTimer != null && !_initialRefreshDelayTimer.Enabled)
                {
                    System.Diagnostics.Debug.WriteLine("NetworkAddressChanged Event (already on UI thread): Starting initial refresh delay timer.");
                    _initialRefreshDelayTimer.Start();
                }
            }
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
            // _visibleColumnKeys = new List<string> { "Name", "Description", "AdminState", "OperationalState" };
            LoadColumnSettings();
        }

        private void LoadColumnSettings()
        {
            if (Properties.Settings.Default.VisibleColumnKeys != null && Properties.Settings.Default.VisibleColumnKeys.Count > 0)
            {
                _visibleColumnKeys = new List<string>();
                foreach (string key in Properties.Settings.Default.VisibleColumnKeys)
                {
                    _visibleColumnKeys.Add(key);
                }
            }
            else
            {
                // Default visible columns if no settings are saved
                _visibleColumnKeys = new List<string> { "Name", "Description", "AdminState", "OperationalState" };
            }
        }

        private void SaveColumnSettings()
        {
            if (_visibleColumnKeys != null)
            {
                if (Properties.Settings.Default.VisibleColumnKeys == null)
                {
                    Properties.Settings.Default.VisibleColumnKeys = new System.Collections.Specialized.StringCollection();
                }
                else
                {
                    Properties.Settings.Default.VisibleColumnKeys.Clear();
                }
                Properties.Settings.Default.VisibleColumnKeys.AddRange(_visibleColumnKeys.ToArray());
                Properties.Settings.Default.Save();
            }
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


        private void PopulateNetworkInterfaces(bool isManualRefresh = false)
        {
            SetupListViewColumns();
            listViewNetworkInterfaces.Items.Clear();

            List<SimpleNetInterfaceInfo> netshInterfaces = FetchInterfacesViaNetsh();
            System.Diagnostics.Debug.WriteLine($"PopulateNetworkInterfaces (isManualRefresh: {isManualRefresh}): Found {netshInterfaces.Count} interfaces via netsh.");

            // Check for interfaces that might need polling
            // This logic is simplified: if polling is already active, let it finish.
            // A more sophisticated approach might add new transitions to an active polling session.
            if (_interfacesToPoll.Any())
            {
                // If polling is active, this call to PopulateNetworkInterfaces might be from the polling completing.
                // Or it could be from the initial delay timer, in which case we don't want to overwrite UI yet.
                // For now, if polling is active, we assume this refresh is the one that should update the UI.
                // The main check will be to see if we need to START polling.
            }

            bool shouldStartPolling = false;
            foreach (var currentInfo in netshInterfaces)
            {
                var previousInfo = _lastKnownInterfaces.FirstOrDefault(i => i.Name == currentInfo.Name);
                if (previousInfo != null)
                {
                    // If previously AdminState was "Disabled" and now it's "Enabled" but "Disconnected"
                    // (or any state that isn't fully "Connected" operationally)
                    if (previousInfo.AdminState.Equals("Disabled", StringComparison.OrdinalIgnoreCase) &&
                        currentInfo.AdminState.Equals("Enabled", StringComparison.OrdinalIgnoreCase) &&
                        !currentInfo.OperationalState.Equals("Connected", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!_interfacesToPoll.ContainsKey(currentInfo.Name))
                        {
                            _interfacesToPoll[currentInfo.Name] = DateTime.UtcNow;
                            shouldStartPolling = true;
                            System.Diagnostics.Debug.WriteLine($"Interface {currentInfo.Name} marked for polling. From {previousInfo.AdminState}/{previousInfo.OperationalState} to {currentInfo.AdminState}/{currentInfo.OperationalState}");
                        }
                    }
                }
                else
                {
                    // New interface, if it's Enabled but not Connected, could also be a candidate for polling
                    // if it just appeared after being enabled.
                    if (currentInfo.AdminState.Equals("Enabled", StringComparison.OrdinalIgnoreCase) &&
                        !currentInfo.OperationalState.Equals("Connected", StringComparison.OrdinalIgnoreCase) &&
                        !currentInfo.OperationalState.Equals("Unknown", StringComparison.OrdinalIgnoreCase)) // Assuming "Unknown" is stable
                    {
                        // This might be too aggressive for newly discovered interfaces.
                        // Let's primarily focus on transitions from Disabled.
                        // Consider adding if it's a common scenario.
                    }
                }
            }

            // If we decided to start polling based on the comparison between current netshInterfaces
            // and _lastKnownInterfaces, then start the timer.
            if (shouldStartPolling && !_pollingTimer.Enabled)
            {
                _pollingTimer.Start();
            }

            // If polling is now active (either started now or was already running for other interfaces),
            // defer the main UI update. The UI will be updated when polling completes.
            // Also, do NOT update _lastKnownInterfaces yet, as the UI doesn't reflect netshInterfaces yet.
            if (!isManualRefresh && _interfacesToPoll.Any())
            {
                System.Diagnostics.Debug.WriteLine($"Polling active for: {string.Join(", ", _interfacesToPoll.Keys)}. Automatic refresh deferring full UI update.");
                return; // Defer UI update only if it's an automatic refresh
            }

            // If we've reached here, no polling is active OR it's a manual refresh.
            // Proceed with full UI update and update _lastKnownInterfaces.
            _lastKnownInterfaces = new List<SimpleNetInterfaceInfo>(netshInterfaces.Select(ni => new SimpleNetInterfaceInfo {
                Name = ni.Name, AdminState = ni.AdminState, OperationalState = ni.OperationalState,
                Description = ni.Description, Id = ni.Id, Type = ni.Type
            }));


            // UI Update part:
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
                    SaveColumnSettings(); // Save the new column selection
                    PopulateNetworkInterfaces(); // Refresh columns and data
                }
            }
        }

        private void BtnRefreshList_Click(object? sender, EventArgs e)
        {
            PopulateNetworkInterfaces(isManualRefresh: true);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Defer hiding until after the form is fully loaded and shown once (helps with first Show() call)
            this.BeginInvoke(new MethodInvoker(delegate {
                Hide();
                ShowInTaskbar = false;
            }));
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
            _isExiting = true; // Signal that we are intentionally exiting
            if (notifyIcon1 != null)
            {
                notifyIcon1.Visible = false;
                notifyIcon1.Dispose();
            }
            Application.Exit();
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !_isExiting)
            {
                e.Cancel = true; // Cancel the actual closing operation
                this.Hide();     // Hide the form
                this.ShowInTaskbar = false; // Optional: remove from taskbar
                // Optionally, show a balloon tip from the tray icon
                // notifyIcon1.ShowBalloonTip(1000, "Application Minimized", "Network Manager is running in the background.", ToolTipIcon.Info);
            }
            else if (_isExiting) // Or if it's a real exit
            {
                // Unregister network change notifications
                NetworkChange.NetworkAddressChanged -= new NetworkAddressChangedEventHandler(AddressChangedCallback);
                // Dispose timers
                if (_initialRefreshDelayTimer != null)
                {
                    _initialRefreshDelayTimer.Stop();
                    _initialRefreshDelayTimer.Dispose();
                }
                if (_pollingTimer != null)
                {
                    _pollingTimer.Stop();
                    _pollingTimer.Dispose();
                }
                if (_wmiWatcher != null)
                {
                    _wmiWatcher.Stop();
                    _wmiWatcher.Dispose();
                    System.Diagnostics.Debug.WriteLine("WMI Watcher stopped and disposed.");
                }
            }
            // If _isExiting is true, or if it's not UserClosing (e.g. Windows shutting down), allow the close
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
                // Ensure the window is in a normal state before showing
                if (this.WindowState == FormWindowState.Minimized)
                {
                    this.WindowState = FormWindowState.Normal;
                }
                this.Show(); // Make the form visible
                this.ShowInTaskbar = true; // Ensure it's in the taskbar
                this.Activate();         // Attempt to give it focus
                this.BringToFront();     // Ensure it's on top of other windows
            }
        }
    }
}
