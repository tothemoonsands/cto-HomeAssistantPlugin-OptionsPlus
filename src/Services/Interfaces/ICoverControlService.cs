namespace Loupedeck.HomeAssistantPlugin.Services
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Service responsible for controlling Home Assistant cover entities.
    /// Provides open/close/stop operations and optionally position control for covers.
    /// </summary>
    public interface ICoverControlService : IDisposable
    {
        /// <summary>
        /// Opens the specified cover entity.
        /// </summary>
        /// <param name="entityId">Target cover entity id (e.g., <c>cover.garage_door</c>).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns><c>true</c> if the service call succeeded; otherwise <c>false</c>.</returns>
        Task<Boolean> OpenCoverAsync(String entityId, CancellationToken ct = default);

        /// <summary>
        /// Closes the specified cover entity.
        /// </summary>
        /// <param name="entityId">Target cover entity id.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns><c>true</c> if the service call succeeded; otherwise <c>false</c>.</returns>
        Task<Boolean> CloseCoverAsync(String entityId, CancellationToken ct = default);

        /// <summary>
        /// Stops the specified cover entity (if it's currently moving).
        /// </summary>
        /// <param name="entityId">Target cover entity id.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns><c>true</c> if the service call succeeded; otherwise <c>false</c>.</returns>
        Task<Boolean> StopCoverAsync(String entityId, CancellationToken ct = default);

        /// <summary>
        /// Stops the tilt movement of the specified cover entity (if it's currently tilting).
        /// </summary>
        /// <param name="entityId">Target cover entity id.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns><c>true</c> if the service call succeeded; otherwise <c>false</c>.</returns>
        Task<Boolean> StopCoverTiltAsync(String entityId, CancellationToken ct = default);

        /// <summary>
        /// Sets the position of the specified cover entity (if position control is supported).
        /// </summary>
        /// <param name="entityId">Target cover entity id.</param>
        /// <param name="position">Position to set (0-100, where 0 is typically closed and 100 is open).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns><c>true</c> if the service call succeeded; otherwise <c>false</c>.</returns>
        Task<Boolean> SetCoverPositionAsync(String entityId, Int32 position, CancellationToken ct = default);

        /// <summary>
        /// Sets the tilt position of the specified cover entity (if tilt control is supported).
        /// </summary>
        /// <param name="entityId">Target cover entity id.</param>
        /// <param name="tiltPosition">Tilt position to set (0-100).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns><c>true</c> if the service call succeeded; otherwise <c>false</c>.</returns>
        Task<Boolean> SetCoverTiltPositionAsync(String entityId, Int32 tiltPosition, CancellationToken ct = default);
    }
}