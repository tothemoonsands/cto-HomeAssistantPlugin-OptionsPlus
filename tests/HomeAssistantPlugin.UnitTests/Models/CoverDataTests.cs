using System;
using Loupedeck.HomeAssistantPlugin.Models;

namespace Loupedeck.HomeAssistantPlugin.Tests.Models
{
    /// <summary>
    /// Comprehensive tests for CoverData record and its computed properties.
    /// Tests cover state interpretation, position handling, and capability-based properties.
    /// </summary>
    public class CoverDataTests
    {
        #region Helper Methods

        private static CoverData CreateTestCover(
            string entityId = "cover.test_cover",
            string friendlyName = "Test Cover",
            string state = "closed",
            bool isOn = false,
            string? deviceId = "device123",
            string deviceName = "Test Device",
            string manufacturer = "Test Manufacturer",
            string model = "Test Model",
            string areaId = "living_room",
            CoverCaps? capabilities = null,
            int? position = null,
            int? tiltPosition = null)
        {
            return new CoverData(
                entityId,
                friendlyName,
                state,
                isOn,
                deviceId,
                deviceName,
                manufacturer,
                model,
                areaId,
                capabilities ?? new CoverCaps(true, false, false),
                position,
                tiltPosition);
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_ValidParameters_CreatesInstanceCorrectly()
        {
            // Arrange & Act
            var cover = CreateTestCover(
                entityId: "cover.garage_door",
                friendlyName: "Garage Door",
                state: "open",
                isOn: true,
                position: 100,
                tiltPosition: null);

            // Assert
            cover.EntityId.Should().Be("cover.garage_door");
            cover.FriendlyName.Should().Be("Garage Door");
            cover.State.Should().Be("open");
            cover.IsOn.Should().BeTrue();
            cover.Position.Should().Be(100);
            cover.TiltPosition.Should().BeNull();
        }

        [Fact]
        public void Constructor_NullableParameters_HandlesNullsCorrectly()
        {
            // Arrange & Act
            var cover = CreateTestCover(
                deviceId: null,
                position: null,
                tiltPosition: null);

            // Assert
            cover.DeviceId.Should().BeNull();
            cover.Position.Should().BeNull();
            cover.TiltPosition.Should().BeNull();
        }

        #endregion

        #region State Property Tests

        [Theory]
        [InlineData("open", true)]
        [InlineData("OPEN", true)]
        [InlineData("Open", true)]
        [InlineData("closed", false)]
        [InlineData("opening", false)]
        [InlineData("closing", false)]
        [InlineData("stopped", false)]
        [InlineData("unknown", false)]
        public void IsOpen_VariousStates_ReturnsCorrectValue(string state, bool expectedIsOpen)
        {
            // Arrange
            var cover = CreateTestCover(state: state, isOn: expectedIsOpen);

            // Act & Assert
            cover.IsOpen.Should().Be(expectedIsOpen);
        }

        [Theory]
        [InlineData("closed", true)]
        [InlineData("CLOSED", true)]
        [InlineData("Closed", true)]
        [InlineData("open", false)]
        [InlineData("opening", false)]
        [InlineData("closing", false)]
        [InlineData("stopped", false)]
        [InlineData("unknown", false)]
        public void IsClosed_VariousStates_ReturnsCorrectValue(string state, bool expectedIsClosed)
        {
            // Arrange
            var cover = CreateTestCover(state: state);

            // Act & Assert
            cover.IsClosed.Should().Be(expectedIsClosed);
        }

        [Theory]
        [InlineData("opening", true)]
        [InlineData("OPENING", true)]
        [InlineData("Opening", true)]
        [InlineData("open", false)]
        [InlineData("closed", false)]
        [InlineData("closing", false)]
        [InlineData("stopped", false)]
        [InlineData("unknown", false)]
        public void IsOpening_VariousStates_ReturnsCorrectValue(string state, bool expectedIsOpening)
        {
            // Arrange
            var cover = CreateTestCover(state: state);

            // Act & Assert
            cover.IsOpening.Should().Be(expectedIsOpening);
        }

        [Theory]
        [InlineData("closing", true)]
        [InlineData("CLOSING", true)]
        [InlineData("Closing", true)]
        [InlineData("open", false)]
        [InlineData("closed", false)]
        [InlineData("opening", false)]
        [InlineData("stopped", false)]
        [InlineData("unknown", false)]
        public void IsClosing_VariousStates_ReturnsCorrectValue(string state, bool expectedIsClosing)
        {
            // Arrange
            var cover = CreateTestCover(state: state);

            // Act & Assert
            cover.IsClosing.Should().Be(expectedIsClosing);
        }

        [Theory]
        [InlineData("stopped", true)]
        [InlineData("STOPPED", true)]
        [InlineData("Stopped", true)]
        [InlineData("open", false)]
        [InlineData("closed", false)]
        [InlineData("opening", false)]
        [InlineData("closing", false)]
        [InlineData("unknown", false)]
        public void IsStopped_VariousStates_ReturnsCorrectValue(string state, bool expectedIsStopped)
        {
            // Arrange
            var cover = CreateTestCover(state: state);

            // Act & Assert
            cover.IsStopped.Should().Be(expectedIsStopped);
        }

        [Theory]
        [InlineData("unknown", true)]
        [InlineData("UNKNOWN", true)]
        [InlineData("Unknown", true)]
        [InlineData("open", false)]
        [InlineData("closed", false)]
        [InlineData("opening", false)]
        [InlineData("closing", false)]
        [InlineData("stopped", false)]
        public void IsUnknown_VariousStates_ReturnsCorrectValue(string state, bool expectedIsUnknown)
        {
            // Arrange
            var cover = CreateTestCover(state: state);

            // Act & Assert
            cover.IsUnknown.Should().Be(expectedIsUnknown);
        }

        [Theory]
        [InlineData("opening", true)]
        [InlineData("closing", true)]
        [InlineData("open", false)]
        [InlineData("closed", false)]
        [InlineData("stopped", false)]
        [InlineData("unknown", false)]
        public void IsMoving_VariousStates_ReturnsCorrectValue(string state, bool expectedIsMoving)
        {
            // Arrange
            var cover = CreateTestCover(state: state);

            // Act & Assert
            cover.IsMoving.Should().Be(expectedIsMoving);
        }

        [Theory]
        [InlineData("opening", false)]
        [InlineData("closing", false)]
        [InlineData("open", true)]
        [InlineData("closed", true)]
        [InlineData("stopped", true)]
        [InlineData("unknown", true)]
        public void IsStationary_VariousStates_ReturnsCorrectValue(string state, bool expectedIsStationary)
        {
            // Arrange
            var cover = CreateTestCover(state: state);

            // Act & Assert
            cover.IsStationary.Should().Be(expectedIsStationary);
        }

        #endregion

        #region Capability Property Tests

        [Fact]
        public void HasPositionControl_WithPositionCapability_ReturnsTrue()
        {
            // Arrange
            var caps = new CoverCaps(true, true, false);
            var cover = CreateTestCover(capabilities: caps);

            // Act & Assert
            cover.HasPositionControl.Should().BeTrue();
        }

        [Fact]
        public void HasPositionControl_WithoutPositionCapability_ReturnsFalse()
        {
            // Arrange
            var caps = new CoverCaps(true, false, false);
            var cover = CreateTestCover(capabilities: caps);

            // Act & Assert
            cover.HasPositionControl.Should().BeFalse();
        }

        [Fact]
        public void HasTiltControl_WithTiltCapability_ReturnsTrue()
        {
            // Arrange
            var caps = new CoverCaps(true, false, true);
            var cover = CreateTestCover(capabilities: caps);

            // Act & Assert
            cover.HasTiltControl.Should().BeTrue();
        }

        [Fact]
        public void HasTiltControl_WithoutTiltCapability_ReturnsFalse()
        {
            // Arrange
            var caps = new CoverCaps(true, false, false);
            var cover = CreateTestCover(capabilities: caps);

            // Act & Assert
            cover.HasTiltControl.Should().BeFalse();
        }

        #endregion

        #region Position Property Tests

        [Theory]
        [InlineData(0)]
        [InlineData(25)]
        [InlineData(50)]
        [InlineData(75)]
        [InlineData(100)]
        public void PositionPercent_WithPosition_ReturnsCorrectValue(int position)
        {
            // Arrange
            var cover = CreateTestCover(position: position);

            // Act & Assert
            cover.PositionPercent.Should().Be(position);
        }

        [Fact]
        public void PositionPercent_WithNullPosition_ReturnsNull()
        {
            // Arrange
            var cover = CreateTestCover(position: null);

            // Act & Assert
            cover.PositionPercent.Should().BeNull();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(30)]
        [InlineData(60)]
        [InlineData(90)]
        [InlineData(100)]
        public void TiltPositionPercent_WithTiltPosition_ReturnsCorrectValue(int tiltPosition)
        {
            // Arrange
            var cover = CreateTestCover(tiltPosition: tiltPosition);

            // Act & Assert
            cover.TiltPositionPercent.Should().Be(tiltPosition);
        }

        [Fact]
        public void TiltPositionPercent_WithNullTiltPosition_ReturnsNull()
        {
            // Arrange
            var cover = CreateTestCover(tiltPosition: null);

            // Act & Assert
            cover.TiltPositionPercent.Should().BeNull();
        }

        #endregion

        #region Real-world Cover Scenarios

        [Fact]
        public void GarageDoor_TypicalScenario_PropertiesWorkCorrectly()
        {
            // Arrange - Garage door in closed state
            var caps = new CoverCaps(true, false, false); // Open/close only
            var cover = CreateTestCover(
                entityId: "cover.garage_door",
                friendlyName: "Main Garage Door",
                state: "closed",
                isOn: false,
                capabilities: caps,
                position: null,
                tiltPosition: null);

            // Assert
            cover.IsClosed.Should().BeTrue();
            cover.IsOpen.Should().BeFalse();
            cover.IsMoving.Should().BeFalse();
            cover.HasPositionControl.Should().BeFalse();
            cover.HasTiltControl.Should().BeFalse();
            cover.PositionPercent.Should().BeNull();
            cover.TiltPositionPercent.Should().BeNull();
        }

        [Fact]
        public void MotorizedBlind_TypicalScenario_PropertiesWorkCorrectly()
        {
            // Arrange - Motorized blind with position control
            var caps = new CoverCaps(true, true, false);
            var cover = CreateTestCover(
                entityId: "cover.living_room_blind",
                friendlyName: "Living Room Blind",
                state: "open",
                isOn: true,
                capabilities: caps,
                position: 75,
                tiltPosition: null);

            // Assert
            cover.IsOpen.Should().BeTrue();
            cover.IsClosed.Should().BeFalse();
            cover.IsMoving.Should().BeFalse();
            cover.HasPositionControl.Should().BeTrue();
            cover.HasTiltControl.Should().BeFalse();
            cover.PositionPercent.Should().Be(75);
            cover.TiltPositionPercent.Should().BeNull();
        }

        [Fact]
        public void VenetianBlind_TypicalScenario_PropertiesWorkCorrectly()
        {
            // Arrange - Venetian blind with position and tilt control
            var caps = new CoverCaps(true, true, true);
            var cover = CreateTestCover(
                entityId: "cover.bedroom_venetian",
                friendlyName: "Bedroom Venetian Blind",
                state: "open",
                isOn: true,
                capabilities: caps,
                position: 60,
                tiltPosition: 45);

            // Assert
            cover.IsOpen.Should().BeTrue();
            cover.IsClosed.Should().BeFalse();
            cover.IsMoving.Should().BeFalse();
            cover.HasPositionControl.Should().BeTrue();
            cover.HasTiltControl.Should().BeTrue();
            cover.PositionPercent.Should().Be(60);
            cover.TiltPositionPercent.Should().Be(45);
        }

        [Fact]
        public void CoverInMotion_OpeningState_PropertiesWorkCorrectly()
        {
            // Arrange - Cover currently opening
            var caps = new CoverCaps(true, true, false);
            var cover = CreateTestCover(
                state: "opening",
                isOn: false, // isOn doesn't necessarily match opening state
                capabilities: caps,
                position: 35);

            // Assert
            cover.IsOpening.Should().BeTrue();
            cover.IsClosing.Should().BeFalse();
            cover.IsMoving.Should().BeTrue();
            cover.IsStationary.Should().BeFalse();
            cover.PositionPercent.Should().Be(35);
        }

        [Fact]
        public void CoverInMotion_ClosingState_PropertiesWorkCorrectly()
        {
            // Arrange - Cover currently closing
            var caps = new CoverCaps(true, true, false);
            var cover = CreateTestCover(
                state: "closing",
                isOn: false,
                capabilities: caps,
                position: 25);

            // Assert
            cover.IsOpening.Should().BeFalse();
            cover.IsClosing.Should().BeTrue();
            cover.IsMoving.Should().BeTrue();
            cover.IsStationary.Should().BeFalse();
            cover.PositionPercent.Should().Be(25);
        }

        #endregion

        #region Record Behavior Tests

        [Fact]
        public void CoverData_Record_SupportsValueEquality()
        {
            // Arrange
            var cover1 = CreateTestCover("cover.test", "Test", "open", true);
            var cover2 = CreateTestCover("cover.test", "Test", "open", true);
            var cover3 = CreateTestCover("cover.test", "Test", "closed", false);

            // Assert
            cover1.Should().Be(cover2); // Same values should be equal
            cover1.Should().NotBe(cover3); // Different values should not be equal
        }

        [Fact]
        public void CoverData_Record_SupportsWithExpressions()
        {
            // Arrange
            var original = CreateTestCover(state: "closed", position: 0);

            // Act - Update state and position using 'with' expression
            var updated = original with { State = "open", Position = 100 };

            // Assert
            original.State.Should().Be("closed");
            original.Position.Should().Be(0);
            updated.State.Should().Be("open");
            updated.Position.Should().Be(100);
            updated.EntityId.Should().Be(original.EntityId); // Other properties unchanged
        }

        [Fact]
        public void CoverData_ToString_ReturnsReadableRepresentation()
        {
            // Arrange
            var cover = CreateTestCover(
                entityId: "cover.test_blind",
                friendlyName: "Test Blind",
                state: "open");

            // Act
            var stringRep = cover.ToString();

            // Assert
            stringRep.Should().Contain("cover.test_blind");
            stringRep.Should().Contain("Test Blind");
            stringRep.Should().Contain("open");
        }

        #endregion

        #region Edge Cases

        [Theory]
        [InlineData("")]
        [InlineData("invalid_state")]
        [InlineData("MIXED_case")]
        public void StateProperties_UnknownStates_HandleSafely(string state)
        {
            // Arrange
            var cover = CreateTestCover(state: state);

            // Act & Assert - Should not throw and return false for all specific states
            cover.IsOpen.Should().BeFalse();
            cover.IsClosed.Should().BeFalse();
            cover.IsOpening.Should().BeFalse();
            cover.IsClosing.Should().BeFalse();
            cover.IsStopped.Should().BeFalse();
            cover.IsUnknown.Should().BeFalse(); // Only true for exact "unknown" match
            cover.IsMoving.Should().BeFalse();
            cover.IsStationary.Should().BeTrue(); // Not moving
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(101)]
        [InlineData(999)]
        public void PositionProperties_OutOfRangeValues_ReturnAsIs(int position)
        {
            // Arrange - Note: The model doesn't enforce range validation
            var cover = CreateTestCover(position: position, tiltPosition: position);

            // Act & Assert - Values are returned as-is (validation happens elsewhere)
            cover.PositionPercent.Should().Be(position);
            cover.TiltPositionPercent.Should().Be(position);
        }

        #endregion
    }
}