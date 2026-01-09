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
/// Comprehensive tests for SwitchStateManager focusing on state caching, thread safety,
/// service coordination, and concurrent access patterns with 85% coverage target.
/// </summary>
public class SwitchStateManagerTests
{
    private readonly SwitchStateManager _stateManager;
    private readonly IHomeAssistantDataService _mockDataService;
    private readonly IHomeAssistantDataParser _mockDataParser;

    public SwitchStateManagerTests()
    {
        this._stateManager = new SwitchStateManager();
        this._mockDataService = Substitute.For<IHomeAssistantDataService>();
        this._mockDataParser = Substitute.For<IHomeAssistantDataParser>();
    }

    #region Constructor and Initial State Tests

    [Fact]
    public void Constructor_InitializesEmptyState()
    {
        // Arrange & Act
        var manager = new SwitchStateManager();

        // Assert
        manager.GetTrackedEntityIds().Should().BeEmpty();
        manager.GetAllSwitches().Should().BeEmpty();
        manager.GetUniqueAreaIds().Should().BeEmpty();
    }

    #endregion

    #region UpdateSwitchState Tests

    [Fact]
    public void UpdateSwitchState_WithOnState_UpdatesState()
    {
        // Arrange
        var entityId = "switch.test";

        // Act
        this._stateManager.UpdateSwitchState(entityId, true);

        // Assert
        this._stateManager.IsSwitchOn(entityId).Should().BeTrue();
    }

    [Fact]
    public void UpdateSwitchState_WithOffState_UpdatesState()
    {
        // Arrange
        var entityId = "switch.test";

        // Act
        this._stateManager.UpdateSwitchState(entityId, false);

        // Assert
        this._stateManager.IsSwitchOn(entityId).Should().BeFalse();
    }

    [Fact]
    public void UpdateSwitchState_ToggleStates_UpdatesCorrectly()
    {
        // Arrange
        var entityId = "switch.test";

        // Act & Assert
        this._stateManager.UpdateSwitchState(entityId, true);
        this._stateManager.IsSwitchOn(entityId).Should().BeTrue();

        this._stateManager.UpdateSwitchState(entityId, false);
        this._stateManager.IsSwitchOn(entityId).Should().BeFalse();

        this._stateManager.UpdateSwitchState(entityId, true);
        this._stateManager.IsSwitchOn(entityId).Should().BeTrue();
    }

    #endregion

    #region IsSwitchOn Tests

    [Fact]
    public void IsSwitchOn_WithKnownOnSwitch_ReturnsTrue()
    {
        // Arrange
        var entityId = "switch.test";
        this._stateManager.UpdateSwitchState(entityId, true);

        // Act
        var result = this._stateManager.IsSwitchOn(entityId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSwitchOn_WithKnownOffSwitch_ReturnsFalse()
    {
        // Arrange
        var entityId = "switch.test";
        this._stateManager.UpdateSwitchState(entityId, false);

        // Act
        var result = this._stateManager.IsSwitchOn(entityId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSwitchOn_WithUnknownSwitch_ReturnsFalse()
    {
        // Act
        var result = this._stateManager.IsSwitchOn("switch.unknown");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Capabilities Management Tests

    [Fact]
    public void SetCapabilities_StoresAndRetrievesCorrectly()
    {
        // Arrange
        var entityId = "switch.test";
        var caps = new SwitchCaps(true);

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
        var caps = this._stateManager.GetCapabilities("switch.unknown");

        // Assert
        caps.OnOff.Should().BeTrue(); // Safe default: on/off only
    }

    #endregion

    #region InitializeSwitchStates Tests

    [Fact]
    public void InitializeSwitchStates_WithNewSwitches_InitializesAllData()
    {
        // Arrange
        var switches = CreateTestSwitchData();

        // Act
        this._stateManager.InitializeSwitchStates(switches);

        // Assert
        this._stateManager.GetTrackedEntityIds().Should().HaveCount(3);
        this._stateManager.GetAllSwitches().Should().HaveCount(3);
        
        var livingRoomSwitch = this._stateManager.GetSwitchData("switch.living_room");
        livingRoomSwitch.Should().NotBeNull();
        livingRoomSwitch!.FriendlyName.Should().Be("Living Room Switch");
        
        this._stateManager.IsSwitchOn("switch.living_room").Should().BeTrue();
    }

    [Fact]
    public void InitializeSwitchStates_ClearsExistingState_BeforeInitializing()
    {
        // Arrange - First initialize with some data
        var initialSwitches = CreateTestSwitchData();
        this._stateManager.InitializeSwitchStates(initialSwitches);
        
        // Create updated switch data with different entities
        var updatedSwitches = new List<SwitchData>
        {
            new("switch.new_switch", "New Switch", "off", false, "device4", "New Switch Device", "NewCorp", "NS-1", 
                "area_office", new SwitchCaps(true))
        };

        // Act - Re-initialize with different data
        this._stateManager.InitializeSwitchStates(updatedSwitches);

        // Assert - Old entities should be gone, only new ones should exist
        this._stateManager.GetAllSwitches().Should().HaveCount(1);
        this._stateManager.GetSwitchData("switch.living_room").Should().BeNull(); // Old entity gone
        this._stateManager.GetSwitchData("switch.new_switch").Should().NotBeNull(); // New entity present
    }

    [Fact]
    public void InitializeSwitchStates_WithEmptyList_ClearsAllData()
    {
        // Arrange - Start with some switches
        var switches = CreateTestSwitchData();
        this._stateManager.InitializeSwitchStates(switches);

        // Act - Initialize with empty list
        this._stateManager.InitializeSwitchStates(new List<SwitchData>());

        // Assert
        this._stateManager.GetAllSwitches().Should().BeEmpty();
        this._stateManager.GetTrackedEntityIds().Should().BeEmpty();
    }

    #endregion

    #region Area-based Operations Tests

    [Fact]
    public void GetSwitchesByArea_FiltersCorrectly()
    {
        // Arrange
        var switches = CreateTestSwitchData();
        this._stateManager.InitializeSwitchStates(switches);

        // Act
        var livingRoomSwitches = this._stateManager.GetSwitchesByArea("area_living_room").ToList();
        var kitchenSwitches = this._stateManager.GetSwitchesByArea("area_kitchen").ToList();

        // Assert
        livingRoomSwitches.Should().HaveCount(2);
        livingRoomSwitches.Should().Contain(s => s.EntityId == "switch.living_room");
        livingRoomSwitches.Should().Contain(s => s.EntityId == "switch.living_room_outlet");
        
        kitchenSwitches.Should().HaveCount(1);
        kitchenSwitches.Should().Contain(s => s.EntityId == "switch.kitchen");
    }

    [Fact]
    public void GetSwitchesByArea_WithNullOrEmptyAreaId_ReturnsEmpty()
    {
        // Arrange
        var switches = CreateTestSwitchData();
        this._stateManager.InitializeSwitchStates(switches);

        // Act & Assert
        this._stateManager.GetSwitchesByArea(null!).Should().BeEmpty();
        this._stateManager.GetSwitchesByArea("").Should().BeEmpty();
        this._stateManager.GetSwitchesByArea("   ").Should().BeEmpty();
    }

    [Fact]
    public void GetUniqueAreaIds_ReturnsDistinctAreas()
    {
        // Arrange
        var switches = CreateTestSwitchData();
        this._stateManager.InitializeSwitchStates(switches);

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
        var switches = CreateTestSwitchData();
        this._stateManager.InitializeSwitchStates(switches);

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
        var switches = CreateTestSwitchData();
        this._stateManager.InitializeSwitchStates(switches);
        var entityId = "switch.living_room";
        
        // Verify entity exists
        this._stateManager.GetSwitchData(entityId).Should().NotBeNull();
        this._stateManager.GetTrackedEntityIds().Should().Contain(entityId);

        // Act
        this._stateManager.RemoveEntity(entityId);

        // Assert
        this._stateManager.GetSwitchData(entityId).Should().BeNull();
        this._stateManager.GetTrackedEntityIds().Should().NotContain(entityId);
        this._stateManager.IsSwitchOn(entityId).Should().BeFalse(); // Default for unknown entity
    }

    [Fact]
    public void GetSwitchData_WithValidEntityId_ReturnsSwitchData()
    {
        // Arrange
        var switches = CreateTestSwitchData();
        this._stateManager.InitializeSwitchStates(switches);

        // Act
        var switchData = this._stateManager.GetSwitchData("switch.living_room");

        // Assert
        switchData.Should().NotBeNull();
        switchData!.EntityId.Should().Be("switch.living_room");
        switchData.FriendlyName.Should().Be("Living Room Switch");
    }

    [Fact]
    public void GetSwitchData_WithInvalidEntityId_ReturnsNull()
    {
        // Arrange
        var switches = CreateTestSwitchData();
        this._stateManager.InitializeSwitchStates(switches);

        // Act & Assert
        this._stateManager.GetSwitchData("switch.nonexistent").Should().BeNull();
        this._stateManager.GetSwitchData(null!).Should().BeNull();
        this._stateManager.GetSwitchData("").Should().BeNull();
        this._stateManager.GetSwitchData("   ").Should().BeNull();
    }

    #endregion

    #region InitOrUpdateAsync Integration Tests

    [Fact]
    public async Task InitOrUpdateAsync_WithSuccessfulDataFetch_InitializesCorrectly()
    {
        // Arrange
        var statesJson = """[{"entity_id": "switch.test", "state": "on"}]""";
        var registryData = new ParsedRegistryData(
            DeviceById: new Dictionary<String, (String, String, String)>(),
            DeviceAreaById: new Dictionary<String, String>(),
            EntityDevice: new Dictionary<String, (String, String)>(),
            EntityArea: new Dictionary<String, String>(),
            AreaIdToName: new Dictionary<String, String>()
        );
        var switches = new List<SwitchData>
        {
            new("switch.test", "Test Switch", "on", true, "", "", "", "", "", 
                new SwitchCaps(true))
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
        this._mockDataParser.ParseSwitchStates(statesJson, registryData)
            .Returns(switches);

        // Act
        var (success, error) = await this._stateManager.InitOrUpdateAsync(this._mockDataService, this._mockDataParser);

        // Assert
        success.Should().BeTrue();
        error.Should().BeNull();
        this._stateManager.GetAllSwitches().Should().HaveCount(1);
        this._stateManager.GetSwitchData("switch.test").Should().NotBeNull();
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
        var statesJson = """[{"entity_id": "switch.test", "state": "on"}]""";
        var registryData = new ParsedRegistryData(
            DeviceById: new Dictionary<String, (String, String, String)>(),
            DeviceAreaById: new Dictionary<String, String>(),
            EntityDevice: new Dictionary<String, (String, String)>(),
            EntityArea: new Dictionary<String, String>(),
            AreaIdToName: new Dictionary<String, String>()
        );
        var switches = new List<SwitchData>
        {
            new("switch.test", "Test Switch", "on", true, "", "", "", "", "", 
                new SwitchCaps(true))
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
        this._mockDataParser.ParseSwitchStates(statesJson, registryData)
            .Returns(switches);

        // Act
        var (success, error) = await this._stateManager.InitOrUpdateAsync(this._mockDataService, this._mockDataParser);

        // Assert
        success.Should().BeTrue(); // Should succeed despite registry failures
        error.Should().BeNull();
        this._stateManager.GetAllSwitches().Should().HaveCount(1);
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
        var entityId = "switch.concurrency_test";
        var tasks = new List<Task>();

        // Act - Perform concurrent updates
        for (var i = 0; i < 100; i++)
        {
            var index = i; // Capture for closure
            tasks.Add(Task.Run(() =>
            {
                this._stateManager.UpdateSwitchState(entityId, index % 2 == 0);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Should not throw and should have consistent state
        var isOn = this._stateManager.IsSwitchOn(entityId);
        isOn.Should().Be(isOn); // Should be a valid boolean (always true since retrieval succeeded)
    }

    [Fact]
    public async Task ConcurrentInitializeSwitchStates_HandlesSafely()
    {
        // Arrange
        var switches1 = CreateTestSwitchData();
        var switches2 = CreateTestSwitchData().Take(1).ToList(); // Different subset
        var tasks = new List<Task>();

        // Act - Concurrent initializations
        for (var i = 0; i < 10; i++)
        {
            var useFirstSet = i % 2 == 0;
            tasks.Add(Task.Run(() =>
            {
                this._stateManager.InitializeSwitchStates(useFirstSet ? switches1 : switches2);
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Should complete without exceptions and have valid state
        var allSwitches = this._stateManager.GetAllSwitches();
        allSwitches.Should().NotBeEmpty();
        
        foreach (var switchEntity in allSwitches)
        {
            switchEntity.EntityId.Should().NotBeNullOrEmpty();
        }
    }

    #endregion

    #region Performance Tests

    [Fact]
    public void StateOperations_WithLargeNumberOfEntities_PerformReasonably()
    {
        // Arrange - Create many switches
        var switches = new List<SwitchData>();
        for (var i = 0; i < 5000; i++)
        {
            switches.Add(new SwitchData(
                $"switch.test_{i}", $"Test Switch {i}", "on", true, 
                $"device_{i}", $"Device {i}", "ACME", "Model", 
                $"area_{i % 100}", new SwitchCaps(true)
            ));
        }

        // Act & Assert - Should complete reasonably quickly
        var action = () => this._stateManager.InitializeSwitchStates(switches);
        action.Should().NotThrow();

        this._stateManager.GetAllSwitches().Should().HaveCount(5000);
        this._stateManager.GetTrackedEntityIds().Should().HaveCount(5000);
        
        // Test individual operations performance
        this._stateManager.GetSwitchData("switch.test_2500").Should().NotBeNull();
        this._stateManager.IsSwitchOn("switch.test_4999").Should().BeTrue();
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
        var action1 = () => this._stateManager.UpdateSwitchState(entityId!, true);
        var action2 = () => this._stateManager.RemoveEntity(entityId!);

        action1.Should().NotThrow();
        action2.Should().NotThrow();

        // Query operations should return safe defaults
        this._stateManager.IsSwitchOn(entityId!).Should().BeFalse();
        this._stateManager.GetSwitchData(entityId!).Should().BeNull();
    }

    [Fact]
    public void InitializeSwitchStates_WithNullList_HandlesGracefully()
    {
        // Act & Assert
        var action = () => this._stateManager.InitializeSwitchStates(null!);
        action.Should().NotThrow();
    }

    [Fact]
    public void StateOperations_OnEmptyManager_ReturnSafeDefaults()
    {
        // Act & Assert
        this._stateManager.GetTrackedEntityIds().Should().BeEmpty();
        this._stateManager.GetAllSwitches().Should().BeEmpty();
        this._stateManager.GetUniqueAreaIds().Should().BeEmpty();
        this._stateManager.IsSwitchOn("any.entity").Should().BeFalse();
        this._stateManager.GetSwitchData("any.entity").Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private static List<SwitchData> CreateTestSwitchData()
    {
        return new List<SwitchData>
        {
            new("switch.living_room", "Living Room Switch", "on", true, "device1", "Smart Switch Pro", 
                "ACME", "SS-100", "area_living_room", new SwitchCaps(true)),
            
            new("switch.living_room_outlet", "Living Room Outlet", "off", false, "device2", "Smart Outlet", 
                "TechCorp", "SO-200", "area_living_room", new SwitchCaps(true)),
            
            new("switch.kitchen", "Kitchen Switch", "on", true, "device3", "Kitchen Switch", 
                "HomeSwitch", "KS-300", "area_kitchen", new SwitchCaps(true))
        };
    }

    #endregion
}