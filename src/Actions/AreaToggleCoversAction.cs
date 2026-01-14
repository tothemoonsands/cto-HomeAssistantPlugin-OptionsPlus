namespace Loupedeck.HomeAssistantPlugin
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Loupedeck;
    using Loupedeck.HomeAssistantPlugin.Models;
    using Loupedeck.HomeAssistantPlugin.Services;

    /// <summary>
    /// Area-based action for controlling all Home Assistant covers in a selected area.
    /// Supports position and tilt position adjustments for all covers within the area.
    /// Uses individual cover capability filtering to send maximum possible settings to each cover.
    /// </summary>
    public sealed class AreaToggleCoversAction : ActionEditorCommand, IDisposable
    {
        /// <summary>
        /// Logging prefix for this action's log messages.
        /// </summary>
        private const String LogPrefix = "[AreaToggleCovers]";

        /// <summary>
        /// Home Assistant client interface for WebSocket communication.
        /// </summary>
        private IHaClient? _ha;

        /// <summary>
        /// Cover control service for position and tilt control.
        /// </summary>
        private ICoverControlService? _coverSvc;

        /// <summary>
        /// Cover state manager for tracking cover properties and capabilities.
        /// </summary>
        private ICoverStateManager? _coverStateManager;

        /// <summary>
        /// Data service for fetching Home Assistant entity states.
        /// </summary>
        private IHomeAssistantDataService? _dataService;

        /// <summary>
        /// Data parser for processing Home Assistant JSON responses.
        /// </summary>
        private IHomeAssistantDataParser? _dataParser;

        /// <summary>
        /// Capability service for analyzing cover feature support.
        /// </summary>
        private readonly CapabilityService _capSvc = new();

        /// <summary>
        /// Indicates whether this instance has been disposed.
        /// </summary>
        private Boolean _disposed = false;


        /// <summary>
        /// Control name for area selection dropdown.
        /// </summary>
        private const String ControlArea = "ha_area";

        /// <summary>
        /// Control name for position adjustment (0-100%).
        /// </summary>
        private const String ControlPosition = "ha_position";

        /// <summary>
        /// Control name for tilt position adjustment (0-100%).
        /// </summary>
        private const String ControlTiltPosition = "ha_tilt_position";

        /// <summary>
        /// Minimum position value supported by Home Assistant (0-100% range).
        /// </summary>
        private const Int32 MinPosition = 0;

        /// <summary>
        /// Maximum position value supported by Home Assistant (0-100% range).
        /// </summary>
        private const Int32 MaxPosition = 100;

        /// <summary>
        /// Minimum tilt position value (0-100% range).
        /// </summary>
        private const Int32 MinTiltPosition = 0;

        /// <summary>
        /// Maximum tilt position value (0-100% range).
        /// </summary>
        private const Int32 MaxTiltPosition = 100;

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
        /// Entity mapping: Entity ID to Area ID from covers data.
        /// </summary>
        private readonly Dictionary<String, String> _entityToAreaId = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a new instance of the <see cref="AreaToggleCoversAction"/> class.
        /// Sets up action editor controls for area selection and parameter configuration.
        /// </summary>
        public AreaToggleCoversAction()
        {
            this.Name = "HomeAssistant.AreaToggleCovers";
            this.DisplayName = "Advanced Toggle Area Covers";
            this.GroupName = "Covers";
            this.Description = "Control all covers in a Home Assistant area with position and tilt settings.";

            // Area selection dropdown (replaces individual cover selection)
            this.ActionEditor.AddControlEx(new ActionEditorListbox(ControlArea, "Area (retry if empty)"));

            // Parameter controls for cover position and tilt
            this.ActionEditor.AddControlEx(
                new ActionEditorTextbox(ControlPosition, "Position (0-100%)")
                    .SetPlaceholder("50")
            );

            this.ActionEditor.AddControlEx(
                new ActionEditorTextbox(ControlTiltPosition, "Tilt Position (0-100%)")
                    .SetPlaceholder("50")
            );

            this.ActionEditor.ListboxItemsRequested += this.OnListboxItemsRequested;

            this._icons = new IconService(new Dictionary<String, String>
            {
                { IconId.Area, "area_icon.svg" }
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
        /// Creates adapters for Home Assistant client, data services, and cover control.
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

                    // Use the singleton CoverStateManager from the main plugin
                    this._coverStateManager = haPlugin.CoverStateManager;
                    var existingCount = this._coverStateManager.GetTrackedEntityIds().Count();
                    PluginLog.Info($"{LogPrefix} Using singleton CoverStateManager with {existingCount} existing tracked entities");

                    // Initialize cover control service
                    this._coverSvc = new CoverControlService(this._ha);

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
        /// Disposes of managed resources, particularly the cover control service.
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
                this._coverSvc?.Dispose();
                this._coverSvc = null;

                // Don't dispose shared services - they're managed by the main plugin
                this._ha = null;
                this._dataService = null;
                this._dataParser = null;
                this._coverStateManager = null;

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

                // Initialize CoverStateManager first (this loads basic cover data)
                if (this._coverStateManager != null)
                {
                    var (success, errorMessage) = await this._coverStateManager.InitOrUpdateAsync(dataService, dataParser, CancellationToken.None);
                    if (!success)
                    {
                        PluginLog.Warning($"{LogPrefix} EnsureDataInitialized: CoverStateManager.InitOrUpdateAsync failed: {errorMessage}");
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

                // Get cover data from CoverStateManager (already initialized above)
                var covers = this._coverStateManager?.GetAllCovers() ?? Enumerable.Empty<CoverData>();

                // Update internal caches using the same logic as the dropdown loading
                this.UpdateInternalCaches(covers, registryData);

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
        /// Executes the area covers command with position and tilt control.
        /// Processes all covers in the selected area with position and tilt position parameters.
        /// Always applies the user's configured position and tilt settings.
        /// Uses individual cover capability filtering to send maximum possible settings to each cover.
        /// </summary>
        /// <param name="ps">Action editor parameters containing user-configured values.</param>
        /// <returns><c>true</c> if all cover operations succeeded; otherwise, <c>false</c>.</returns>
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

                // Get all available covers (from CoverStateManager)
                var allCovers = this._coverStateManager?.GetTrackedEntityIds() ?? Enumerable.Empty<String>();

                // Get covers in selected area using internal cache
                var areaCovers = allCovers.Where(entityId =>
                    this._entityToAreaId.TryGetValue(entityId, out var coverAreaId) &&
                    String.Equals(coverAreaId, selectedArea, StringComparison.OrdinalIgnoreCase)
                ).ToList();

                if (!areaCovers.Any())
                {
                    var areaName = this._areaIdToName.TryGetValue(selectedArea, out var name) ? name : selectedArea;
                    PluginLog.Warning($"{LogPrefix} No covers found in area '{areaName}'");
                    this.Plugin.OnPluginStatusChanged(PluginStatus.Error, $"No covers found in area '{areaName}'");
                    return false;
                }

                PluginLog.Info($"{LogPrefix} Processing {areaCovers.Count} covers in area '{selectedArea}'");

                // Parse control values using defined constants
                var position = this.ParseIntParameter(ps, ControlPosition, MinPosition, MaxPosition);
                var tiltPosition = this.ParseIntParameter(ps, ControlTiltPosition, MinTiltPosition, MaxTiltPosition);

                // Process covers with individual capability filtering
                var success = this.ProcessAreaCovers(areaCovers, position, tiltPosition);

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
        /// Processes all covers in the area with individual capability filtering.
        /// Key difference from AdvancedToggleCovers: processes each cover with its own capabilities instead of intersection.
        /// Always applies the user's configured position and tilt settings.
        /// </summary>
        /// <param name="areaCovers">Collection of cover entity IDs in the area.</param>
        /// <param name="position">Position value (0-100%) or null.</param>
        /// <param name="tiltPosition">Tilt position value (0-100%) or null.</param>
        /// <returns><c>true</c> if all covers processed successfully; otherwise, <c>false</c>.</returns>
        private Boolean ProcessAreaCovers(IEnumerable<String> areaCovers, Int32? position, Int32? tiltPosition)
        {
            PluginLog.Info($"{LogPrefix} Processing {areaCovers.Count()} area covers with user-configured settings: position={position}%, tiltPosition={tiltPosition}%");

            var success = true;

            foreach (var entityId in areaCovers)
            {
                try
                {
                    // Get INDIVIDUAL capabilities for this specific cover
                    var individualCaps = this._coverStateManager?.GetCapabilities(entityId)
                        ?? new CoverCaps(true, false, false);

                    // Process this cover with ITS OWN capabilities
                    success &= this.ProcessSingleCover(entityId, individualCaps, position, tiltPosition);
                }
                catch (Exception ex)
                {
                    PluginLog.Error(ex, $"{LogPrefix} Failed to process cover {entityId}");
                    success = false;
                }
            }

            return success;
        }

        /// <summary>
        /// Parses and validates an integer parameter from action editor parameters.
        /// Clamps the value to the specified range and logs warnings for invalid inputs.
        /// </summary>
        /// <param name="ps">Action editor parameters.</param>
        /// <param name="controlName">Name of the control to parse.</param>
        /// <param name="min">Minimum allowed value (inclusive).</param>
        /// <param name="max">Maximum allowed value (inclusive).</param>
        /// <returns>Parsed and clamped integer value, or <c>null</c> if parsing failed or value was empty.</returns>
        private Int32? ParseIntParameter(ActionEditorActionParameters ps, String controlName, Int32 min, Int32 max)
        {
            if (!ps.TryGetString(controlName, out var valueStr) || String.IsNullOrWhiteSpace(valueStr))
            {
                return null;
            }

            if (Int32.TryParse(valueStr, out var value))
            {
                var clamped = Math.Clamp(value, min, max);
                if (clamped != value)
                {
                    PluginLog.Debug($"{LogPrefix} Parameter {controlName}: {value} clamped to {clamped} (range {min}-{max})");
                }
                return clamped;
            }

            PluginLog.Warning($"{LogPrefix} Parameter {controlName}: failed to parse '{valueStr}' as integer");
            return null;
        }

        /// <summary>
        /// Processes a single cover with its individual capabilities.
        /// This is the core difference from AdvancedToggleCovers - uses individual capabilities instead of intersection.
        /// Always applies the user's configured position and tilt settings.
        /// </summary>
        /// <param name="entityId">Cover entity ID.</param>
        /// <param name="individualCaps">Individual capabilities of this specific cover.</param>
        /// <param name="position">Position value or null.</param>
        /// <param name="tiltPosition">Tilt position value or null.</param>
        /// <returns><c>true</c> if cover processed successfully; otherwise, <c>false</c>.</returns>
        private Boolean ProcessSingleCover(String entityId, CoverCaps individualCaps, Int32? position, Int32? tiltPosition)
        {
            PluginLog.Info($"{LogPrefix} Processing cover: {entityId}");
            PluginLog.Info($"{LogPrefix} Individual capabilities: onoff={individualCaps.OnOff} position={individualCaps.Position} tiltPosition={individualCaps.TiltPosition}");
            PluginLog.Info($"{LogPrefix} Input parameters: position={position}% tiltPosition={tiltPosition}%");

            if (this._coverSvc == null)
            {
                PluginLog.Error($"{LogPrefix} ProcessSingleCover: CoverControlService not available");
                return false;
            }

            PluginLog.Info($"{LogPrefix} Applying user-configured settings to cover {entityId}");

            var overallSuccess = true;
            var anyActionTaken = false;

            // Set position if THIS cover supports it and parameter is specified
            if (position.HasValue && individualCaps.Position)
            {
                var pos = Math.Clamp(position.Value, MinPosition, MaxPosition);
                var posSuccess = this._coverSvc.SetCoverPositionAsync(entityId, pos).GetAwaiter().GetResult();
                PluginLog.Info($"{LogPrefix} HA SERVICE CALL: set_cover_position entity_id={entityId} position={pos} -> success={posSuccess}");
                overallSuccess &= posSuccess;
                anyActionTaken = true;

                if (posSuccess && this._coverStateManager != null)
                {
                    // Update local state
                    this._coverStateManager.UpdateCoverPosition(entityId, pos);
                    this._coverStateManager.UpdateCoverState(entityId, pos == 0 ? "closed" : "open");
                    PluginLog.Info($"{LogPrefix} Updated local state: {entityId} position={pos}%");
                }
            }
            else if (position.HasValue && !individualCaps.Position)
            {
                PluginLog.Info($"{LogPrefix} Skipping position for {entityId} - not supported");
            }

            // Set tilt position if THIS cover supports it and parameter is specified
            if (tiltPosition.HasValue && individualCaps.TiltPosition)
            {
                var tiltPos = Math.Clamp(tiltPosition.Value, MinTiltPosition, MaxTiltPosition);
                var tiltSuccess = this._coverSvc.SetCoverTiltPositionAsync(entityId, tiltPos).GetAwaiter().GetResult();
                PluginLog.Info($"{LogPrefix} HA SERVICE CALL: set_cover_tilt_position entity_id={entityId} tilt_position={tiltPos} -> success={tiltSuccess}");
                overallSuccess &= tiltSuccess;
                anyActionTaken = true;

                if (tiltSuccess && this._coverStateManager != null)
                {
                    // Update local state
                    this._coverStateManager.UpdateCoverTiltPosition(entityId, tiltPos);
                    PluginLog.Info($"{LogPrefix} Updated local state: {entityId} tilt_position={tiltPos}%");
                }
            }
            else if (tiltPosition.HasValue && !individualCaps.TiltPosition)
            {
                PluginLog.Info($"{LogPrefix} Skipping tilt position for {entityId} - not supported");
            }

            // If no specific parameters were provided or supported, just open the cover
            if (!anyActionTaken && individualCaps.OnOff)
            {
                PluginLog.Info($"{LogPrefix} No position/tilt parameters specified or supported, opening cover {entityId}");
                var openSuccess = this._coverSvc.OpenCoverAsync(entityId).GetAwaiter().GetResult();
                PluginLog.Info($"{LogPrefix} HA SERVICE CALL: open_cover entity_id={entityId} -> success={openSuccess}");
                overallSuccess = openSuccess;

                if (openSuccess && this._coverStateManager != null)
                {
                    // Update local state - cover is now open
                    this._coverStateManager.UpdateCoverState(entityId, "open");
                    this._coverStateManager.UpdateCoverPosition(entityId, 100);
                    PluginLog.Info($"{LogPrefix} Updated local state: {entityId} opened");
                }
            }

            if (!overallSuccess)
            {
                var friendlyName = entityId; // Could be enhanced to get friendly name
                this.Plugin.OnPluginStatusChanged(PluginStatus.Error,
                    $"Failed to control cover {friendlyName}");
            }

            return overallSuccess;
        }

        /// <summary>
        /// Populates the area dropdown with areas that contain covers.
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
                // Get areas that have covers (from entity->area cache)
                var areasWithCovers = this._entityToAreaId.Values
                    .Where(areaId => !String.IsNullOrEmpty(areaId))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                // Order by area name
                var orderedAreas = areasWithCovers
                    .Select(aid => (aid, name: this._areaIdToName.TryGetValue(aid, out var n) ? n : aid))
                    .OrderBy(t => t.name, StringComparer.CurrentCultureIgnoreCase);

                var count = 0;
                foreach (var (areaId, areaName) in orderedAreas)
                {
                    // Count covers in this area from cache
                    var coverCount = this._entityToAreaId.Values.Count(aid =>
                        String.Equals(aid, areaId, StringComparison.OrdinalIgnoreCase));

                    var displayName = $"{areaName} ({coverCount} cover{(coverCount == 1 ? "" : "s")})";
                    e.AddItem(name: areaId, displayName: displayName, description: $"Area with {coverCount} covers");
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

                // Initialize CoverStateManager (dataParser is now guaranteed to be non-null)
                if (this._coverStateManager != null)
                {
                    var (success, errorMessage) = await this._coverStateManager.InitOrUpdateAsync(dataService, dataParser, CancellationToken.None);
                    if (!success)
                    {
                        PluginLog.Warning($"{LogPrefix} Background refresh: CoverStateManager update failed - {errorMessage}");
                        return;
                    }
                }

                // Fetch registry data
                var (okEnt, entJson, errEnt) = await dataService.FetchEntityRegistryAsync(CancellationToken.None);
                var (okDev, devJson, errDev) = await dataService.FetchDeviceRegistryAsync(CancellationToken.None);
                var (okArea, areaJson, errArea) = await dataService.FetchAreaRegistryAsync(CancellationToken.None);

                // Parse data (both dataService and dataParser are now guaranteed to be non-null)
                var registryData = dataParser.ParseRegistries(devJson, entJson, areaJson);
                var covers = dataParser.ParseCoverStates(json, registryData);

                // Update caches
                this.UpdateInternalCaches(covers, registryData);

                PluginLog.Info($"{LogPrefix} Background refresh: Cache updated with {covers.Count()} covers in {this._areaIdToName.Count} areas - ready for next dropdown open");
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

                // STEP 4: Initialize CoverStateManager (dataService and dataParser are now guaranteed to be non-null)
                if (this._coverStateManager != null)
                {
                    var (success, errorMessage) = this._coverStateManager.InitOrUpdateAsync(dataService, dataParser, CancellationToken.None).GetAwaiter().GetResult();
                    if (!success)
                    {
                        PluginLog.Warning($"{LogPrefix} Full load: CoverStateManager.InitOrUpdateAsync failed: {errorMessage}");
                        this.Plugin.OnPluginStatusChanged(PluginStatus.Error, $"Failed to load cover data: {errorMessage}");
                        e.AddItem("!init_failed", "Failed to load covers", errorMessage ?? "Check connection to Home Assistant");
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
                var covers = dataParser.ParseCoverStates(json, registryData);

                // STEP 7: Update caches
                this.UpdateInternalCaches(covers, registryData);

                // STEP 8: Populate list from fresh data
                var areaIds = new HashSet<String>(StringComparer.OrdinalIgnoreCase);
                foreach (var cover in covers)
                {
                    if (!String.IsNullOrEmpty(cover.AreaId))
                    {
                        areaIds.Add(cover.AreaId);
                    }
                }

                var orderedAreas = areaIds
                    .Select(aid => (aid, name: this._areaIdToName.TryGetValue(aid, out var n) ? n : aid))
                    .OrderBy(t => t.name, StringComparer.CurrentCultureIgnoreCase);

                var count = 0;
                foreach (var (areaId, areaName) in orderedAreas)
                {
                    var coversInArea = covers.Where(c => String.Equals(c.AreaId, areaId, StringComparison.OrdinalIgnoreCase));
                    var coverCount = coversInArea.Count();

                    var displayName = $"{areaName} ({coverCount} cover{(coverCount == 1 ? "" : "s")})";
                    e.AddItem(name: areaId, displayName: displayName, description: $"Area with {coverCount} covers");
                    count++;
                }

                PluginLog.Info($"{LogPrefix} Full load: List populated with {count} area(s)");

                if (count > 0)
                {
                    this.Plugin.OnPluginStatusChanged(PluginStatus.Normal, $"Successfully loaded {count} areas with covers");
                }
                else
                {
                    e.AddItem("!no_areas", "No areas with covers found", "Check Home Assistant configuration");
                    this.Plugin.OnPluginStatusChanged(PluginStatus.Warning, "No areas with covers found");
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
        /// Updates internal caches from services - follows the successful HomeAssistantCoversDynamicFolder pattern.
        /// Key difference: Extracts areas from covers data, not from registry-only data.
        /// </summary>
        /// <param name="covers">Cover data containing area assignments.</param>
        /// <param name="registryData">Registry data for area names.</param>
        private void UpdateInternalCaches(IEnumerable<CoverData> covers, ParsedRegistryData registryData)
        {
            // Clear existing UI data (following HomeAssistantCoversDynamicFolder pattern)
            this._entityToAreaId.Clear();
            this._areaIdToName.Clear();

            PluginLog.Debug($"{LogPrefix} UpdateInternalCaches: Clearing area caches, following successful HomeAssistantCoversDynamicFolder pattern");

            // Update from registry data - area ID to name mapping
            foreach (var (areaId, areaName) in registryData.AreaIdToName)
            {
                this._areaIdToName[areaId] = areaName;
            }

            // CRITICAL: Extract areas FROM covers data (the key working pattern)
            foreach (var cover in covers)
            {
                // Map entity to area (this is where areas come from - covers data, not registry!)
                this._entityToAreaId[cover.EntityId] = cover.AreaId;
            }

            var coverCount = covers.Count();
            var areaCount = registryData.AreaIdToName.Count;
            PluginLog.Info($"{LogPrefix} Updated internal caches: {coverCount} covers, {areaCount} areas - areas extracted from covers data");
        }
    }
}