using System;
using System.Text.Json;
using Xunit;
using Loupedeck.HomeAssistantPlugin;
using Loupedeck.HomeAssistantPlugin.Services;

namespace Loupedeck.HomeAssistantPlugin.Tests.Services
{
    /// <summary>
    /// Unit tests for CapabilityService with 100% coverage target
    /// Tests capability detection logic, priority-based color mode selection, fallback detection, and error handling
    /// </summary>
    public class CapabilityServiceTests
    {
        private readonly CapabilityService _capabilityService;

        public CapabilityServiceTests()
        {
            _capabilityService = new CapabilityService();
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_CreatesInstance_Successfully()
        {
            // Act & Assert
            Assert.NotNull(_capabilityService);
        }

        [Fact]
        public void Constructor_ImplementsICapabilityService()
        {
            // Assert
            Assert.IsAssignableFrom<ICapabilityService>(_capabilityService);
        }

        #endregion

        #region Supported Color Modes Detection

        [Fact]
        public void ForLight_WithSupportedColorModes_OnOffOnly_ReturnsCorrectCaps()
        {
            // Arrange
            var jsonString = """
            {
                "supported_color_modes": ["onoff"]
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.True(result.OnOff);
            Assert.False(result.Brightness);
            Assert.False(result.ColorTemp);
            Assert.False(result.ColorHs);
            Assert.Null(result.PreferredColorMode);
        }

        [Fact]
        public void ForLight_WithSupportedColorModes_BrightnessOnly_ReturnsCorrectCaps()
        {
            // Arrange
            var jsonString = """
            {
                "supported_color_modes": ["brightness"]
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.False(result.OnOff);
            Assert.True(result.Brightness);
            Assert.False(result.ColorTemp);
            Assert.False(result.ColorHs);
            Assert.Null(result.PreferredColorMode);
        }

        [Fact]
        public void ForLight_WithSupportedColorModes_ColorTemp_ReturnsCorrectCaps()
        {
            // Arrange
            var jsonString = """
            {
                "supported_color_modes": ["color_temp"]
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.False(result.OnOff);
            Assert.True(result.Brightness);  // Brightness is implied by color_temp
            Assert.True(result.ColorTemp);
            Assert.False(result.ColorHs);
            Assert.Null(result.PreferredColorMode);
        }

        [Theory]
        [InlineData("hs")]
        [InlineData("rgb")]
        [InlineData("xy")]
        [InlineData("rgbw")]
        [InlineData("rgbww")]
        public void ForLight_WithSupportedColorModes_ColorModes_ReturnsCorrectCaps(string colorMode)
        {
            // Arrange
            var jsonString = $$"""
            {
                "supported_color_modes": ["{{colorMode}}"]
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.False(result.OnOff);
            Assert.True(result.Brightness);  // Brightness is implied by color modes
            Assert.False(result.ColorTemp);
            Assert.True(result.ColorHs);
            Assert.Equal(colorMode, result.PreferredColorMode);
        }

        [Fact]
        public void ForLight_WithSupportedColorModes_WhiteMode_ReturnsCorrectCaps()
        {
            // Arrange
            var jsonString = """
            {
                "supported_color_modes": ["white"]
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.False(result.OnOff);
            Assert.True(result.Brightness);  // Brightness is implied by white mode
            Assert.False(result.ColorTemp);
            Assert.False(result.ColorHs);
            Assert.Null(result.PreferredColorMode);
        }

        #endregion

        #region Preferred Color Mode Priority Testing

        [Fact]
        public void ForLight_WithMultipleColorModes_PrioritizesRGBWW()
        {
            // Arrange
            var jsonString = """
            {
                "supported_color_modes": ["hs", "rgb", "rgbw", "rgbww", "xy"]
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.True(result.ColorHs);
            Assert.Equal("rgbww", result.PreferredColorMode);
        }

        [Fact]
        public void ForLight_WithMultipleColorModes_PrioritizesRGBW()
        {
            // Arrange - No RGBWW, should pick RGBW
            var jsonString = """
            {
                "supported_color_modes": ["hs", "rgb", "rgbw", "xy"]
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.True(result.ColorHs);
            Assert.Equal("rgbw", result.PreferredColorMode);
        }

        [Fact]
        public void ForLight_WithMultipleColorModes_PrioritizesRGB()
        {
            // Arrange - No RGBW/RGBWW, should pick RGB
            var jsonString = """
            {
                "supported_color_modes": ["hs", "rgb", "xy"]
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.True(result.ColorHs);
            Assert.Equal("rgb", result.PreferredColorMode);
        }

        [Fact]
        public void ForLight_WithMultipleColorModes_PrioritizesHS()
        {
            // Arrange - No RGB/RGBW/RGBWW, should pick HS
            var jsonString = """
            {
                "supported_color_modes": ["hs", "xy"]
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.True(result.ColorHs);
            Assert.Equal("hs", result.PreferredColorMode);
        }

        [Fact]
        public void ForLight_WithMultipleColorModes_PrioritizesXY()
        {
            // Arrange - Only XY available
            var jsonString = """
            {
                "supported_color_modes": ["xy"]
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.True(result.ColorHs);
            Assert.Equal("xy", result.PreferredColorMode);
        }

        #endregion

        #region Combined Capabilities Testing

        [Fact]
        public void ForLight_WithMultipleModes_CombinesCapabilitiesCorrectly()
        {
            // Arrange
            var jsonString = """
            {
                "supported_color_modes": ["onoff", "brightness", "color_temp", "rgb"]
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.True(result.OnOff);
            Assert.True(result.Brightness);
            Assert.True(result.ColorTemp);
            Assert.True(result.ColorHs);
            Assert.Equal("rgb", result.PreferredColorMode);
        }

        [Fact]
        public void ForLight_WithBrightnessAndColor_ImpliesBrightness()
        {
            // Arrange
            var jsonString = """
            {
                "supported_color_modes": ["hs", "color_temp"]
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.True(result.Brightness);  // Should be implied by both color modes
            Assert.True(result.ColorTemp);
            Assert.True(result.ColorHs);
        }

        #endregion

        #region Heuristic Fallback Testing

        [Fact]
        public void ForLight_NoSupportedColorModes_WithBrightnessAttribute_ReturnsCorrectCaps()
        {
            // Arrange
            var jsonString = """
            {
                "brightness": 128
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.False(result.OnOff);  // Not on/off only since brightness is present
            Assert.True(result.Brightness);
            Assert.False(result.ColorTemp);
            Assert.False(result.ColorHs);
            Assert.Null(result.PreferredColorMode);
        }

        [Fact]
        public void ForLight_NoSupportedColorModes_WithColorTempAttributes_ReturnsCorrectCaps()
        {
            // Arrange
            var jsonString = """
            {
                "min_mireds": 153,
                "max_mireds": 500,
                "color_temp": 250
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.False(result.OnOff);
            Assert.False(result.Brightness);  // Only color_temp detected
            Assert.True(result.ColorTemp);
            Assert.False(result.ColorHs);
        }

        [Fact]
        public void ForLight_NoSupportedColorModes_WithColorTempKelvin_ReturnsCorrectCaps()
        {
            // Arrange
            var jsonString = """
            {
                "color_temp_kelvin": 4000
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.False(result.OnOff);
            Assert.False(result.Brightness);
            Assert.True(result.ColorTemp);
            Assert.False(result.ColorHs);
        }

        [Theory]
        [InlineData("hs_color")]
        [InlineData("rgb_color")]
        [InlineData("xy_color")]
        public void ForLight_NoSupportedColorModes_WithColorAttributes_ReturnsCorrectCaps(string colorAttribute)
        {
            // Arrange
            var jsonString = $$"""
            {
                "{{colorAttribute}}": [120, 50]
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.False(result.OnOff);
            Assert.False(result.Brightness);
            Assert.False(result.ColorTemp);
            Assert.True(result.ColorHs);
        }

        [Fact]
        public void ForLight_NoSupportedColorModes_NoKnownAttributes_ReturnsOnOffOnly()
        {
            // Arrange
            var jsonString = """
            {
                "friendly_name": "Test Light",
                "some_other_attribute": "value"
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.True(result.OnOff);   // Fallback to on/off only
            Assert.False(result.Brightness);
            Assert.False(result.ColorTemp);
            Assert.False(result.ColorHs);
        }

        [Fact]
        public void ForLight_NoSupportedColorModes_MixedAttributes_CombinesCorrectly()
        {
            // Arrange
            var jsonString = """
            {
                "brightness": 128,
                "hs_color": [120, 75],
                "min_mireds": 200
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.False(result.OnOff);  // Not on/off only since other capabilities detected
            Assert.True(result.Brightness);
            Assert.True(result.ColorTemp);
            Assert.True(result.ColorHs);
        }

        #endregion

        #region Case Insensitivity Testing

        [Fact]
        public void ForLight_WithSupportedColorModes_CaseInsensitive_ReturnsCorrectCaps()
        {
            // Arrange - Test case insensitivity
            var jsonString = """
            {
                "supported_color_modes": ["RGB", "COLOR_TEMP", "HS"]
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.True(result.ColorHs);
            Assert.True(result.ColorTemp);
            Assert.True(result.Brightness);
            Assert.Equal("rgb", result.PreferredColorMode);  // Home Assistant uses lowercase
        }

        #endregion

        #region Error Handling and Edge Cases

        [Fact]
        public void ForLight_EmptyJsonObject_ReturnsFallbackCaps()
        {
            // Arrange
            var jsonString = """{}""";
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.True(result.OnOff);   // Fallback to on/off only
            Assert.False(result.Brightness);
            Assert.False(result.ColorTemp);
            Assert.False(result.ColorHs);
        }

        [Fact]
        public void ForLight_NullJsonValue_ReturnsFallbackCaps()
        {
            // Arrange
            var jsonString = """null""";
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert - Should return fallback capabilities for non-object JSON
            Assert.True(result.OnOff);
            Assert.False(result.Brightness);
            Assert.False(result.ColorTemp);
            Assert.False(result.ColorHs);
        }

        [Fact]
        public void ForLight_JsonArray_ReturnsFallbackCaps()
        {
            // Arrange
            var jsonString = """["not", "an", "object"]""";
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert - Should return fallback capabilities for non-object JSON
            Assert.True(result.OnOff);
            Assert.False(result.Brightness);
            Assert.False(result.ColorTemp);
            Assert.False(result.ColorHs);
        }

        [Fact]
        public void ForLight_JsonString_ReturnsFallbackCaps()
        {
            // Arrange
            var jsonString = "\"just a string\"";
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert - Should return fallback capabilities for non-object JSON
            Assert.True(result.OnOff);
            Assert.False(result.Brightness);
            Assert.False(result.ColorTemp);
            Assert.False(result.ColorHs);
        }

        [Fact]
        public void ForLight_EmptySupportedColorModesArray_ReturnsFallbackLogic()
        {
            // Arrange
            var jsonString = """
            {
                "supported_color_modes": [],
                "brightness": 128
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert - Should fall back to heuristic detection
            Assert.False(result.OnOff);
            Assert.True(result.Brightness);
            Assert.False(result.ColorTemp);
            Assert.False(result.ColorHs);
        }

        [Fact]
        public void ForLight_SupportedColorModesNotArray_UsesHeuristics()
        {
            // Arrange
            var jsonString = """
            {
                "supported_color_modes": "not an array",
                "hs_color": [180, 75]
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert - Should fall back to heuristic detection
            Assert.False(result.OnOff);
            Assert.False(result.Brightness);
            Assert.False(result.ColorTemp);
            Assert.True(result.ColorHs);
        }

        [Fact]
        public void ForLight_NonStringColorModes_IgnoresInvalidModes()
        {
            // Arrange
            var jsonString = """
            {
                "supported_color_modes": ["hs", 123, null, "rgb", true]
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert - Should only process string values
            Assert.True(result.ColorHs);
            Assert.Equal("rgb", result.PreferredColorMode);
        }

        #endregion

        #region Malformed JSON Edge Cases

        [Fact]
        public void ForLight_MalformedColorModesWithValidHeuristics_UsesHeuristics()
        {
            // Arrange
            var jsonString = """
            {
                "supported_color_modes": {"not": "array"},
                "brightness": 200,
                "color_temp": 300
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert - Should fall back to heuristic detection
            Assert.False(result.OnOff);
            Assert.True(result.Brightness);
            Assert.True(result.ColorTemp);
            Assert.False(result.ColorHs);
        }

        #endregion

        #region Intersection Logic for Multiple Lights

        [Fact]
        public void ForLight_ComplexCapabilityMix_HandlesAllCombinations()
        {
            // Arrange - Test a light with maximum capabilities
            var jsonString = """
            {
                "supported_color_modes": ["onoff", "brightness", "white", "color_temp", "hs", "rgb", "xy", "rgbw", "rgbww"]
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert - All capabilities should be detected
            Assert.True(result.OnOff);
            Assert.True(result.Brightness);
            Assert.True(result.ColorTemp);
            Assert.True(result.ColorHs);
            Assert.Equal("rgbww", result.PreferredColorMode); // Highest priority
        }

        [Fact]
        public void ForLight_MinimalCapabilities_OnlyOnOff()
        {
            // Arrange
            var jsonString = """
            {
                "supported_color_modes": ["onoff"]
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert - Minimal capabilities
            Assert.True(result.OnOff);
            Assert.False(result.Brightness);
            Assert.False(result.ColorTemp);
            Assert.False(result.ColorHs);
            Assert.Null(result.PreferredColorMode);
        }

        #endregion

        #region Performance and Logging

        [Fact]
        public void ForLight_MultipleInvocations_PerformConsistently()
        {
            // Arrange
            var jsonString = """
            {
                "supported_color_modes": ["rgb", "color_temp"],
                "brightness": 128
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act - Multiple invocations
            var result1 = _capabilityService.ForLight(attributes);
            var result2 = _capabilityService.ForLight(attributes);
            var result3 = _capabilityService.ForLight(attributes);

            // Assert - Results should be identical
            Assert.Equal(result1.OnOff, result2.OnOff);
            Assert.Equal(result1.Brightness, result2.Brightness);
            Assert.Equal(result1.ColorTemp, result2.ColorTemp);
            Assert.Equal(result1.ColorHs, result2.ColorHs);
            Assert.Equal(result1.PreferredColorMode, result2.PreferredColorMode);

            Assert.Equal(result2.OnOff, result3.OnOff);
            Assert.Equal(result2.Brightness, result3.Brightness);
            Assert.Equal(result2.ColorTemp, result3.ColorTemp);
            Assert.Equal(result2.ColorHs, result3.ColorHs);
            Assert.Equal(result2.PreferredColorMode, result3.PreferredColorMode);
        }

        #endregion

        #region Real-World Scenarios

        [Fact]
        public void ForLight_TypicalSmartBulb_ReturnsExpectedCapabilities()
        {
            // Arrange - Simulate a typical smart bulb's attributes
            var jsonString = """
            {
                "supported_color_modes": ["color_temp", "hs"],
                "min_mireds": 153,
                "max_mireds": 500,
                "brightness": 255,
                "hs_color": [120.5, 75.2],
                "color_temp": 250
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.False(result.OnOff);
            Assert.True(result.Brightness);
            Assert.True(result.ColorTemp);
            Assert.True(result.ColorHs);
            Assert.Equal("hs", result.PreferredColorMode);
        }

        [Fact]
        public void ForLight_BasicDimmableBulb_ReturnsExpectedCapabilities()
        {
            // Arrange - Simulate a basic dimmable bulb
            var jsonString = """
            {
                "supported_color_modes": ["brightness"],
                "brightness": 180
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert
            Assert.False(result.OnOff);
            Assert.True(result.Brightness);
            Assert.False(result.ColorTemp);
            Assert.False(result.ColorHs);
            Assert.Null(result.PreferredColorMode);
        }

        [Fact]
        public void ForLight_LegacyLightWithoutColorModes_UsesHeuristics()
        {
            // Arrange - Simulate a legacy light without supported_color_modes
            var jsonString = """
            {
                "brightness": 200,
                "min_mireds": 200,
                "max_mireds": 400,
                "color_temp": 300,
                "rgb_color": [255, 128, 64]
            }
            """;
            var attributes = JsonDocument.Parse(jsonString).RootElement;

            // Act
            var result = _capabilityService.ForLight(attributes);

            // Assert - Should detect capabilities from individual attributes
            Assert.False(result.OnOff);
            Assert.True(result.Brightness);
            Assert.True(result.ColorTemp);
            Assert.True(result.ColorHs);
            Assert.Null(result.PreferredColorMode); // No preferred mode in heuristic mode
        }

        #endregion
    }
}