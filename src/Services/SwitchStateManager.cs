namespace Loupedeck.HomeAssistantPlugin.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Loupedeck.HomeAssistantPlugin.Models;

    /// <summary>
    /// Implementation of ISwitchStateManager for managing switch states and capabilities
    /// </summary>
    internal class SwitchStateManager : ISwitchStateManager
    {
        // Constants
        private const String UnassignedAreaId = "!unassigned";
        private const String UnassignedAreaName = "(No area)";

        // Internal state dictionaries
        private readonly Dictionary<String, Boolean> _isOnByEntity =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<String, SwitchCaps> _capsByEntity =
            new(StringComparer.OrdinalIgnoreCase);

        // Full SwitchData storage for enhanced functionality
        private readonly Dictionary<String, SwitchData> _switchData =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Updates the on/off state for a switch.
        /// </summary>
        /// <param name="entityId">Switch entity ID.</param>
        /// <param name="isOn">Whether the switch is on.</param>
        public void UpdateSwitchState(String entityId, Boolean isOn)
        {
            PluginLog.Verbose(() => $"[SwitchStateManager] UpdateSwitchState: {entityId} isOn={isOn}");
            this._isOnByEntity[entityId] = isOn;
        }

        /// <summary>
        /// Checks if a switch is currently on.
        /// </summary>
        /// <param name="entityId">Switch entity ID.</param>
        /// <returns>True if the switch is on.</returns>
        public Boolean IsSwitchOn(String entityId) => this._isOnByEntity.TryGetValue(entityId, out var isOn) && isOn;

        /// <summary>
        /// Sets the capabilities for a switch entity.
        /// </summary>
        /// <param name="entityId">Switch entity ID.</param>
        /// <param name="caps">Switch capabilities.</param>
        public void SetCapabilities(String entityId, SwitchCaps caps)
        {
            this._capsByEntity[entityId] = caps;
            PluginLog.Verbose(() => $"[SwitchStateManager] Set capabilities for {entityId}: onoff={caps.OnOff}");
        }

        /// <summary>
        /// Gets the capabilities for a switch entity.
        /// </summary>
        /// <param name="entityId">Switch entity ID.</param>
        /// <returns>Switch capabilities.</returns>
        public SwitchCaps GetCapabilities(String entityId)
        {
            var result = this._capsByEntity.TryGetValue(entityId, out var caps)
                ? caps
                : new SwitchCaps(true); // Safe default: on/off only

            return result;
        }

        /// <summary>
        /// Initializes switch state from parsed switch data.
        /// </summary>
        /// <param name="switches">Collection of switch data.</param>
        public void InitializeSwitchStates(IEnumerable<SwitchData> switches)
        {
            var existingCount = this._isOnByEntity.Count;
            var updatedCount = 0;

            PluginLog.Info(() => $"[SwitchStateManager] Initializing switch states for {switches.Count()} switches with {existingCount} existing cached states");

            // Clear and rebuild state
            this._capsByEntity.Clear();
            this._switchData.Clear();
            this._isOnByEntity.Clear();

            foreach (var switchEntity in switches)
            {
                // Store full SwitchData object
                this._switchData[switchEntity.EntityId] = switchEntity;

                // Update on/off state from Home Assistant (this is current truth)
                this._isOnByEntity[switchEntity.EntityId] = switchEntity.IsOn;

                // Update capabilities from Home Assistant
                this._capsByEntity[switchEntity.EntityId] = switchEntity.Capabilities;

                updatedCount++;
                PluginLog.Verbose(() => $"[SwitchStateManager] NEW switch {switchEntity.EntityId}: isOn={switchEntity.IsOn}");
            }

            PluginLog.Info(() => $"[SwitchStateManager] State initialization completed: {updatedCount} switches initialized");
        }

        /// <summary>
        /// Removes an entity from all internal caches
        /// </summary>
        /// <param name="entityId">Entity ID to remove</param>
        public void RemoveEntity(String entityId)
        {
            this._isOnByEntity.Remove(entityId);
            this._capsByEntity.Remove(entityId);
            this._switchData.Remove(entityId);
            PluginLog.Verbose(() => $"[SwitchStateManager] Removed entity {entityId} from all caches including switch data");
        }

        /// <summary>
        /// Gets all currently tracked entity IDs
        /// </summary>
        /// <returns>Collection of entity IDs</returns>
        public IEnumerable<String> GetTrackedEntityIds() => this._isOnByEntity.Keys.ToList();

        /// <summary>
        /// Gets all stored switch data objects
        /// </summary>
        /// <returns>Collection of all switch data</returns>
        public IEnumerable<SwitchData> GetAllSwitches() => this._switchData.Values.ToList();

        /// <summary>
        /// Gets switches in a specific area
        /// </summary>
        /// <param name="areaId">Area ID to filter by</param>
        /// <returns>Collection of switches in the specified area</returns>
        public IEnumerable<SwitchData> GetSwitchesByArea(String areaId)
        {
            return String.IsNullOrWhiteSpace(areaId)
                ? Enumerable.Empty<SwitchData>()
                : this._switchData.Values
                .Where(switchEntity => String.Equals(switchEntity.AreaId, areaId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Gets all unique area IDs from stored switches
        /// </summary>
        /// <returns>Collection of distinct area IDs</returns>
        public IEnumerable<String> GetUniqueAreaIds()
        {
            return this._switchData.Values
                .Where(switchEntity => !String.IsNullOrWhiteSpace(switchEntity.AreaId))
                .Select(switchEntity => switchEntity.AreaId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Gets specific switch's full data
        /// </summary>
        /// <param name="entityId">Entity ID of the switch</param>
        /// <returns>Switch data if found, null otherwise</returns>
        public SwitchData? GetSwitchData(String entityId)
        {
            return String.IsNullOrWhiteSpace(entityId) ? null : this._switchData.TryGetValue(entityId, out var switchData) ? switchData : null;
        }

        /// <summary>
        /// Gets area ID to friendly name mapping from stored switches
        /// </summary>
        /// <returns>Dictionary mapping area IDs to friendly names</returns>
        public Dictionary<String, String> GetAreaIdToNameMapping()
        {
            var areaMapping = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

            // Extract unique areas and their names from stored switch data
            var areaGroups = this._switchData.Values
                .Where(switchEntity => !String.IsNullOrWhiteSpace(switchEntity.AreaId))
                .GroupBy(switchEntity => switchEntity.AreaId, StringComparer.OrdinalIgnoreCase);

            foreach (var group in areaGroups)
            {
                var areaId = group.Key;
                // Use the area name from any switch in the area (they should all have the same area name)
                var firstSwitch = group.FirstOrDefault();
                if (firstSwitch != null)
                {
                    // Extract area name from device name or use area ID as fallback
                    var areaName = firstSwitch.AreaId; // This could be enhanced to get actual friendly names from registry
                    areaMapping[areaId] = areaName;
                }
            }

            return areaMapping;
        }

        /// <summary>
        /// Initializes or updates switch states by fetching data from Home Assistant
        /// This method handles all the data fetching and parsing internally
        /// </summary>
        /// <param name="dataService">Service for fetching data from Home Assistant</param>
        /// <param name="dataParser">Service for parsing Home Assistant data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task with success status and optional error message</returns>
        public async Task<(Boolean Success, String? ErrorMessage)> InitOrUpdateAsync(
            IHomeAssistantDataService dataService,
            IHomeAssistantDataParser dataParser,
            CancellationToken cancellationToken = default)
        {
            try
            {
                PluginLog.Info("[SwitchStateManager] InitOrUpdateAsync: Starting self-contained initialization");

                // Fetch all required data from Home Assistant APIs
                var (okStates, statesJson, errStates) = await dataService.FetchStatesAsync(cancellationToken).ConfigureAwait(false);
                if (!okStates)
                {
                    var errorMsg = $"Failed to fetch states: {errStates}";
                    PluginLog.Error($"[SwitchStateManager] InitOrUpdateAsync: {errorMsg}");
                    return (false, errorMsg);
                }

                // Validate statesJson is not null even if fetch succeeded
                if (String.IsNullOrEmpty(statesJson))
                {
                    const String errorMsg = "FetchStatesAsync succeeded but returned null or empty JSON data";
                    PluginLog.Error($"[SwitchStateManager] InitOrUpdateAsync: {errorMsg}");
                    return (false, errorMsg);
                }

                // Fetch registry data (optional - don't fail if these aren't available)
                var (okEnt, entJson, errEnt) = await dataService.FetchEntityRegistryAsync(cancellationToken).ConfigureAwait(false);
                var (okDev, devJson, errDev) = await dataService.FetchDeviceRegistryAsync(cancellationToken).ConfigureAwait(false);
                var (okArea, areaJson, errArea) = await dataService.FetchAreaRegistryAsync(cancellationToken).ConfigureAwait(false);

                if (!okEnt || !okDev || !okArea)
                {
                    PluginLog.Warning($"[SwitchStateManager] InitOrUpdateAsync: Some registry data unavailable (ent:{okEnt}, dev:{okDev}, area:{okArea}) - continuing with basic initialization");
                }

                // Parse registry data
                var registryData = dataParser.ParseRegistries(devJson, entJson, areaJson);

                // Parse switch states - this will call the new method we'll add to the parser
                var switches = dataParser.ParseSwitchStates(statesJson, registryData);

                // Initialize switch state manager with parsed switches
                this.InitializeSwitchStates(switches);

                PluginLog.Info($"[SwitchStateManager] InitOrUpdateAsync: Successfully initialized with {switches.Count} switches");
                return (true, null);
            }
            catch (Exception ex)
            {
                var errorMsg = $"InitOrUpdateAsync failed: {ex.Message}";
                PluginLog.Error(ex, $"[SwitchStateManager] {errorMsg}");
                return (false, errorMsg);
            }
        }
    }
}