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
        var capabilityService = new CapabilityService();
        this._parser = new HomeAssistantDataParser(capabilityService);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidCapabilityService_InitializesSuccessfully()
    {
        // Arrange & Act
        var capabilityService = new CapabilityService();
        var parser = new HomeAssistantDataParser(capabilityService);

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
        var lightCaps = new Models.LightCaps(true, true, false, true);
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
        var lightCaps = new Models.LightCaps(true, true, false, true);
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
        var lightCaps = new Models.LightCaps(true, true, true, false);
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
        var lightCaps = new Models.LightCaps(true, true, true, false);
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
            DeviceById: new Dictionary<String, (String name, String mf, String model)>(StringComparer.OrdinalIgnoreCase)
            {
                ["device123"] = ("Smart Bulb Pro", "ACME", "SB-100")
            },
            DeviceAreaById: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
            {
                ["device123"] = "area_living_room"
            },
            EntityDevice: new Dictionary<String, (String deviceId, String originalName)>(StringComparer.OrdinalIgnoreCase)
            {
                ["light.living_room"] = ("device123", "Living Room Light")
            },
            EntityArea: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase),
            AreaIdToName: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
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
            DeviceById: new Dictionary<String, (String name, String mf, String model)>(StringComparer.OrdinalIgnoreCase)
            {
                ["device123"] = ("Test Device", "ACME", "TD-1")
            },
            DeviceAreaById: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
            {
                ["device123"] = "area_bedroom" // Device assigned to bedroom
            },
            EntityDevice: new Dictionary<String, (String deviceId, String originalName)>(StringComparer.OrdinalIgnoreCase)
            {
                ["light.test"] = ("device123", "Test Light")
            },
            EntityArea: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
            {
                ["light.test"] = "area_kitchen" // Entity directly assigned to kitchen - should win
            },
            AreaIdToName: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
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
            DeviceById: new Dictionary<String, (String name, String mf, String model)>(StringComparer.OrdinalIgnoreCase),
            DeviceAreaById: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase),
            EntityDevice: new Dictionary<String, (String deviceId, String originalName)>(StringComparer.OrdinalIgnoreCase),
            EntityArea: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase),
            AreaIdToName: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
        );
    }

    #endregion

    #region ParseSwitchStates Tests - Core Functionality

    [Fact]
    public void ParseSwitchStates_ValidSwitchJson_ExtractsSwitchData()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();
        var switchCaps = new SwitchCaps(true);

        var statesJson = """
        [
            {
                "entity_id": "switch.living_room",
                "state": "on",
                "attributes": {
                    "friendly_name": "Living Room Switch",
                    "device_class": "switch"
                }
            }
        ]
        """;

        // Act
        var result = this._parser.ParseSwitchStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        var switchEntity = result.First();
        switchEntity.EntityId.Should().Be("switch.living_room");
        switchEntity.FriendlyName.Should().Be("Living Room Switch");
        switchEntity.IsOn.Should().BeTrue();
        switchEntity.State.Should().Be("on");
        switchEntity.Capabilities.OnOff.Should().BeTrue();
    }

    [Fact]
    public void ParseSwitchStates_SwitchWithOutletDeviceClass_ExtractsSwitchData()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();

        var statesJson = """
        [
            {
                "entity_id": "switch.smart_outlet",
                "state": "off",
                "attributes": {
                    "friendly_name": "Smart Outlet",
                    "device_class": "outlet",
                    "icon": "mdi:power-socket"
                }
            }
        ]
        """;

        // Act
        var result = this._parser.ParseSwitchStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        var switchEntity = result.First();
        switchEntity.EntityId.Should().Be("switch.smart_outlet");
        switchEntity.FriendlyName.Should().Be("Smart Outlet");
        switchEntity.IsOn.Should().BeFalse();
        switchEntity.State.Should().Be("off");
        switchEntity.Capabilities.OnOff.Should().BeTrue();
    }

    [Fact]
    public void ParseSwitchStates_MultipleSwitches_ExtractsAllSwitchData()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();

        var statesJson = """
        [
            {
                "entity_id": "switch.living_room",
                "state": "on",
                "attributes": {
                    "friendly_name": "Living Room Switch"
                }
            },
            {
                "entity_id": "switch.kitchen_outlet",
                "state": "off",
                "attributes": {
                    "friendly_name": "Kitchen Outlet",
                    "device_class": "outlet"
                }
            },
            {
                "entity_id": "switch.garden_sprinkler",
                "state": "on",
                "attributes": {
                    "friendly_name": "Garden Sprinkler",
                    "device_class": "switch"
                }
            }
        ]
        """;

        // Act
        var result = this._parser.ParseSwitchStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(3);
        
        var livingRoomSwitch = result.FirstOrDefault(s => s.EntityId == "switch.living_room");
        livingRoomSwitch.Should().NotBeNull();
        livingRoomSwitch!.IsOn.Should().BeTrue();
        
        var kitchenOutlet = result.FirstOrDefault(s => s.EntityId == "switch.kitchen_outlet");
        kitchenOutlet.Should().NotBeNull();
        kitchenOutlet!.IsOn.Should().BeFalse();
        
        var gardenSprinkler = result.FirstOrDefault(s => s.EntityId == "switch.garden_sprinkler");
        gardenSprinkler.Should().NotBeNull();
        gardenSprinkler!.IsOn.Should().BeTrue();
    }

    #endregion

    #region ParseSwitchStates Tests - Edge Cases

    [Fact]
    public void ParseSwitchStates_NullStatesJson_ThrowsArgumentException()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();

        // Act & Assert
        var action = () => this._parser.ParseSwitchStates(null!, registryData);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*States JSON cannot be null or empty*");
    }

    [Fact]
    public void ParseSwitchStates_EmptyStatesJson_ThrowsArgumentException()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();

        // Act & Assert
        var action = () => this._parser.ParseSwitchStates("", registryData);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*States JSON cannot be null or empty*");
    }

    [Fact]
    public void ParseSwitchStates_MalformedJson_ThrowsException()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();
        var malformedJson = """[{"entity_id": "switch.test", "state": "on",}]"""; // Trailing comma

        // Act & Assert
        var action = () => this._parser.ParseSwitchStates(malformedJson, registryData);
        action.Should().Throw<Exception>();
    }

    [Fact]
    public void ParseSwitchStates_NonSwitchEntities_FiltersOut()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();
        
        var statesJson = """
        [
            {"entity_id": "light.test_light", "state": "on"},
            {"entity_id": "sensor.temperature", "state": "22.5"},
            {"entity_id": "switch.valid_switch", "state": "off"}
        ]
        """;

        // Act
        var result = this._parser.ParseSwitchStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        result.First().EntityId.Should().Be("switch.valid_switch");
    }

    [Fact]
    public void ParseSwitchStates_SwitchWithoutEntityId_SkipsEntity()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();
        
        var statesJson = """
        [
            {"state": "on", "attributes": {"friendly_name": "No Entity ID"}},
            {"entity_id": "switch.valid", "state": "on"}
        ]
        """;

        // Act
        var result = this._parser.ParseSwitchStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        result.First().EntityId.Should().Be("switch.valid");
    }

    [Fact]
    public void ParseSwitchStates_SwitchWithoutFriendlyName_UseEntityIdAsName()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();

        var statesJson = """
        [
            {
                "entity_id": "switch.no_name",
                "state": "on"
            }
        ]
        """;

        // Act
        var result = this._parser.ParseSwitchStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        var switchEntity = result.First();
        switchEntity.FriendlyName.Should().Be("switch.no_name"); // Should fall back to entity ID
    }

    [Fact]
    public void ParseSwitchStates_SwitchWithoutAttributes_HandlesGracefully()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();

        var statesJson = """
        [
            {
                "entity_id": "switch.minimal",
                "state": "off"
            }
        ]
        """;

        // Act
        var result = this._parser.ParseSwitchStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        var switchEntity = result.First();
        switchEntity.EntityId.Should().Be("switch.minimal");
        switchEntity.IsOn.Should().BeFalse();
        switchEntity.Capabilities.OnOff.Should().BeTrue();
    }

    #endregion

    #region ParseSwitchStates Tests - Registry Integration

    [Fact]
    public void ParseSwitchStates_WithRegistryData_MapsDeviceAndAreaInformation()
    {
        // Arrange
        var registryData = new ParsedRegistryData(
            DeviceById: new Dictionary<String, (String name, String mf, String model)>(StringComparer.OrdinalIgnoreCase)
            {
                ["device123"] = ("Smart Switch Pro", "ACME", "SS-100")
            },
            DeviceAreaById: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
            {
                ["device123"] = "area_living_room"
            },
            EntityDevice: new Dictionary<String, (String deviceId, String originalName)>(StringComparer.OrdinalIgnoreCase)
            {
                ["switch.living_room"] = ("device123", "Living Room Switch")
            },
            EntityArea: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase),
            AreaIdToName: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
            {
                ["area_living_room"] = "Living Room"
            }
        );

        var statesJson = """
        [
            {
                "entity_id": "switch.living_room",
                "state": "on",
                "attributes": {
                    "friendly_name": "Living Room Switch"
                }
            }
        ]
        """;

        // Act
        var result = this._parser.ParseSwitchStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        var switchEntity = result.First();
        switchEntity.DeviceId.Should().Be("device123");
        switchEntity.DeviceName.Should().Be("Smart Switch Pro");
        switchEntity.Manufacturer.Should().Be("ACME");
        switchEntity.Model.Should().Be("SS-100");
        switchEntity.AreaId.Should().Be("area_living_room");
    }

    [Fact]
    public void ParseSwitchStates_EntityAreaOverridesDeviceArea()
    {
        // Arrange - Entity has direct area assignment that should override device area
        var registryData = new ParsedRegistryData(
            DeviceById: new Dictionary<String, (String name, String mf, String model)>(StringComparer.OrdinalIgnoreCase)
            {
                ["device123"] = ("Test Device", "ACME", "TD-1")
            },
            DeviceAreaById: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
            {
                ["device123"] = "area_bedroom" // Device assigned to bedroom
            },
            EntityDevice: new Dictionary<String, (String deviceId, String originalName)>(StringComparer.OrdinalIgnoreCase)
            {
                ["switch.test"] = ("device123", "Test Switch")
            },
            EntityArea: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
            {
                ["switch.test"] = "area_kitchen" // Entity directly assigned to kitchen - should win
            },
            AreaIdToName: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
            {
                ["area_bedroom"] = "Bedroom",
                ["area_kitchen"] = "Kitchen"
            }
        );

        var statesJson = """
        [
            {
                "entity_id": "switch.test",
                "state": "on"
            }
        ]
        """;

        // Act
        var result = this._parser.ParseSwitchStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        result.First().AreaId.Should().Be("area_kitchen"); // Entity area should override device area
    }

    [Fact]
    public void ParseSwitchStates_NoAreaAssignment_UsesUnassignedArea()
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();

        var statesJson = """
        [
            {
                "entity_id": "switch.orphaned",
                "state": "on"
            }
        ]
        """;

        // Act
        var result = this._parser.ParseSwitchStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        result.First().AreaId.Should().Be("!unassigned");
    }

    #endregion

    #region ParseSwitchStates Tests - State Handling

    [Theory]
    [InlineData("on", true)]
    [InlineData("ON", true)]
    [InlineData("On", true)]
    [InlineData("off", false)]
    [InlineData("OFF", false)]
    [InlineData("Off", false)]
    [InlineData("unknown", false)]
    [InlineData("unavailable", false)]
    [InlineData("", false)]
    public void ParseSwitchStates_VariousStates_HandlesCorrectly(String state, Boolean expectedIsOn)
    {
        // Arrange
        var registryData = CreateEmptyRegistryData();

        var statesJson = $$"""
        [
            {
                "entity_id": "switch.state_test",
                "state": "{{state}}",
                "attributes": {
                    "friendly_name": "State Test Switch"
                }
            }
        ]
        """;

        // Act
        var result = this._parser.ParseSwitchStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        var switchEntity = result.First();
        switchEntity.IsOn.Should().Be(expectedIsOn);
        switchEntity.State.Should().Be(state);
    }

    #endregion

    #region ParseSwitchStates Tests - Performance

    [Fact]
    public void ParseSwitchStates_LargePayload_PerformsReasonably()
    {
        // Arrange - Create a large number of switches
        var registryData = CreateEmptyRegistryData();

        var switchEntities = new List<String>();
        for (var i = 0; i < 1000; i++)
        {
            switchEntities.Add($$"""
            {
                "entity_id": "switch.test_{{i}}",
                "state": "{{(i % 2 == 0 ? "on" : "off")}}",
                "attributes": {
                    "friendly_name": "Test Switch {{i}}",
                    "device_class": "{{(i % 3 == 0 ? "outlet" : "switch")}}"
                }
            }
            """);
        }

        var statesJson = $"[{String.Join(",", switchEntities)}]";

        // Act & Assert - Should complete without timeout
        var result = this._parser.ParseSwitchStates(statesJson, registryData);
        result.Should().HaveCount(1000);
    }

    #endregion

    #region ParseSwitchStates Tests - Real-world Scenarios

    [Fact]
    public void ParseSwitchStates_PhilipsHueSmartPlug_ParsesCorrectly()
    {
        // Arrange - Typical Philips Hue smart plug
        var registryData = CreateEmptyRegistryData();

        var statesJson = """
        [
            {
                "entity_id": "switch.hue_smart_plug_1",
                "state": "on",
                "attributes": {
                    "friendly_name": "Hue Smart Plug 1",
                    "device_class": "outlet",
                    "supported_features": 0
                }
            }
        ]
        """;

        // Act
        var result = this._parser.ParseSwitchStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        var switchEntity = result.First();
        switchEntity.EntityId.Should().Be("switch.hue_smart_plug_1");
        switchEntity.FriendlyName.Should().Be("Hue Smart Plug 1");
        switchEntity.IsOn.Should().BeTrue();
        switchEntity.Capabilities.OnOff.Should().BeTrue();
    }

    [Fact]
    public void ParseSwitchStates_ShellyRelaySwitch_ParsesCorrectly()
    {
        // Arrange - Typical Shelly relay switch
        var registryData = CreateEmptyRegistryData();

        var statesJson = """
        [
            {
                "entity_id": "switch.shelly_relay_0",
                "state": "off",
                "attributes": {
                    "friendly_name": "Shelly 1 Relay 0",
                    "device_class": "switch",
                    "icon": "mdi:electric-switch"
                }
            }
        ]
        """;

        // Act
        var result = this._parser.ParseSwitchStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        var switchEntity = result.First();
        switchEntity.EntityId.Should().Be("switch.shelly_relay_0");
        switchEntity.FriendlyName.Should().Be("Shelly 1 Relay 0");
        switchEntity.IsOn.Should().BeFalse();
        switchEntity.Capabilities.OnOff.Should().BeTrue();
    }

    [Fact]
    public void ParseSwitchStates_ZigbeeWallSwitch_ParsesCorrectly()
    {
        // Arrange - Typical Zigbee wall switch
        var registryData = CreateEmptyRegistryData();

        var statesJson = """
        [
            {
                "entity_id": "switch.bedroom_wall_switch",
                "state": "on",
                "attributes": {
                    "friendly_name": "Bedroom Wall Switch",
                    "device_class": "switch",
                    "supported_features": 0,
                    "attribution": "Data provided by zigbee2mqtt"
                }
            }
        ]
        """;

        // Act
        var result = this._parser.ParseSwitchStates(statesJson, registryData);

        // Assert
        result.Should().HaveCount(1);
        var switchEntity = result.First();
        switchEntity.EntityId.Should().Be("switch.bedroom_wall_switch");
        switchEntity.FriendlyName.Should().Be("Bedroom Wall Switch");
        switchEntity.IsOn.Should().BeTrue();
        switchEntity.Capabilities.OnOff.Should().BeTrue();
    }

    #endregion
}