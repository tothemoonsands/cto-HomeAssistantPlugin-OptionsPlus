// Models/SwitchCaps.cs
namespace Loupedeck.HomeAssistantPlugin.Models
{
    using System;
    using System.Text.Json;

    /// <summary>
    /// Capability model for Home Assistant switches.
    /// OnOff: device supports simple on/off control
    /// </summary>
    public readonly record struct SwitchCaps(Boolean OnOff)
    {
        /// <summary>
        /// Creates switch capabilities from Home Assistant attributes.
        /// For switches, this is typically just on/off control.
        /// </summary>
        /// <param name="attrs">JSON attributes from Home Assistant switch entity</param>
        /// <returns>Switch capabilities with OnOff set to true by default</returns>
        public static SwitchCaps FromAttributes(JsonElement attrs)
        {
            var startTime = DateTime.UtcNow;
            PluginLog.Verbose("[SwitchCaps] FromAttributes() called - parsing switch capabilities from JSON attributes");

            try
            {
                // Switches in Home Assistant typically only support on/off
                // We default to true since all switches should support basic on/off control
                var onoff = true;

                var result = new SwitchCaps(onoff);
                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;

                PluginLog.Info($"[SwitchCaps] Capability analysis completed in {elapsed:F1}ms - Result: {result}");
                return result;
            }
            catch (Exception ex)
            {
                var elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
                PluginLog.Error($"[SwitchCaps] Exception during capability parsing after {elapsed:F1}ms: {ex.Message}");

                // Return safe defaults on error
                var fallback = new SwitchCaps(true);
                PluginLog.Warning($"[SwitchCaps] Returning fallback capabilities: {fallback}");
                return fallback;
            }
        }
    }
}