namespace Loupedeck.HomeAssistantPlugin
{
    using System;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    using Loupedeck.HomeAssistantPlugin.Services;

    internal sealed class CoverControlService : ICoverControlService
    {
        // ====================================================================
        // CONSTANTS - Cover Control Service Configuration
        // ====================================================================

        // --- Service Timeout Constants ---
        private const Int32 ServiceCallTimeoutSeconds = 4;             // Timeout for Home Assistant service calls

        private readonly IHaClient _ha;

        public CoverControlService(IHaClient ha)
        {
            PluginLog.Info("[CoverControlService] Constructor - Initializing cover control service");

            this._ha = ha ?? throw new ArgumentNullException(nameof(ha));

            PluginLog.Info("[CoverControlService] Constructor completed - Cover control service initialized");
        }

        public async Task<Boolean> OpenCoverAsync(String entityId, CancellationToken ct = default)
        {
            try
            {
                PluginLog.Info($"[cover] Sending open_cover command to {entityId}");

                var (ok, err) = await this._ha.CallServiceAsync("cover", "open_cover", entityId, null, ct).ConfigureAwait(false);

                if (ok)
                {
                    PluginLog.Info($"[cover] open_cover -> {entityId} OK");
                }
                else
                {
                    PluginLog.Warning($"[cover] open_cover failed for {entityId}: {err}");
                }

                return ok;
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, $"[cover] OpenCoverAsync exception for {entityId}");
                return false;
            }
        }

        public async Task<Boolean> CloseCoverAsync(String entityId, CancellationToken ct = default)
        {
            try
            {
                PluginLog.Info($"[cover] Sending close_cover command to {entityId}");

                var (ok, err) = await this._ha.CallServiceAsync("cover", "close_cover", entityId, null, ct).ConfigureAwait(false);

                if (ok)
                {
                    PluginLog.Info($"[cover] close_cover -> {entityId} OK");
                }
                else
                {
                    PluginLog.Warning($"[cover] close_cover failed for {entityId}: {err}");
                }

                return ok;
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, $"[cover] CloseCoverAsync exception for {entityId}");
                return false;
            }
        }

        public async Task<Boolean> StopCoverAsync(String entityId, CancellationToken ct = default)
        {
            try
            {
                PluginLog.Info($"[cover] Sending stop_cover command to {entityId}");

                var (ok, err) = await this._ha.CallServiceAsync("cover", "stop_cover", entityId, null, ct).ConfigureAwait(false);

                if (ok)
                {
                    PluginLog.Info($"[cover] stop_cover -> {entityId} OK");
                }
                else
                {
                    PluginLog.Warning($"[cover] stop_cover failed for {entityId}: {err}");
                }

                return ok;
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, $"[cover] StopCoverAsync exception for {entityId}");
                return false;
            }
        }

        public async Task<Boolean> SetCoverPositionAsync(String entityId, Int32 position, CancellationToken ct = default)
        {
            try
            {
                // Clamp position to valid range
                position = Math.Clamp(position, 0, 100);

                PluginLog.Info($"[cover] Sending set_cover_position command to {entityId} with position: {position}");

                // Create JSON data with position parameter
                var jsonData = JsonSerializer.SerializeToElement(new { position = position });

                var (ok, err) = await this._ha.CallServiceAsync("cover", "set_cover_position", entityId, jsonData, ct).ConfigureAwait(false);

                if (ok)
                {
                    PluginLog.Info($"[cover] set_cover_position -> {entityId} OK (position: {position})");
                }
                else
                {
                    PluginLog.Warning($"[cover] set_cover_position failed for {entityId}: {err}");
                }

                return ok;
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, $"[cover] SetCoverPositionAsync exception for {entityId}");
                return false;
            }
        }

        public async Task<Boolean> SetCoverTiltPositionAsync(String entityId, Int32 tiltPosition, CancellationToken ct = default)
        {
            try
            {
                // Clamp tilt position to valid range
                tiltPosition = Math.Clamp(tiltPosition, 0, 100);

                PluginLog.Info($"[cover] Sending set_cover_tilt_position command to {entityId} with tilt_position: {tiltPosition}");

                // Create JSON data with tilt_position parameter
                var jsonData = JsonSerializer.SerializeToElement(new { tilt_position = tiltPosition });

                var (ok, err) = await this._ha.CallServiceAsync("cover", "set_cover_tilt_position", entityId, jsonData, ct).ConfigureAwait(false);

                if (ok)
                {
                    PluginLog.Info($"[cover] set_cover_tilt_position -> {entityId} OK (tilt_position: {tiltPosition})");
                }
                else
                {
                    PluginLog.Warning($"[cover] set_cover_tilt_position failed for {entityId}: {err}");
                }

                return ok;
            }
            catch (Exception ex)
            {
                PluginLog.Warning(ex, $"[cover] SetCoverTiltPositionAsync exception for {entityId}");
                return false;
            }
        }

        public void Dispose()
        {
            PluginLog.Info("[CoverControlService] Dispose - Cleaning up cover control service");

            try
            {
                // Cover control service doesn't have any resources to dispose
                // since it doesn't use debounced senders like the light service
                PluginLog.Info("[CoverControlService] Dispose completed successfully");
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "[CoverControlService] Dispose encountered errors during cleanup");
            }
        }
    }
}