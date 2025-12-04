namespace Loupedeck.HomeAssistantPlugin.Services
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Service responsible for controlling Home Assistant switch entities.
    /// Provides simple on/off/toggle operations for switches.
    /// </summary>
    public interface ISwitchControlService : IDisposable
    {
        /// <summary>
        /// Turns the specified switch entity on, optionally including a JSON payload
        /// (for switches, this is typically not needed but included for consistency).
        /// </summary>
        /// <param name="entityId">Target switch entity id (e.g., <c>switch.kitchen_light</c>).</param>
        /// <param name="data">Optional JSON payload to include with the service call.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns><c>true</c> if the service call succeeded; otherwise <c>false</c>.</returns>
        Task<Boolean> TurnOnAsync(String entityId, JsonElement? data = null, CancellationToken ct = default);

        /// <summary>
        /// Turns the specified switch entity off.
        /// </summary>
        /// <param name="entityId">Target switch entity id.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns><c>true</c> if the service call succeeded; otherwise <c>false</c>.</returns>
        Task<Boolean> TurnOffAsync(String entityId, CancellationToken ct = default);

        /// <summary>
        /// Toggles the specified switch entity on/off.
        /// </summary>
        /// <param name="entityId">Target switch entity id.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns><c>true</c> if the service call succeeded; otherwise <c>false</c>.</returns>
        Task<Boolean> ToggleAsync(String entityId, CancellationToken ct = default);
    }
}