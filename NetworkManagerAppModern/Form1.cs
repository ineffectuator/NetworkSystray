using System;
using System.Drawing; // For SystemIcons
using System.Windows.Forms;
using System.Net.NetworkInformation; // Required for network interface access
using System.Diagnostics; // Required for Process
using System.Collections.Generic; // For List<T>
using System.Linq; // For LINQ operations
using System.Threading.Tasks; // For Task

using System.Text.RegularExpressions; // For parsing netsh output
using System.Management; // For WMI event watcher
using Windows.Devices.Geolocation; // For Geolocator

namespace NetworkManagerAppModern
{
    // Helper class for storing info parsed from netsh
    public class SimpleNetInterfaceInfo
    {
        public string Name { get; set; } = string.Empty;
        public string AdminState { get; set; } = string.Empty; // e.g., "Enabled", "Disabled"
        public string OperationalState { get; set; } = string.Empty; // e.g., "Connected", "Disconnected"
        public string? Description { get; set; } // Optional: from NetworkInterface object
        // public string? Type { get; set; } // Optional - Replaced by NetType
        public NetworkInterfaceType? NetType { get; set; } // To store the actual enum type
        public string? Id { get; set; } // Optional

        // We will primarily rely on Name for enabling/disabling via netsh
    }

    // Helper structure for defining columns
    public struct ColumnDefinition
    {
        public string Key { get; }
        public string HeaderText { get; }
        public int DefaultWidth { get; }
        public Func<SimpleNetInterfaceInfo, string> ValueGetter { get; }

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
                this.notifyIcon1.Icon = Properties.Resources.appicon;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Custom icon 'appicon' failed to load: {ex.Message}. Using fallback system icon.");
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
            // Event handlers for new buttons
            this.btnConnectSelected.Click += new System.EventHandler(this.BtnConnectSelected_Click);
            this.btnDisconnectSelected.Click += new System.EventHandler(this.BtnDisconnectSelected_Click);
            this.btnSelectColumns.Click += new System.EventHandler(this.BtnSelectColumns_Click);
            this.listViewNetworkInterfaces.SelectedIndexChanged += new System.EventHandler(this.ListViewNetworkInterfaces_SelectedIndexChanged);

            PopulateNetworkInterfaces(); // Initial population

            NetworkChange.NetworkAddressChanged += new NetworkAddressChangedEventHandler(AddressChangedCallback);

            _initialRefreshDelayTimer = new System.Windows.Forms.Timer();
            _initialRefreshDelayTimer.Interval = 250;
            _initialRefreshDelayTimer.Tick += InitialRefreshDelayTimer_Tick;

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
                _wmiWatcher = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"An unexpected error occurred while initializing WMI watcher: {ex.Message}");
                _wmiWatcher = null;
            }
        }

        private void WmiEventHandler(object sender, EventArrivedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("WMI Network Adapter event arrived.");
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(() => {
                    if (!this.IsDisposed && this.Handle != IntPtr.Zero && _initialRefreshDelayTimer != null && !_initialRefreshDelayTimer.Enabled)
                    {
                        System.Diagnostics.Debug.WriteLine("WMI Event: Starting initial refresh delay timer.");
                        _initialRefreshDelayTimer.Start();
                    }
                }));
            }
            else
            {
                if (!this.IsDisposed && this.Handle != IntPtr.Zero && _initialRefreshDelayTimer != null && !_initialRefreshDelayTimer.Enabled)
                {
                    System.Diagnostics.Debug.WriteLine("WMI Event (already on UI thread): Starting initial refresh delay timer.");
                    _initialRefreshDelayTimer.Start();
                }
            }
        }

        private void InitialRefreshDelayTimer_Tick(object? sender, EventArgs e)
        {
            _initialRefreshDelayTimer.Stop();
            PopulateNetworkInterfaces(isManualRefresh: false);
        }

        private void PollingTimer_Tick(object? sender, EventArgs e)
        {
            CheckPolledInterfaces();
        }

        private SimpleNetInterfaceInfo? FetchSingleInterfaceInfo(string interfaceName)
        {
            string commandArgs = $"interface show interface name=\"{interfaceName}\"";
            System.Diagnostics.Debug.WriteLine($"FetchSingleInterfaceInfo: Executing 'netsh {commandArgs}'");

            ProcessStartInfo psi = new ProcessStartInfo("netsh", commandArgs)
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
                    if (process == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"FetchSingleInterfaceInfo: Process for '{interfaceName}' failed to start.");
                        return null;
                    }

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    System.Diagnostics.Debug.WriteLine($"FetchSingleInterfaceInfo: Raw output for '{interfaceName}':\n{output}");

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

                        var match = Regex.Match(line, @"^(?<admin>\S+)\s+(?<state>\S+)\s+(?<type>\S+)\s+(?<name>.+)$");
                        if (match.Success)
                        {
                            string reportedName = match.Groups["name"].Value.Trim();
                            if (reportedName.Equals(interfaceName, StringComparison.OrdinalIgnoreCase))
                            {
                                System.Diagnostics.Debug.WriteLine($"FetchSingleInterfaceInfo: Matched for '{interfaceName}'. Admin: {match.Groups["admin"].Value.Trim()}, State: {match.Groups["state"].Value.Trim()}");
                                return new SimpleNetInterfaceInfo
                                {
                                    AdminState = match.Groups["admin"].Value.Trim(),
                                    OperationalState = match.Groups["state"].Value.Trim(),
                                    Name = reportedName
                                };
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"FetchSingleInterfaceInfo: Found interface '{reportedName}' but it does not match requested '{interfaceName}'. Skipping.");
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"FetchSingleInterfaceInfo: No regex match for line: '{line}'");
                        }
                    }
                    System.Diagnostics.Debug.WriteLine($"FetchSingleInterfaceInfo: No matching interface found for '{interfaceName}' after parsing output.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FetchSingleInterfaceInfo: Exception for '{interfaceName}': {ex.Message}");
            }
            return null;
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

            foreach (var entry in _interfacesToPoll.ToList())
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
                         currentInfo.OperationalState.Equals("Non-operational", StringComparison.OrdinalIgnoreCase) ||
                         currentInfo.OperationalState.Equals("Operational", StringComparison.OrdinalIgnoreCase)
                         ))
                    {
                        System.Diagnostics.Debug.WriteLine($"Interface {interfaceName} confirmed as Enabled and in a stable operational state.");
                        stopPollingThisInterface = true;
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Interface {interfaceName} no longer found by netsh during poll.");
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
                    System.Diagnostics.Debug.WriteLine($"Marking {interfaceName} to be removed from polling queue (State: {currentInfo?.AdminState}/{currentInfo?.OperationalState}).");

                    var knownInterface = _lastKnownInterfaces.FirstOrDefault(i => i.Name == interfaceName);
                    if (knownInterface != null && currentInfo != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Updating _lastKnownInterfaces for {interfaceName} from {knownInterface.AdminState}/{knownInterface.OperationalState} to {currentInfo.AdminState}/{currentInfo.OperationalState}");
                        knownInterface.AdminState = currentInfo.AdminState;
                        knownInterface.OperationalState = currentInfo.OperationalState;
                    }
                    else if (knownInterface == null && currentInfo != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Warning: {interfaceName} was polled but not in _lastKnownInterfaces. Adding it with state {currentInfo.AdminState}/{currentInfo.OperationalState}");
                        _lastKnownInterfaces.Add(new SimpleNetInterfaceInfo { Name = currentInfo.Name, AdminState = currentInfo.AdminState, OperationalState = currentInfo.OperationalState });
                    }
                    else if (currentInfo == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Interface {interfaceName} disappeared during poll. Removing from _lastKnownInterfaces if present.");
                        _lastKnownInterfaces.RemoveAll(i => i.Name == interfaceName);
                    }
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

            if (!_interfacesToPoll.Any() && anInterfaceWasPolledThisTick)
            {
                _pollingTimer.Stop();
                System.Diagnostics.Debug.WriteLine("All polling finished, stopping polling timer.");
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
        }

        private void AddressChangedCallback(object? sender, EventArgs e)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(() => {
                    if (!this.IsDisposed && this.Handle != IntPtr.Zero && _initialRefreshDelayTimer != null && !_initialRefreshDelayTimer.Enabled)
                    {
                        System.Diagnostics.Debug.WriteLine("NetworkAddressChanged Event: Starting initial refresh delay timer.");
                        _initialRefreshDelayTimer.Start();
                    }
                }));
            }
            else
            {
                if (!this.IsDisposed && this.Handle != IntPtr.Zero && _initialRefreshDelayTimer != null && !_initialRefreshDelayTimer.Enabled)
                {
                    System.Diagnostics.Debug.WriteLine("NetworkAddressChanged Event (already on UI thread): Starting initial refresh delay timer.");
                    _initialRefreshDelayTimer.Start();
                }
            }
        }

        private void InitializeColumnData()
        {
            _allColumnDefinitions = new List<ColumnDefinition>
            {
                new ColumnDefinition("Name", "Name", 150, info => info.Name),
                new ColumnDefinition("AdminState", "Admin State", 100, info => info.AdminState),
                new ColumnDefinition("OperationalState", "Op. Status", 120, info => info.OperationalState),
                new ColumnDefinition("Description", "Description", 250, info => info.Description ?? ""),
                new ColumnDefinition("Type", "Type", 100, info => info.NetType?.ToString() ?? ""), // Updated to use NetType
                new ColumnDefinition("ID", "ID", 200, info => info.Id ?? ""),
            };
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
                StandardOutputEncoding = System.Text.Encoding.Default
            };

            try
            {
                using (Process? process = Process.Start(psi))
                {
                    if (process == null) return interfaces;

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

                        var match = Regex.Match(line, @"^(?<admin>\S+)\s+(?<state>\S+)\s+(?<type>\S+)\s+(?<name>.+)$");
                        if (match.Success)
                        {
                            string adminVal = match.Groups["admin"].Value.Trim();
                            string stateVal = match.Groups["state"].Value.Trim();
                            string typeVal = match.Groups["type"].Value.Trim();
                            string nameVal = match.Groups["name"].Value.Trim();

                            System.Diagnostics.Debug.WriteLine($"FetchInterfacesViaNetsh: Parsed Line: Raw='{line}'");
                            System.Diagnostics.Debug.WriteLine($"  -> Admin='{adminVal}', State='{stateVal}', Type='{typeVal}', Name='{nameVal}'");

                            interfaces.Add(new SimpleNetInterfaceInfo
                            {
                                AdminState = adminVal,
                                OperationalState = stateVal,
                                Name = nameVal
                            });
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"FetchInterfacesViaNetsh: No regex match for line: '{line}'");
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
            List<SimpleNetInterfaceInfo> netshInterfaces = FetchInterfacesViaNetsh();
            System.Diagnostics.Debug.WriteLine($"PopulateNetworkInterfaces (isManualRefresh: {isManualRefresh}): Found {netshInterfaces.Count} interfaces via netsh.");

            bool newPollingInitiatedThisCall = false;
            if (!isManualRefresh)
            {
                foreach (var currentInfo in netshInterfaces)
                {
                    var previousInfo = _lastKnownInterfaces.FirstOrDefault(i => i.Name == currentInfo.Name);
                    if (previousInfo != null)
                    {
                        if (previousInfo.AdminState.Equals("Disabled", StringComparison.OrdinalIgnoreCase) &&
                            currentInfo.AdminState.Equals("Enabled", StringComparison.OrdinalIgnoreCase) &&
                            !currentInfo.OperationalState.Equals("Connected", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!_interfacesToPoll.ContainsKey(currentInfo.Name))
                            {
                                _interfacesToPoll[currentInfo.Name] = DateTime.UtcNow;
                                newPollingInitiatedThisCall = true;
                                System.Diagnostics.Debug.WriteLine($"Interface {currentInfo.Name} marked for polling. From {previousInfo.AdminState}/{previousInfo.OperationalState} to {currentInfo.AdminState}/{currentInfo.OperationalState}");
                            }
                        }
                    }
                }

                if (newPollingInitiatedThisCall && !_pollingTimer.Enabled)
                {
                    System.Diagnostics.Debug.WriteLine("New polling initiated, ensuring polling timer is started.");
                    _pollingTimer.Start();
                }
            }

            if (newPollingInitiatedThisCall && !isManualRefresh)
            {
                System.Diagnostics.Debug.WriteLine($"Polling newly initiated for automatic refresh. Deferring UI clear & update. Active polls: {string.Join(", ", _interfacesToPoll.Keys)}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"Proceeding with UI clear and full update. isManualRefresh: {isManualRefresh}, newPollingInitiatedThisCall: {newPollingInitiatedThisCall}, pollsInProgress: {_interfacesToPoll.Count}");

            string? previouslySelectedInterfaceName = null;
            if (listViewNetworkInterfaces.SelectedItems.Count > 0)
            {
                previouslySelectedInterfaceName = listViewNetworkInterfaces.SelectedItems[0].Tag as string;
            }

            SetupListViewColumns();
            listViewNetworkInterfaces.Items.Clear();

            // Temporary list to build fully populated interface info
            var fullyPopulatedInterfaces = new List<SimpleNetInterfaceInfo>();


            Dictionary<string, NetworkInterface> systemInterfaces = new Dictionary<string, NetworkInterface>();
            try
            {
                    systemInterfaces = NetworkInterface.GetAllNetworkInterfaces()
                                                .Where(ni => !string.IsNullOrEmpty(ni.Name))
                                                .GroupBy(ni => ni.Name)
                                                .ToDictionary(g => g.Key, g => g.First());
            }
            catch (Exception ex) {
                    System.Diagnostics.Debug.WriteLine($"Error getting system interfaces: {ex.Message}");
            }

            if (_visibleColumnKeys == null || !_visibleColumnKeys.Any() || _allColumnDefinitions == null)
            {
                System.Diagnostics.Debug.WriteLine("Cannot populate ListView: VisibleColumnKeys or AllColumnDefinitions is null/empty.");
                return;
            }

            foreach (var baseInfo in netshInterfaces)
            {
                // Log baseInfo before creating displayInfo
                System.Diagnostics.Debug.WriteLine($"PopulateNetworkInterfaces: Processing baseInfo: Name='{baseInfo.Name}', AdminState='{baseInfo.AdminState}', OperationalState='{baseInfo.OperationalState}'");

                SimpleNetInterfaceInfo displayInfo = new SimpleNetInterfaceInfo {
                    Name = baseInfo.Name,
                    AdminState = baseInfo.AdminState,
                    OperationalState = baseInfo.OperationalState
                    // Description, Type, ID will be populated next if available
                };

                if (systemInterfaces.TryGetValue(displayInfo.Name, out NetworkInterface? systemAdapter))
                {
                    displayInfo.Description = systemAdapter.Description;
                    displayInfo.NetType = systemAdapter.NetworkInterfaceType; // Store the enum
                    displayInfo.Id = systemAdapter.Id;
                }

                ListViewItem item;
                var firstKey = _visibleColumnKeys.FirstOrDefault();
                if (firstKey == null) {
                    System.Diagnostics.Debug.WriteLine("PopulateNetworkInterfaces: First visible column key is null, cannot create ListView item.");
                    continue;
                }
                ColumnDefinition firstColDef = _allColumnDefinitions.FirstOrDefault(cd => cd.Key == firstKey);
                if (string.IsNullOrEmpty(firstColDef.Key))
                {
                    System.Diagnostics.Debug.WriteLine($"PopulateNetworkInterfaces: First column definition for key '{firstKey}' not found. Skipping item.");
                    continue;
                }

                string firstColText = firstColDef.ValueGetter(displayInfo);
                System.Diagnostics.Debug.WriteLine($"PopulateNetworkInterfaces: For item '{displayInfo.Name}', first visible column ('{firstKey}') text = '{firstColText}'");
                item = new ListViewItem(firstColText);

                for (int i = 1; i < _visibleColumnKeys.Count; i++)
                {
                    string currentKey = _visibleColumnKeys[i];
                    ColumnDefinition colDef = _allColumnDefinitions.FirstOrDefault(cd => cd.Key == currentKey);
                    if (string.IsNullOrEmpty(colDef.Key))
                    {
                        System.Diagnostics.Debug.WriteLine($"PopulateNetworkInterfaces: Column definition for key '{currentKey}' not found. Skipping subitem for '{displayInfo.Name}'.");
                        item.SubItems.Add("");
                        continue;
                    }

                    string subItemText;
                    if (currentKey == "AdminState")
                    {
                        // Explicitly use displayInfo.AdminState for the AdminState column
                        subItemText = displayInfo.AdminState;
                        System.Diagnostics.Debug.WriteLine($"PopulateNetworkInterfaces: For item '{displayInfo.Name}', AdminState column using explicit displayInfo.AdminState='{subItemText}' (display index {i}, subitem index {i-1})");
                    }
                    else
                    {
                        subItemText = colDef.ValueGetter(displayInfo);
                        System.Diagnostics.Debug.WriteLine($"PopulateNetworkInterfaces: For item '{displayInfo.Name}', subitem for key='{currentKey}' (display index {i}, subitem index {i-1}) text = '{subItemText}' (via ValueGetter)");
                    }
                    item.SubItems.Add(subItemText);
                }

                item.Tag = displayInfo.Name;

                // FINAL CHECK LOGGING
                if (item.Tag is string tagNameForFinalCheck && tagNameForFinalCheck.Equals("Wi-Fi", StringComparison.OrdinalIgnoreCase))
                {
                    System.Diagnostics.Debug.WriteLine($"FINAL CHECK for item '{tagNameForFinalCheck}' before adding to ListView:");
                    System.Diagnostics.Debug.WriteLine($"  item.Text (Col 0) = '{item.Text}'");
                    if (_visibleColumnKeys.Count > 1 && item.SubItems.Count > 0)
                    {
                        // Assuming AdminState is typically _visibleColumnKeys[1] which maps to SubItems[0]
                        System.Diagnostics.Debug.WriteLine($"  item.SubItems[0].Text (Col 1, e.g., AdminState if Key={_visibleColumnKeys[1]}) = '{item.SubItems[0].Text}'");
                    }
                    if (_visibleColumnKeys.Count > 2 && item.SubItems.Count > 1)
                    {
                        // Assuming OperationalState is typically _visibleColumnKeys[2] which maps to SubItems[1]
                        System.Diagnostics.Debug.WriteLine($"  item.SubItems[1].Text (Col 2, e.g., OpState if Key={_visibleColumnKeys[2]}) = '{item.SubItems[1].Text}'");
                    }
                }
                listViewNetworkInterfaces.Items.Add(item);
                fullyPopulatedInterfaces.Add(displayInfo); // Add the fully populated info
            }

            _lastKnownInterfaces = fullyPopulatedInterfaces; // Assign the fully populated list

            // Restore selection
            if (!string.IsNullOrEmpty(previouslySelectedInterfaceName))
            {
                foreach (ListViewItem item in listViewNetworkInterfaces.Items)
                {
                    if (item.Tag is string tagName && tagName == previouslySelectedInterfaceName)
                    {
                        item.Selected = true;
                        item.EnsureVisible();
                        listViewNetworkInterfaces.Focus(); // Ensure the ListView has focus to show selection
                        break;
                    }
                }
            }
        }

        private void BtnSelectColumns_Click(object? sender, EventArgs e)
        {
            if (_allColumnDefinitions == null || _visibleColumnKeys == null) return;

            List<string> allColumnKeys = _allColumnDefinitions.Select(cd => cd.Key).ToList();
            List<string> currentlyVisibleKeys = new List<string>(_visibleColumnKeys);

            using (ColumnSelectionForm colForm = new ColumnSelectionForm(allColumnKeys, currentlyVisibleKeys))
            {
                if (colForm.ShowDialog(this) == DialogResult.OK)
                {
                    _visibleColumnKeys = new List<string>(colForm.SelectedColumnKeys);
                    SaveColumnSettings();
                    PopulateNetworkInterfaces();
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

        private async Task<bool> IsLocationServiceEnabledAsync()
        {
            try
            {
                var accessStatus = await Geolocator.RequestAccessAsync();

                switch (accessStatus)
                {
                    case GeolocationAccessStatus.Allowed:
                        var geolocator = new Geolocator();
                        geolocator.DesiredAccuracy = PositionAccuracy.Default;
                        var pos=await geolocator.GetGeopositionAsync();

                        System.Diagnostics.Debug.WriteLine($"Location access reported as Allowed. Checking LocationStatus: {geolocator.LocationStatus}");
                        // Only consider it truly enabled if the status is Ready.
                        // Other statuses like Initializing, NoData, or even if it's Allowed but Disabled/NotAvailable globally.

                        if (geolocator.LocationStatus == PositionStatus.NotInitialized)
                        {
                            System.Diagnostics.Debug.WriteLine("LocationStatus is NotInitialized, but we don't care.");
                            return true;
                        }
                        else if (geolocator.LocationStatus == PositionStatus.Ready)
                        {
                            System.Diagnostics.Debug.WriteLine("LocationStatus is Ready.");
                            return true;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"LocationStatus is {geolocator.LocationStatus}, not Ready. Treating as disabled/unavailable.");
                            return false;
                        }

                    case GeolocationAccessStatus.Denied:
                        System.Diagnostics.Debug.WriteLine("Location access denied by user or policy.");
                        return false;

                    case GeolocationAccessStatus.Unspecified:
                        System.Diagnostics.Debug.WriteLine("Location access unspecified or error.");
                        return false;
                        // This can also mean location is turned off in settings,
                        // or the system doesn't have location capabilities.
                }
            }
            catch (Exception ex)
            {
                // This can happen if the location service is completely unavailable or due to other system issues.
                System.Diagnostics.Debug.WriteLine($"Error checking location status: {ex.Message}");
                return false; // Assume not enabled or error
            }
            return false; // Default to false
        }


        private void BtnConnectSelected_Click(object? sender, EventArgs e)
        {
            UpdateSelectedInterfacesConnectionState(true);
        }

        private void BtnDisconnectSelected_Click(object? sender, EventArgs e)
        {
            // To be implemented in the next step (Implement Disconnect Logic)
            UpdateSelectedInterfacesConnectionState(false);
        }


        private void ExecuteNetshWlanCommand(string interfaceName, string? profileName, bool connect)
        {
            string action = connect ? "connect" : "disconnect";
            string arguments;

            if (connect)
            {
                if (string.IsNullOrEmpty(profileName))
                {
                    MessageBox.Show($"No profile specified to connect Wi-Fi interface '{interfaceName}'.", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                arguments = $"wlan connect name=\"{profileName}\" interface=\"{interfaceName}\"";
            }
            else // Disconnect
            {
                arguments = $"wlan disconnect interface=\"{interfaceName}\"";
            }

            System.Diagnostics.Debug.WriteLine($"ExecuteNetshWlanCommand: Executing 'netsh.exe' with arguments: \"{arguments}\"");

            ProcessStartInfo psi = new ProcessStartInfo("netsh", arguments)
            {
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = true,
            };

            try
            {
                using (Process? process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        MessageBox.Show($"Failed to start netsh process for Wi-Fi {action} on interface '{interfaceName}'.", "Process Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        string errorMsg = $"Netsh wlan {action} command for interface '{interfaceName}'" +
                                          (connect ? $" (profile: {profileName})" : "") +
                                          $" failed with exit code: {process.ExitCode}.";
                        MessageBox.Show(errorMsg, "Netsh WLAN Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                string detailedError = $"Wi-Fi {action} operation for interface '{interfaceName}' failed.\nWin32 Error Code: {ex.NativeErrorCode}\nMessage: {ex.Message}";
                if (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
                {
                    detailedError = $"Wi-Fi {action} for interface '{interfaceName}' was canceled by the user (UAC prompt denied).";
                }
                MessageBox.Show(detailedError, "Operation Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An unexpected error occurred while trying to {action} Wi-Fi on interface '{interfaceName}': {ex.Message}", "Unexpected Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void UpdateSelectedInterfacesConnectionState(bool connect)
        {
            if (listViewNetworkInterfaces.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a Wi-Fi interface.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ListViewItem selectedItem = listViewNetworkInterfaces.SelectedItems[0]; // Assuming single selection for connect/disconnect for simplicity now
            if (!(selectedItem.Tag is string interfaceName)) return;

            SimpleNetInterfaceInfo? netInfo = _lastKnownInterfaces.FirstOrDefault(i => i.Name == interfaceName);
            if (netInfo == null || netInfo.NetType != NetworkInterfaceType.Wireless80211)
            {
                MessageBox.Show("Connect/Disconnect operations are only supported for Wi-Fi interfaces.", "Unsupported Interface", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (netInfo.AdminState?.Equals("Disabled", StringComparison.OrdinalIgnoreCase) == true)
            {
                 MessageBox.Show($"Interface '{interfaceName}' is Disabled. Please Enable it first.", "Interface Disabled", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                 return;
            }

            // Pre-checks based on current operational state
            if (connect && netInfo.OperationalState?.Equals("Connected", StringComparison.OrdinalIgnoreCase) == true)
            {
                // Optionally, ask if they want to connect to a different profile, which might disconnect first.
                // For simplicity now, just inform or allow trying (netsh might handle it or error).
                // Let's allow the attempt, as user might be selecting a specific profile.
                // If we want to be stricter:
                // MessageBox.Show($"Interface '{interfaceName}' is already connected. Disconnect first to choose a different profile.", "Already Connected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                // PopulateNetworkInterfaces(true); // Refresh to clear any potential pending UI from a misclick
                // return;
                System.Diagnostics.Debug.WriteLine($"Interface {interfaceName} is already connected. Proceeding with connect command, possibly to a new profile.");
            }
            else if (!connect && netInfo.OperationalState?.Equals("Disconnected", StringComparison.OrdinalIgnoreCase) == true)
            {
                MessageBox.Show($"Interface '{interfaceName}' is already disconnected.", "Already Disconnected", MessageBoxButtons.OK, MessageBoxIcon.Information);
                PopulateNetworkInterfaces(true); // Refresh to clear any potential pending UI
                return;
            }

            // UI update for pending state (OperationalState column)
            string pendingStatusText = connect ? "Connecting..." : "Disconnecting...";
            int opStateDisplayIndex = GetDisplayIndexOfColumnKey("OperationalState");

            if (opStateDisplayIndex != -1)
            {
                 if (opStateDisplayIndex == 0) selectedItem.Text = pendingStatusText;
                 else if (opStateDisplayIndex > 0 && selectedItem.SubItems.Count >= opStateDisplayIndex)
                 {
                    selectedItem.SubItems[opStateDisplayIndex].Text = pendingStatusText;
                 }
                 selectedItem.ForeColor = SystemColors.GrayText;
                 selectedItem.BackColor = SystemColors.ControlLight;
            }


            if (connect)
            {
                List<string> profiles = GetWlanProfiles(interfaceName);
                string? profileToConnect = null;

                if (profiles.Count == 0)
                {
                    MessageBox.Show($"No Wi-Fi profiles found for interface '{interfaceName}'. Please add a profile in Windows settings.", "No Profiles", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    // Revert pending UI state if needed, or let refresh handle it
                    PopulateNetworkInterfaces(true); // Force refresh to clear pending state
                    return;
                }
                else if (profiles.Count == 1)
                {
                    profileToConnect = profiles[0];
                    System.Diagnostics.Debug.WriteLine($"Attempting to connect interface '{interfaceName}' using single available profile: '{profileToConnect}'");
                }
                else // Multiple profiles
                {
                    using (var profileDialog = new ProfileSelectionDialog(profiles))
                    {
                        if (profileDialog.ShowDialog(this) == DialogResult.OK)
                        {
                            profileToConnect = profileDialog.SelectedProfile;
                            System.Diagnostics.Debug.WriteLine($"Attempting to connect interface '{interfaceName}' using selected profile: '{profileToConnect}'");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"User cancelled profile selection for interface '{interfaceName}'.");
                            PopulateNetworkInterfaces(true); // Force refresh to clear pending state
                            return; // User cancelled
                        }
                    }
                }

                if (!string.IsNullOrEmpty(profileToConnect))
                {
                    ExecuteNetshWlanCommand(interfaceName, profileToConnect, true);
                }
            }
            else // Disconnect
            {
                // Disconnect logic will be fully handled in the next step
                System.Diagnostics.Debug.WriteLine($"Attempting to disconnect interface: '{interfaceName}' (Actual command in next step)");
                 ExecuteNetshWlanCommand(interfaceName, null, false); // Placeholder call
            }
             // Rely on WMI/NetworkAddressChanged events to trigger PopulateNetworkInterfaces for final state update
        }

        private List<string> GetWlanProfiles(string interfaceName)
        {
            var profiles = new List<string>();
            string commandArgs = $"wlan show profiles interface=\"{interfaceName}\"";
            System.Diagnostics.Debug.WriteLine($"GetWlanProfiles: Executing 'netsh {commandArgs}'");

            ProcessStartInfo psi = new ProcessStartInfo("netsh", commandArgs)
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.Default // Use default encoding which should handle most system outputs
            };

            try
            {
                using (Process? process = Process.Start(psi))
                {
                    if (process == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"GetWlanProfiles: Process for '{interfaceName}' failed to start.");
                        return profiles;
                    }

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    System.Diagnostics.Debug.WriteLine($"GetWlanProfiles: Raw output for '{interfaceName}':\n{output}");

                    // Example output:
                    // Profiles on interface Wi-Fi:
                    // Group policy profiles (read only)
                    // ---------------------------------
                    //     <None>
                    // User profiles
                    // -------------
                    //     All User Profile     : MyWifiProfile1
                    //     All User Profile     : AnotherProfile
                    //
                    // We are interested in lines like "    All User Profile     : ProfileName"
                    // Or sometimes it might just be "    User_profile_name" under "Profiles on interface..." directly
                    // A more robust regex might be needed if format varies significantly.
                    // Current regex looks for " : " followed by the profile name.

                    var lines = output.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
                    bool profilesSectionStarted = false; // Might not be strictly needed with current regex

                    foreach (var line in lines)
                    {
                        if (line.Contains("Profiles on interface")) // Good starting point
                        {
                            profilesSectionStarted = true;
                            continue;
                        }

                        if (profilesSectionStarted) // Process lines after the header
                        {
                            var match = Regex.Match(line, @":\s*(.+)$"); // Look for " : " and capture what's after
                            if (match.Success)
                            {
                                string profileName = match.Groups[1].Value.Trim();
                                if (!string.IsNullOrEmpty(profileName) && !profileName.Equals("<None>", StringComparison.OrdinalIgnoreCase))
                                {
                                    profiles.Add(profileName);
                                    System.Diagnostics.Debug.WriteLine($"GetWlanProfiles: Found profile '{profileName}'");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetWlanProfiles: Exception for '{interfaceName}': {ex.Message}");
                MessageBox.Show($"Error fetching WLAN profiles for {interfaceName}: {ex.Message}", "WLAN Profile Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return profiles.Distinct().ToList(); // Ensure uniqueness
        }

        private async void ListViewNetworkInterfaces_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // Default to hiding buttons
            btnConnectSelected.Visible = false;
            btnDisconnectSelected.Visible = false;

            if (listViewNetworkInterfaces.SelectedItems.Count > 0)
            {
                ListViewItem selectedItem = listViewNetworkInterfaces.SelectedItems[0];
                if (selectedItem.Tag is string interfaceName)
                {
                    // Find the corresponding SimpleNetInterfaceInfo object to get its type
                    // This requires SimpleNetInterfaceInfo to store NetworkInterfaceType,
                    // which will be addressed in the next step.
                    // For now, we'll use a placeholder logic.
                    SimpleNetInterfaceInfo? netInfo = _lastKnownInterfaces.FirstOrDefault(i => i.Name == interfaceName);

                    if (netInfo != null && netInfo.NetType == NetworkInterfaceType.Wireless80211 && await IsLocationServiceEnabledAsync()) // Compare enum directly
                    {
                        btnConnectSelected.Visible = true;
                        btnDisconnectSelected.Visible = true;
                        // Also, wire up event handlers for Connect/Disconnect if not already done
                        // We'll do this once in the constructor or ensure they are always wired.
                        // For now, assume they will be wired.
                    }
                    else
                    {
                        btnConnectSelected.Visible = false;
                        btnDisconnectSelected.Visible = false;
                    }
                }
                else
                {
                    btnConnectSelected.Visible = false;
                    btnDisconnectSelected.Visible = false;
                }
            }
            else
            {
                // No item selected, hide buttons
                btnConnectSelected.Visible = false;
                btnDisconnectSelected.Visible = false;
            }
        }

        // Helper method to find the display index of a column based on its key
        private int GetDisplayIndexOfColumnKey(string columnKeyToFind)
        {
            if (_visibleColumnKeys == null)
            {
                System.Diagnostics.Debug.WriteLine("GetDisplayIndexOfColumnKey: _visibleColumnKeys is null.");
                return -1;
            }
            System.Diagnostics.Debug.WriteLine($"GetDisplayIndexOfColumnKey: Searching for '{columnKeyToFind}' in _visibleColumnKeys: [{string.Join(", ", _visibleColumnKeys)}]");
            for (int i = 0; i < _visibleColumnKeys.Count; i++)
            {
                if (_visibleColumnKeys[i] == columnKeyToFind)
                {
                    System.Diagnostics.Debug.WriteLine($"GetDisplayIndexOfColumnKey: Found '{columnKeyToFind}' at display index {i}.");
                    return i;
                }
            }
            System.Diagnostics.Debug.WriteLine($"GetDisplayIndexOfColumnKey: Did not find '{columnKeyToFind}'.");
            return -1;
        }

        private void UpdateSelectedInterfaces(bool enable)
        {
            System.Diagnostics.Debug.WriteLine($"UpdateSelectedInterfaces called with enable: {enable}");
            if (listViewNetworkInterfaces.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select one or more network interfaces.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string targetState = enable ? "Enabled" : "Disabled";
            string pendingStatusText = enable ? "Enabling..." : "Disabling...";

            System.Diagnostics.Debug.WriteLine($"UpdateSelectedInterfaces: Target state: {targetState}, Pending text: {pendingStatusText}");
            int adminStateDisplayIndex = GetDisplayIndexOfColumnKey("AdminState");
            System.Diagnostics.Debug.WriteLine($"UpdateSelectedInterfaces: adminStateDisplayIndex for 'AdminState' key: {adminStateDisplayIndex}");


            List<string> interfaceNamesToProcess = new List<string>();

            foreach (ListViewItem selectedItem in listViewNetworkInterfaces.SelectedItems)
            {
                System.Diagnostics.Debug.WriteLine($"UpdateSelectedInterfaces: Processing selected item with Tag: {selectedItem.Tag}");
                string currentDisplayedAdminState = string.Empty;

                if (adminStateDisplayIndex != -1)
                {
                    if (adminStateDisplayIndex == 0)
                    {
                        currentDisplayedAdminState = selectedItem.Text;
                        System.Diagnostics.Debug.WriteLine($"UpdateSelectedInterfaces: Reading AdminState from selectedItem.Text (display index 0): '{currentDisplayedAdminState}'");
                    }
                    else if (adminStateDisplayIndex > 0 && selectedItem.SubItems.Count > (adminStateDisplayIndex)) // SubItem index is displayIndex - 1
                    {
                        currentDisplayedAdminState = selectedItem.SubItems[adminStateDisplayIndex].Text;
                        System.Diagnostics.Debug.WriteLine($"UpdateSelectedInterfaces: Reading AdminState from selectedItem.SubItems[{adminStateDisplayIndex}].Text: '{currentDisplayedAdminState}'");
                    }
                    else
                    {
                         System.Diagnostics.Debug.WriteLine($"UpdateSelectedInterfaces: AdminState column (display index {adminStateDisplayIndex}) not found or SubItems too short for item {selectedItem.Tag}. SubItem count: {selectedItem.SubItems.Count}");
                    }
                }
                else
                {
                     System.Diagnostics.Debug.WriteLine("UpdateSelectedInterfaces: AdminState column key not found in visible columns.");
                }

                bool alreadyInTargetState = !string.IsNullOrEmpty(currentDisplayedAdminState) && currentDisplayedAdminState.Equals(targetState, StringComparison.OrdinalIgnoreCase);
                bool alreadyPending = !string.IsNullOrEmpty(currentDisplayedAdminState) && currentDisplayedAdminState.Equals(pendingStatusText, StringComparison.OrdinalIgnoreCase);

                System.Diagnostics.Debug.WriteLine($"UpdateSelectedInterfaces: For item {selectedItem.Tag} - CurrentDisplayedAdminState: '{currentDisplayedAdminState}', TargetState: '{targetState}', IsAlreadyInTargetState: {alreadyInTargetState}, IsAlreadyPending: {alreadyPending}");

                if (alreadyInTargetState || alreadyPending)
                {
                    System.Diagnostics.Debug.WriteLine($"Interface {selectedItem.Tag} is already in or pending '{targetState}' state. Skipping.");
                    continue;
                }

                selectedItem.ForeColor = SystemColors.GrayText;
                selectedItem.BackColor = SystemColors.ControlLight;
                System.Diagnostics.Debug.WriteLine($"UpdateSelectedInterfaces: Set ForeColor=GrayText, BackColor=ControlLight for item {selectedItem.Tag}");


                if (adminStateDisplayIndex != -1)
                {
                    if (adminStateDisplayIndex == 0)
                    {
                        selectedItem.Text = pendingStatusText;
                        System.Diagnostics.Debug.WriteLine($"UpdateSelectedInterfaces: Set selectedItem.Text to '{pendingStatusText}' for item {selectedItem.Tag}");
                    }
                    else if (adminStateDisplayIndex > 0)
                    {
                        if (selectedItem.SubItems.Count > (adminStateDisplayIndex) )
                        {
                             selectedItem.SubItems[adminStateDisplayIndex].Text = pendingStatusText;
                             System.Diagnostics.Debug.WriteLine($"UpdateSelectedInterfaces: Set selectedItem.SubItems[{adminStateDisplayIndex}].Text to '{pendingStatusText}' for item {selectedItem.Tag}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"Warning: AdminState column (display index {adminStateDisplayIndex}) still not found in SubItems for item {selectedItem.Tag} when trying to write. SubItem count: {selectedItem.SubItems.Count}");
                        }
                    }
                }
                else
                {
                     System.Diagnostics.Debug.WriteLine("Warning: AdminState column is not visible, cannot set pending text for it (this should have been logged before).");
                }

                if (selectedItem.Tag is string interfaceName && !string.IsNullOrEmpty(interfaceName))
                {
                    interfaceNamesToProcess.Add(interfaceName);
                }
            }

            if (!interfaceNamesToProcess.Any())
            {
                System.Diagnostics.Debug.WriteLine("No interfaces needed processing after pre-checks.");
                return;
            }

            foreach (string interfaceNameToProcess in interfaceNamesToProcess)
            {
                System.Diagnostics.Debug.WriteLine($"Attempting to {(enable ? "enable" : "disable")} interface using Name from Tag: '{interfaceNameToProcess}'");
                ExecuteNetshCommandByName(interfaceNameToProcess, enable);
            }
        }

        private void ExecuteNetshCommandByName(string interfaceName, bool enable)
        {
            System.Diagnostics.Debug.WriteLine($"ExecuteNetshCommandByName: Raw interfaceName received = '{interfaceName}'");
            string processedName = interfaceName;
            string arguments = $"interface set interface name=\"{processedName}\" admin={(enable ? "enable" : "disable")}";
            System.Diagnostics.Debug.WriteLine($"ExecuteNetshCommandByName: Executing 'netsh.exe' with arguments: \"{arguments}\"");

            ProcessStartInfo psi = new ProcessStartInfo("netsh", arguments)
            {
                Verb = "runas",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = true,
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
                if (ex.NativeErrorCode == 1223)
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
            _isExiting = true;
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
                e.Cancel = true;
                this.Hide();
                this.ShowInTaskbar = false;
            }
            else if (_isExiting)
            {
                NetworkChange.NetworkAddressChanged -= new NetworkAddressChangedEventHandler(AddressChangedCallback);
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
        }

        private void ToggleFormVisibility()
        {
            if (this.Visible)
            {
                this.Hide();
            }
            else
            {
                if (this.WindowState == FormWindowState.Minimized)
                {
                    this.WindowState = FormWindowState.Normal;
                }
                this.Show();
                this.ShowInTaskbar = true;
                this.Activate();
                this.BringToFront();
            }
        }
    }
}
