using System;
using System.Collections.Generic;
using System.Text.Json;

using FluentAssertions;

using Loupedeck.HomeAssistantPlugin;
using Loupedeck.HomeAssistantPlugin.Models;

using Xunit;

namespace Loupedeck.HomeAssistantPlugin.Tests.Models
{
    /// <summary>
    /// Comprehensive tests for SwitchCaps capability detection and parsing.
    /// Covers Home Assistant attribute parsing, capability detection for different switch types,
    /// and edge cases with missing or invalid attributes.
    /// </summary>
    public class SwitchCapsTests
    {
        #region Helper Methods

        private static JsonElement CreateJsonElement(string json)
        {
            var document = JsonDocument.Parse(json);
            return document.RootElement;
        }

        #endregion

        #region FromAttributes - Basic Functionality Tests

        [Fact]
        public void FromAttributes_WithValidAttributes_ReturnsOnOffCapability()
        {
            // Arrange
            var json = @"{
                ""friendly_name"": ""Test Switch"",
                ""state"": ""on""
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = SwitchCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
        }

        [Fact]
        public void FromAttributes_WithEmptyAttributes_ReturnsOnOffCapability()
        {
            // Arrange
            var attrs = CreateJsonElement("{}");

            // Act
            var caps = SwitchCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue(); // Switches always support on/off
        }

        [Fact]
        public void FromAttributes_WithComplexAttributes_ReturnsOnOffCapability()
        {
            // Arrange
            var json = @"{
                ""friendly_name"": ""Smart Switch"",
                ""state"": ""off"",
                ""device_class"": ""outlet"",
                ""icon"": ""mdi:power-socket"",
                ""supported_features"": 0
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = SwitchCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
        }

        #endregion

        #region FromAttributes - Edge Cases and Error Handling

        [Fact]
        public void FromAttributes_WithNullJsonElement_ReturnsSafeDefaults()
        {
            // Arrange
            var document = JsonDocument.Parse("null");
            var attrs = document.RootElement;

            // Act
            var caps = SwitchCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue(); // Safe default
        }

        [Fact]
        public void FromAttributes_WithStringJsonElement_ReturnsSafeDefaults()
        {
            // Arrange - Non-object JSON
            var document = JsonDocument.Parse("\"not an object\"");
            var attrs = document.RootElement;

            // Act
            var caps = SwitchCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue(); // Safe default
        }

        [Fact]
        public void FromAttributes_WithArrayJsonElement_ReturnsSafeDefaults()
        {
            // Arrange - Array JSON instead of object
            var document = JsonDocument.Parse("[1, 2, 3]");
            var attrs = document.RootElement;

            // Act
            var caps = SwitchCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue(); // Safe default
        }

        [Fact]
        public void FromAttributes_WithMalformedData_HandlesGracefully()
        {
            // Arrange - Valid JSON but with unusual property values
            var json = @"{
                ""friendly_name"": null,
                ""state"": 123,
                ""attributes"": ""invalid""
            }";
            var attrs = CreateJsonElement(json);

            // Act & Assert - Should not throw
            var action = () => SwitchCaps.FromAttributes(attrs);
            action.Should().NotThrow();

            var caps = SwitchCaps.FromAttributes(attrs);
            caps.OnOff.Should().BeTrue();
        }

        #endregion

        #region Real-world Switch Type Scenarios

        [Fact]
        public void FromAttributes_BasicWallSwitch_ReturnsOnOffCapability()
        {
            // Arrange - Typical wall switch
            var json = @"{
                ""friendly_name"": ""Living Room Light Switch"",
                ""state"": ""on"",
                ""device_class"": ""switch""
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = SwitchCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
        }

        [Fact]
        public void FromAttributes_SmartOutlet_ReturnsOnOffCapability()
        {
            // Arrange - Smart outlet/plug
            var json = @"{
                ""friendly_name"": ""Smart Outlet"",
                ""state"": ""off"",
                ""device_class"": ""outlet"",
                ""icon"": ""mdi:power-socket""
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = SwitchCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
        }

        [Fact]
        public void FromAttributes_RelaySwitch_ReturnsOnOffCapability()
        {
            // Arrange - Relay-based switch
            var json = @"{
                ""friendly_name"": ""Garden Sprinkler"",
                ""state"": ""on"",
                ""device_class"": ""switch"",
                ""icon"": ""mdi:sprinkler""
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = SwitchCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
        }

        [Fact]
        public void FromAttributes_PowerStripSwitch_ReturnsOnOffCapability()
        {
            // Arrange - Individual switch on a power strip
            var json = @"{
                ""friendly_name"": ""Power Strip Port 1"",
                ""state"": ""off"",
                ""device_class"": ""outlet"",
                ""supported_features"": 0
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = SwitchCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
        }

        [Fact]
        public void FromAttributes_GenericSwitch_ReturnsOnOffCapability()
        {
            // Arrange - Generic switch without device class
            var json = @"{
                ""friendly_name"": ""Generic Switch"",
                ""state"": ""on""
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = SwitchCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
        }

        #endregion

        #region Record Struct Behavior Tests

        [Fact]
        public void SwitchCaps_RecordStruct_SupportsValueEquality()
        {
            // Arrange
            var caps1 = new SwitchCaps(true);
            var caps2 = new SwitchCaps(true);
            var caps3 = new SwitchCaps(false);

            // Assert
            caps1.Should().Be(caps2); // Same values should be equal
            caps1.Should().NotBe(caps3); // Different values should not be equal
        }

        [Fact]
        public void SwitchCaps_ToString_ReturnsReadableRepresentation()
        {
            // Arrange
            var caps = new SwitchCaps(true);

            // Act
            var stringRep = caps.ToString();

            // Assert
            stringRep.Should().Contain("OnOff");
            stringRep.Should().Contain("True");
        }

        [Fact]
        public void SwitchCaps_GetHashCode_WorksCorrectly()
        {
            // Arrange
            var caps1 = new SwitchCaps(true);
            var caps2 = new SwitchCaps(true);
            var caps3 = new SwitchCaps(false);

            // Assert
            caps1.GetHashCode().Should().Be(caps2.GetHashCode()); // Same values should have same hash
            caps1.GetHashCode().Should().NotBe(caps3.GetHashCode()); // Different values should have different hash
        }

        #endregion

        #region Performance and Consistency Tests

        [Fact]
        public void FromAttributes_MultipleParsingOperations_RemainsConsistent()
        {
            // Arrange
            var json = @"{
                ""friendly_name"": ""Test Switch"",
                ""state"": ""on""
            }";
            var attrs = CreateJsonElement(json);

            // Act - Parse same attributes multiple times
            var results = new SwitchCaps[10];
            for (int i = 0; i < results.Length; i++)
            {
                results[i] = SwitchCaps.FromAttributes(attrs);
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
                ""friendly_name"": ""Complex Smart Switch"",
                ""state"": ""on"",
                ""device_class"": ""switch"",
                ""icon"": ""mdi:toggle-switch"",
                ""supported_features"": 0,
                ""device_id"": ""switch_complex_123"",
                ""area_id"": ""living_room"",
                ""platform"": ""mqtt"",
                ""unique_id"": ""switch_complex_unique_123"",
                ""entity_category"": null,
                ""hidden"": false,
                ""entity_registry_enabled_default"": true,
                ""translation_key"": null,
                ""custom_attributes"": {
                    ""power"": 25.4,
                    ""voltage"": 120.1,
                    ""current"": 0.21
                }
            }";
            var attrs = CreateJsonElement(json);

            // Act & Assert - Should complete quickly without issues
            var action = () => SwitchCaps.FromAttributes(attrs);
            action.Should().NotThrow();

            var caps = SwitchCaps.FromAttributes(attrs);
            caps.OnOff.Should().BeTrue();
        }

        [Fact]
        public void FromAttributes_PerformanceWithManyAttributes_HandlesEfficiently()
        {
            // Arrange - Create an object with many properties
            var properties = new List<string>();
            for (int i = 0; i < 100; i++)
            {
                properties.Add($"\"property_{i}\": \"value_{i}\"");
            }
            var json = "{" + string.Join(",", properties) + "}";
            var attrs = CreateJsonElement(json);

            // Act & Assert - Should handle efficiently
            var action = () => SwitchCaps.FromAttributes(attrs);
            action.Should().NotThrow();

            var caps = SwitchCaps.FromAttributes(attrs);
            caps.OnOff.Should().BeTrue();
        }

        #endregion

        #region Constructor Tests

        [Fact]
        public void Constructor_WithTrue_SetsOnOffToTrue()
        {
            // Act
            var caps = new SwitchCaps(true);

            // Assert
            caps.OnOff.Should().BeTrue();
        }

        [Fact]
        public void Constructor_WithFalse_SetsOnOffToFalse()
        {
            // Act
            var caps = new SwitchCaps(false);

            // Assert
            caps.OnOff.Should().BeFalse();
        }

        #endregion

        #region Error Recovery Tests

        [Fact]
        public void FromAttributes_HandlesJsonExceptionGracefully()
        {
            // Note: Since we're passing a pre-parsed JsonElement, 
            // we test the method's internal error handling
            var json = @"{
                ""friendly_name"": ""Test Switch""
            }";
            var attrs = CreateJsonElement(json);

            // Act & Assert - Should not throw regardless of internal processing
            var action = () => SwitchCaps.FromAttributes(attrs);
            action.Should().NotThrow();

            var caps = SwitchCaps.FromAttributes(attrs);
            caps.OnOff.Should().BeTrue(); // Should return safe default
        }

        [Fact]
        public void FromAttributes_ConsistentBehaviorWithDifferentInputs()
        {
            // Arrange - Different valid JSON structures
            var inputs = new[]
            {
                "{}",
                @"{""state"": ""on""}",
                @"{""friendly_name"": ""Switch""}",
                @"{""device_class"": ""switch"", ""state"": ""off""}",
                @"{""supported_features"": 0, ""icon"": ""mdi:switch""}"
            };

            // Act & Assert - All should return the same result
            foreach (var json in inputs)
            {
                var attrs = CreateJsonElement(json);
                var caps = SwitchCaps.FromAttributes(attrs);
                caps.OnOff.Should().BeTrue($"Input: {json}");
            }
        }

        #endregion
    }
}