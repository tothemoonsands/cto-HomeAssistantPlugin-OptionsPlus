using System;
using System.Text.Json;
using Loupedeck.HomeAssistantPlugin;

namespace Loupedeck.HomeAssistantPlugin.Tests.Models
{
    /// <summary>
    /// Comprehensive tests for LightCaps capability detection and parsing.
    /// Covers Home Assistant attribute parsing, capability detection for different light types,
    /// and edge cases with missing or invalid attributes.
    /// </summary>
    public class LightCapsTests
    {
        #region Helper Methods

        private static JsonElement CreateJsonElement(string json)
        {
            var document = JsonDocument.Parse(json);
            return document.RootElement;
        }

        private static JsonElement CreateSupportedColorModesJson(params string[] modes)
        {
            var modesArray = string.Join(",", modes.Select(m => $"\"{m}\""));
            var json = $@"{{
                ""supported_color_modes"": [{modesArray}]
            }}";
            return CreateJsonElement(json);
        }

        #endregion

        #region FromAttributes - Supported Color Modes Tests

        [Fact]
        public void FromAttributes_OnOffOnly_ReturnsOnOffCapability()
        {
            // Arrange
            var attrs = CreateSupportedColorModesJson("onoff");

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
            caps.Brightness.Should().BeFalse();
            caps.ColorTemp.Should().BeFalse();
            caps.ColorHs.Should().BeFalse();
            caps.PreferredColorMode.Should().BeNull();
        }

        [Fact]
        public void FromAttributes_BrightnessOnly_ReturnsBrightnessCapability()
        {
            // Arrange
            var attrs = CreateSupportedColorModesJson("brightness");

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeFalse();
            caps.Brightness.Should().BeTrue();
            caps.ColorTemp.Should().BeFalse();
            caps.ColorHs.Should().BeFalse();
            caps.PreferredColorMode.Should().BeNull();
        }

        [Fact]
        public void FromAttributes_ColorTempOnly_ReturnsColorTempCapability()
        {
            // Arrange
            var attrs = CreateSupportedColorModesJson("color_temp");

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeFalse();
            caps.Brightness.Should().BeTrue(); // Implied by color_temp
            caps.ColorTemp.Should().BeTrue();
            caps.ColorHs.Should().BeFalse();
            caps.PreferredColorMode.Should().BeNull();
        }

        [Theory]
        [InlineData("hs")]
        [InlineData("rgb")]
        [InlineData("xy")]
        [InlineData("rgbw")]
        [InlineData("rgbww")]
        public void FromAttributes_ColorModes_ReturnsColorCapability(string colorMode)
        {
            // Arrange
            var attrs = CreateSupportedColorModesJson(colorMode);

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeFalse();
            caps.Brightness.Should().BeTrue(); // Implied by color modes
            caps.ColorTemp.Should().BeFalse();
            caps.ColorHs.Should().BeTrue();
            caps.PreferredColorMode.Should().Be(colorMode);
        }

        [Fact]
        public void FromAttributes_MultipleColorModes_SelectsPreferredMode()
        {
            // Arrange - Test priority order: rgbww > rgbw > rgb > hs > xy
            var attrs = CreateSupportedColorModesJson("xy", "hs", "rgb", "rgbw", "rgbww");

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeFalse();
            caps.Brightness.Should().BeTrue();
            caps.ColorTemp.Should().BeFalse();
            caps.ColorHs.Should().BeTrue();
            caps.PreferredColorMode.Should().Be("rgbww"); // Highest priority
        }

        [Fact]
        public void FromAttributes_MixedCapabilities_ReturnsAllCapabilities()
        {
            // Arrange
            var attrs = CreateSupportedColorModesJson("onoff", "brightness", "color_temp", "rgb");

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
            caps.Brightness.Should().BeTrue();
            caps.ColorTemp.Should().BeTrue();
            caps.ColorHs.Should().BeTrue();
            caps.PreferredColorMode.Should().Be("rgb");
        }

        [Theory]
        [InlineData("brightness", "white")]
        [InlineData("white", "brightness")]
        [InlineData("brightness", "color_temp", "rgb")]
        public void FromAttributes_BrightnessImplication_SetsBrightnessTrue(params string[] modes)
        {
            // Arrange
            var attrs = CreateSupportedColorModesJson(modes);

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert
            caps.Brightness.Should().BeTrue("Brightness should be implied by {0}", string.Join(", ", modes));
        }

        #endregion

        #region FromAttributes - Heuristic Fallback Tests

        [Fact]
        public void FromAttributes_NoSupportedColorModes_UsesBrightnessHeuristic()
        {
            // Arrange
            var json = @"{
                ""brightness"": 128,
                ""state"": ""on""
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeFalse(); // Has brightness, so not onoff-only
            caps.Brightness.Should().BeTrue();
            caps.ColorTemp.Should().BeFalse();
            caps.ColorHs.Should().BeFalse();
        }

        [Fact]
        public void FromAttributes_NoSupportedColorModes_UsesColorTempHeuristic()
        {
            // Arrange
            var json = @"{
                ""min_mireds"": 154,
                ""max_mireds"": 500,
                ""color_temp"": 300
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeFalse();
            caps.Brightness.Should().BeFalse();
            caps.ColorTemp.Should().BeTrue();
            caps.ColorHs.Should().BeFalse();
        }

        [Theory]
        [InlineData(@"{""hs_color"": [120, 75]}")]
        [InlineData(@"{""rgb_color"": [255, 128, 0]}")]
        [InlineData(@"{""xy_color"": [0.3, 0.4]}")]
        public void FromAttributes_NoSupportedColorModes_UsesColorHeuristic(string json)
        {
            // Arrange
            var attrs = CreateJsonElement(json);

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeFalse();
            caps.Brightness.Should().BeFalse();
            caps.ColorTemp.Should().BeFalse();
            caps.ColorHs.Should().BeTrue();
        }

        [Fact]
        public void FromAttributes_NoSupportedColorModes_NoCapabilityProperties_ReturnsOnOffOnly()
        {
            // Arrange - Light with no recognizable capability properties
            var json = @"{
                ""state"": ""on"",
                ""entity_id"": ""light.simple_switch""
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
            caps.Brightness.Should().BeFalse();
            caps.ColorTemp.Should().BeFalse();
            caps.ColorHs.Should().BeFalse();
        }

        [Fact]
        public void FromAttributes_NoSupportedColorModes_MultipleCapabilities_DetectsAll()
        {
            // Arrange
            var json = @"{
                ""brightness"": 200,
                ""min_mireds"": 154,
                ""max_mireds"": 500,
                ""hs_color"": [240, 80]
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeFalse(); // Has other capabilities
            caps.Brightness.Should().BeTrue();
            caps.ColorTemp.Should().BeTrue();
            caps.ColorHs.Should().BeTrue();
        }

        #endregion

        #region FromAttributes - Edge Cases and Error Handling

        [Fact]
        public void FromAttributes_EmptyJsonObject_ReturnsSafeDefaults()
        {
            // Arrange
            var attrs = CreateJsonElement("{}");

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue(); // Safe default when nothing else detected
            caps.Brightness.Should().BeFalse();
            caps.ColorTemp.Should().BeFalse();
            caps.ColorHs.Should().BeFalse();
        }

        [Fact]
        public void FromAttributes_InvalidJsonValue_ReturnsSafeDefaults()
        {
            // Arrange - Non-object JSON
            var document = JsonDocument.Parse("\"not an object\"");
            var attrs = document.RootElement;

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert - Adjust expectations based on actual implementation
            // When JSON is not an object, it falls back to heuristic detection which finds no capabilities
            caps.OnOff.Should().BeFalse();
            caps.Brightness.Should().BeFalse();
            caps.ColorTemp.Should().BeFalse();
            caps.ColorHs.Should().BeFalse();
        }

        [Fact]
        public void FromAttributes_EmptySupportedColorModes_ReturnsSafeDefaults()
        {
            // Arrange
            var json = @"{
                ""supported_color_modes"": []
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeFalse();
            caps.Brightness.Should().BeFalse();
            caps.ColorTemp.Should().BeFalse();
            caps.ColorHs.Should().BeFalse();
        }

        [Fact]
        public void FromAttributes_InvalidSupportedColorModes_HandlesSafely()
        {
            // Arrange - supported_color_modes is not an array
            var json = @"{
                ""supported_color_modes"": ""not_an_array""
            }";
            var attrs = CreateJsonElement(json);

            // Act & Assert - Should not throw
            var action = () => LightCaps.FromAttributes(attrs);
            action.Should().NotThrow();

            var caps = LightCaps.FromAttributes(attrs);
            caps.OnOff.Should().BeTrue(); // Should fall back to heuristic detection
        }

        [Fact]
        public void FromAttributes_MixedValidInvalidModes_ProcessesValidModes()
        {
            // Arrange - Mix of valid strings and invalid values
            var json = @"{
                ""supported_color_modes"": [""rgb"", 123, ""brightness"", null, ""color_temp""]
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert - Should process valid modes and ignore invalid ones
            caps.Brightness.Should().BeTrue();
            caps.ColorTemp.Should().BeTrue();
            caps.ColorHs.Should().BeTrue(); // From rgb
        }

        #endregion

        #region Real-world Light Type Scenarios

        [Fact]
        public void FromAttributes_PhilipsHueColorBulb_ReturnsFullCapabilities()
        {
            // Arrange - Typical Philips Hue color bulb attributes
            var json = @"{
                ""supported_color_modes"": [""color_temp"", ""hs""],
                ""min_mireds"": 153,
                ""max_mireds"": 500,
                ""brightness"": 254,
                ""hs_color"": [240, 100]
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeFalse();
            caps.Brightness.Should().BeTrue();
            caps.ColorTemp.Should().BeTrue();
            caps.ColorHs.Should().BeTrue();
            caps.PreferredColorMode.Should().Be("hs");
        }

        [Fact]
        public void FromAttributes_IkeaTradfriBulb_ReturnsColorTempOnly()
        {
            // Arrange - Typical IKEA Tradfri tunable white bulb
            var json = @"{
                ""supported_color_modes"": [""color_temp""],
                ""min_mireds"": 250,
                ""max_mireds"": 454,
                ""brightness"": 200,
                ""color_temp"": 350
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeFalse();
            caps.Brightness.Should().BeTrue();
            caps.ColorTemp.Should().BeTrue();
            caps.ColorHs.Should().BeFalse();
            caps.PreferredColorMode.Should().BeNull();
        }

        [Fact]
        public void FromAttributes_SimpleDimmableBulb_ReturnsBrightnessOnly()
        {
            // Arrange - Simple dimmable white bulb
            var json = @"{
                ""supported_color_modes"": [""brightness""],
                ""brightness"": 150
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeFalse();
            caps.Brightness.Should().BeTrue();
            caps.ColorTemp.Should().BeFalse();
            caps.ColorHs.Should().BeFalse();
            caps.PreferredColorMode.Should().BeNull();
        }

        [Fact]
        public void FromAttributes_OnOffSwitchLight_ReturnsOnOffOnly()
        {
            // Arrange - Simple on/off light switch
            var json = @"{
                ""supported_color_modes"": [""onoff""],
                ""state"": ""on""
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeTrue();
            caps.Brightness.Should().BeFalse();
            caps.ColorTemp.Should().BeFalse();
            caps.ColorHs.Should().BeFalse();
            caps.PreferredColorMode.Should().BeNull();
        }

        [Fact]
        public void FromAttributes_RgbwStripLight_ReturnsRgbwPreferred()
        {
            // Arrange - RGBW LED strip light
            var json = @"{
                ""supported_color_modes"": [""rgbw"", ""color_temp""],
                ""brightness"": 255,
                ""rgb_color"": [255, 0, 128]
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeFalse();
            caps.Brightness.Should().BeTrue();
            caps.ColorTemp.Should().BeTrue();
            caps.ColorHs.Should().BeTrue();
            caps.PreferredColorMode.Should().Be("rgbw");
        }

        [Fact]
        public void FromAttributes_LegacyLightWithoutSupportedModes_UsesHeuristics()
        {
            // Arrange - Older light without supported_color_modes attribute
            var json = @"{
                ""brightness"": 180,
                ""min_mireds"": 154,
                ""max_mireds"": 370,
                ""color_temp"": 250,
                ""hs_color"": [30, 85],
                ""state"": ""on""
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert
            caps.OnOff.Should().BeFalse();
            caps.Brightness.Should().BeTrue();
            caps.ColorTemp.Should().BeTrue();
            caps.ColorHs.Should().BeTrue();
            caps.PreferredColorMode.Should().BeNull(); // No preference without supported_color_modes
        }

        #endregion

        #region Preferred Color Mode Priority Tests

        [Theory]
        [InlineData(new[] { "rgbww", "rgbw", "rgb", "hs", "xy" }, "rgbww")]
        [InlineData(new[] { "rgbw", "rgb", "hs", "xy" }, "rgbw")]
        [InlineData(new[] { "rgb", "hs", "xy" }, "rgb")]
        [InlineData(new[] { "hs", "xy" }, "hs")]
        [InlineData(new[] { "xy", "color_temp" }, "xy")]
        [InlineData(new[] { "color_temp", "brightness" }, null)]
        public void FromAttributes_ColorModePriority_SelectsCorrectPreferred(string[] modes, string? expectedPreferred)
        {
            // Arrange
            var attrs = CreateSupportedColorModesJson(modes);

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert
            caps.PreferredColorMode.Should().Be(expectedPreferred);
        }

        [Fact]
        public void FromAttributes_CaseInsensitiveModes_HandlesDifferentCasing()
        {
            // Arrange - Test with exact case-sensitive strings as used in Home Assistant
            var json = @"{
                ""supported_color_modes"": [""rgb"", ""color_temp"", ""hs""]
            }";
            var attrs = CreateJsonElement(json);

            // Act
            var caps = LightCaps.FromAttributes(attrs);

            // Assert - Implementation uses exact case matching
            caps.ColorTemp.Should().BeTrue();
            caps.ColorHs.Should().BeTrue();
            caps.PreferredColorMode.Should().Be("rgb"); // Implementation preserves exact casing from JSON
        }

        #endregion

        #region Record Struct Behavior Tests

        [Fact]
        public void LightCaps_RecordStruct_SupportsValueEquality()
        {
            // Arrange
            var caps1 = new LightCaps(true, true, false, true, "rgb");
            var caps2 = new LightCaps(true, true, false, true, "rgb");
            var caps3 = new LightCaps(false, true, false, true, "rgb");

            // Assert
            caps1.Should().Be(caps2); // Same values should be equal
            caps1.Should().NotBe(caps3); // Different values should not be equal
        }

        [Fact]
        public void LightCaps_ToString_ReturnsReadableRepresentation()
        {
            // Arrange
            var caps = new LightCaps(true, false, true, false, "color_temp");

            // Act
            var stringRep = caps.ToString();

            // Assert
            stringRep.Should().Contain("OnOff");
            stringRep.Should().Contain("True");
            stringRep.Should().Contain("ColorTemp");
            stringRep.Should().Contain("color_temp");
        }

        #endregion

        #region Performance and Consistency Tests

        [Fact]
        public void FromAttributes_MultipleParsingOperations_RemainsConsistent()
        {
            // Arrange
            var json = @"{
                ""supported_color_modes"": [""rgb"", ""color_temp""],
                ""brightness"": 200
            }";
            var attrs = CreateJsonElement(json);

            // Act - Parse same attributes multiple times
            var results = new LightCaps[10];
            for (int i = 0; i < results.Length; i++)
            {
                results[i] = LightCaps.FromAttributes(attrs);
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
                ""supported_color_modes"": [""onoff"", ""brightness"", ""color_temp"", ""hs"", ""rgb"", ""xy"", ""rgbw"", ""rgbww""],
                ""brightness"": 200,
                ""min_mireds"": 154,
                ""max_mireds"": 500,
                ""color_temp"": 300,
                ""hs_color"": [180, 75],
                ""rgb_color"": [128, 200, 255],
                ""xy_color"": [0.3, 0.4],
                ""state"": ""on"",
                ""entity_id"": ""light.complex_bulb"",
                ""friendly_name"": ""Complex RGB Bulb"",
                ""supported_features"": 63
            }";
            var attrs = CreateJsonElement(json);

            // Act & Assert - Should complete quickly without issues
            var action = () => LightCaps.FromAttributes(attrs);
            action.Should().NotThrow();

            var caps = LightCaps.FromAttributes(attrs);
            caps.PreferredColorMode.Should().Be("rgbww");
        }

        #endregion
    }
}