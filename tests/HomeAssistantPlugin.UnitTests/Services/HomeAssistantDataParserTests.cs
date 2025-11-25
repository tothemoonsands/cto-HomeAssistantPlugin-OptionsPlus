using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

using FluentAssertions;

using Loupedeck.HomeAssistantPlugin.Models;
using Loupedeck.HomeAssistantPlugin.Services;

using NSubstitute;

using Xunit;

namespace HomeAssistantPlugin.UnitTests.Services;

/// <summary>
/// Comprehensive tests for HomeAssistantDataParser focusing on JSON parsing robustness,
/// registry data processing, and edge case handling with 85% coverage target.
/// </summary>
public class HomeAssistantDataParserTests
{
    private readonly ICapabilityService _mockCapabilityService;
    private readonly HomeAssistantDataParser _parser;

    public HomeAssistantDataParserTests()
    {
        this._mockCapabilityService = Substitute.For<ICapabilityService>();
        this._parser = new HomeAssistantDataParser(this._mockCapabilityService);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidCapabilityService_InitializesSuccessfully()
    {
        // Arrange & Act
        var parser = new HomeAssistantDataParser(this._mockCapabilityService);

        // Assert
        parser.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullCapabilityService_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var action = () => new HomeAssistantDataParser(null!);
        action.Should().Throw<ArgumentNullException>()
            .WithMessage("*capabilityService*");
    }

    #endregion

    #region ValidateJsonData Tests

    [Fact]
    public void ValidateJsonData_ValidData_ReturnsTrue()
    {
        // Arrange
        var statesJson = """[{"entity_id": "light.test", "state": "on"}]""";
        var servicesJson = """{"light": {"turn_on": {}}}""";

        // Act
        var result = this._parser.ValidateJsonData(statesJson, servicesJson);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(null, """{"light": {}}""")]
    [InlineData("", """{"light": {}}""")]
    [InlineData("   ", """{"light": {}}""")]
    public void ValidateJsonData_NullOrEmptyStatesJson_ReturnsFalse(String? statesJson, String servicesJson)
    {
        // Act
        var result = this._parser.ValidateJsonData(statesJson, servicesJson);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("""[{"entity_id": "light.test"}]""", null)]
    [InlineData("""[{"entity_id": "light.test"}]""", "")]
    [InlineData("""[{"entity_id": "light.test"}]""", "   ")]
    public void ValidateJsonData_NullOrEmptyServicesJson_ReturnsFalse(String statesJson, String? servicesJson)
    {
        // Act
        var result = this._parser.ValidateJsonData(statesJson, servicesJson);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ParseLightStates Tests - Core Functionality

    [Fact]
    public void ParseLightStates_ValidLightJson_ExtractsLightData()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();
        var lightCaps = new LightCaps(true, true, false, true);
        this._mockCapabilityService.ForLight(Arg.Any<JsonElement>()).Returns(lightCaps);

        var statesJson = """
        [
            {
                "entity_id": "light.living_room",
                "state": "on",
                "attributes": {
                    "friendly_name": "Living Room Light",
                    "brightness": 128,
                    "color_mode": "hs",
                    "hs_color": [120, 75],
                    "supported_color_modes": ["hs", "color_temp"]
                }
            }
        ]
        """;

        // Act
        var result = this._parser.ParseLightStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        var light = result.First();
        light.EntityId.Should().Be("light.living_room");
        light.FriendlyName.Should().Be("Living Room Light");
        light.IsOn.Should().BeTrue();
        light.Brightness.Should().Be(128);
        light.Hue.Should().Be(120);
        light.Saturation.Should().Be(75);
        light.Capabilities.Should().Be(lightCaps);
    }

    [Fact]
    public void ParseLightStates_LightWithRgbColor_ConvertsToHueSaturation()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();
        var lightCaps = new LightCaps(true, true, false, true);
        this._mockCapabilityService.ForLight(Arg.Any<JsonElement>()).Returns(lightCaps);

        var statesJson = """
        [
            {
                "entity_id": "light.rgb_light",
                "state": "on",
                "attributes": {
                    "friendly_name": "RGB Light",
                    "brightness": 255,
                    "rgb_color": [255, 0, 0]
                }
            }
        ]
        """;

        // Act
        var result = this._parser.ParseLightStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        var light = result.First();
        light.EntityId.Should().Be("light.rgb_light");
        light.Hue.Should().BeGreaterOrEqualTo(0).And.BeLessOrEqualTo(360);
        light.Saturation.Should().BeGreaterOrEqualTo(0).And.BeLessOrEqualTo(100);
    }

    [Fact]
    public void ParseLightStates_LightWithColorTemperature_ExtractsTemperatureData()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();
        var lightCaps = new LightCaps(true, true, true, false);
        this._mockCapabilityService.ForLight(Arg.Any<JsonElement>()).Returns(lightCaps);

        var statesJson = """
        [
            {
                "entity_id": "light.temp_light",
                "state": "on",
                "attributes": {
                    "friendly_name": "Temperature Light",
                    "brightness": 200,
                    "color_temp": 300,
                    "min_mireds": 153,
                    "max_mireds": 500
                }
            }
        ]
        """;

        // Act
        var result = this._parser.ParseLightStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        var light = result.First();
        light.EntityId.Should().Be("light.temp_light");
        light.ColorTempMired.Should().Be(300);
        light.MinMired.Should().Be(153);
        light.MaxMired.Should().Be(500);
    }

    [Fact]
    public void ParseLightStates_LightWithKelvinTemperature_ConvertsToMired()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();
        var lightCaps = new LightCaps(true, true, true, false);
        this._mockCapabilityService.ForLight(Arg.Any<JsonElement>()).Returns(lightCaps);

        var statesJson = """
        [
            {
                "entity_id": "light.kelvin_light",
                "state": "on",
                "attributes": {
                    "color_temp_kelvin": 3000,
                    "min_mireds": 153,
                    "max_mireds": 500
                }
            }
        ]
        """;

        // Act
        var result = this._parser.ParseLightStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        var light = result.First();
        light.ColorTempMired.Should().BeInRange(153, 500);
    }

    #endregion

    #region ParseLightStates Tests - Edge Cases

    [Fact]
    public void ParseLightStates_NullStatesJson_ThrowsArgumentException()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();

        // Act & Assert
        var action = () => this._parser.ParseLightStates(null!, registryData);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*States JSON cannot be null or empty*");
    }

    [Fact]
    public void ParseLightStates_EmptyStatesJson_ThrowsArgumentException()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();

        // Act & Assert
        var action = () => this._parser.ParseLightStates("", registryData);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*States JSON cannot be null or empty*");
    }

    [Fact]
    public void ParseLightStates_MalformedJson_ThrowsException()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();
        var malformedJson = """[{"entity_id": "light.test", "state": "on",}]"""; // Trailing comma

        // Act & Assert
        var action = () => this._parser.ParseLightStates(malformedJson, registryData);
        action.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseLightStates_NonLightEntities_FiltersOut()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();
        
        var statesJson = """
        [
            {"entity_id": "switch.test_switch", "state": "on"},
            {"entity_id": "sensor.temperature", "state": "22.5"},
            {"entity_id": "light.valid_light", "state": "off"}
        ]
        """;

        // Act
        var result = this._parser.ParseLightStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        result.First().EntityId.Should().Be("light.valid_light");
    }

    [Fact]
    public void ParseLightStates_LightWithoutEntityId_SkipsEntity()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();
        
        var statesJson = """
        [
            {"state": "on", "attributes": {"friendly_name": "No Entity ID"}},
            {"entity_id": "light.valid", "state": "on"}
        ]
        """;

        // Act
        var result = this._parser.ParseLightStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        result.First().EntityId.Should().Be("light.valid");
    }

    [Fact]
    public void ParseLightStates_LightOffWithoutBrightness_UsesFallbackBrightness()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();
        var lightCaps = new LightCaps(true, false, false, false);
        this._mockCapabilityService.ForLight(Arg.Any<JsonElement>()).Returns(lightCaps);

        var statesJson = """
        [
            {
                "entity_id": "light.simple",
                "state": "off"
            }
        ]
        """;

        // Act
        var result = this._parser.ParseLightStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        var light = result.First();
        light.IsOn.Should().BeFalse();
        light.Brightness.Should().Be(0); // OFF state should have 0 brightness
    }

    [Fact]
    public void ParseLightStates_LightOnWithoutBrightness_UsesMidBrightness()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();
        var lightCaps = new LightCaps(true, false, false, false);
        this._mockCapabilityService.ForLight(Arg.Any<JsonElement>()).Returns(lightCaps);

        var statesJson = """
        [
            {
                "entity_id": "light.simple",
                "state": "on"
            }
        ]
        """;

        // Act
        var result = this._parser.ParseLightStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        var light = result.First();
        light.IsOn.Should().BeTrue();
        light.Brightness.Should().Be(128); // Mid brightness fallback for ON without brightness
    }

    [Theory]
    [InlineData(-50, 0)]   // Below minimum
    [InlineData(0, 0)]     // At minimum
    [InlineData(128, 128)] // Normal value
    [InlineData(255, 255)] // At maximum
    [InlineData(300, 255)] // Above maximum
    public void ParseLightStates_BrightnessValues_ClampsToValidRange(Int32 inputBrightness, Int32 expectedBrightness)
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();
        var lightCaps = new LightCaps(true, true, false, false);
        this._mockCapabilityService.ForLight(Arg.Any<JsonElement>()).Returns(lightCaps);

        var statesJson = $$"""
        [
            {
                "entity_id": "light.brightness_test",
                "state": "on",
                "attributes": {
                    "brightness": {{inputBrightness}}
                }
            }
        ]
        """;

        // Act
        var result = this._parser.ParseLightStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        result.First().Brightness.Should().Be(expectedBrightness);
    }

    [Theory]
    [InlineData(-10, 350)]   // Negative hue should wrap
    [InlineData(0, 0)]       // Zero hue
    [InlineData(180, 180)]   // Normal hue
    [InlineData(360, 0)]     // 360 should wrap to 0
    [InlineData(450, 90)]    // > 360 should wrap
    public void ParseLightStates_HueValues_WrapsCorrectly(Double inputHue, Double expectedHue)
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();
        var lightCaps = new LightCaps(true, true, false, true);
        this._mockCapabilityService.ForLight(Arg.Any<JsonElement>()).Returns(lightCaps);

        var statesJson = $$"""
        [
            {
                "entity_id": "light.hue_test",
                "state": "on",
                "attributes": {
                    "hs_color": [{{inputHue}}, 50]
                }
            }
        ]
        """;

        // Act
        var result = this._parser.ParseLightStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        result.First().Hue.Should().Be(expectedHue);
    }

    [Theory]
    [InlineData(-10, 0)]    // Below minimum
    [InlineData(0, 0)]      // At minimum
    [InlineData(50, 50)]    // Normal value
    [InlineData(100, 100)]  // At maximum
    [InlineData(150, 100)]  // Above maximum
    public void ParseLightStates_SaturationValues_ClampsToValidRange(Double inputSaturation, Double expectedSaturation)
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();
        var lightCaps = new LightCaps(true, true, false, true);
        this._mockCapabilityService.ForLight(Arg.Any<JsonElement>()).Returns(lightCaps);

        var statesJson = $$"""
        [
            {
                "entity_id": "light.saturation_test",
                "state": "on",
                "attributes": {
                    "hs_color": [180, {{inputSaturation}}]
                }
            }
        ]
        """;

        // Act
        var result = this._parser.ParseLightStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        result.First().Saturation.Should().Be(expectedSaturation);
    }

    #endregion

    #region ParseLightStates Tests - Registry Integration

    [Fact]
    public void ParseLightStates_WithRegistryData_MapsDeviceAndAreaInformation()
    {
        // Arrange
        var registryData = new ParsedRegistryData(
            deviceById: new Dictionary<String, (String name, String mf, String model)>(StringComparer.OrdinalIgnoreCase)
            {
                ["device123"] = ("Smart Bulb Pro", "ACME", "SB-100")
            },
            deviceAreaById: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
            {
                ["device123"] = "area_living_room"
            },
            entityDevice: new Dictionary<String, (String deviceId, String originalName)>(StringComparer.OrdinalIgnoreCase)
            {
                ["light.living_room"] = ("device123", "Living Room Light")
            },
            entityArea: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase),
            areaIdToName: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
            {
                ["area_living_room"] = "Living Room"
            }
        );

        var lightCaps = new LightCaps(true, true, false, true);
        this._mockCapabilityService.ForLight(Arg.Any<JsonElement>()).Returns(lightCaps);

        var statesJson = """
        [
            {
                "entity_id": "light.living_room",
                "state": "on",
                "attributes": {
                    "friendly_name": "Living Room Light"
                }
            }
        ]
        """;

        // Act
        var result = this._parser.ParseLightStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        var light = result.First();
        light.DeviceId.Should().Be("device123");
        light.DeviceName.Should().Be("Smart Bulb Pro");
        light.Manufacturer.Should().Be("ACME");
        light.Model.Should().Be("SB-100");
        light.AreaId.Should().Be("area_living_room");
    }

    [Fact]
    public void ParseLightStates_EntityAreaOverridesDeviceArea()
    {
        // Arrange - Entity has direct area assignment that should override device area
        var registryData = new ParsedRegistryData(
            deviceById: new Dictionary<String, (String name, String mf, String model)>(StringComparer.OrdinalIgnoreCase)
            {
                ["device123"] = ("Test Device", "ACME", "TD-1")
            },
            deviceAreaById: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
            {
                ["device123"] = "area_bedroom" // Device assigned to bedroom
            },
            entityDevice: new Dictionary<String, (String deviceId, String originalName)>(StringComparer.OrdinalIgnoreCase)
            {
                ["light.test"] = ("device123", "Test Light")
            },
            entityArea: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
            {
                ["light.test"] = "area_kitchen" // Entity directly assigned to kitchen - should win
            },
            areaIdToName: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
            {
                ["area_bedroom"] = "Bedroom",
                ["area_kitchen"] = "Kitchen"
            }
        );

        var lightCaps = new LightCaps(true, false, false, false);
        this._mockCapabilityService.ForLight(Arg.Any<JsonElement>()).Returns(lightCaps);

        var statesJson = """
        [
            {
                "entity_id": "light.test",
                "state": "on"
            }
        ]
        """;

        // Act
        var result = this._parser.ParseLightStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        result.First().AreaId.Should().Be("area_kitchen"); // Entity area should override device area
    }

    [Fact]
    public void ParseLightStates_NoAreaAssignment_UsesUnassignedArea()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();
        var lightCaps = new LightCaps(true, false, false, false);
        this._mockCapabilityService.ForLight(Arg.Any<JsonElement>()).Returns(lightCaps);

        var statesJson = """
        [
            {
                "entity_id": "light.orphaned",
                "state": "on"
            }
        ]
        """;

        // Act
        var result = this._parser.ParseLightStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        result.First().AreaId.Should().Be("!unassigned");
    }

    #endregion

    #region ParseRegistries Tests

    [Fact]
    public void ParseRegistries_ValidData_ParsesAllRegistries()
    {
        // Arrange
        var deviceJson = """
        [
            {
                "id": "device1",
                "name": "Test Device",
                "manufacturer": "ACME Corp",
                "model": "TD-100",
                "area_id": "area1"
            }
        ]
        """;

        var entityJson = """
        [
            {
                "entity_id": "light.test",
                "device_id": "device1",
                "original_name": "Test Light",
                "area_id": "area2"
            }
        ]
        """;

        var areaJson = """
        [
            {
                "area_id": "area1",
                "name": "Living Room"
            },
            {
                "area_id": "area2", 
                "name": "Kitchen"
            }
        ]
        """;

        // Act
        var result = this._parser.ParseRegistries(deviceJson, entityJson, areaJson);

        // Assert
        result.Should().NotBeNull();
        result.DeviceById.Should().ContainKey("device1");
        result.DeviceById["device1"].name.Should().Be("Test Device");
        result.DeviceById["device1"].mf.Should().Be("ACME Corp");
        result.DeviceById["device1"].model.Should().Be("TD-100");
        
        result.DeviceAreaById.Should().ContainKey("device1").WhoseValue.Should().Be("area1");
        
        result.EntityDevice.Should().ContainKey("light.test");
        result.EntityDevice["light.test"].deviceId.Should().Be("device1");
        result.EntityDevice["light.test"].originalName.Should().Be("Test Light");
        
        result.EntityArea.Should().ContainKey("light.test").WhoseValue.Should().Be("area2");
        
        result.AreaIdToName.Should().ContainKey("area1").WhoseValue.Should().Be("Living Room");
        result.AreaIdToName.Should().ContainKey("area2").WhoseValue.Should().Be("Kitchen");
        result.AreaIdToName.Should().ContainKey("!unassigned").WhoseValue.Should().Be("(No area)");
    }

    [Fact]
    public void ParseRegistries_NullData_ReturnsEmptyStructures()
    {
        // Act
        var result = this._parser.ParseRegistries(null, null, null);

        // Assert
        result.Should().NotBeNull();
        result.DeviceById.Should().BeEmpty();
        result.DeviceAreaById.Should().BeEmpty();
        result.EntityDevice.Should().BeEmpty();
        result.EntityArea.Should().BeEmpty();
        result.AreaIdToName.Should().ContainKey("!unassigned"); // Only unassigned area should exist
    }

    [Fact]
    public void ParseRegistries_MalformedDeviceJson_HandlesGracefully()
    {
        // Arrange
        var malformedDeviceJson = """[{"id": "device1", "name":}]"""; // Malformed JSON

        // Act
        var result = this._parser.ParseRegistries(malformedDeviceJson, null, null);

        // Assert - Should not throw, should return empty device data
        result.Should().NotBeNull();
        result.DeviceById.Should().BeEmpty();
    }

    #endregion

    #region ProcessServices Tests

    [Fact]
    public void ProcessServices_ValidServicesJson_ProcessesSuccessfully()
    {
        // Arrange
        var servicesJson = """
        {
            "light": {
                "turn_on": {
                    "fields": {
                        "brightness": {},
                        "hs_color": {},
                        "color_temp": {}
                    },
                    "target": {}
                },
                "turn_off": {
                    "fields": {},
                    "target": {}
                }
            }
        }
        """;

        // Act & Assert - Should not throw
        var action = () => this._parser.ProcessServices(servicesJson);
        action.Should().NotThrow();
    }

    [Fact]
    public void ProcessServices_NullServicesJson_HandlesGracefully()
    {
        // Act & Assert - Should not throw
        var action = () => this._parser.ProcessServices(null!);
        action.Should().NotThrow();
    }

    [Fact]
    public void ProcessServices_EmptyServicesJson_HandlesGracefully()
    {
        // Act & Assert - Should not throw
        var action = () => this._parser.ProcessServices("");
        action.Should().NotThrow();
    }

    [Fact]
    public void ProcessServices_MalformedJson_HandlesGracefully()
    {
        // Arrange
        var malformedJson = """{"light": {"turn_on":}}"""; // Malformed

        // Act & Assert - Should not throw
        var action = () => this._parser.ProcessServices(malformedJson);
        action.Should().NotThrow();
    }

    [Fact]
    public void ProcessServices_NoLightDomain_HandlesGracefully()
    {
        // Arrange
        var servicesJson = """
        {
            "switch": {
                "turn_on": {}
            }
        }
        """;

        // Act & Assert - Should not throw
        var action = () => this._parser.ProcessServices(servicesJson);
        action.Should().NotThrow();
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void ParseLightStates_LargePayload_PerformsReasonably()
    {
        // Arrange - Create a large number of lights
        var registryData = CreateEmptyRegistryData();
        var lightCaps = new LightCaps(true, true, false, true);
        this._mockCapabilityService.ForLight(Arg.Any<JsonElement>()).Returns(lightCaps);

        var lightEntities = new List<String>();
        for (var i = 0; i < 1000; i++)
        {
            lightEntities.Add($$"""
            {
                "entity_id": "light.test_{{i}}",
                "state": "{{(i % 2 == 0 ? "on" : "off")}}",
                "attributes": {
                    "friendly_name": "Test Light {{i}}",
                    "brightness": {{(i % 255) + 1}},
                    "hs_color": [{{i % 360}}, {{(i % 100) + 1}}]
                }
            }
            """);
        }

        var statesJson = $"[{String.Join(",", lightEntities)}]";

        // Act & Assert - Should complete without timeout
        var result = this._parser.ParseLightStates(statesJson, registryData);
        result.Should().HaveCount(1000);
    }

    #endregion

    #region Helper Methods

    private static ParsedRegistryData CreateEmptyRegistryData()
    {
        return new ParsedRegistryData(
            deviceById: new Dictionary<String, (String name, String mf, String model)>(StringComparer.OrdinalIgnoreCase),
            deviceAreaById: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase),
            entityDevice: new Dictionary<String, (String deviceId, String originalName)>(StringComparer.OrdinalIgnoreCase),
            entityArea: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase),
            areaIdToName: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
        );
    }

    #endregion
}