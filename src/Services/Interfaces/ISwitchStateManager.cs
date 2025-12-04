namespace Loupedeck.HomeAssistantPlugin.Services
{
    using System;
    using System.Collections.Generic;

    using Loupedeck.HomeAssistantPlugin.Models;

    /// <summary>
    /// Service responsible for managing switch states and capabilities
    /// </summary>
    public interface ISwitchStateManager
    {
        /// <summary>
        /// Updates the on/off state for a switch
        /// </summary>
        /// <param name="entityId">Switch entity ID</param>
        /// <param name="isOn">Whether the switch is on</param>
        void UpdateSwitchState(String entityId, Boolean isOn);

        /// <summary>
        /// Checks if a switch is currently on
        /// </summary>
        /// <param name="entityId">Switch entity ID</param>
        /// <returns>True if the switch is on</returns>
        Boolean IsSwitchOn(String entityId);

        /// <summary>
        /// Sets the capabilities for a switch entity
        /// </summary>
        /// <param name="entityId">Switch entity ID</param>
        /// <param name="caps">Switch capabilities</param>
        void SetCapabilities(String entityId, SwitchCaps caps);

        /// <summary>
        /// Gets the capabilities for a switch entity
        /// </summary>
        /// <param name="entityId">Switch entity ID</param>
        /// <returns>Switch capabilities</returns>
        SwitchCaps GetCapabilities(String entityId);

        /// <summary>
        /// Initializes switch state from parsed switch data
        /// </summary>
        /// <param name="switches">Collection of switch data</param>
        void InitializeSwitchStates(IEnumerable<SwitchData> switches);

        /// <summary>
        /// Initializes or updates switch states by fetching data from Home Assistant
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
        /// Gets all stored switch data objects
        /// </summary>
        /// <returns>Collection of all switch data</returns>
        IEnumerable<SwitchData> GetAllSwitches();

        /// <summary>
        /// Gets switches in a specific area
        /// </summary>
        /// <param name="areaId">Area ID to filter by</param>
        /// <returns>Collection of switches in the specified area</returns>
        IEnumerable<SwitchData> GetSwitchesByArea(String areaId);

        /// <summary>
        /// Gets all unique area IDs from stored switches
        /// </summary>
        /// <returns>Collection of distinct area IDs</returns>
        IEnumerable<String> GetUniqueAreaIds();

        /// <summary>
        /// Gets specific switch's full data
        /// </summary>
        /// <param name="entityId">Entity ID of the switch</param>
        /// <returns>Switch data if found, null otherwise</returns>
        SwitchData? GetSwitchData(String entityId);

        /// <summary>
        /// Gets area ID to friendly name mapping from stored switches
        /// </summary>
        /// <returns>Dictionary mapping area IDs to friendly names</returns>
        Dictionary<String, String> GetAreaIdToNameMapping();
    }
}