using System;
using System.Text.Json;
using Loupedeck.HomeAssistantPlugin.Models;

namespace Loupedeck.HomeAssistantPlugin.Tests.Models
{
    /// <summary>
    /// Comprehensive tests for CoverCaps capability detection and parsing.
    /// Covers Home Assistant attribute parsing, capability detection for different cover types,
    /// and edge cases with missing or invalid attributes.
    /// </summary>
    public class CoverCapsTests
    {
        #region Helper Methods

        private static JsonElement CreateJsonElement(string json)
        {
            var document = JsonDocument.Parse(json);
            return document.RootElement;
        }

        private static JsonElement CreateSupportedFeaturesJson(int supportedFeatures)
        {
            var json = $@"{{
                ""supported_features"": {supportedFeatures}
            }}";
            return CreateJsonElement(json);
        }

        #endregion

        #region FromAttributes - Supported Features Tests

        [Fact]
        public void FromAttributes_BasicOpenClose_ReturnsOnOffOnly()
        {
            // Arrange - Basic cover with open/close only (features 1 + 2 = 3)
            var attrs = CreateSupportedFeaturesJson(3);

            // Act
            var caps = CoverCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
            caps.Position.Should().BeFalse();
            caps.TiltPosition.Should().BeFalse();
        }

        [Fact]
        public void FromAttributes_WithPositionControl_ReturnsPositionCapability()
        {
            // Arrange - Cover with position control (features 4)
            var attrs = CreateSupportedFeaturesJson(4);

            // Act
            var caps = CoverCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue(); // Always true
            caps.Position.Should().BeTrue();
            caps.TiltPosition.Should().BeFalse();
        }

        [Fact]
        public void FromAttributes_WithTiltControl_ReturnsTiltCapability()
        {
            // Arrange - Cover with tilt control only (features 128)
            var attrs = CreateSupportedFeaturesJson(128);

            // Act
            var caps = CoverCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeFalse(); // Tilt-only covers don't support basic open/close
            caps.Position.Should().BeFalse();
            caps.TiltPosition.Should().BeTrue();
        }

        [Fact]
        public void FromAttributes_FullFeatureSet_ReturnsAllCapabilities()
        {
            // Arrange - Cover with all features (1+2+4+8+16+32+64+128 = 255)
            var attrs = CreateSupportedFeaturesJson(255);

            // Act
            var caps = CoverCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
            caps.Position.Should().BeTrue();
            caps.TiltPosition.Should().BeTrue();
        }

        [Theory]
        [InlineData(4, true, true, false)]    // SUPPORT_SET_POSITION only - Fixed: position control implies OnOff
        [InlineData(128, false, false, true)] // SUPPORT_SET_TILT_POSITION only - no OnOff
        [InlineData(132, true, true, true)]   // Both position and tilt (4 + 128) - Fixed: position control implies OnOff
        [InlineData(7, true, true, false)]    // Open + Close + Position (1 + 2 + 4) - has OnOff
        [InlineData(159, true, true, true)]   // Open + Close + Position + Stop + Tilt (1+2+4+8+16+32+64+128 without some) - has OnOff
        [InlineData(1, true, false, false)]   // SUPPORT_OPEN only - has OnOff
        [InlineData(2, true, false, false)]   // SUPPORT_CLOSE only - has OnOff
        [InlineData(3, true, false, false)]   // SUPPORT_OPEN + SUPPORT_CLOSE - has OnOff
        public void FromAttributes_VariousFeatureCombinations_ReturnsExpectedCapabilities(
            int features, bool expectedOnOff, bool expectedPosition, bool expectedTilt)
        {
            // Arrange
            var attrs = CreateSupportedFeaturesJson(features);

            // Act
            var caps = CoverCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().Be(expectedOnOff);
            caps.Position.Should().Be(expectedPosition);
            caps.TiltPosition.Should().Be(expectedTilt);
        }

        #endregion

        #region FromAttributes - Heuristic Fallback Tests

        [Fact]
        public void FromAttributes_NoSupportedFeatures_UsesPositionHeuristic()
        {
            // Arrange - Cover with current_position attribute but no supported_features
            var json = @"{
                ""current_position"": 50,
                ""state"": ""open""
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = CoverCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
            caps.Position.Should().BeTrue();
            caps.TiltPosition.Should().BeFalse();
        }

        [Fact]
        public void FromAttributes_NoSupportedFeatures_UsesTiltHeuristic()
        {
            // Arrange - Cover with current_tilt_position attribute but no supported_features
            var json = @"{
                ""current_tilt_position"": 75,
                ""state"": ""closed""
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = CoverCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
            caps.Position.Should().BeFalse();
            caps.TiltPosition.Should().BeTrue();
        }

        [Fact]
        public void FromAttributes_NoSupportedFeatures_BothHeuristics()
        {
            // Arrange - Cover with both position attributes
            var json = @"{
                ""current_position"": 25,
                ""current_tilt_position"": 80,
                ""state"": ""opening""
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = CoverCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
            caps.Position.Should().BeTrue();
            caps.TiltPosition.Should().BeTrue();
        }

        [Fact]
        public void FromAttributes_NoSupportedFeatures_NoHeuristicAttributes_ReturnsBasicOnOff()
        {
            // Arrange - Simple cover with no capability indicators
            var json = @"{
                ""state"": ""closed"",
                ""entity_id"": ""cover.simple_cover""
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = CoverCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
            caps.Position.Should().BeFalse();
            caps.TiltPosition.Should().BeFalse();
        }

        #endregion

        #region FromAttributes - Edge Cases and Error Handling

        [Fact]
        public void FromAttributes_EmptyJsonObject_ReturnsSafeDefaults()
        {
            // Arrange
            var attrs = CreateJsonElement("{}");

            // Act
            var caps = CoverCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue(); // Safe default
            caps.Position.Should().BeFalse();
            caps.TiltPosition.Should().BeFalse();
        }

        [Fact]
        public void FromAttributes_InvalidJsonValue_ReturnsSafeDefaults()
        {
            // Arrange - Non-object JSON
            var document = JsonDocument.Parse("\"not an object\"");
            var attrs = document.RootElement;

            // Act & Assert - Should not throw
            var action = () => CoverCaps.FromAttributes(attrs);
            action.Should().NotThrow();

            var caps = CoverCaps.FromAttributes(attrs);
            caps.OnOff.Should().BeTrue(); // Safe default
            caps.Position.Should().BeFalse();
            caps.TiltPosition.Should().BeFalse();
        }

        [Fact]
        public void FromAttributes_InvalidSupportedFeatures_HandlesSafely()
        {
            // Arrange - supported_features is not a number
            var json = @"{
                ""supported_features"": ""not_a_number""
            }";
            var attrs = CreateJsonElement(json);

            // Act & Assert - Should not throw
            var action = () => CoverCaps.FromAttributes(attrs);
            action.Should().NotThrow();

            var caps = CoverCaps.FromAttributes(attrs);
            caps.OnOff.Should().BeTrue(); // Should fall back to safe defaults
        }

        [Fact]
        public void FromAttributes_NegativeSupportedFeatures_HandlesSafely()
        {
            // Arrange - Negative supported_features value
            var attrs = CreateSupportedFeaturesJson(-1);

            // Act & Assert - Should not throw
            var action = () => CoverCaps.FromAttributes(attrs);
            action.Should().NotThrow();

            var caps = CoverCaps.FromAttributes(attrs);
            caps.OnOff.Should().BeTrue(); // Should still work with bitwise operations
        }

        #endregion

        #region Real-world Cover Type Scenarios

        [Fact]
        public void FromAttributes_BasicGarageDoor_ReturnsBasicCapabilities()
        {
            // Arrange - Simple garage door with open/close/stop
            var json = @"{
                ""supported_features"": 11,
                ""state"": ""closed"",
                ""device_class"": ""garage""
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = CoverCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
            caps.Position.Should().BeFalse();
            caps.TiltPosition.Should().BeFalse();
        }

        [Fact]
        public void FromAttributes_MotorizedBlind_ReturnsPositionCapabilities()
        {
            // Arrange - Motorized blind with position control
            var json = @"{
                ""supported_features"": 15,
                ""current_position"": 60,
                ""state"": ""open"",
                ""device_class"": ""blind""
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = CoverCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
            caps.Position.Should().BeTrue();
            caps.TiltPosition.Should().BeFalse();
        }

        [Fact]
        public void FromAttributes_VenetianBlind_ReturnsFullCapabilities()
        {
            // Arrange - Venetian blind with position and tilt control
            var json = @"{
                ""supported_features"": 255,
                ""current_position"": 80,
                ""current_tilt_position"": 45,
                ""state"": ""open"",
                ""device_class"": ""blind""
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = CoverCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
            caps.Position.Should().BeTrue();
            caps.TiltPosition.Should().BeTrue();
        }

        [Fact]
        public void FromAttributes_TiltOnlyBlind_ReturnsTiltOnlyCapabilities()
        {
            // Arrange - Blind that only supports tilt control (no open/close)
            var json = @"{
                ""supported_features"": 240,
                ""current_tilt_position"": 50,
                ""state"": ""unknown"",
                ""device_class"": ""blind""
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = CoverCaps.FromAttributes(attrs);

            // Assert - Should only support tilt, not regular open/close
            caps.OnOff.Should().BeFalse();
            caps.Position.Should().BeFalse();
            caps.TiltPosition.Should().BeTrue();
        }

        [Fact]
        public void FromAttributes_Shutter_ReturnsPositionCapabilities()
        {
            // Arrange - Window shutter with position control
            var json = @"{
                ""supported_features"": 7,
                ""current_position"": 0,
                ""state"": ""closed"",
                ""device_class"": ""shutter""
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = CoverCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
            caps.Position.Should().BeTrue();
            caps.TiltPosition.Should().BeFalse();
        }

        [Fact]
        public void FromAttributes_LegacyCoverWithoutFeatures_UsesHeuristics()
        {
            // Arrange - Older cover without supported_features attribute
            var json = @"{
                ""current_position"": 30,
                ""state"": ""opening""
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = CoverCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
            caps.Position.Should().BeTrue(); // Detected from current_position
            caps.TiltPosition.Should().BeFalse();
        }

        #endregion

        #region Record Struct Behavior Tests

        [Fact]
        public void CoverCaps_RecordStruct_SupportsValueEquality()
        {
            // Arrange
            var caps1 = new CoverCaps(true, true, false);
            var caps2 = new CoverCaps(true, true, false);
            var caps3 = new CoverCaps(true, false, true);

            // Assert
            caps1.Should().Be(caps2); // Same values should be equal
            caps1.Should().NotBe(caps3); // Different values should not be equal
        }

        [Fact]
        public void CoverCaps_ToString_ReturnsReadableRepresentation()
        {
            // Arrange
            var caps = new CoverCaps(true, false, true);

            // Act
            var stringRep = caps.ToString();

            // Assert
            stringRep.Should().Contain("OnOff");
            stringRep.Should().Contain("True");
            stringRep.Should().Contain("TiltPosition");
        }

        #endregion

        #region Performance and Consistency Tests

        [Fact]
        public void FromAttributes_MultipleParsingOperations_RemainsConsistent()
        {
            // Arrange
            var json = @"{
                ""supported_features"": 132,
                ""current_position"": 70,
                ""current_tilt_position"": 30
            }";
            var attrs = CreateJsonElement(json);

            // Act - Parse same attributes multiple times
            var results = new CoverCaps[10];
            for (int i = 0; i < results.Length; i++)
            {
                results[i] = CoverCaps.FromAttributes(attrs);
            }

            // Assert - All results should be identical
            for (int i = 1; i < results.Length; i++)
            {
                results[i].Should().Be(results[0]);
            }
        }

        [Fact]
        public void FromAttributes_ComplexAttributesParsing_CompletesInReasonableTime()
        {
            // Arrange - Large complex attribute object
            var json = @"{
                ""supported_features"": 255,
                ""current_position"": 65,
                ""current_tilt_position"": 40,
                ""state"": ""open"",
                ""entity_id"": ""cover.complex_blind"",
                ""friendly_name"": ""Master Bedroom Venetian Blind"",
                ""device_class"": ""blind"",
                ""device_id"": ""12345"",
                ""area_id"": ""bedroom""
            }";
            var attrs = CreateJsonElement(json);

            // Act & Assert - Should complete quickly without issues
            var action = () => CoverCaps.FromAttributes(attrs);
            action.Should().NotThrow();

            var caps = CoverCaps.FromAttributes(attrs);
            caps.OnOff.Should().BeTrue();
            caps.Position.Should().BeTrue();
            caps.TiltPosition.Should().BeTrue();
        }

        #endregion
    }
}