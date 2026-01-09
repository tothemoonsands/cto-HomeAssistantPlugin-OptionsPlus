namespace Loupedeck.HomeAssistantPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Loupedeck;
    using Loupedeck.HomeAssistantPlugin.Models;
    using Loupedeck.HomeAssistantPlugin.Services;

    /// <summary>
    /// Area-based action for toggling all Home Assistant switches in a selected area.
    /// Simplified compared to lights - switches only support on/off functionality.
    /// Uses individual switch capability filtering for consistency with light actions.
    /// </summary>
    public sealed class AreaToggleSwitchesAction : ActionEditorCommand, IDisposable
    {
        /// <summary>
        /// Logging prefix for this action's log messages.
        /// </summary>
        private const String LogPrefix = "[AreaToggleSwitches]";

        /// <summary>
        /// Home Assistant client interface for WebSocket communication.
        /// </summary>
        private IHaClient? _ha;

        /// <summary>
        /// Switch control service for switch operations.
        /// </summary>
        private ISwitchControlService? _switchSvc;

        /// <summary>
        /// Switch state manager for tracking switch properties and capabilities.
        /// </summary>
        private ISwitchStateManager? _switchStateManager;

        /// <summary>
        /// Data service for fetching Home Assistant entity states.
        /// </summary>
        private IHomeAssistantDataService? _dataService;

        /// <summary>
        /// Data parser for processing Home Assistant JSON responses.
        /// </summary>
        private IHomeAssistantDataParser? _dataParser;

        /// <summary>
        /// Capability service for analyzing switch feature support.
        /// </summary>
        private readonly CapabilityService _capSvc = new();

        /// <summary>
        /// Indicates whether this instance has been disposed.
        /// </summary>
        private Boolean _disposed = false;

        /// <summary>
        /// Simple toggle state for all switches in the area. Defaults to false (off state).
        /// This boolean alternates between true/false on each press, and all switches get the same command.
        /// </summary>
        private Boolean _areaSwitchesState = false;

        /// <summary>
        /// Control name for area selection dropdown.
        /// </summary>
        private const String ControlArea = "ha_area";

        /// <summary>
        /// Authentication timeout in seconds for Home Assistant connections.
        /// </summary>
        private const Int32 AuthTimeoutSeconds = 8;

        /// <summary>
        /// Icon service for rendering action button graphics.
        /// </summary>
        private readonly IconService _icons;

        /// <summary>
        /// Area mapping: Area ID to friendly name from registry data.
        /// </summary>
        private readonly Dictionary<String, String> _areaIdToName = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Entity mapping: Entity ID to Area ID from switches data.
        /// </summary>
        private readonly Dictionary<String, String> _entityToAreaId = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="AreaToggleSwitchesAction"/> class.
        /// Sets up action editor controls for area selection (no parameter controls needed for switches).
        /// </summary>
        public AreaToggleSwitchesAction()
        {
            this.Name = "HomeAssistant.AreaToggleSwitches";
            this.DisplayName = "Toggle Area Switches";
            this.GroupName = "Switches";
            this.Description = "Toggle all switches in a Home Assistant area on/off.";

            // Area selection dropdown (replaces individual switch selection)
            this.ActionEditor.AddControlEx(new ActionEditorListbox(ControlArea, "Area (retry if empty)"));

            this.ActionEditor.ListboxItemsRequested += this.OnListboxItemsRequested;

            this._icons = new IconService(new Dictionary<String, String>
            {
                { IconId.Area, "area_switch_icon.svg" }
            });

            PluginLog.Info($"{LogPrefix} Constructor completed - dependency initialization deferred to OnLoad()");
        }

        /// <summary>
        /// Gets the command image for the action button.
        /// </summary>
        /// <param name="parameters">Action editor parameters.</param>
        /// <param name="width">Requested image width.</param>
        /// <param name="height">Requested image height.</param>
        /// <returns>Bitmap image showing an area icon.</returns>
        protected override BitmapImage GetCommandImage(ActionEditorActionParameters parameters, Int32 width, Int32 height) =>
            // Show area icon for area-based control
            this._icons.Get(IconId.Area);

        /// <summary>
        /// Loads the action and initializes service dependencies using modern dependency injection pattern.
        /// Creates adapters for Home Assistant client, data services, and switch control.
        /// </summary>
        /// <returns><c>true</c> if initialization succeeded; otherwise, <c>false</c>.</returns>
        protected override Boolean OnLoad()
        {
            PluginLog.Info($"{LogPrefix} OnLoad() START");

            try
            {
                if (this.Plugin is HomeAssistantPlugin haPlugin)
                {
                    PluginLog.Info($"{LogPrefix} Initializing dependencies using modern service architecture");

                    // Initialize dependency injection - use the shared HaClient from Plugin
                    this._ha = new HaClientAdapter(haPlugin.HaClient);
                    this._dataService = new HomeAssistantDataService(this._ha);
                    this._dataParser = new HomeAssistantDataParser(this._capSvc);

                    // Use the singleton SwitchStateManager from the main plugin
                    this._switchStateManager = haPlugin.SwitchStateManager;
                    var existingCount = this._switchStateManager.GetTrackedEntityIds().Count();
                    PluginLog.Info($"{LogPrefix} Using singleton SwitchStateManager with {existingCount} existing tracked entities");

                    // Initialize switch control service
                    this._switchSvc = new SwitchControlService(this._ha);

                    PluginLog.Info($"{LogPrefix} All dependencies initialized successfully");
                    return true;
                }
                else
                {
                    PluginLog.Error($"{LogPrefix} Plugin is not HomeAssistantPlugin, actual type: {this.Plugin?.GetType()?.Name ?? "null"}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"{LogPrefix} OnLoad() failed with exception");
                return false;
            }
        }

        /// <summary>
        /// Unloads the action and disposes of resources.
        /// </summary>
        /// <returns>Always <c>true</c> indicating successful unload.</returns>
        protected override Boolean OnUnload()
        {
            PluginLog.Info($"{LogPrefix} OnUnload()");
            this.Dispose();
            return true;
        }

        /// <summary>
        /// Disposes of managed resources, particularly the switch control service.
        /// Shared services are managed by the main plugin and are not disposed here.
        /// </summary>
        public void Dispose()
        {
            if (this._disposed)
            {
                return;
            }

            PluginLog.Info($"{LogPrefix} Disposing resources");

            try
            {
                this._switchSvc?.Dispose();
                this._switchSvc = null;

                // Don't dispose shared services - they're managed by the main plugin
                this._ha = null;
                this._dataService = null;
                this._dataParser = null;
                this._switchStateManager = null;

                this._disposed = true;
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"{LogPrefix} Error during disposal");
            }
        }

        /// <summary>
        /// Ensures Home Assistant connection is established and authenticated.
        /// Validates configuration settings and attempts connection if not already authenticated.
        /// </summary>
        /// <returns><c>true</c> if connection is ready; otherwise, <c>false</c>.</returns>
        private async Task<Boolean> EnsureHaReadyAsync()
        {
            if (this._ha == null)
            {
                PluginLog.Error($"{LogPrefix} EnsureHaReady: HaClient not initialized");
                return false;
            }

            if (this._ha.IsAuthenticated)
            {
                PluginLog.Info($"{LogPrefix} EnsureHaReady: already authenticated");
                return true;
            }

            if (!this.Plugin.TryGetPluginSetting(HomeAssistantPlugin.SettingBaseUrl, out var baseUrl) ||
                String.IsNullOrWhiteSpace(baseUrl))
            {
                PluginLog.Warning($"{LogPrefix} EnsureHaReady: Missing ha.baseUrl setting");
                HealthBus.Error("Missing Base URL");
                return false;
            }

            if (!this.Plugin.TryGetPluginSetting(HomeAssistantPlugin.SettingToken, out var token) ||
                String.IsNullOrWhiteSpace(token))
            {
                PluginLog.Warning($"{LogPrefix} EnsureHaReady: Missing ha.token setting");
                HealthBus.Error("Missing Token");
                return false;
            }

            try
            {
                PluginLog.Info($"{LogPrefix} Connecting to HA using modern service architecture… url='{baseUrl}'");
                var (ok, msg) = await this._ha.ConnectAndAuthenticateAsync(
                    baseUrl, token, TimeSpan.FromSeconds(AuthTimeoutSeconds), CancellationToken.None
                ).ConfigureAwait(false);

                if (ok)
                {
                    HealthBus.Ok("Auth OK");
                    PluginLog.Info($"{LogPrefix} Auth result ok={ok} msg='{msg}'");
                }
                else
                {
                    HealthBus.Error(msg ?? "Auth failed");
                    PluginLog.Warning($"{LogPrefix} Auth failed: {msg}");
                }

                return ok;
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"{LogPrefix} EnsureHaReady exception");
                HealthBus.Error("Auth error");
                return false;
            }
        }

        /// <summary>
        /// Ensures that area and entity data is initialized for action execution.
        /// This method handles the case where the action is executed immediately after plugin startup
        /// before the dropdown has been opened (which would normally populate the caches).
        /// </summary>
        /// <returns><c>true</c> if data initialization succeeded; otherwise, <c>false</c>.</returns>
        private async Task<Boolean> EnsureDataInitializedAsync()
        {
            // Check if caches are already populated
            if (this._areaIdToName.Any() && this._entityToAreaId.Any())
            {
                PluginLog.Info($"{LogPrefix} Data caches already populated ({this._areaIdToName.Count} areas, {this._entityToAreaId.Count} entity mappings)");
                return true;
            }

            PluginLog.Info($"{LogPrefix} Data caches empty - initializing for action execution");

            try
            {
                // Ensure Home Assistant connection
                if (!await this.EnsureHaReadyAsync())
                {
                    PluginLog.Warning($"{LogPrefix} EnsureDataInitialized: EnsureHaReady failed");
                    return false;
                }

                // Check DataService availability
                if (this._dataService == null)
                {
                    PluginLog.Error($"{LogPrefix} EnsureDataInitialized: DataService not available");
                    return false;
                }

                // Assign to local variable to help compiler flow analysis and prevent race conditions
                var dataService = this._dataService;
                var dataParser = this._dataParser;

                // Ensure both dataService and dataParser are available
                if (dataService == null || dataParser == null)
                {
                    PluginLog.Error($"{LogPrefix} EnsureDataInitialized: DataService or DataParser not available");
                    return false;
                }

                // Initialize SwitchStateManager first (this loads basic switch data)
                if (this._switchStateManager != null)
                {
                    var (success, errorMessage) = await this._switchStateManager.InitOrUpdateAsync(dataService, dataParser, CancellationToken.None);
                    if (!success)
                    {
                        PluginLog.Warning($"{LogPrefix} EnsureDataInitialized: SwitchStateManager.InitOrUpdateAsync failed: {errorMessage}");
                        return false;
                    }
                }

                // Fetch registry data for area information
                PluginLog.Info($"{LogPrefix} Fetching registry data for area mapping");
                var (okEnt, entJson, errEnt) = await dataService.FetchEntityRegistryAsync(CancellationToken.None);
                var (okDev, devJson, errDev) = await dataService.FetchDeviceRegistryAsync(CancellationToken.None);
                var (okArea, areaJson, errArea) = await dataService.FetchAreaRegistryAsync(CancellationToken.None);

                // Parse registry data (dataParser is now guaranteed to be non-null)
                var registryData = dataParser.ParseRegistries(devJson, entJson, areaJson);

                // Get switch data from SwitchStateManager (already initialized above)
                var switches = this._switchStateManager?.GetAllSwitches() ?? Enumerable.Empty<SwitchData>();

                // Update internal caches using the same logic as the dropdown loading
                this.UpdateInternalCaches(switches, registryData);

                PluginLog.Info($"{LogPrefix} Data initialization completed: {this._areaIdToName.Count} areas, {this._entityToAreaId.Count} entity mappings");
                return true;
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"{LogPrefix} EnsureDataInitialized failed");
                return false;
            }
        }

        /// <summary>
        /// Executes the area toggle switches command.
        /// Processes all switches in the selected area with simple on/off toggle behavior.
        /// Uses individual switch capability filtering for consistency (though switches are simpler than lights).
        /// </summary>
        /// <param name="ps">Action editor parameters containing user-configured values.</param>
        /// <returns><c>true</c> if all switch operations succeeded; otherwise, <c>false</c>.</returns>
        protected override Boolean RunCommand(ActionEditorActionParameters ps)
        {
            try
            {
                PluginLog.Info($"{LogPrefix} RunCommand START");

                // Make sure we're online before doing anything
                if (!this.EnsureHaReadyAsync().GetAwaiter().GetResult())
                {
                    PluginLog.Warning($"{LogPrefix} RunCommand: EnsureHaReady failed");
                    return false;
                }

                // Ensure data is initialized (handles first execution after plugin startup)
                if (!this.EnsureDataInitializedAsync().GetAwaiter().GetResult())
                {
                    PluginLog.Warning($"{LogPrefix} RunCommand: EnsureDataInitialized failed");
                    this.Plugin.OnPluginStatusChanged(PluginStatus.Error, "Failed to load area data");
                    return false;
                }

                // Get selected area
                if (!ps.TryGetString(ControlArea, out var selectedArea) || String.IsNullOrWhiteSpace(selectedArea))
                {
                    PluginLog.Warning($"{LogPrefix} No area selected");
                    this.Plugin.OnPluginStatusChanged(PluginStatus.Error, "No area selected");
                    return false;
                }

                // Validate area exists using internal cache (now guaranteed to be populated)
                if (!this._areaIdToName.ContainsKey(selectedArea))
                {
                    PluginLog.Warning($"{LogPrefix} Selected area '{selectedArea}' does not exist in available areas");
                    this.Plugin.OnPluginStatusChanged(PluginStatus.Error, $"Area '{selectedArea}' not found");
                    return false;
                }

                // Get all available switches (from SwitchStateManager)
                var allSwitches = this._switchStateManager?.GetTrackedEntityIds() ?? Enumerable.Empty<String>();

                // Get switches in selected area using internal cache
                var areaSwitches = allSwitches.Where(entityId =>
                    this._entityToAreaId.TryGetValue(entityId, out var switchAreaId) &&
                    String.Equals(switchAreaId, selectedArea, StringComparison.OrdinalIgnoreCase)
                ).ToList();

                if (!areaSwitches.Any())
                {
                    var areaName = this._areaIdToName.TryGetValue(selectedArea, out var name) ? name : selectedArea;
                    PluginLog.Warning($"{LogPrefix} No switches found in area '{areaName}'");
                    this.Plugin.OnPluginStatusChanged(PluginStatus.Error, $"No switches found in area '{areaName}'");
                    return false;
                }

                PluginLog.Info($"{LogPrefix} Processing {areaSwitches.Count} switches in area '{selectedArea}'");

                // Process switches with individual capability filtering
                var success = this.ProcessAreaSwitches(areaSwitches);

                PluginLog.Info($"{LogPrefix} RunCommand completed with success={success}");
                return success;
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"{LogPrefix} RunCommand exception");
                return false;
            }
            finally
            {
                PluginLog.Info($"{LogPrefix} RunCommand END");
            }
        }

        /// <summary>
        /// Processes all switches in the area with individual capability filtering.
        /// Simpler than lights since switches only support on/off functionality.
        /// </summary>
        /// <param name="areaSwitches">Collection of switch entity IDs in the area.</param>
        /// <returns><c>true</c> if all switches processed successfully; otherwise, <c>false</c>.</returns>
        private Boolean ProcessAreaSwitches(IEnumerable<String> areaSwitches)
        {
            // Toggle the area state before processing switches - all switches get the same command
            this._areaSwitchesState = !this._areaSwitchesState;
            PluginLog.Info($"{LogPrefix} Toggled area switches state to: {(this._areaSwitchesState ? "ON" : "OFF")}");

            var success = true;

            foreach (var entityId in areaSwitches)
            {
                try
                {
                    // Get INDIVIDUAL capabilities for this specific switch
                    var individualCaps = this._switchStateManager?.GetCapabilities(entityId)
                        ?? new SwitchCaps(true); // Switches just support on/off

                    // Process this switch with ITS OWN capabilities
                    success &= this.ProcessSingleSwitch(entityId, individualCaps);
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, $"{LogPrefix} Failed to process switch {entityId}");
                    success = false;
                }
            }

            return success;
        }

        /// <summary>
        /// Processes a single switch with its individual capabilities.
        /// Much simpler than lights since switches only support on/off functionality.
        /// </summary>
        /// <param name="entityId">Switch entity ID.</param>
        /// <param name="individualCaps">Individual capabilities of this specific switch.</param>
        /// <returns><c>true</c> if switch processed successfully; otherwise, <c>false</c>.</returns>
        private Boolean ProcessSingleSwitch(String entityId, SwitchCaps individualCaps)
        {
            PluginLog.Info($"{LogPrefix} Processing switch: {entityId}");
            PluginLog.Info($"{LogPrefix} Individual capabilities: onoff={individualCaps.OnOff}");

            if (this._switchSvc == null)
            {
                PluginLog.Error($"{LogPrefix} ProcessSingleSwitch: SwitchControlService not available");
                return false;
            }

            // Always use area state to determine on/off
            PluginLog.Info($"{LogPrefix} Using area state to determine command: {(this._areaSwitchesState ? "ON" : "OFF")}");

            // Use simple area toggle state - all switches get the same command
            if (!this._areaSwitchesState)
            {
                PluginLog.Info($"{LogPrefix} Area state is OFF, turning OFF switch {entityId}");
                var success = this._switchSvc.TurnOffAsync(entityId).GetAwaiter().GetResult();
                PluginLog.Info($"{LogPrefix} HA SERVICE CALL: switch.turn_off entity_id={entityId} -> success={success}");

                if (success && this._switchStateManager != null)
                {
                    // Update local state - switch is now off
                    this._switchStateManager.UpdateSwitchState(entityId, false);
                    PluginLog.Info($"{LogPrefix} Updated local state: {entityId} turned OFF");
                }

                if (!success)
                {
                    var friendlyName = entityId; // Could be enhanced to get friendly name
                    this.Plugin.OnPluginStatusChanged(PluginStatus.Error,
                        $"Failed to turn off switch {friendlyName}");
                }

                return success;
            }

            // Area state is ON, turn switch ON
            PluginLog.Info($"{LogPrefix} Area state is ON, turning ON switch {entityId}");
            var onSuccess = this._switchSvc.TurnOnAsync(entityId).GetAwaiter().GetResult();
            PluginLog.Info($"{LogPrefix} HA SERVICE CALL: switch.turn_on entity_id={entityId} -> success={onSuccess}");

            if (onSuccess && this._switchStateManager != null)
            {
                // Update local state - switch is now on
                this._switchStateManager.UpdateSwitchState(entityId, true);
                PluginLog.Info($"{LogPrefix} Updated local state: {entityId} turned ON");
            }

            if (!onSuccess)
            {
                var friendlyName = entityId; // Could be enhanced to get friendly name
                this.Plugin.OnPluginStatusChanged(PluginStatus.Error,
                    $"Failed to turn on switch {friendlyName}");
            }

            return onSuccess;
        }

        /// <summary>
        /// Populates the area dropdown with areas that contain switches.
        /// PERFORMANCE FIX: Cache-first approach - populate immediately from cache if available, then refresh in background.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event arguments containing dropdown information.</param>
        private void OnListboxItemsRequested(Object? sender, ActionEditorListboxItemsRequestedEventArgs e)
        {
            if (!e.ControlName.EqualsNoCase(ControlArea))
            {
                return;
            }

            PluginLog.Info($"{LogPrefix} ListboxItemsRequested for areas - using CACHE-FIRST approach for instant population");

            try
            {
                // PERFORMANCE FIX: Check cache FIRST and populate immediately if available
                if (this._areaIdToName.Any() && this._entityToAreaId.Any())
                {
                    PluginLog.Info($"{LogPrefix} Cache available - populating list IMMEDIATELY from {this._areaIdToName.Count} cached areas");
                    this.PopulateAreaListFromCache(e);

                    // Trigger background refresh to update cache (fire and forget)
                    PluginLog.Info($"{LogPrefix} Starting background refresh to update cache");
                    _ = Task.Run(async () => await this.RefreshAreaCacheAsync(e));
                    return;
                }

                // No cache available - must do full load (first time or after error)
                PluginLog.Info($"{LogPrefix} No cache available - performing full load for initial population");
                this.PerformFullAreaLoad(e);
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"{LogPrefix} Area list population failed with cache-first approach");
                e.AddItem("!error", "Error loading areas", ex.Message);
                this.Plugin.OnPluginStatusChanged(PluginStatus.Error, $"Error loading areas: {ex.Message}");
            }
        }

        /// <summary>
        /// Populates the area list immediately from cached data for instant UI response.
        /// </summary>
        /// <param name="e">Event arguments containing dropdown information.</param>
        private void PopulateAreaListFromCache(ActionEditorListboxItemsRequestedEventArgs e)
        {
            try
            {
                // Get areas that have switches (from entity->area cache)
                var areasWithSwitches = this._entityToAreaId.Values
                    .Where(areaId => !String.IsNullOrEmpty(areaId))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Order by area name
                var orderedAreas = areasWithSwitches
                    .Select(aid => (aid, name: this._areaIdToName.TryGetValue(aid, out var n) ? n : aid))
                    .OrderBy(t => t.name, StringComparer.CurrentCultureIgnoreCase);

                var count = 0;
                foreach (var (areaId, areaName) in orderedAreas)
                {
                    // Count switches in this area from cache
                    var switchCount = this._entityToAreaId.Values.Count(aid =>
                        String.Equals(aid, areaId, StringComparison.OrdinalIgnoreCase));

                    var displayName = $"{areaName} ({switchCount} switch{(switchCount == 1 ? "" : "es")})";
                    e.AddItem(name: areaId, displayName: displayName, description: $"Area with {switchCount} switches");
                    count++;
                }

                PluginLog.Info($"{LogPrefix} INSTANT population from cache: {count} area(s)");

                // Keep current selection
                var current = e.ActionEditorState?.GetControlValue(ControlArea) as String;
                if (!String.IsNullOrEmpty(current))
                {
                    PluginLog.Info($"{LogPrefix} Keeping current selection from cache: '{current}'");
                    e.SetSelectedItemName(current);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"{LogPrefix} Failed to populate from cache");
                // Fall back to full load if cache population fails
                this.PerformFullAreaLoad(e);
            }
        }

        /// <summary>
        /// Performs background refresh of area cache and updates the UI when complete.
        /// </summary>
        /// <param name="e">Event arguments for updating the list when refresh completes.</param>
        private async Task RefreshAreaCacheAsync(ActionEditorListboxItemsRequestedEventArgs e)
        {
            try
            {
                PluginLog.Info($"{LogPrefix} Background refresh: Starting cache update");

                // Perform full data fetch in background
                if (!await this.EnsureHaReadyAsync())
                {
                    PluginLog.Warning($"{LogPrefix} Background refresh: EnsureHaReady failed");
                    return;
                }

                if (this._dataService == null)
                {
                    PluginLog.Error($"{LogPrefix} Background refresh: DataService not available");
                    return;
                }

                // Assign to local variable to help compiler flow analysis and prevent race conditions
                var dataService = this._dataService;
                var dataParser = this._dataParser;

                // Ensure both services are available
                if (dataService == null || dataParser == null)
                {
                    PluginLog.Error($"{LogPrefix} Background refresh: DataService or DataParser not available");
                    return;
                }

                // Fetch all required data
                var (ok, json, error) = await dataService.FetchStatesAsync(CancellationToken.None);
                if (!ok || String.IsNullOrEmpty(json))
                {
                    PluginLog.Warning($"{LogPrefix} Background refresh: Failed to fetch states - {error}");
                    return;
                }

                // Initialize SwitchStateManager (dataParser is now guaranteed to be non-null)
                if (this._switchStateManager != null)
                {
                    var (success, errorMessage) = await this._switchStateManager.InitOrUpdateAsync(dataService, dataParser, CancellationToken.None);
                    if (!success)
                    {
                        PluginLog.Warning($"{LogPrefix} Background refresh: SwitchStateManager update failed - {errorMessage}");
                        return;
                    }
                }

                // Fetch registry data
                var (okEnt, entJson, errEnt) = await dataService.FetchEntityRegistryAsync(CancellationToken.None);
                var (okDev, devJson, errDev) = await dataService.FetchDeviceRegistryAsync(CancellationToken.None);
                var (okArea, areaJson, errArea) = await dataService.FetchAreaRegistryAsync(CancellationToken.None);

                // Parse data (both dataService and dataParser are now guaranteed to be non-null)
                var registryData = dataParser.ParseRegistries(devJson, entJson, areaJson);
                var switches = dataParser.ParseSwitchStates(json, registryData);

                // Update caches
                this.UpdateInternalCaches(switches, registryData);

                PluginLog.Info($"{LogPrefix} Background refresh: Cache updated with {switches.Count()} switches in {this._areaIdToName.Count} areas - ready for next dropdown open");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"{LogPrefix} Background refresh failed");
            }
        }

        /// <summary>
        /// Performs full area load when no cache is available (initial load or after errors).
        /// </summary>
        /// <param name="e">Event arguments containing dropdown information.</param>
        private void PerformFullAreaLoad(ActionEditorListboxItemsRequestedEventArgs e)
        {
            try
            {
                // STEP 1: Ensure HA connection
                if (!this.EnsureHaReadyAsync().GetAwaiter().GetResult())
                {
                    PluginLog.Warning($"{LogPrefix} Full load: EnsureHaReady failed (not connected/authenticated)");
                    if (!this.Plugin.TryGetPluginSetting(HomeAssistantPlugin.SettingBaseUrl, out var _) ||
                        !this.Plugin.TryGetPluginSetting(HomeAssistantPlugin.SettingToken, out var _))
                    {
                        e.AddItem("!not_configured", "Home Assistant not configured", "Open plugin settings");
                        this.Plugin.OnPluginStatusChanged(PluginStatus.Error, "Home Assistant URL and Token not configured");
                    }
                    else
                    {
                        e.AddItem("!not_connected", "Could not connect to Home Assistant", "Check URL/token");
                        this.Plugin.OnPluginStatusChanged(PluginStatus.Error, "Could not connect to Home Assistant");
                    }
                    return;
                }

                // STEP 2: Check DataService availability
                if (this._dataService == null)
                {
                    PluginLog.Error($"{LogPrefix} Full load: DataService not available");
                    e.AddItem("!no_service", "Data service not available", "Plugin initialization error");
                    this.Plugin.OnPluginStatusChanged(PluginStatus.Error, "Plugin initialization error");
                    return;
                }

                // Assign to local variable to help compiler flow analysis and prevent race conditions
                var dataService = this._dataService;
                var dataParser = this._dataParser;

                // Ensure both services are available
                if (dataService == null || dataParser == null)
                {
                    PluginLog.Error($"{LogPrefix} Full load: DataService or DataParser not available");
                    e.AddItem("!no_service", "Data service or parser not available", "Plugin initialization error");
                    this.Plugin.OnPluginStatusChanged(PluginStatus.Error, "Plugin initialization error");
                    return;
                }

                // STEP 3: Fetch states
                PluginLog.Info($"{LogPrefix} Full load: Fetching states using modern service architecture");
                var (ok, json, error) = dataService.FetchStatesAsync(CancellationToken.None).GetAwaiter().GetResult();
                PluginLog.Info($"{LogPrefix} Full load: FetchStatesAsync ok={ok} error='{error}' bytes={json?.Length ?? 0}");

                if (!ok || String.IsNullOrEmpty(json))
                {
                    e.AddItem("!no_states", $"Failed to fetch states: {error ?? "unknown"}", "Check connection");
                    this.Plugin.OnPluginStatusChanged(PluginStatus.Error, $"Failed to fetch entity states error: {error}");
                    return;
                }

                // STEP 4: Initialize SwitchStateManager (dataService and dataParser are now guaranteed to be non-null)
                if (this._switchStateManager != null)
                {
                    var (success, errorMessage) = this._switchStateManager.InitOrUpdateAsync(dataService, dataParser, CancellationToken.None).GetAwaiter().GetResult();
                    if (!success)
                    {
                        PluginLog.Warning($"{LogPrefix} Full load: SwitchStateManager.InitOrUpdateAsync failed: {errorMessage}");
                        this.Plugin.OnPluginStatusChanged(PluginStatus.Error, $"Failed to load switch data: {errorMessage}");
                        e.AddItem("!init_failed", "Failed to load switches", errorMessage ?? "Check connection to Home Assistant");
                        return;
                    }
                }

                // STEP 5: Fetch registry data
                PluginLog.Info($"{LogPrefix} Full load: Fetching registry data for area information");
                var (okEnt, entJson, errEnt) = dataService.FetchEntityRegistryAsync(CancellationToken.None).GetAwaiter().GetResult();
                var (okDev, devJson, errDev) = dataService.FetchDeviceRegistryAsync(CancellationToken.None).GetAwaiter().GetResult();
                var (okArea, areaJson, errArea) = dataService.FetchAreaRegistryAsync(CancellationToken.None).GetAwaiter().GetResult();

                // STEP 6: Parse data (both dataService and dataParser are now guaranteed to be non-null)
                var registryData = dataParser.ParseRegistries(devJson, entJson, areaJson);
                var switches = dataParser.ParseSwitchStates(json, registryData);

                // STEP 7: Update caches
                this.UpdateInternalCaches(switches, registryData);

                // STEP 8: Populate list from fresh data
                var areaIds = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
                foreach (var switchEntity in switches)
                {
                    if (!String.IsNullOrEmpty(switchEntity.AreaId))
                    {
                        areaIds.Add(switchEntity.AreaId);
                    }
                }

                var orderedAreas = areaIds
                    .Select(aid => (aid, name: this._areaIdToName.TryGetValue(aid, out var n) ? n : aid))
                    .OrderBy(t => t.name, StringComparer.CurrentCultureIgnoreCase);

                var count = 0;
                foreach (var (areaId, areaName) in orderedAreas)
                {
                    var switchesInArea = switches.Where(s => String.Equals(s.AreaId, areaId, StringComparison.OrdinalIgnoreCase));
                    var switchCount = switchesInArea.Count();

                    var displayName = $"{areaName} ({switchCount} switch{(switchCount == 1 ? "" : "es")})";
                    e.AddItem(name: areaId, displayName: displayName, description: $"Area with {switchCount} switches");
                    count++;
                }

                PluginLog.Info($"{LogPrefix} Full load: List populated with {count} area(s)");

                if (count > 0)
                {
                    this.Plugin.OnPluginStatusChanged(PluginStatus.Normal, $"Successfully loaded {count} areas with switches");
                }
                else
                {
                    e.AddItem("!no_areas", "No areas with switches found", "Check Home Assistant configuration");
                    this.Plugin.OnPluginStatusChanged(PluginStatus.Warning, "No areas with switches found");
                }

                // Keep current selection
                var current = e.ActionEditorState?.GetControlValue(ControlArea) as String;
                if (!String.IsNullOrEmpty(current))
                {
                    PluginLog.Info($"{LogPrefix} Full load: Keeping current selection: '{current}'");
                    e.SetSelectedItemName(current);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, $"{LogPrefix} Full load failed");
                e.AddItem("!error", "Error loading areas", ex.Message);
                this.Plugin.OnPluginStatusChanged(PluginStatus.Error, $"Error loading areas: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates internal caches from services - follows the successful HomeAssistantLightsDynamicFolder pattern.
        /// Key difference: Extracts areas from switches data, not from registry-only data.
        /// </summary>
        /// <param name="switches">Switch data containing area assignments.</param>
        /// <param name="registryData">Registry data for area names.</param>
        private void UpdateInternalCaches(IEnumerable<SwitchData> switches, ParsedRegistryData registryData)
        {
            // Clear existing UI data (following HomeAssistantLightsDynamicFolder pattern)
            this._entityToAreaId.Clear();
            this._areaIdToName.Clear();

            PluginLog.Debug($"{LogPrefix} UpdateInternalCaches: Clearing area caches, following successful HomeAssistantLightsDynamicFolder pattern");

            // Update from registry data - area ID to name mapping
            foreach (var (areaId, areaName) in registryData.AreaIdToName)
            {
                this._areaIdToName[areaId] = areaName;
            }

            // CRITICAL: Extract areas FROM switches data (the key working pattern)
            foreach (var switchEntity in switches)
            {
                // Map entity to area (this is where areas come from - switches data, not registry!)
                this._entityToAreaId[switchEntity.EntityId] = switchEntity.AreaId;
            }

            var switchCount = switches.Count();
            var areaCount = registryData.AreaIdToName.Count;
            PluginLog.Info($"{LogPrefix} Updated internal caches: {switchCount} switches, {areaCount} areas - areas extracted from switches data");
        }
    }
}