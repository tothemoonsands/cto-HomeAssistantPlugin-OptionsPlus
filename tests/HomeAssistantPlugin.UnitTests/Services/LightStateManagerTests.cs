using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Loupedeck.HomeAssistantPlugin.Models;
using Loupedeck.HomeAssistantPlugin.Services;

using NSubstitute;

using Xunit;

namespace Loupedeck.HomeAssistantPlugin.Tests.Services;

/// <summary>
/// Comprehensive tests for LightStateManager focusing on state caching, thread safety,
/// service coordination, and concurrent access patterns with 85% coverage target.
/// </summary>
public class LightStateManagerTests
{
    private readonly LightStateManager _stateManager;
    private readonly IHomeAssistantDataService _mockDataService;
    private readonly IHomeAssistantDataParser _mockDataParser;

    public LightStateManagerTests()
    {
        this._stateManager = new LightStateManager();
        this._mockDataService = Substitute.For<IHomeAssistantDataService>();
        this._mockDataParser = Substitute.For<IHomeAssistantDataParser>();
    }

    #region Constructor and Initial State Tests

    [Fact]
    public void Constructor_InitializesEmptyState()
    {
        // Arrange & Act
        var manager = new LightStateManager();

        // Assert
        manager.GetTrackedEntityIds().Should().BeEmpty();
        manager.GetAllLights().Should().BeEmpty();
        manager.GetUniqueAreaIds().Should().BeEmpty();
    }

    #endregion

    #region UpdateLightState Tests

    [Fact]
    public void UpdateLightState_WithBasicParameters_UpdatesState()
    {
        // Arrange
        var entityId = "light.test";

        // Act
        this._stateManager.UpdateLightState(entityId, true, 128);

        // Assert
        this._stateManager.IsLightOn(entityId).Should().BeTrue();
        this._stateManager.GetEffectiveBrightness(entityId).Should().Be(128);
        var hsb = this._stateManager.GetHsbValues(entityId);
        hsb.B.Should().Be(128);
    }

    [Fact]
    public void UpdateLightState_WithoutBrightness_UpdatesOnlyOnOffState()
    {
        // Arrange
        var entityId = "light.test";
        
        // Set initial brightness
        this._stateManager.SetCachedBrightness(entityId, 200);

        // Act
        this._stateManager.UpdateLightState(entityId, false);

        // Assert
        this._stateManager.IsLightOn(entityId).Should().BeFalse();
        this._stateManager.GetEffectiveBrightness(entityId).Should().Be(0); // Should be 0 when off
        var hsb = this._stateManager.GetHsbValues(entityId);
        hsb.B.Should().Be(200); // Cached brightness should remain
    }

    [Theory]
    [InlineData(-50, 0)]     // Below minimum
    [InlineData(0, 0)]       // At minimum
    [InlineData(128, 128)]   // Normal value
    [InlineData(255, 255)]   // At maximum
    [InlineData(300, 255)]   // Above maximum
    public void UpdateLightState_WithBrightnessValues_ClampsCorrectly(Int32 input, Int32 expected)
    {
        // Arrange
        var entityId = "light.test";

        // Act
        this._stateManager.UpdateLightState(entityId, true, input);

        // Assert
        this._stateManager.GetEffectiveBrightness(entityId).Should().Be(expected);
    }

    #endregion

    #region UpdateHsColor Tests

    [Fact]
    public void UpdateHsColor_WithValidValues_UpdatesHsColor()
    {
        // Arrange
        var entityId = "light.test";

        // Act
        this._stateManager.UpdateHsColor(entityId, 180, 75);

        // Assert
        var hsb = this._stateManager.GetHsbValues(entityId);
        hsb.H.Should().Be(180);
        hsb.S.Should().Be(75);
    }

    [Fact]
    public void UpdateHsColor_WithPartialValues_UpdatesOnlySpecifiedValues()
    {
        // Arrange
        var entityId = "light.test";
        this._stateManager.UpdateHsColor(entityId, 120, 50); // Set initial values

        // Act - Update only hue
        this._stateManager.UpdateHsColor(entityId, 240, null);

        // Assert
        var hsb = this._stateManager.GetHsbValues(entityId);
        hsb.H.Should().Be(240); // Updated
        hsb.S.Should().Be(50);  // Preserved
    }

    [Theory]
    [InlineData(-30, 330)]   // Negative hue should wrap
    [InlineData(0, 0)]       // Zero hue
    [InlineData(180, 180)]   // Normal hue
    [InlineData(360, 0)]     // 360 should wrap to 0
    [InlineData(450, 90)]    // > 360 should wrap
    public void UpdateHsColor_HueValues_WrapsCorrectly(Double inputHue, Double expectedHue)
    {
        // Arrange
        var entityId = "light.test";

        // Act
        this._stateManager.UpdateHsColor(entityId, inputHue, 50);

        // Assert
        var hsb = this._stateManager.GetHsbValues(entityId);
        hsb.H.Should().Be(expectedHue);
    }

    [Theory]
    [InlineData(-10, 0)]     // Below minimum
    [InlineData(0, 0)]       // At minimum
    [InlineData(50, 50)]     // Normal value
    [InlineData(100, 100)]   // At maximum
    [InlineData(150, 100)]   // Above maximum
    public void UpdateHsColor_SaturationValues_ClampsCorrectly(Double inputSat, Double expectedSat)
    {
        // Arrange
        var entityId = "light.test";

        // Act
        this._stateManager.UpdateHsColor(entityId, 180, inputSat);

        // Assert
        var hsb = this._stateManager.GetHsbValues(entityId);
        hsb.S.Should().Be(expectedSat);
    }

    #endregion

    #region UpdateColorTemp Tests

    [Fact]
    public void UpdateColorTemp_WithMiredValue_UpdatesColorTemp()
    {
        // Arrange
        var entityId = "light.test";

        // Act
        this._stateManager.UpdateColorTemp(entityId, 300, null, 153, 500);

        // Assert
        var temp = this._stateManager.GetColorTempMired(entityId);
        temp.Should().NotBeNull();
        temp!.Value.Min.Should().Be(153);
        temp.Value.Max.Should().Be(500);
        temp.Value.Cur.Should().Be(300);
    }

    [Fact]
    public void UpdateColorTemp_WithKelvinValue_ConvertsToMired()
    {
        // Arrange
        var entityId = "light.test";

        // Act
        this._stateManager.UpdateColorTemp(entityId, null, 3000, 153, 500);

        // Assert
        var temp = this._stateManager.GetColorTempMired(entityId);
        temp.Should().NotBeNull();
        temp!.Value.Cur.Should().BeInRange(153, 500); // Converted kelvin should be clamped to range
    }

    [Fact]
    public void UpdateColorTemp_WithMiredOutOfRange_ClampsToRange()
    {
        // Arrange
        var entityId = "light.test";

        // Act
        this._stateManager.UpdateColorTemp(entityId, 100, null, 153, 500); // 100 is below min 153

        // Assert
        var temp = this._stateManager.GetColorTempMired(entityId);
        temp!.Value.Cur.Should().Be(153); // Should be clamped to minimum
    }

    [Fact]
    public void UpdateColorTemp_PreservesExistingRangeWhenNotSpecified()
    {
        // Arrange
        var entityId = "light.test";
        this._stateManager.UpdateColorTemp(entityId, 300, null, 153, 500);

        // Act - Update without specifying new range
        this._stateManager.UpdateColorTemp(entityId, 400, null, null, null);

        // Assert
        var temp = this._stateManager.GetColorTempMired(entityId);
        temp!.Value.Min.Should().Be(153); // Preserved
        temp.Value.Max.Should().Be(500);  // Preserved
        temp.Value.Cur.Should().Be(400);  // Updated
    }

    #endregion

    #region GetEffectiveBrightness Tests

    [Fact]
    public void GetEffectiveBrightness_WhenLightOn_ReturnsCachedBrightness()
    {
        // Arrange
        var entityId = "light.test";
        this._stateManager.UpdateLightState(entityId, true);
        this._stateManager.SetCachedBrightness(entityId, 200);

        // Act
        var brightness = this._stateManager.GetEffectiveBrightness(entityId);

        // Assert
        brightness.Should().Be(200);
    }

    [Fact]
    public void GetEffectiveBrightness_WhenLightOff_ReturnsZero()
    {
        // Arrange
        var entityId = "light.test";
        this._stateManager.SetCachedBrightness(entityId, 200);
        this._stateManager.UpdateLightState(entityId, false);

        // Act
        var brightness = this._stateManager.GetEffectiveBrightness(entityId);

        // Assert
        brightness.Should().Be(0); // Should return 0 when off regardless of cached brightness
    }

    [Fact]
    public void GetEffectiveBrightness_UnknownEntity_ReturnsZero()
    {
        // Act
        var brightness = this._stateManager.GetEffectiveBrightness("light.unknown");

        // Assert
        brightness.Should().Be(0);
    }

    #endregion

    #region Capabilities Management Tests

    [Fact]
    public void SetCapabilities_StoresAndRetrievesCorrectly()
    {
        // Arrange
        var entityId = "light.test";
        var caps = new LightCaps(true, true, true, true);

        // Act
        this._stateManager.SetCapabilities(entityId, caps);
        var retrievedCaps = this._stateManager.GetCapabilities(entityId);

        // Assert
        retrievedCaps.Should().Be(caps);
    }

    [Fact]
    public void GetCapabilities_UnknownEntity_ReturnsDefaultCapabilities()
    {
        // Act
        var caps = this._stateManager.GetCapabilities("light.unknown");

        // Assert
        caps.OnOff.Should().BeTrue();      // Safe default: on/off only
        caps.Brightness.Should().BeFalse();
        caps.ColorTemp.Should().BeFalse();
        caps.ColorHs.Should().BeFalse();
    }

    #endregion

    #region InitializeLightStates Tests

    [Fact]
    public void InitializeLightStates_WithNewLights_InitializesAllData()
    {
        // Arrange
        var lights = CreateTestLightData();

        // Act
        this._stateManager.InitializeLightStates(lights);

        // Assert
        this._stateManager.GetTrackedEntityIds().Should().HaveCount(3);
        this._stateManager.GetAllLights().Should().HaveCount(3);
        
        var livingRoomLight = this._stateManager.GetLightData("light.living_room");
        livingRoomLight.Should().NotBeNull();
        livingRoomLight!.FriendlyName.Should().Be("Living Room Light");
        
        this._stateManager.IsLightOn("light.living_room").Should().BeTrue();
        this._stateManager.GetEffectiveBrightness("light.living_room").Should().Be(128);
    }

    [Fact]
    public void InitializeLightStates_PreservesExistingUserAdjustments()
    {
        // Arrange - First initialize with some data
        var initialLights = CreateTestLightData();
        this._stateManager.InitializeLightStates(initialLights);
        
        // Make user adjustments
        this._stateManager.UpdateHsColor("light.living_room", 240, 85);
        this._stateManager.SetCachedBrightness("light.living_room", 200);
        
        // Create updated light data with different values
        var updatedLights = new List<LightData>
        {
            new("light.living_room", "Living Room Light", "on", true, "device1", "Smart Bulb", "ACME", "SB-1", 
                "area_living_room", 100, 120, 50, 350, 153, 500, new LightCaps(true, true, true, true))
        };

        // Act - Re-initialize (simulate HA data refresh)
        this._stateManager.InitializeLightStates(updatedLights);

        // Assert - User adjustments should be preserved
        var hsb = this._stateManager.GetHsbValues("light.living_room");
        hsb.H.Should().Be(240); // User adjustment preserved
        hsb.S.Should().Be(85);  // User adjustment preserved
        hsb.B.Should().Be(200); // User adjustment preserved
        
        // On/off state should be updated from HA
        this._stateManager.IsLightOn("light.living_room").Should().BeTrue();
    }

    [Fact]
    public void InitializeLightStates_WithEmptyList_ClearsNonEssentialData()
    {
        // Arrange - Start with some lights
        var lights = CreateTestLightData();
        this._stateManager.InitializeLightStates(lights);

        // Act - Initialize with empty list
        this._stateManager.InitializeLightStates(new List<LightData>());

        // Assert
        this._stateManager.GetAllLights().Should().BeEmpty();
        this._stateManager.GetTrackedEntityIds().Should().BeEmpty();
    }

    #endregion

    #region SetCached Methods Tests

    [Fact]
    public void SetCachedBrightness_ClampsToValidRange()
    {
        // Arrange
        var entityId = "light.test";

        // Act & Assert
        this._stateManager.SetCachedBrightness(entityId, -50);
        this._stateManager.GetHsbValues(entityId).B.Should().Be(0);

        this._stateManager.SetCachedBrightness(entityId, 300);
        this._stateManager.GetHsbValues(entityId).B.Should().Be(255);
    }

    [Fact]
    public void SetCachedTempMired_WithRangeUpdates_UpdatesCorrectly()
    {
        // Arrange
        var entityId = "light.test";

        // Act
        this._stateManager.SetCachedTempMired(entityId, 100, 600, 250);

        // Assert
        var temp = this._stateManager.GetColorTempMired(entityId);
        temp!.Value.Min.Should().Be(100);
        temp.Value.Max.Should().Be(600);
        temp.Value.Cur.Should().Be(250);
    }

    [Fact]
    public void SetCachedTempMired_PreservesExistingRangeWhenNull()
    {
        // Arrange
        var entityId = "light.test";
        this._stateManager.SetCachedTempMired(entityId, 153, 500, 300);

        // Act - Update current without changing range
        this._stateManager.SetCachedTempMired(entityId, null, null, 400);

        // Assert
        var temp = this._stateManager.GetColorTempMired(entityId);
        temp!.Value.Min.Should().Be(153); // Preserved
        temp.Value.Max.Should().Be(500);  // Preserved
        temp.Value.Cur.Should().Be(400);  // Updated
    }

    #endregion

    #region Area-based Operations Tests

    [Fact]
    public void GetLightsByArea_FiltersCorrectly()
    {
        // Arrange
        var lights = CreateTestLightData();
        this._stateManager.InitializeLightStates(lights);

        // Act
        var livingRoomLights = this._stateManager.GetLightsByArea("area_living_room").ToList();
        var kitchenLights = this._stateManager.GetLightsByArea("area_kitchen").ToList();

        // Assert
        livingRoomLights.Should().HaveCount(2);
        livingRoomLights.Should().Contain(l => l.EntityId == "light.living_room");
        livingRoomLights.Should().Contain(l => l.EntityId == "light.living_room_accent");
        
        kitchenLights.Should().HaveCount(1);
        kitchenLights.Should().Contain(l => l.EntityId == "light.kitchen");
    }

    [Fact]
    public void GetLightsByArea_WithNullOrEmptyAreaId_ReturnsEmpty()
    {
        // Arrange
        var lights = CreateTestLightData();
        this._stateManager.InitializeLightStates(lights);

        // Act & Assert
        this._stateManager.GetLightsByArea(null!).Should().BeEmpty();
        this._stateManager.GetLightsByArea("").Should().BeEmpty();
        this._stateManager.GetLightsByArea("   ").Should().BeEmpty();
    }

    [Fact]
    public void GetUniqueAreaIds_ReturnsDistinctAreas()
    {
        // Arrange
        var lights = CreateTestLightData();
        this._stateManager.InitializeLightStates(lights);

        // Act
        var areaIds = this._stateManager.GetUniqueAreaIds().ToList();

        // Assert
        areaIds.Should().HaveCount(2);
        areaIds.Should().Contain("area_living_room");
        areaIds.Should().Contain("area_kitchen");
    }

    [Fact]
    public void GetAreaIdToNameMapping_CreatesCorrectMapping()
    {
        // Arrange
        var lights = CreateTestLightData();
        this._stateManager.InitializeLightStates(lights);

        // Act
        var mapping = this._stateManager.GetAreaIdToNameMapping();

        // Assert
        mapping.Should().ContainKey("area_living_room");
        mapping.Should().ContainKey("area_kitchen");
        // Note: The current implementation uses area ID as name (could be enhanced with registry integration)
    }

    #endregion

    #region Entity Management Tests

    [Fact]
    public void RemoveEntity_RemovesFromAllCaches()
    {
        // Arrange
        var lights = CreateTestLightData();
        this._stateManager.InitializeLightStates(lights);
        var entityId = "light.living_room";
        
        // Verify entity exists
        this._stateManager.GetLightData(entityId).Should().NotBeNull();
        this._stateManager.GetTrackedEntityIds().Should().Contain(entityId);

        // Act
        this._stateManager.RemoveEntity(entityId);

        // Assert
        this._stateManager.GetLightData(entityId).Should().BeNull();
        this._stateManager.GetTrackedEntityIds().Should().NotContain(entityId);
        this._stateManager.IsLightOn(entityId).Should().BeFalse(); // Default for unknown entity
    }

    [Fact]
    public void GetLightData_WithValidEntityId_ReturnsLightData()
    {
        // Arrange
        var lights = CreateTestLightData();
        this._stateManager.InitializeLightStates(lights);

        // Act
        var lightData = this._stateManager.GetLightData("light.living_room");

        // Assert
        lightData.Should().NotBeNull();
        lightData!.EntityId.Should().Be("light.living_room");
        lightData.FriendlyName.Should().Be("Living Room Light");
    }

    [Fact]
    public void GetLightData_WithInvalidEntityId_ReturnsNull()
    {
        // Arrange
        var lights = CreateTestLightData();
        this._stateManager.InitializeLightStates(lights);

        // Act & Assert
        this._stateManager.GetLightData("light.nonexistent").Should().BeNull();
        this._stateManager.GetLightData(null!).Should().BeNull();
        this._stateManager.GetLightData("").Should().BeNull();
        this._stateManager.GetLightData("   ").Should().BeNull();
    }

    #endregion

    #region InitOrUpdateAsync Integration Tests

    [Fact]
    public async Task InitOrUpdateAsync_WithSuccessfulDataFetch_InitializesCorrectly()
    {
        // Arrange
        var statesJson = """[{"entity_id": "light.test", "state": "on"}]""";
        var registryData = new ParsedRegistryData(
            new Dictionary<String, (String, String, String)>(),
            new Dictionary<String, String>(),
            new Dictionary<String, (String, String)>(),
            new Dictionary<String, String>(),
            new Dictionary<String, String>()
        );
        var lights = new List<LightData>
        {
            new("light.test", "Test Light", "on", true, "", "", "", "", "", 128, 0, 0, 300, 153, 500, 
                new LightCaps(true, true, false, false))
        };

        this._mockDataService.FetchStatesAsync(Arg.Any<CancellationToken>())
            .Returns((true, statesJson, null));
        this._mockDataService.FetchEntityRegistryAsync(Arg.Any<CancellationToken>())
            .Returns((true, "[]", null));
        this._mockDataService.FetchDeviceRegistryAsync(Arg.Any<CancellationToken>())
            .Returns((true, "[]", null));
        this._mockDataService.FetchAreaRegistryAsync(Arg.Any<CancellationToken>())
            .Returns((true, "[]", null));

        this._mockDataParser.ParseRegistries(Arg.Any<String?>(), Arg.Any<String?>(), Arg.Any<String?>())
            .Returns(registryData);
        this._mockDataParser.ParseLightStates(statesJson, registryData)
            .Returns(lights);

        // Act
        var (success, error) = await this._stateManager.InitOrUpdateAsync(this._mockDataService, this._mockDataParser);

        // Assert
        success.Should().BeTrue();
        error.Should().BeNull();
        this._stateManager.GetAllLights().Should().HaveCount(1);
        this._stateManager.GetLightData("light.test").Should().NotBeNull();
    }

    [Fact]
    public async Task InitOrUpdateAsync_WithFailedStatesFetch_ReturnsError()
    {
        // Arrange
        this._mockDataService.FetchStatesAsync(Arg.Any<CancellationToken>())
            .Returns((false, null, "Connection failed"));

        // Act
        var (success, error) = await this._stateManager.InitOrUpdateAsync(this._mockDataService, this._mockDataParser);

        // Assert
        success.Should().BeFalse();
        error.Should().Contain("Failed to fetch states");
    }

    [Fact]
    public async Task InitOrUpdateAsync_WithNullStatesJson_ReturnsError()
    {
        // Arrange
        this._mockDataService.FetchStatesAsync(Arg.Any<CancellationToken>())
            .Returns((true, null, null)); // Success but null JSON

        // Act
        var (success, error) = await this._stateManager.InitOrUpdateAsync(this._mockDataService, this._mockDataParser);

        // Assert
        success.Should().BeFalse();
        error.Should().Contain("null or empty JSON data");
    }

    [Fact]
    public async Task InitOrUpdateAsync_WithRegistryFailures_ContinuesWithWarning()
    {
        // Arrange
        var statesJson = """[{"entity_id": "light.test", "state": "on"}]""";
        var registryData = new ParsedRegistryData(
            new Dictionary<String, (String, String, String)>(),
            new Dictionary<String, String>(),
            new Dictionary<String, (String, String)>(),
            new Dictionary<String, String>(),
            new Dictionary<String, String>()
        );
        var lights = new List<LightData>
        {
            new("light.test", "Test Light", "on", true, "", "", "", "", "", 128, 0, 0, 300, 153, 500, 
                new LightCaps(true, true, false, false))
        };

        this._mockDataService.FetchStatesAsync(Arg.Any<CancellationToken>())
            .Returns((true, statesJson, null));
        
        // Registry fetches fail
        this._mockDataService.FetchEntityRegistryAsync(Arg.Any<CancellationToken>())
            .Returns((false, null, "Registry unavailable"));
        this._mockDataService.FetchDeviceRegistryAsync(Arg.Any<CancellationToken>())
            .Returns((false, null, "Registry unavailable"));
        this._mockDataService.FetchAreaRegistryAsync(Arg.Any<CancellationToken>())
            .Returns((false, null, "Registry unavailable"));

        this._mockDataParser.ParseRegistries(null, null, null)
            .Returns(registryData);
        this._mockDataParser.ParseLightStates(statesJson, registryData)
            .Returns(lights);

        // Act
        var (success, error) = await this._stateManager.InitOrUpdateAsync(this._mockDataService, this._mockDataParser);

        // Assert
        success.Should().BeTrue(); // Should succeed despite registry failures
        error.Should().BeNull();
        this._stateManager.GetAllLights().Should().HaveCount(1);
    }

    [Fact]
    public async Task InitOrUpdateAsync_WithException_ReturnsError()
    {
        // Arrange
        this._mockDataService.FetchStatesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<(bool, string?, string?)>>(callInfo => throw new InvalidOperationException("Test exception"));

        // Act
        var (success, error) = await this._stateManager.InitOrUpdateAsync(this._mockDataService, this._mockDataParser);

        // Assert
        success.Should().BeFalse();
        error.Should().Contain("Test exception");
    }

    [Fact]
    public async Task InitOrUpdateAsync_WithCancellation_HandlesCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        this._mockDataService.FetchStatesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<(bool, string?, string?)>>(callInfo => throw new OperationCanceledException());

        // Act
        var (success, error) = await this._stateManager.InitOrUpdateAsync(this._mockDataService, this._mockDataParser, cts.Token);

        // Assert
        success.Should().BeFalse();
        error.Should().NotBeNull();
    }

    #endregion

    #region Thread Safety and Concurrency Tests

    [Fact]
    public async Task ConcurrentStateUpdates_MaintainConsistency()
    {
        // Arrange
        var entityId = "light.concurrency_test";
        var tasks = new List<Task>();

        // Act - Perform concurrent updates
        for (var i = 0; i < 100; i++)
        {
            var index = i; // Capture for closure
            tasks.Add(Task.Run(() =>
            {
                this._stateManager.UpdateLightState(entityId, index % 2 == 0, index);
                this._stateManager.UpdateHsColor(entityId, index % 360, (index % 100) + 1);
                this._stateManager.SetCachedBrightness(entityId, (index % 255) + 1);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Should not throw and should have consistent state
        var hsb = this._stateManager.GetHsbValues(entityId);
        hsb.H.Should().BeInRange(0, 359);
        hsb.S.Should().BeInRange(1, 100);
        hsb.B.Should().BeInRange(1, 255);
        
        var isOn = this._stateManager.IsLightOn(entityId);
        var brightness = this._stateManager.GetEffectiveBrightness(entityId);
        if (!isOn)
        {
            brightness.Should().Be(0);
        }
        else
        {
            brightness.Should().BeInRange(1, 255);
        }
    }

    [Fact]
    public async Task ConcurrentInitializeLightStates_HandlesSafely()
    {
        // Arrange
        var lights1 = CreateTestLightData();
        var lights2 = CreateTestLightData().Take(1).ToList(); // Different subset
        var tasks = new List<Task>();

        // Act - Concurrent initializations
        for (var i = 0; i < 10; i++)
        {
            var useFirstSet = i % 2 == 0;
            tasks.Add(Task.Run(() =>
            {
                this._stateManager.InitializeLightStates(useFirstSet ? lights1 : lights2);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Should complete without exceptions and have valid state
        var allLights = this._stateManager.GetAllLights();
        allLights.Should().NotBeEmpty();
        
        foreach (var light in allLights)
        {
            light.EntityId.Should().NotBeNullOrEmpty();
        }
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void StateOperations_WithLargeNumberOfEntities_PerformReasonably()
    {
        // Arrange - Create many lights
        var lights = new List<LightData>();
        for (var i = 0; i < 5000; i++)
        {
            lights.Add(new LightData(
                $"light.test_{i}", $"Test Light {i}", "on", true, 
                $"device_{i}", $"Device {i}", "ACME", "Model", 
                $"area_{i % 100}", 128, i % 360, (i % 100) + 1, 300, 153, 500,
                new LightCaps(true, true, true, true)
            ));
        }

        // Act & Assert - Should complete reasonably quickly
        var action = () => this._stateManager.InitializeLightStates(lights);
        action.Should().NotThrow();

        this._stateManager.GetAllLights().Should().HaveCount(5000);
        this._stateManager.GetTrackedEntityIds().Should().HaveCount(5000);
        
        // Test individual operations performance
        this._stateManager.GetLightData("light.test_2500").Should().NotBeNull();
        this._stateManager.IsLightOn("light.test_4999").Should().BeTrue();
    }

    #endregion

    #region Edge Cases and Error Handling

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void StateOperations_WithInvalidEntityIds_HandleGracefully(String? entityId)
    {
        // Act & Assert - Should not throw
        var action1 = () => this._stateManager.UpdateLightState(entityId!, true, 128);
        var action2 = () => this._stateManager.UpdateHsColor(entityId!, 180, 50);
        var action3 = () => this._stateManager.UpdateColorTemp(entityId!, 300, null, 153, 500);
        var action4 = () => this._stateManager.SetCachedBrightness(entityId!, 128);
        var action5 = () => this._stateManager.RemoveEntity(entityId!);

        action1.Should().NotThrow();
        action2.Should().NotThrow();
        action3.Should().NotThrow();
        action4.Should().NotThrow();
        action5.Should().NotThrow();

        // Query operations should return safe defaults
        this._stateManager.IsLightOn(entityId!).Should().BeFalse();
        this._stateManager.GetEffectiveBrightness(entityId!).Should().Be(0);
        this._stateManager.GetLightData(entityId!).Should().BeNull();
    }

    [Fact]
    public void InitializeLightStates_WithNullList_HandlesGracefully()
    {
        // Act & Assert
        var action = () => this._stateManager.InitializeLightStates(null!);
        action.Should().NotThrow();
    }

    [Fact]
    public void StateOperations_OnEmptyManager_ReturnSafeDefaults()
    {
        // Act & Assert
        this._stateManager.GetTrackedEntityIds().Should().BeEmpty();
        this._stateManager.GetAllLights().Should().BeEmpty();
        this._stateManager.GetUniqueAreaIds().Should().BeEmpty();
        this._stateManager.IsLightOn("any.entity").Should().BeFalse();
        this._stateManager.GetEffectiveBrightness("any.entity").Should().Be(0);
        this._stateManager.GetLightData("any.entity").Should().BeNull();
        
        var hsb = this._stateManager.GetHsbValues("any.entity");
        hsb.H.Should().Be(0);
        hsb.S.Should().Be(0);
        hsb.B.Should().Be(0);
        
        this._stateManager.GetColorTempMired("any.entity").Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private static List<LightData> CreateTestLightData()
    {
        return new List<LightData>
        {
            new("light.living_room", "Living Room Light", "on", true, "device1", "Smart Bulb Pro", 
                "ACME", "SB-100", "area_living_room", 128, 120, 75, 300, 153, 500, 
                new LightCaps(true, true, true, true)),
            
            new("light.living_room_accent", "Living Room Accent", "off", false, "device2", "LED Strip", 
                "TechCorp", "LS-200", "area_living_room", 0, 0, 0, 370, 153, 500, 
                new LightCaps(true, true, false, true)),
            
            new("light.kitchen", "Kitchen Light", "on", true, "device3", "Kitchen Fixture", 
                "HomeLight", "KF-300", "area_kitchen", 200, 60, 90, 250, 153, 500, 
                new LightCaps(true, true, true, false))
        };
    }

    #endregion
}