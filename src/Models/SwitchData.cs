namespace Loupedeck.HomeAssistantPlugin.Models
{
    using System;

    /// <summary>
    /// Represents comprehensive data for a single switch entity from Home Assistant
    /// </summary>
    public record SwitchData(
        String EntityId,
        String FriendlyName,
        String State,
        Boolean IsOn,
        String? DeviceId,
        String DeviceName,
        String Manufacturer,
        String Model,
        String AreaId,
        SwitchCaps Capabilities
    );
}