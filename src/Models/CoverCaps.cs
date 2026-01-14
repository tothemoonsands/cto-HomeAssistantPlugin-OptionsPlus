// Models/CoverCaps.cs
namespace Loupedeck.HomeAssistantPlugin.Models
{
    using System;
    using System.Text.Json;

    /// <summary>
    /// Capability model for Home Assistant covers.
    /// OnOff: device supports open/close control
    /// Position: device supports position control (0-100%)
    /// TiltPosition: device supports tilt position control (0-100%)
    /// </summary>
    public readonly record struct CoverCaps(Boolean OnOff, Boolean Position, Boolean TiltPosition)
    {
        /// <summary>
        /// Creates cover capabilities from Home Assistant attributes.
        /// Analyzes supported features to determine position and tilt support.
        /// </summary>
        /// <param name="attrs">JSON attributes from Home Assistant cover entity</param>
        /// <returns>Cover capabilities based on supported features</returns>
        public static CoverCaps FromAttributes(JsonElement attrs)
        {
            var startTime = DateTime.UtcNow;
            PluginLog.Verbose("[CoverCaps] FromAttributes() called - parsing cover capabilities from JSON attributes");

            try
            {
                // Initialize capabilities - OnOff should only be true if cover supports basic open/close
                var onoff = false;
                var position = false;
                var tiltPosition = false;

                // Check for supported_features attribute to determine capabilities
                if (attrs.TryGetProperty("supported_features", out var supportedFeaturesElement))
                {
                    if (supportedFeaturesElement.TryGetInt32(out var supportedFeatures))
                    {
                        // Home Assistant cover feature flags:
                        // SUPPORT_OPEN = 1
                        // SUPPORT_CLOSE = 2
                        // SUPPORT_SET_POSITION = 4
                        // SUPPORT_STOP = 8
                        // SUPPORT_OPEN_TILT = 16
                        // SUPPORT_CLOSE_TILT = 32
                        // SUPPORT_STOP_TILT = 64
                        // SUPPORT_SET_TILT_POSITION = 128

                        // Check if basic open/close is supported
                        onoff = (supportedFeatures & 1) != 0 || (supportedFeatures & 2) != 0; // SUPPORT_OPEN or SUPPORT_CLOSE

                        // Check if position control is supported
                        position = (supportedFeatures & 4) != 0; // SUPPORT_SET_POSITION

                        // Check if tilt position control is supported
                        tiltPosition = (supportedFeatures & 128) != 0; // SUPPORT_SET_TILT_POSITION

                        PluginLog.Verbose($"[CoverCaps] Supported features: {supportedFeatures}, OnOff: {onoff}, Position: {position}, TiltPosition: {tiltPosition}");
                    }
                }
                else
                {
                    // No supported_features - assume basic open/close for backwards compatibility
                    onoff = true;
                }

                // Also check for current_position and current_tilt_position attributes as indicators
                if (attrs.TryGetProperty("current_position", out _))
                {
                    position = true;
                }

                if (attrs.TryGetProperty("current_tilt_position", out _))
                {
                    tiltPosition = true;
                }

                var result = new CoverCaps(onoff, position, tiltPosition);
                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;

                PluginLog.Info($"[CoverCaps] Capability analysis completed in {elapsed:F1}ms - Result: {result}");
                return result;
            }
            catch (Exception ex)
            {
                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                PluginLog.Error($"[CoverCaps] Exception during capability parsing after {elapsed:F1}ms: {ex.Message}");

                // Return safe defaults on error
                var fallback = new CoverCaps(true, false, false);
                PluginLog.Warning($"[CoverCaps] Returning fallback capabilities: {fallback}");
                return fallback;
            }
        }
    }
}