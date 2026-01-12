namespace Loupedeck.HomeAssistantPlugin.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    using Loupedeck.HomeAssistantPlugin.Models;

    /// <summary>
    /// Implementation of ICoverStateManager for managing cover states and capabilities
    /// </summary>
    internal class CoverStateManager : ICoverStateManager
    {
        // Constants
        private const String UnassignedAreaId = "!unassigned";
        private const String UnassignedAreaName = "(No area)";

        // Internal state dictionaries
        private readonly Dictionary<String, String> _stateByEntity =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<String, Int32?> _positionByEntity =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<String, Int32?> _tiltPositionByEntity =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<String, CoverCaps> _capsByEntity =
            new(StringComparer.OrdinalIgnoreCase);

        // Full CoverData storage for enhanced functionality
        private readonly Dictionary<String, CoverData> _coverData =
            new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Updates the state for a cover.
        /// </summary>
        /// <param name="entityId">Cover entity ID.</param>
        /// <param name="state">The cover state (open, closed, opening, closing, stopped, unknown).</param>
        public void UpdateCoverState(String entityId, String state)
        {
            PluginLog.Verbose(() => $"[CoverStateManager] UpdateCoverState: {entityId} state={state}");
            this._stateByEntity[entityId] = state ?? "unknown";
            
            // Update the CoverData if it exists
            if (this._coverData.TryGetValue(entityId, out var coverData))
            {
                var isOn = String.Equals(state, "open", StringComparison.OrdinalIgnoreCase);
                var updatedCover = coverData with { State = state, IsOn = isOn };
                this._coverData[entityId] = updatedCover;
            }
        }

        /// <summary>
        /// Updates the position for a cover.
        /// </summary>
        /// <param name="entityId">Cover entity ID.</param>
        /// <param name="position">Position value (0-100).</param>
        public void UpdateCoverPosition(String entityId, Int32? position)
        {
            PluginLog.Verbose(() => $"[CoverStateManager] UpdateCoverPosition: {entityId} position={position}");
            this._positionByEntity[entityId] = position;

            // Update the CoverData if it exists
            if (this._coverData.TryGetValue(entityId, out var coverData))
            {
                var updatedCover = coverData with { Position = position };
                this._coverData[entityId] = updatedCover;
            }
        }

        /// <summary>
        /// Updates the tilt position for a cover.
        /// </summary>
        /// <param name="entityId">Cover entity ID.</param>
        /// <param name="tiltPosition">Tilt position value (0-100).</param>
        public void UpdateCoverTiltPosition(String entityId, Int32? tiltPosition)
        {
            PluginLog.Verbose(() => $"[CoverStateManager] UpdateCoverTiltPosition: {entityId} tiltPosition={tiltPosition}");
            this._tiltPositionByEntity[entityId] = tiltPosition;

            // Update the CoverData if it exists
            if (this._coverData.TryGetValue(entityId, out var coverData))
            {
                var updatedCover = coverData with { TiltPosition = tiltPosition };
                this._coverData[entityId] = updatedCover;
            }
        }

        /// <summary>
        /// Checks if a cover is currently open.
        /// </summary>
        /// <param name="entityId">Cover entity ID.</param>
        /// <returns>True if the cover is open.</returns>
        public Boolean IsCoverOpen(String entityId)
        {
            var state = this.GetCoverState(entityId);
            return String.Equals(state, "open", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if a cover is currently closed.
        /// </summary>
        /// <param name="entityId">Cover entity ID.</param>
        /// <returns>True if the cover is closed.</returns>
        public Boolean IsCoverClosed(String entityId)
        {
            var state = this.GetCoverState(entityId);
            return String.Equals(state, "closed", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if a cover is currently moving.
        /// </summary>
        /// <param name="entityId">Cover entity ID.</param>
        /// <returns>True if the cover is moving.</returns>
        public Boolean IsCoverMoving(String entityId)
        {
            var state = this.GetCoverState(entityId);
            return String.Equals(state, "opening", StringComparison.OrdinalIgnoreCase) ||
                   String.Equals(state, "closing", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Gets the current state of a cover.
        /// </summary>
        /// <param name="entityId">Cover entity ID.</param>
        /// <returns>Current cover state.</returns>
        public String GetCoverState(String entityId)
        {
            return this._stateByEntity.TryGetValue(entityId, out var state) ? state : "unknown";
        }

        /// <summary>
        /// Gets the current position of a cover.
        /// </summary>
        /// <param name="entityId">Cover entity ID.</param>
        /// <returns>Current position (0-100) or null if not supported/available.</returns>
        public Int32? GetCoverPosition(String entityId)
        {
            return this._positionByEntity.TryGetValue(entityId, out var position) ? position : null;
        }

        /// <summary>
        /// Gets the current tilt position of a cover.
        /// </summary>
        /// <param name="entityId">Cover entity ID.</param>
        /// <returns>Current tilt position (0-100) or null if not supported/available.</returns>
        public Int32? GetCoverTiltPosition(String entityId)
        {
            return this._tiltPositionByEntity.TryGetValue(entityId, out var tiltPosition) ? tiltPosition : null;
        }

        /// <summary>
        /// Sets the capabilities for a cover entity.
        /// </summary>
        /// <param name="entityId">Cover entity ID.</param>
        /// <param name="caps">Cover capabilities.</param>
        public void SetCapabilities(String entityId, CoverCaps caps)
        {
            this._capsByEntity[entityId] = caps;
            PluginLog.Verbose(() => $"[CoverStateManager] Set capabilities for {entityId}: onoff={caps.OnOff}, position={caps.Position}, tiltPosition={caps.TiltPosition}");
        }

        /// <summary>
        /// Gets the capabilities for a cover entity.
        /// </summary>
        /// <param name="entityId">Cover entity ID.</param>
        /// <returns>Cover capabilities.</returns>
        public CoverCaps GetCapabilities(String entityId)
        {
            var result = this._capsByEntity.TryGetValue(entityId, out var caps)
                ? caps
                : new CoverCaps(true, false, false); // Safe default: open/close only

            return result;
        }

        /// <summary>
        /// Initializes cover state from parsed cover data.
        /// </summary>
        /// <param name="covers">Collection of cover data.</param>
        public void InitializeCoverStates(IEnumerable<CoverData> covers)
        {
            var existingCount = this._stateByEntity.Count;
            var updatedCount = 0;

            PluginLog.Info(() => $"[CoverStateManager] Initializing cover states for {covers.Count()} covers with {existingCount} existing cached states");

            // Clear and rebuild state
            this._capsByEntity.Clear();
            this._coverData.Clear();
            this._stateByEntity.Clear();
            this._positionByEntity.Clear();
            this._tiltPositionByEntity.Clear();

            foreach (var coverEntity in covers)
            {
                // Store full CoverData object
                this._coverData[coverEntity.EntityId] = coverEntity;

                // Update state from Home Assistant (this is current truth)
                this._stateByEntity[coverEntity.EntityId] = coverEntity.State;
                this._positionByEntity[coverEntity.EntityId] = coverEntity.Position;
                this._tiltPositionByEntity[coverEntity.EntityId] = coverEntity.TiltPosition;

                // Update capabilities from Home Assistant
                this._capsByEntity[coverEntity.EntityId] = coverEntity.Capabilities;

                updatedCount++;
                PluginLog.Verbose(() => $"[CoverStateManager] NEW cover {coverEntity.EntityId}: state={coverEntity.State}, position={coverEntity.Position}, tiltPosition={coverEntity.TiltPosition}");
            }

            PluginLog.Info(() => $"[CoverStateManager] State initialization completed: {updatedCount} covers initialized");
        }

        /// <summary>
        /// Removes an entity from all internal caches.
        /// </summary>
        /// <param name="entityId">Entity ID to remove.</param>
        public void RemoveEntity(String entityId)
        {
            this._stateByEntity.Remove(entityId);
            this._positionByEntity.Remove(entityId);
            this._tiltPositionByEntity.Remove(entityId);
            this._capsByEntity.Remove(entityId);
            this._coverData.Remove(entityId);
            PluginLog.Verbose(() => $"[CoverStateManager] Removed entity {entityId} from all caches including cover data");
        }

        /// <summary>
        /// Gets all currently tracked entity IDs.
        /// </summary>
        /// <returns>Collection of entity IDs.</returns>
        public IEnumerable<String> GetTrackedEntityIds() => this._stateByEntity.Keys.ToList();

        /// <summary>
        /// Gets all stored cover data objects.
        /// </summary>
        /// <returns>Collection of all cover data.</returns>
        public IEnumerable<CoverData> GetAllCovers() => this._coverData.Values.ToList();

        /// <summary>
        /// Gets covers in a specific area.
        /// </summary>
        /// <param name="areaId">Area ID to filter by.</param>
        /// <returns>Collection of covers in the specified area.</returns>
        public IEnumerable<CoverData> GetCoversByArea(String areaId)
        {
            return String.IsNullOrWhiteSpace(areaId)
                ? Enumerable.Empty<CoverData>()
                : this._coverData.Values
                .Where(coverEntity => String.Equals(coverEntity.AreaId, areaId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        /// <summary>
        /// Gets all unique area IDs from stored covers.
        /// </summary>
        /// <returns>Collection of distinct area IDs.</returns>
        public IEnumerable<String> GetUniqueAreaIds()
        {
            return this._coverData.Values
                .Where(coverEntity => !String.IsNullOrWhiteSpace(coverEntity.AreaId))
                .Select(coverEntity => coverEntity.AreaId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// Gets specific cover's full data.
        /// </summary>
        /// <param name="entityId">Entity ID of the cover.</param>
        /// <returns>Cover data if found, null otherwise.</returns>
        public CoverData? GetCoverData(String entityId)
        {
            return String.IsNullOrWhiteSpace(entityId) ? null : this._coverData.TryGetValue(entityId, out var coverData) ? coverData : null;
        }

        /// <summary>
        /// Gets area ID to friendly name mapping from stored covers.
        /// </summary>
        /// <returns>Dictionary mapping area IDs to friendly names.</returns>
        public Dictionary<String, String> GetAreaIdToNameMapping()
        {
            var areaMapping = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

            // Extract unique areas and their names from stored cover data
            var areaGroups = this._coverData.Values
                .Where(coverEntity => !String.IsNullOrWhiteSpace(coverEntity.AreaId))
                .GroupBy(coverEntity => coverEntity.AreaId, StringComparer.OrdinalIgnoreCase);

            foreach (var group in areaGroups)
            {
                var areaId = group.Key;
                // Use the area name from any cover in the area (they should all have the same area name)
                var firstCover = group.FirstOrDefault();
                if (firstCover != null)
                {
                    // Extract area name from device name or use area ID as fallback
                    var areaName = firstCover.AreaId; // This could be enhanced to get actual friendly names from registry
                    areaMapping[areaId] = areaName;
                }
            }

            return areaMapping;
        }

        /// <summary>
        /// Initializes or updates cover states by fetching data from Home Assistant.
        /// This method handles all the data fetching and parsing internally.
        /// </summary>
        /// <param name="dataService">Service for fetching data from Home Assistant.</param>
        /// <param name="dataParser">Service for parsing Home Assistant data.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task with success status and optional error message.</returns>
        public async Task<(Boolean Success, String? ErrorMessage)> InitOrUpdateAsync(
            IHomeAssistantDataService dataService,
            IHomeAssistantDataParser dataParser,
            CancellationToken cancellationToken = default)
        {
            try
            {
                PluginLog.Info("[CoverStateManager] InitOrUpdateAsync: Starting self-contained initialization");

                // Fetch all required data from Home Assistant APIs
                var (okStates, statesJson, errStates) = await dataService.FetchStatesAsync(cancellationToken).ConfigureAwait(false);
                if (!okStates)
                {
                    var errorMsg = $"Failed to fetch states: {errStates}";
                    PluginLog.Error($"[CoverStateManager] InitOrUpdateAsync: {errorMsg}");
                    return (false, errorMsg);
                }

                // Validate statesJson is not null even if fetch succeeded
                if (String.IsNullOrEmpty(statesJson))
                {
                    const String errorMsg = "FetchStatesAsync succeeded but returned null or empty JSON data";
                    PluginLog.Error($"[CoverStateManager] InitOrUpdateAsync: {errorMsg}");
                    return (false, errorMsg);
                }

                // Fetch registry data (optional - don't fail if these aren't available)
                var (okEnt, entJson, errEnt) = await dataService.FetchEntityRegistryAsync(cancellationToken).ConfigureAwait(false);
                var (okDev, devJson, errDev) = await dataService.FetchDeviceRegistryAsync(cancellationToken).ConfigureAwait(false);
                var (okArea, areaJson, errArea) = await dataService.FetchAreaRegistryAsync(cancellationToken).ConfigureAwait(false);

                if (!okEnt || !okDev || !okArea)
                {
                    PluginLog.Warning($"[CoverStateManager] InitOrUpdateAsync: Some registry data unavailable (ent:{okEnt}, dev:{okDev}, area:{okArea}) - continuing with basic initialization");
                }

                // Parse registry data
                var registryData = dataParser.ParseRegistries(devJson, entJson, areaJson);

                // Parse cover states using the data parser
                var covers = dataParser.ParseCoverStates(statesJson, registryData);
                PluginLog.Info($"[CoverStateManager] ParseCoverStates completed - parsed {covers.Count} covers from states JSON");

                // Initialize cover state manager with parsed covers
                this.InitializeCoverStates(covers);

                PluginLog.Info($"[CoverStateManager] InitOrUpdateAsync: Successfully initialized with {covers.Count} covers");
                return (true, null);
            }
            catch (Exception ex)
            {
                var errorMsg = $"InitOrUpdateAsync failed: {ex.Message}";
                PluginLog.Error(ex, $"[CoverStateManager] {errorMsg}");
                return (false, errorMsg);
            }
        }
    }
}