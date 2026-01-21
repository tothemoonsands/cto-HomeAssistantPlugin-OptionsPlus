namespace Loupedeck.HomeAssistantPlugin.Models
{
    using System;

    /// <summary>
    /// Represents comprehensive data for a single cover entity from Home Assistant
    /// </summary>
    public record CoverData(
        String EntityId,
        String FriendlyName,
        String State,
        Boolean IsOn,
        String? DeviceId,
        String DeviceName,
        String Manufacturer,
        String Model,
        String AreaId,
        CoverCaps Capabilities,
        Int32? Position,
        Int32? TiltPosition
    )
    {
        /// <summary>
        /// Gets whether the cover is in an open state.
        /// Maps IsOn to cover-specific semantics where true indicates open.
        /// </summary>
        public Boolean IsOpen => this.IsOn;

        /// <summary>
        /// Gets whether the cover is in a closed state.
        /// </summary>
        public Boolean IsClosed => String.Equals(this.State, "closed", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets whether the cover is currently opening.
        /// </summary>
        public Boolean IsOpening => String.Equals(this.State, "opening", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets whether the cover is currently closing.
        /// </summary>
        public Boolean IsClosing => String.Equals(this.State, "closing", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets whether the cover is in a stopped state (not moving).
        /// </summary>
        public Boolean IsStopped => String.Equals(this.State, "stopped", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets whether the cover is in an unknown state.
        /// </summary>
        public Boolean IsUnknown => String.Equals(this.State, "unknown", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets whether the cover is currently in motion (opening or closing).
        /// </summary>
        public Boolean IsMoving => this.IsOpening || this.IsClosing;

        /// <summary>
        /// Gets whether the cover is stationary (not in motion).
        /// </summary>
        public Boolean IsStationary => !this.IsMoving;

        /// <summary>
        /// Gets whether the cover supports position control.
        /// </summary>
        public Boolean HasPositionControl => this.Capabilities.Position;

        /// <summary>
        /// Gets whether the cover supports tilt position control.
        /// </summary>
        public Boolean HasTiltControl => this.Capabilities.TiltPosition;

        /// <summary>
        /// Gets the current position as a percentage (0-100), or null if position is not available.
        /// For most covers: 0 = closed, 100 = open.
        /// </summary>
        public Int32? PositionPercent => this.Position;

        /// <summary>
        /// Gets the current tilt position as a percentage (0-100), or null if tilt position is not available.
        /// </summary>
        public Int32? TiltPositionPercent => this.TiltPosition;
    };
}