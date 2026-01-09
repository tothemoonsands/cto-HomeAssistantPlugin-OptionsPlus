using System;
using System.Collections.Generic;
using System.Linq;

using FluentAssertions;

using Loupedeck.HomeAssistantPlugin.Models;
using Loupedeck.HomeAssistantPlugin.Services;

using Xunit;

namespace Loupedeck.HomeAssistantPlugin.Tests.Services;

/// <summary>
/// Comprehensive tests for RegistryService focusing on device/entity/area mapping logic,
/// fallback scenarios, and registry data caching with 85% coverage target.
/// </summary>
public class RegistryServiceTests
{
    private readonly RegistryService _registryService;

    public RegistryServiceTests()
    {
        this._registryService = new RegistryService();
    }

    #region Constructor and Basic Setup Tests

    [Fact]
    public void Constructor_CreatesEmptyRegistries()
    {
        // Arrange & Act
        var service = new RegistryService();

        // Assert
        var stats = service.GetRegistryStats();
        stats.DeviceCount.Should().Be(0);
        stats.EntityCount.Should().Be(0);
        stats.AreaCount.Should().Be(1); // Should have unassigned area
        
        service.GetAreaName("!unassigned").Should().Be("(No area)");
    }

    #endregion

    #region UpdateRegistries Tests

    [Fact]
    public void UpdateRegistries_WithValidData_UpdatesAllRegistries()
    {
        // Arrange
        var parsedData = CreateTestRegistryData();

        // Act
        this._registryService.UpdateRegistries(parsedData);

        // Assert
        var stats = this._registryService.GetRegistryStats();
        stats.DeviceCount.Should().Be(2);
        stats.EntityCount.Should().Be(3);
        stats.AreaCount.Should().Be(3); // 2 test areas + unassigned

        // Verify device data
        var deviceInfo = this._registryService.GetDeviceInfo("device1");
        deviceInfo.name.Should().Be("Smart Bulb Pro");
        deviceInfo.manufacturer.Should().Be("ACME");
        deviceInfo.model.Should().Be("SB-100");

        // Verify area data
        this._registryService.GetAreaName("area_living_room").Should().Be("Living Room");
        this._registryService.GetAreaName("area_kitchen").Should().Be("Kitchen");
    }

    [Fact]
    public void UpdateRegistries_EnsuresUnassignedAreaExists()
    {
        // Arrange
        var parsedData = new ParsedRegistryData(
            DeviceById: new Dictionary<String, (String name, String mf, String model)>(),
            DeviceAreaById: new Dictionary<String, String>(),
            EntityDevice: new Dictionary<String, (String deviceId, String originalName)>(),
            EntityArea: new Dictionary<String, String>(),
            AreaIdToName: new Dictionary<String, String>() // No unassigned area
        );

        // Act
        this._registryService.UpdateRegistries(parsedData);

        // Assert
        this._registryService.AreaExists("!unassigned").Should().BeTrue();
        this._registryService.GetAreaName("!unassigned").Should().Be("(No area)");
    }

    [Fact]
    public void UpdateRegistries_WithEmptyData_ClearsExistingData()
    {
        // Arrange - First populate with data
        var initialData = CreateTestRegistryData();
        this._registryService.UpdateRegistries(initialData);
        
        var emptyData = new ParsedRegistryData(
            DeviceById: new Dictionary<String, (String name, String mf, String model)>(),
            DeviceAreaById: new Dictionary<String, String>(),
            EntityDevice: new Dictionary<String, (String deviceId, String originalName)>(),
            EntityArea: new Dictionary<String, String>(),
            AreaIdToName: new Dictionary<String, String>()
        );

        // Act
        this._registryService.UpdateRegistries(emptyData);

        // Assert
        var stats = this._registryService.GetRegistryStats();
        stats.DeviceCount.Should().Be(0);
        stats.EntityCount.Should().Be(0);
        stats.AreaCount.Should().Be(1); // Only unassigned area remains
    }

    #endregion

    #region GetDeviceArea Tests

    [Fact]
    public void GetDeviceArea_ExistingDevice_ReturnsAreaId()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var areaId = this._registryService.GetDeviceArea("device1");

        // Assert
        areaId.Should().Be("area_living_room");
    }

    [Fact]
    public void GetDeviceArea_NonExistentDevice_ReturnsNull()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var areaId = this._registryService.GetDeviceArea("nonexistent_device");

        // Assert
        areaId.Should().BeNull();
    }

    [Fact]
    public void GetDeviceArea_CaseInsensitive_ReturnsAreaId()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var areaId = this._registryService.GetDeviceArea("DEVICE1");

        // Assert
        areaId.Should().Be("area_living_room");
    }

    #endregion

    #region GetEntityArea Tests

    [Fact]
    public void GetEntityArea_EntityWithDirectArea_ReturnsEntityArea()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var areaId = this._registryService.GetEntityArea("light.kitchen_main");

        // Assert
        areaId.Should().Be("area_kitchen"); // Direct entity area assignment
    }

    [Fact]
    public void GetEntityArea_EntityWithoutDirectAreaButWithDevice_ReturnsDeviceArea()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var areaId = this._registryService.GetEntityArea("light.living_room_main");

        // Assert
        areaId.Should().Be("area_living_room"); // Falls back to device area
    }

    [Fact]
    public void GetEntityArea_EntityWithNoAreaAssignment_ReturnsNull()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var areaId = this._registryService.GetEntityArea("light.orphaned");

        // Assert
        areaId.Should().BeNull();
    }

    [Fact]
    public void GetEntityArea_NonExistentEntity_ReturnsNull()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var areaId = this._registryService.GetEntityArea("light.nonexistent");

        // Assert
        areaId.Should().BeNull();
    }

    [Fact]
    public void GetEntityArea_EntityAreaOverridesDeviceArea()
    {
        // Arrange - Create scenario where entity has direct area that differs from device area
        var testData = new ParsedRegistryData(
            DeviceById: new Dictionary<String, (String name, String mf, String model)>(StringComparer.OrdinalIgnoreCase)
            {
                ["device_test"] = ("Test Device", "ACME", "TD-1")
            },
            DeviceAreaById: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
            {
                ["device_test"] = "area_bedroom" // Device in bedroom
            },
            EntityDevice: new Dictionary<String, (String deviceId, String originalName)>(StringComparer.OrdinalIgnoreCase)
            {
                ["light.test"] = ("device_test", "Test Light")
            },
            EntityArea: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
            {
                ["light.test"] = "area_kitchen" // Entity directly assigned to kitchen
            },
            AreaIdToName: new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
            {
                ["area_bedroom"] = "Bedroom",
                ["area_kitchen"] = "Kitchen"
            }
        );

        this._registryService.UpdateRegistries(testData);

        // Act
        var areaId = this._registryService.GetEntityArea("light.test");

        // Assert
        areaId.Should().Be("area_kitchen"); // Entity area should override device area
    }

    #endregion

    #region GetAreaName Tests

    [Fact]
    public void GetAreaName_ExistingArea_ReturnsAreaName()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var areaName = this._registryService.GetAreaName("area_living_room");

        // Assert
        areaName.Should().Be("Living Room");
    }

    [Fact]
    public void GetAreaName_NonExistentArea_ReturnsNull()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var areaName = this._registryService.GetAreaName("area_nonexistent");

        // Assert
        areaName.Should().BeNull();
    }

    [Fact]
    public void GetAreaName_UnassignedArea_ReturnsCorrectName()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var areaName = this._registryService.GetAreaName("!unassigned");

        // Assert
        areaName.Should().Be("(No area)");
    }

    #endregion

    #region GetDeviceInfo Tests

    [Fact]
    public void GetDeviceInfo_ExistingDevice_ReturnsCompleteInfo()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var deviceInfo = this._registryService.GetDeviceInfo("device1");

        // Assert
        deviceInfo.name.Should().Be("Smart Bulb Pro");
        deviceInfo.manufacturer.Should().Be("ACME");
        deviceInfo.model.Should().Be("SB-100");
    }

    [Fact]
    public void GetDeviceInfo_NonExistentDevice_ReturnsEmptyStrings()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var deviceInfo = this._registryService.GetDeviceInfo("nonexistent_device");

        // Assert
        deviceInfo.name.Should().Be("");
        deviceInfo.manufacturer.Should().Be("");
        deviceInfo.model.Should().Be("");
    }

    #endregion

    #region GetAreasWithLights Tests

    [Fact]
    public void GetAreasWithLights_WithValidLights_ReturnsOrderedAreas()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());
        var lightEntityIds = new[] { "light.living_room_main", "light.kitchen_main", "light.orphaned" };

        // Act
        var areas = this._registryService.GetAreasWithLights(lightEntityIds).ToList();

        // Assert
        areas.Should().HaveCount(3);
        areas.Should().Contain("area_living_room");
        areas.Should().Contain("area_kitchen");
        areas.Should().Contain("!unassigned"); // Orphaned light should be in unassigned
        
        // Should be ordered by area name
        areas.Should().BeInAscendingOrder(aid => this._registryService.GetAreaName(aid) ?? aid);
    }

    [Fact]
    public void GetAreasWithLights_EmptyLightList_ReturnsEmpty()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var areas = this._registryService.GetAreasWithLights(Array.Empty<String>());

        // Assert
        areas.Should().BeEmpty();
    }

    [Fact]
    public void GetAreasWithLights_DuplicateAreas_ReturnsUniqueAreas()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());
        var lightEntityIds = new[] { "light.living_room_main", "light.living_room_secondary" };

        // Act
        var areas = this._registryService.GetAreasWithLights(lightEntityIds).ToList();

        // Assert
        areas.Should().HaveCount(1);
        areas.Should().Contain("area_living_room");
    }

    #endregion

    #region GetLightsInArea Tests

    [Fact]
    public void GetLightsInArea_ValidArea_ReturnsLightsInArea()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());
        var allLights = new[] { "light.living_room_main", "light.kitchen_main", "light.orphaned" };

        // Act
        var lightsInLivingRoom = this._registryService.GetLightsInArea("area_living_room", allLights).ToList();

        // Assert
        lightsInLivingRoom.Should().HaveCount(1);
        lightsInLivingRoom.Should().Contain("light.living_room_main");
    }

    [Fact]
    public void GetLightsInArea_UnassignedArea_ReturnsOrphanedLights()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());
        var allLights = new[] { "light.living_room_main", "light.kitchen_main", "light.orphaned" };

        // Act
        var orphanedLights = this._registryService.GetLightsInArea("!unassigned", allLights).ToList();

        // Assert
        orphanedLights.Should().HaveCount(1);
        orphanedLights.Should().Contain("light.orphaned");
    }

    [Fact]
    public void GetLightsInArea_NonExistentArea_ReturnsEmpty()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());
        var allLights = new[] { "light.living_room_main", "light.kitchen_main" };

        // Act
        var lights = this._registryService.GetLightsInArea("area_nonexistent", allLights).ToList();

        // Assert
        lights.Should().BeEmpty();
    }

    #endregion

    #region GetEntityDeviceId Tests

    [Fact]
    public void GetEntityDeviceId_ExistingEntity_ReturnsDeviceId()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var deviceId = this._registryService.GetEntityDeviceId("light.living_room_main");

        // Assert
        deviceId.Should().Be("device1");
    }

    [Fact]
    public void GetEntityDeviceId_NonExistentEntity_ReturnsNull()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var deviceId = this._registryService.GetEntityDeviceId("light.nonexistent");

        // Assert
        deviceId.Should().BeNull();
    }

    #endregion

    #region GetEntityOriginalName Tests

    [Fact]
    public void GetEntityOriginalName_ExistingEntity_ReturnsOriginalName()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var originalName = this._registryService.GetEntityOriginalName("light.living_room_main");

        // Assert
        originalName.Should().Be("Living Room Main Light");
    }

    [Fact]
    public void GetEntityOriginalName_NonExistentEntity_ReturnsNull()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var originalName = this._registryService.GetEntityOriginalName("light.nonexistent");

        // Assert
        originalName.Should().BeNull();
    }

    #endregion

    #region AreaExists Tests

    [Fact]
    public void AreaExists_ExistingArea_ReturnsTrue()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act & Assert
        this._registryService.AreaExists("area_living_room").Should().BeTrue();
        this._registryService.AreaExists("!unassigned").Should().BeTrue();
    }

    [Fact]
    public void AreaExists_NonExistentArea_ReturnsFalse()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act & Assert
        this._registryService.AreaExists("area_nonexistent").Should().BeFalse();
    }

    #endregion

    #region GetAll*Ids Tests

    [Fact]
    public void GetAllAreaIds_ReturnsAllAreas()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var areaIds = this._registryService.GetAllAreaIds().ToList();

        // Assert
        areaIds.Should().HaveCount(3);
        areaIds.Should().Contain("area_living_room");
        areaIds.Should().Contain("area_kitchen");
        areaIds.Should().Contain("!unassigned");
    }

    [Fact]
    public void GetAllDeviceIds_ReturnsAllDevices()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var deviceIds = this._registryService.GetAllDeviceIds().ToList();

        // Assert
        deviceIds.Should().HaveCount(2);
        deviceIds.Should().Contain("device1");
        deviceIds.Should().Contain("device2");
    }

    [Fact]
    public void GetAllEntityIds_ReturnsAllEntities()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var entityIds = this._registryService.GetAllEntityIds().ToList();

        // Assert
        entityIds.Should().HaveCount(3);
        entityIds.Should().Contain("light.living_room_main");
        entityIds.Should().Contain("light.kitchen_main");
        entityIds.Should().Contain("light.orphaned");
    }

    #endregion

    #region ResolveEntityAreaId Tests

    [Fact]
    public void ResolveEntityAreaId_EntityWithArea_ReturnsAreaId()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var areaId = this._registryService.ResolveEntityAreaId("light.kitchen_main");

        // Assert
        areaId.Should().Be("area_kitchen");
    }

    [Fact]
    public void ResolveEntityAreaId_EntityWithoutArea_ReturnsUnassigned()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var areaId = this._registryService.ResolveEntityAreaId("light.orphaned");

        // Assert
        areaId.Should().Be("!unassigned");
    }

    [Fact]
    public void ResolveEntityAreaId_NonExistentEntity_ReturnsUnassigned()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var areaId = this._registryService.ResolveEntityAreaId("light.nonexistent");

        // Assert
        areaId.Should().Be("!unassigned");
    }

    #endregion

    #region GetRegistryStats Tests

    [Fact]
    public void GetRegistryStats_ReturnsCorrectCounts()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act
        var stats = this._registryService.GetRegistryStats();

        // Assert
        stats.DeviceCount.Should().Be(2);
        stats.EntityCount.Should().Be(3);
        stats.AreaCount.Should().Be(3); // 2 test areas + unassigned
    }

    [Fact]
    public void GetRegistryStats_EmptyRegistry_ReturnsZeroExceptUnassignedArea()
    {
        // Arrange - Empty registry service

        // Act
        var stats = this._registryService.GetRegistryStats();

        // Assert
        stats.DeviceCount.Should().Be(0);
        stats.EntityCount.Should().Be(0);
        stats.AreaCount.Should().Be(1); // Only unassigned area
    }

    #endregion

    #region ClearAll Tests

    [Fact]
    public void ClearAll_RemovesAllDataExceptUnassigned()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());
        
        // Verify data exists before clearing
        var statsBefore = this._registryService.GetRegistryStats();
        statsBefore.DeviceCount.Should().BeGreaterThan(0);

        // Act
        this._registryService.ClearAll();

        // Assert
        var statsAfter = this._registryService.GetRegistryStats();
        statsAfter.DeviceCount.Should().Be(0);
        statsAfter.EntityCount.Should().Be(0);
        statsAfter.AreaCount.Should().Be(1); // Unassigned area should remain
        
        this._registryService.AreaExists("!unassigned").Should().BeTrue();
        this._registryService.GetAreaName("!unassigned").Should().Be("(No area)");
    }

    #endregion

    #region Edge Cases and Error Handling

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Methods_WithEmptyStrings_HandleGracefully(String emptyInput)
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act & Assert - Should not throw
        this._registryService.GetDeviceArea(emptyInput).Should().BeNull();
        this._registryService.GetEntityArea(emptyInput).Should().BeNull();
        this._registryService.GetAreaName(emptyInput).Should().BeNull();
        this._registryService.GetEntityDeviceId(emptyInput).Should().BeNull();
        this._registryService.GetEntityOriginalName(emptyInput).Should().BeNull();
        this._registryService.AreaExists(emptyInput).Should().BeFalse();
        this._registryService.ResolveEntityAreaId(emptyInput).Should().Be("!unassigned");
    }

    [Fact]
    public void CaseInsensitiveOperations_WorkCorrectly()
    {
        // Arrange
        this._registryService.UpdateRegistries(CreateTestRegistryData());

        // Act & Assert - Test case insensitive operations
        this._registryService.GetDeviceArea("DEVICE1").Should().Be("area_living_room");
        this._registryService.GetEntityArea("LIGHT.LIVING_ROOM_MAIN").Should().Be("area_living_room");
        this._registryService.GetAreaName("AREA_LIVING_ROOM").Should().Be("Living Room");
        this._registryService.AreaExists("AREA_KITCHEN").Should().BeTrue();
        
        var deviceInfo = this._registryService.GetDeviceInfo("DEVICE1");
        deviceInfo.name.Should().Be("Smart Bulb Pro");
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void UpdateRegistries_LargeDataset_PerformsReasonably()
    {
        // Arrange - Create large dataset
        var largeDeviceData = new Dictionary<String, (String name, String mf, String model)>(StringComparer.OrdinalIgnoreCase);
        var largeDeviceAreaData = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        var largeEntityData = new Dictionary<String, (String deviceId, String originalName)>(StringComparer.OrdinalIgnoreCase);
        var largeEntityAreaData = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);
        var largeAreaData = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < 5000; i++)
        {
            var deviceId = $"device_{i}";
            var areaId = $"area_{i % 100}"; // 100 areas, multiple devices per area
            var entityId = $"light.device_{i}_main";

            largeDeviceData[deviceId] = ($"Device {i}", $"Manufacturer {i % 10}", $"Model {i % 20}");
            largeDeviceAreaData[deviceId] = areaId;
            largeEntityData[entityId] = (deviceId, $"Light {i}");
            largeEntityAreaData[entityId] = areaId;
            largeAreaData[areaId] = $"Area {i % 100}";
        }

        var largeRegistryData = new ParsedRegistryData(
            largeDeviceData,
            largeDeviceAreaData,
            largeEntityData,
            largeEntityAreaData,
            largeAreaData
        );

        // Act & Assert - Should complete without timeout
        var action = () => this._registryService.UpdateRegistries(largeRegistryData);
        action.Should().NotThrow();

        var stats = this._registryService.GetRegistryStats();
        stats.DeviceCount.Should().Be(5000);
        stats.EntityCount.Should().Be(5000);
        stats.AreaCount.Should().Be(101); // 100 + unassigned
    }

    #endregion

    #region Helper Methods

    private static ParsedRegistryData CreateTestRegistryData()
    {
        var deviceById = new Dictionary<String, (String name, String mf, String model)>(StringComparer.OrdinalIgnoreCase)
        {
            ["device1"] = ("Smart Bulb Pro", "ACME", "SB-100"),
            ["device2"] = ("LED Strip", "TechCorp", "LS-200")
        };

        var deviceAreaById = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
        {
            ["device1"] = "area_living_room",
            ["device2"] = "area_kitchen"
        };

        var entityDevice = new Dictionary<String, (String deviceId, String originalName)>(StringComparer.OrdinalIgnoreCase)
        {
            ["light.living_room_main"] = ("device1", "Living Room Main Light"),
            ["light.kitchen_main"] = ("device2", "Kitchen Main Light"),
            ["light.orphaned"] = ("", "Orphaned Light") // No device assignment
        };

        var entityArea = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
        {
            ["light.kitchen_main"] = "area_kitchen" // Direct entity area assignment
            // light.living_room_main has no direct area, should fall back to device area
            // light.orphaned has no area assignment
        };

        var areaIdToName = new Dictionary<String, String>(StringComparer.OrdinalIgnoreCase)
        {
            ["area_living_room"] = "Living Room",
            ["area_kitchen"] = "Kitchen"
        };

        return new ParsedRegistryData(DeviceById: deviceById, DeviceAreaById: deviceAreaById, EntityDevice: entityDevice, EntityArea: entityArea, AreaIdToName: areaIdToName);
    }

    #endregion
}