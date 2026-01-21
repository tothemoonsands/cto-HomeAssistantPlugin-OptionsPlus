namespace Loupedeck.HomeAssistantPlugin.Services
{
    using System;
    using System.Collections.Generic;

    using Loupedeck.HomeAssistantPlugin.Models;

    /// <summary>
    /// Service responsible for managing cover states and capabilities
    /// </summary>
    public interface ICoverStateManager
    {
        /// <summary>
        /// Updates the state for a cover (open, closed, opening, closing, stopped, unknown)
        /// </summary>
        /// <param name="entityId">Cover entity ID</param>
        /// <param name="state">The cover state</param>
        void UpdateCoverState(String entityId, String state);

        /// <summary>
        /// Updates the position for a cover (if position control is supported)
        /// </summary>
        /// <param name="entityId">Cover entity ID</param>
        /// <param name="position">Position value (0-100)</param>
        void UpdateCoverPosition(String entityId, Int32? position);

        /// <summary>
        /// Updates the tilt position for a cover (if tilt control is supported)
        /// </summary>
        /// <param name="entityId">Cover entity ID</param>
        /// <param name="tiltPosition">Tilt position value (0-100)</param>
        void UpdateCoverTiltPosition(String entityId, Int32? tiltPosition);

        /// <summary>
        /// Checks if a cover is currently open
        /// </summary>
        /// <param name="entityId">Cover entity ID</param>
        /// <returns>True if the cover is open</returns>
        Boolean IsCoverOpen(String entityId);

        /// <summary>
        /// Checks if a cover is currently closed
        /// </summary>
        /// <param name="entityId">Cover entity ID</param>
        /// <returns>True if the cover is closed</returns>
        Boolean IsCoverClosed(String entityId);

        /// <summary>
        /// Checks if a cover is currently moving (opening or closing)
        /// </summary>
        /// <param name="entityId">Cover entity ID</param>
        /// <returns>True if the cover is moving</returns>
        Boolean IsCoverMoving(String entityId);

        /// <summary>
        /// Gets the current state of a cover
        /// </summary>
        /// <param name="entityId">Cover entity ID</param>
        /// <returns>Current cover state</returns>
        String GetCoverState(String entityId);

        /// <summary>
        /// Gets the current position of a cover (if supported)
        /// </summary>
        /// <param name="entityId">Cover entity ID</param>
        /// <returns>Current position (0-100) or null if not supported/available</returns>
        Int32? GetCoverPosition(String entityId);

        /// <summary>
        /// Gets the current tilt position of a cover (if supported)
        /// </summary>
        /// <param name="entityId">Cover entity ID</param>
        /// <returns>Current tilt position (0-100) or null if not supported/available</returns>
        Int32? GetCoverTiltPosition(String entityId);

        /// <summary>
        /// Sets the capabilities for a cover entity
        /// </summary>
        /// <param name="entityId">Cover entity ID</param>
        /// <param name="caps">Cover capabilities</param>
        void SetCapabilities(String entityId, CoverCaps caps);

        /// <summary>
        /// Gets the capabilities for a cover entity
        /// </summary>
        /// <param name="entityId">Cover entity ID</param>
        /// <returns>Cover capabilities</returns>
        CoverCaps GetCapabilities(String entityId);

        /// <summary>
        /// Initializes cover state from parsed cover data
        /// </summary>
        /// <param name="covers">Collection of cover data</param>
        void InitializeCoverStates(IEnumerable<CoverData> covers);

        /// <summary>
        /// Initializes or updates cover states by fetching data from Home Assistant
        /// This method handles all the data fetching and parsing internally
        /// </summary>
        /// <param name="dataService">Service for fetching data from Home Assistant</param>
        /// <param name="dataParser">Service for parsing Home Assistant data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task with success status and optional error message</returns>
        System.Threading.Tasks.Task<(Boolean Success, String? ErrorMessage)> InitOrUpdateAsync(
            IHomeAssistantDataService dataService,
            IHomeAssistantDataParser dataParser,
            System.Threading.CancellationToken cancellationToken = default);

        /// <summary>
        /// Removes an entity from all internal caches
        /// </summary>
        /// <param name="entityId">Entity ID to remove</param>
        void RemoveEntity(String entityId);

        /// <summary>
        /// Gets all currently tracked entity IDs
        /// </summary>
        /// <returns>Collection of entity IDs</returns>
        IEnumerable<String> GetTrackedEntityIds();

        /// <summary>
        /// Gets all stored cover data objects
        /// </summary>
        /// <returns>Collection of all cover data</returns>
        IEnumerable<CoverData> GetAllCovers();

        /// <summary>
        /// Gets covers in a specific area
        /// </summary>
        /// <param name="areaId">Area ID to filter by</param>
        /// <returns>Collection of covers in the specified area</returns>
        IEnumerable<CoverData> GetCoversByArea(String areaId);

        /// <summary>
        /// Gets all unique area IDs from stored covers
        /// </summary>
        /// <returns>Collection of distinct area IDs</returns>
        IEnumerable<String> GetUniqueAreaIds();

        /// <summary>
        /// Gets specific cover's full data
        /// </summary>
        /// <param name="entityId">Entity ID of the cover</param>
        /// <returns>Cover data if found, null otherwise</returns>
        CoverData? GetCoverData(String entityId);

        /// <summary>
        /// Gets area ID to friendly name mapping from stored covers
        /// </summary>
        /// <returns>Dictionary mapping area IDs to friendly names</returns>
        Dictionary<String, String> GetAreaIdToNameMapping();
    }
}