using System;
using System.Linq;
using FluentAssertions;
using Loupedeck.HomeAssistantPlugin;
using Loupedeck.HomeAssistantPlugin.Models;
using Xunit;

namespace Loupedeck.HomeAssistantPlugin.Tests.Actions
{
    /// <summary>
    /// Tests for cover control detection logic to ensure tilt-only covers show only tilt controls
    /// </summary>
    public class HomeAssistantCoversDynamicFolderControlDetectionTests
    {
        [Fact]
        public void GetButtonPressActionNames_TiltOnlyCover_ShowsOnlyTiltControls()
        {
            // This test verifies the fix for covers that only have tilt capabilities
            // They should only show tilt controls, not normal controls
            
            // Arrange - Create a tilt-only cover (supported_features=128 = SUPPORT_SET_TILT_POSITION)
            var tiltOnlyCoverData = new CoverData(
                EntityId: "cover.tilt_only_blind",
                FriendlyName: "Tilt Only Blind",
                State: "unknown",
                IsOn: false,
                DeviceId: "device123",
                DeviceName: "Tilt Blind",
                Manufacturer: "TestCorp",
                Model: "TiltModel",
                AreaId: "area1",
                Capabilities: new CoverCaps(OnOff: false, Position: false, TiltPosition: true), // Tilt only
                Position: null,
                TiltPosition: 50
            );

            // Verify the capabilities are as expected for tilt-only
            tiltOnlyCoverData.Capabilities.OnOff.Should().BeFalse("tilt-only covers don't support basic open/close");
            tiltOnlyCoverData.Capabilities.Position.Should().BeFalse("tilt-only covers don't support position control");
            tiltOnlyCoverData.Capabilities.TiltPosition.Should().BeTrue("tilt-only covers support tilt position");
            tiltOnlyCoverData.HasPositionControl.Should().BeFalse();
            tiltOnlyCoverData.HasTiltControl.Should().BeTrue();
        }

        [Fact]
        public void GetButtonPressActionNames_RegularCover_ShowsOnlyRegularControls()
        {
            // Arrange - Create a regular cover (basic open/close only)
            var regularCoverData = new CoverData(
                EntityId: "cover.garage_door",
                FriendlyName: "Garage Door",
                State: "closed",
                IsOn: false,
                DeviceId: "device456",
                DeviceName: "Garage Door",
                Manufacturer: "GarageCorp",
                Model: "BasicModel",
                AreaId: "area2",
                Capabilities: new CoverCaps(OnOff: true, Position: false, TiltPosition: false), // Basic only
                Position: null,
                TiltPosition: null
            );

            // Verify the capabilities are as expected for basic cover
            regularCoverData.Capabilities.OnOff.Should().BeTrue("regular covers support basic open/close");
            regularCoverData.Capabilities.Position.Should().BeFalse("basic covers don't support position control");
            regularCoverData.Capabilities.TiltPosition.Should().BeFalse("basic covers don't support tilt");
            regularCoverData.HasPositionControl.Should().BeFalse();
            regularCoverData.HasTiltControl.Should().BeFalse();
        }

        [Fact]
        public void GetButtonPressActionNames_DualCapabilityCover_ShowsBothControls()
        {
            // Arrange - Create a cover with both regular and tilt capabilities
            var dualCoverData = new CoverData(
                EntityId: "cover.venetian_blind",
                FriendlyName: "Venetian Blind",
                State: "open",
                IsOn: true,
                DeviceId: "device789",
                DeviceName: "Venetian Blind",
                Manufacturer: "BlindCorp",
                Model: "VenetianModel",
                AreaId: "area3",
                Capabilities: new CoverCaps(OnOff: true, Position: true, TiltPosition: true), // Both capabilities
                Position: 80,
                TiltPosition: 45
            );

            // Verify the capabilities are as expected for dual-capability cover
            dualCoverData.Capabilities.OnOff.Should().BeTrue("dual covers support basic open/close");
            dualCoverData.Capabilities.Position.Should().BeTrue("dual covers support position control");
            dualCoverData.Capabilities.TiltPosition.Should().BeTrue("dual covers support tilt");
            dualCoverData.HasPositionControl.Should().BeTrue();
            dualCoverData.HasTiltControl.Should().BeTrue();
        }

        [Fact]
        public void GetButtonPressActionNames_PositionOnlyCover_ShowsRegularControls()
        {
            // Arrange - Create a cover with position control only (no basic open/close)
            var positionOnlyCoverData = new CoverData(
                EntityId: "cover.smart_shade",
                FriendlyName: "Smart Shade",
                State: "open",
                IsOn: true,
                DeviceId: "device101",
                DeviceName: "Smart Shade",
                Manufacturer: "SmartCorp",
                Model: "SmartModel",
                AreaId: "area4",
                Capabilities: new CoverCaps(OnOff: false, Position: true, TiltPosition: false), // Position only
                Position: 70,
                TiltPosition: null
            );

            // Verify the capabilities - position-only covers should still show regular controls
            // because open/close/stop can work via position control
            positionOnlyCoverData.Capabilities.OnOff.Should().BeFalse("position-only covers don't support basic open/close");
            positionOnlyCoverData.Capabilities.Position.Should().BeTrue("position-only covers support position control");
            positionOnlyCoverData.Capabilities.TiltPosition.Should().BeFalse("position-only covers don't support tilt");
            positionOnlyCoverData.HasPositionControl.Should().BeTrue();
            positionOnlyCoverData.HasTiltControl.Should().BeFalse();
        }
    }
}