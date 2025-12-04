namespace Loupedeck.HomeAssistantPlugin
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Loupedeck.HomeAssistantPlugin.Services;

    internal sealed class SwitchControlService : ISwitchControlService
    {
        // ====================================================================
        // CONSTANTS - Switch Control Service Configuration
        // ====================================================================

        // --- Service Timeout Constants ---
        private const Int32 ServiceCallTimeoutSeconds = 4;             // Timeout for Home Assistant service calls

        private readonly IHaClient _ha;

        public SwitchControlService(IHaClient ha)
        {
            PluginLog.Info("[SwitchControlService] Constructor - Initializing switch control service");

            this._ha = ha ?? throw new ArgumentNullException(nameof(ha));

            PluginLog.Info("[SwitchControlService] Constructor completed - Switch control service initialized");
        }

        public async Task<Boolean> TurnOnAsync(String entityId, JsonElement? data = null, CancellationToken ct = default)
        {
            try
            {
                var dataStr = data?.ToString() ?? "null";
                PluginLog.Info($"[switch] Sending turn_on command to {entityId} with data: {dataStr}");

                var (ok, err) = await this._ha.CallServiceAsync("switch", "turn_on", entityId, data, ct).ConfigureAwait(false);

                if (ok)
                {
                    PluginLog.Info($"[switch] turn_on -> {entityId} OK");
                }
                else
                {
                    PluginLog.Warning($"[switch] turn_on failed for {entityId}: {err}");
                }

                return ok;
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, $"[switch] TurnOnAsync exception for {entityId}");
                return false;
            }
        }

        public async Task<Boolean> TurnOffAsync(String entityId, CancellationToken ct = default)
        {
            try
            {
                PluginLog.Info($"[switch] Sending turn_off command to {entityId}");

                var (ok, err) = await this._ha.CallServiceAsync("switch", "turn_off", entityId, null, ct).ConfigureAwait(false);

                if (ok)
                {
                    PluginLog.Info($"[switch] turn_off -> {entityId} OK");
                }
                else
                {
                    PluginLog.Warning($"[switch] turn_off failed for {entityId}: {err}");
                }

                return ok;
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, $"[switch] TurnOffAsync exception for {entityId}");
                return false;
            }
        }

        public async Task<Boolean> ToggleAsync(String entityId, CancellationToken ct = default)
        {
            try
            {
                PluginLog.Info($"[switch] Sending toggle command to {entityId}");

                var (ok, err) = await this._ha.CallServiceAsync("switch", "toggle", entityId, null, ct).ConfigureAwait(false);

                if (ok)
                {
                    PluginLog.Info($"[switch] toggle -> {entityId} OK");
                }
                else
                {
                    PluginLog.Warning($"[switch] toggle failed for {entityId}: {err}");
                }

                return ok;
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, $"[switch] ToggleAsync exception for {entityId}");
                return false;
            }
        }

        public void Dispose()
        {
            PluginLog.Info("[SwitchControlService] Dispose - Cleaning up switch control service");

            try
            {
                // Switch control service doesn't have any resources to dispose
                // since it doesn't use debounced senders like the light service
                PluginLog.Info("[SwitchControlService] Dispose completed successfully");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "[SwitchControlService] Dispose encountered errors during cleanup");
            }
        }
    }
}