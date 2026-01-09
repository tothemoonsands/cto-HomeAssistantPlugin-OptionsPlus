using System;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Loupedeck.HomeAssistantPlugin.Services;

using NSubstitute;

using Xunit;

namespace Loupedeck.HomeAssistantPlugin.Tests.Services;

/// <summary>
/// Comprehensive tests for HomeAssistantDataService focusing on API communication wrapper,
/// error handling, connection management, and external integration patterns with 85% coverage target.
/// </summary>
public class HomeAssistantDataServiceTests
{
    private readonly IHaClient _mockHaClient;
    private readonly HomeAssistantDataService _dataService;

    public HomeAssistantDataServiceTests()
    {
        this._mockHaClient = Substitute.For<IHaClient>();
        this._dataService = new HomeAssistantDataService(this._mockHaClient);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidClient_InitializesSuccessfully()
    {
        // Arrange & Act
        var service = new HomeAssistantDataService(this._mockHaClient);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullClient_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var action = () => new HomeAssistantDataService(null!);
        action.Should().Throw<ArgumentNullException>()
            .WithMessage("*client*");
    }

    #endregion

    #region FetchStatesAsync Tests

    [Fact]
    public async Task FetchStatesAsync_WithSuccessfulConnection_ReturnsStatesData()
    {
        // Arrange
        var expectedJson = """[{"entity_id": "light.test", "state": "on"}]""";
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync("get_states", Arg.Any<CancellationToken>())
            .Returns((true, expectedJson, null));

        // Act
        var (success, json, error) = await this._dataService.FetchStatesAsync(CancellationToken.None);

        // Assert
        success.Should().BeTrue();
        json.Should().Be(expectedJson);
        error.Should().BeNull();
        
        await this._mockHaClient.Received(1).EnsureConnectedAsync(
            Arg.Is<TimeSpan>(ts => ts.TotalSeconds == 8), Arg.Any<CancellationToken>());
        await this._mockHaClient.Received(1).RequestAsync("get_states", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FetchStatesAsync_WithConnectionFailure_ReturnsError()
    {
        // Arrange
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var (success, json, error) = await this._dataService.FetchStatesAsync(CancellationToken.None);

        // Assert
        success.Should().BeFalse();
        json.Should().BeNull();
        error.Should().Be("Could not establish connection to Home Assistant");
        
        await this._mockHaClient.DidNotReceive().RequestAsync(Arg.Any<String>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FetchStatesAsync_WithRequestFailure_ReturnsRequestError()
    {
        // Arrange
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync("get_states", Arg.Any<CancellationToken>())
            .Returns((false, null, "Request failed"));

        // Act
        var (success, json, error) = await this._dataService.FetchStatesAsync(CancellationToken.None);

        // Assert
        success.Should().BeFalse();
        json.Should().BeNull();
        error.Should().Be("Request failed");
    }

    [Fact]
    public async Task FetchStatesAsync_WithException_ReturnsExceptionMessage()
    {
        // Arrange
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(callInfo => throw new InvalidOperationException("Connection error"));

        // Act
        var (success, json, error) = await this._dataService.FetchStatesAsync(CancellationToken.None);

        // Assert
        success.Should().BeFalse();
        json.Should().BeNull();
        error.Should().Be("Connection error");
    }

    [Fact]
    public async Task FetchStatesAsync_WithCancellation_HandlesCancellation()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(callInfo => throw new OperationCanceledException());

        // Act
        var (success, json, error) = await this._dataService.FetchStatesAsync(cts.Token);

        // Assert
        success.Should().BeFalse();
        json.Should().BeNull();
        error.Should().NotBeNull();
    }

    [Fact]
    public async Task FetchStatesAsync_WithConnectionTimeout_ReturnsTimeoutError()
    {
        // Arrange
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(callInfo => throw new TimeoutException("Connection timeout"));

        // Act
        var (success, json, error) = await this._dataService.FetchStatesAsync(CancellationToken.None);

        // Assert
        success.Should().BeFalse();
        json.Should().BeNull();
        error.Should().Be("Connection timeout");
    }

    #endregion

    #region FetchServicesAsync Tests

    [Fact]
    public async Task FetchServicesAsync_WithSuccessfulConnection_ReturnsServicesData()
    {
        // Arrange
        var expectedJson = """{"light": {"turn_on": {}, "turn_off": {}}}""";
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync("get_services", Arg.Any<CancellationToken>())
            .Returns((true, expectedJson, null));

        // Act
        var (success, json, error) = await this._dataService.FetchServicesAsync(CancellationToken.None);

        // Assert
        success.Should().BeTrue();
        json.Should().Be(expectedJson);
        error.Should().BeNull();
        
        await this._mockHaClient.Received(1).RequestAsync("get_services", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FetchServicesAsync_WithConnectionFailure_ReturnsError()
    {
        // Arrange
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var (success, json, error) = await this._dataService.FetchServicesAsync(CancellationToken.None);

        // Assert
        success.Should().BeFalse();
        json.Should().BeNull();
        error.Should().Be("Could not establish connection to Home Assistant");
    }

    [Fact]
    public async Task FetchServicesAsync_WithRequestFailure_ReturnsRequestError()
    {
        // Arrange
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync("get_services", Arg.Any<CancellationToken>())
            .Returns((false, null, "Services unavailable"));

        // Act
        var (success, json, error) = await this._dataService.FetchServicesAsync(CancellationToken.None);

        // Assert
        success.Should().BeFalse();
        json.Should().BeNull();
        error.Should().Be("Services unavailable");
    }

    #endregion

    #region FetchEntityRegistryAsync Tests

    [Fact]
    public async Task FetchEntityRegistryAsync_WithSuccessfulConnection_ReturnsEntityData()
    {
        // Arrange
        var expectedJson = """[{"entity_id": "light.test", "device_id": "device1"}]""";
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync("config/entity_registry/list", Arg.Any<CancellationToken>())
            .Returns((true, expectedJson, null));

        // Act
        var (success, json, error) = await this._dataService.FetchEntityRegistryAsync(CancellationToken.None);

        // Assert
        success.Should().BeTrue();
        json.Should().Be(expectedJson);
        error.Should().BeNull();
        
        await this._mockHaClient.Received(1).RequestAsync("config/entity_registry/list", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FetchEntityRegistryAsync_WithConnectionFailure_ReturnsError()
    {
        // Arrange
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var (success, json, error) = await this._dataService.FetchEntityRegistryAsync(CancellationToken.None);

        // Assert
        success.Should().BeFalse();
        json.Should().BeNull();
        error.Should().Be("Could not establish connection to Home Assistant");
    }

    [Fact]
    public async Task FetchEntityRegistryAsync_WithRequestFailure_ReturnsRequestError()
    {
        // Arrange
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync("config/entity_registry/list", Arg.Any<CancellationToken>())
            .Returns((false, null, "Registry access denied"));

        // Act
        var (success, json, error) = await this._dataService.FetchEntityRegistryAsync(CancellationToken.None);

        // Assert
        success.Should().BeFalse();
        json.Should().BeNull();
        error.Should().Be("Registry access denied");
    }

    #endregion

    #region FetchDeviceRegistryAsync Tests

    [Fact]
    public async Task FetchDeviceRegistryAsync_WithSuccessfulConnection_ReturnsDeviceData()
    {
        // Arrange
        var expectedJson = """[{"id": "device1", "name": "Smart Bulb", "manufacturer": "ACME"}]""";
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync("config/device_registry/list", Arg.Any<CancellationToken>())
            .Returns((true, expectedJson, null));

        // Act
        var (success, json, error) = await this._dataService.FetchDeviceRegistryAsync(CancellationToken.None);

        // Assert
        success.Should().BeTrue();
        json.Should().Be(expectedJson);
        error.Should().BeNull();
        
        await this._mockHaClient.Received(1).RequestAsync("config/device_registry/list", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FetchDeviceRegistryAsync_WithConnectionFailure_ReturnsError()
    {
        // Arrange
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var (success, json, error) = await this._dataService.FetchDeviceRegistryAsync(CancellationToken.None);

        // Assert
        success.Should().BeFalse();
        json.Should().BeNull();
        error.Should().Be("Could not establish connection to Home Assistant");
    }

    [Fact]
    public async Task FetchDeviceRegistryAsync_WithRequestFailure_ReturnsRequestError()
    {
        // Arrange
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync("config/device_registry/list", Arg.Any<CancellationToken>())
            .Returns((false, null, "Device registry unavailable"));

        // Act
        var (success, json, error) = await this._dataService.FetchDeviceRegistryAsync(CancellationToken.None);

        // Assert
        success.Should().BeFalse();
        json.Should().BeNull();
        error.Should().Be("Device registry unavailable");
    }

    #endregion

    #region FetchAreaRegistryAsync Tests

    [Fact]
    public async Task FetchAreaRegistryAsync_WithSuccessfulConnection_ReturnsAreaData()
    {
        // Arrange
        var expectedJson = """[{"area_id": "living_room", "name": "Living Room"}]""";
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync("config/area_registry/list", Arg.Any<CancellationToken>())
            .Returns((true, expectedJson, null));

        // Act
        var (success, json, error) = await this._dataService.FetchAreaRegistryAsync(CancellationToken.None);

        // Assert
        success.Should().BeTrue();
        json.Should().Be(expectedJson);
        error.Should().BeNull();
        
        await this._mockHaClient.Received(1).RequestAsync("config/area_registry/list", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FetchAreaRegistryAsync_WithConnectionFailure_ReturnsError()
    {
        // Arrange
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var (success, json, error) = await this._dataService.FetchAreaRegistryAsync(CancellationToken.None);

        // Assert
        success.Should().BeFalse();
        json.Should().BeNull();
        error.Should().Be("Could not establish connection to Home Assistant");
    }

    [Fact]
    public async Task FetchAreaRegistryAsync_WithRequestFailure_ReturnsRequestError()
    {
        // Arrange
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync("config/area_registry/list", Arg.Any<CancellationToken>())
            .Returns((false, null, "Area registry not accessible"));

        // Act
        var (success, json, error) = await this._dataService.FetchAreaRegistryAsync(CancellationToken.None);

        // Assert
        success.Should().BeFalse();
        json.Should().BeNull();
        error.Should().Be("Area registry not accessible");
    }

    #endregion

    #region Connection Management and Timeout Tests

    [Fact]
    public async Task AllFetchMethods_UseCorrectConnectionTimeout()
    {
        // Arrange
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync(Arg.Any<String>(), Arg.Any<CancellationToken>())
            .Returns((true, "{}", null));

        // Act
        await this._dataService.FetchStatesAsync(CancellationToken.None);
        await this._dataService.FetchServicesAsync(CancellationToken.None);
        await this._dataService.FetchEntityRegistryAsync(CancellationToken.None);
        await this._dataService.FetchDeviceRegistryAsync(CancellationToken.None);
        await this._dataService.FetchAreaRegistryAsync(CancellationToken.None);

        // Assert - All methods should use 8-second timeout
        await this._mockHaClient.Received(5).EnsureConnectedAsync(
            Arg.Is<TimeSpan>(ts => ts.TotalSeconds == 8), 
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FetchMethods_PassThroughCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync(Arg.Any<String>(), Arg.Any<CancellationToken>())
            .Returns((true, "{}", null));

        // Act
        await this._dataService.FetchStatesAsync(cts.Token);

        // Assert
        await this._mockHaClient.Received(1).EnsureConnectedAsync(
            Arg.Any<TimeSpan>(), 
            Arg.Is<CancellationToken>(ct => ct == cts.Token));
        await this._mockHaClient.Received(1).RequestAsync(
            Arg.Any<String>(), 
            Arg.Is<CancellationToken>(ct => ct == cts.Token));
    }

    [Theory]
    [InlineData("get_states")]
    [InlineData("get_services")]
    [InlineData("config/entity_registry/list")]
    [InlineData("config/device_registry/list")]
    [InlineData("config/area_registry/list")]
    public async Task FetchMethods_UseCorrectApiEndpoints(String expectedEndpoint)
    {
        // Arrange
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync(Arg.Any<String>(), Arg.Any<CancellationToken>())
            .Returns((true, "{}", null));

        // Act
        switch (expectedEndpoint)
        {
            case "get_states":
                await this._dataService.FetchStatesAsync(CancellationToken.None);
                break;
            case "get_services":
                await this._dataService.FetchServicesAsync(CancellationToken.None);
                break;
            case "config/entity_registry/list":
                await this._dataService.FetchEntityRegistryAsync(CancellationToken.None);
                break;
            case "config/device_registry/list":
                await this._dataService.FetchDeviceRegistryAsync(CancellationToken.None);
                break;
            case "config/area_registry/list":
                await this._dataService.FetchAreaRegistryAsync(CancellationToken.None);
                break;
        }

        // Assert
        await this._mockHaClient.Received(1).RequestAsync(expectedEndpoint, Arg.Any<CancellationToken>());
    }

    #endregion

    #region Error Handling and Resilience Tests

    [Fact]
    public async Task FetchMethods_WithNetworkException_HandleGracefully()
    {
        // Arrange
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync(Arg.Any<String>(), Arg.Any<CancellationToken>())
            .Returns<Task<(bool, string?, string?)>>(callInfo => throw new System.Net.NetworkInformation.NetworkInformationException());

        // Act
        var (success1, _, error1) = await this._dataService.FetchStatesAsync(CancellationToken.None);
        var (success2, _, error2) = await this._dataService.FetchServicesAsync(CancellationToken.None);

        // Assert
        success1.Should().BeFalse();
        success2.Should().BeFalse();
        error1.Should().NotBeNull();
        error2.Should().NotBeNull();
    }

    [Fact]
    public async Task FetchMethods_WithNullResponseData_HandleCorrectly()
    {
        // Arrange
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync(Arg.Any<String>(), Arg.Any<CancellationToken>())
            .Returns((true, null, null)); // Success but null data

        // Act
        var (success, json, error) = await this._dataService.FetchStatesAsync(CancellationToken.None);

        // Assert
        success.Should().BeTrue(); // Request succeeded
        json.Should().BeNull(); // But data is null
        error.Should().BeNull();
    }

    [Fact]
    public async Task FetchMethods_WithEmptyResponseData_HandleCorrectly()
    {
        // Arrange
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync(Arg.Any<String>(), Arg.Any<CancellationToken>())
            .Returns((true, "", null)); // Success with empty string

        // Act
        var (success, json, error) = await this._dataService.FetchStatesAsync(CancellationToken.None);

        // Assert
        success.Should().BeTrue();
        json.Should().Be(""); // Empty string should be preserved
        error.Should().BeNull();
    }

    [Fact]
    public async Task FetchMethods_WithLargeResponse_HandleCorrectly()
    {
        // Arrange
        var largeJson = new String('x', 1_000_000); // 1MB of data
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync(Arg.Any<String>(), Arg.Any<CancellationToken>())
            .Returns((true, largeJson, null));

        // Act
        var (success, json, error) = await this._dataService.FetchStatesAsync(CancellationToken.None);

        // Assert
        success.Should().BeTrue();
        json.Should().Be(largeJson);
        error.Should().BeNull();
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task ConcurrentFetchOperations_HandleCorrectly()
    {
        // Arrange
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync(Arg.Any<String>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => 
            {
                var endpoint = (String)callInfo[0];
                return Task.FromResult((true, $"{{\"endpoint\": \"{endpoint}\"}}", (String?)null));
            });

        // Act - Perform concurrent fetches
        var tasks = new[]
        {
            this._dataService.FetchStatesAsync(CancellationToken.None),
            this._dataService.FetchServicesAsync(CancellationToken.None),
            this._dataService.FetchEntityRegistryAsync(CancellationToken.None),
            this._dataService.FetchDeviceRegistryAsync(CancellationToken.None),
            this._dataService.FetchAreaRegistryAsync(CancellationToken.None)
        };

        var results = await Task.WhenAll(tasks);

        // Assert - All should succeed
        foreach (var (success, json, error) in results)
        {
            success.Should().BeTrue();
            json.Should().NotBeNull();
            error.Should().BeNull();
        }

        // Verify all endpoints were called
        await this._mockHaClient.Received(1).RequestAsync("get_states", Arg.Any<CancellationToken>());
        await this._mockHaClient.Received(1).RequestAsync("get_services", Arg.Any<CancellationToken>());
        await this._mockHaClient.Received(1).RequestAsync("config/entity_registry/list", Arg.Any<CancellationToken>());
        await this._mockHaClient.Received(1).RequestAsync("config/device_registry/list", Arg.Any<CancellationToken>());
        await this._mockHaClient.Received(1).RequestAsync("config/area_registry/list", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MultipleFetchesOfSameEndpoint_HandleCorrectly()
    {
        // Arrange
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        
        var callCount = 0;
        this._mockHaClient.RequestAsync("get_states", Arg.Any<CancellationToken>())
            .Returns(callInfo => 
            {
                callCount++;
                return Task.FromResult((true, $"{{\"call\": {callCount}}}", (String?)null));
            });

        // Act - Make multiple concurrent calls to same endpoint
        var tasks = new[]
        {
            this._dataService.FetchStatesAsync(CancellationToken.None),
            this._dataService.FetchStatesAsync(CancellationToken.None),
            this._dataService.FetchStatesAsync(CancellationToken.None)
        };

        var results = await Task.WhenAll(tasks);

        // Assert - All should succeed and get unique responses
        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r =>
        {
            r.Success.Should().BeTrue();
            r.Json.Should().NotBeNull();
            r.Error.Should().BeNull();
        });

        await this._mockHaClient.Received(3).RequestAsync("get_states", Arg.Any<CancellationToken>());
    }

    #endregion

    #region Performance and Resource Management Tests

    [Fact]
    public async Task FetchOperations_WithSlowConnection_EventuallySucceed()
    {
        // Arrange - Simulate slow connection establishment
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(async callInfo =>
            {
                await Task.Delay(100); // Simulate slow connection
                return true;
            });
        this._mockHaClient.RequestAsync(Arg.Any<String>(), Arg.Any<CancellationToken>())
            .Returns((true, "{\"status\": \"ok\"}", null));

        // Act
        var (success, json, error) = await this._dataService.FetchStatesAsync(CancellationToken.None);

        // Assert
        success.Should().BeTrue();
        json.Should().NotBeNull();
        error.Should().BeNull();
    }

    [Fact]
    public async Task FetchOperations_WithMemoryPressure_HandleCorrectly()
    {
        // Arrange - Simulate operations under memory pressure
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Simulate potential OutOfMemoryException handling
        var largeData = new String('A', 100_000); // Large but manageable string
        this._mockHaClient.RequestAsync(Arg.Any<String>(), Arg.Any<CancellationToken>())
            .Returns((true, largeData, null));

        // Act & Assert - Should handle large responses without issues
        for (var i = 0; i < 10; i++)
        {
            var (success, json, error) = await this._dataService.FetchStatesAsync(CancellationToken.None);
            success.Should().BeTrue();
            json.Should().HaveLength(100_000);
            error.Should().BeNull();
        }
    }

    #endregion

    #region Integration Pattern Tests

    [Fact]
    public async Task FetchMethods_FollowConsistentPattern()
    {
        // Arrange
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync(Arg.Any<String>(), Arg.Any<CancellationToken>())
            .Returns((true, "{}", null));

        // Act - Test all methods follow same pattern
        var statesResult = await this._dataService.FetchStatesAsync(CancellationToken.None);
        var servicesResult = await this._dataService.FetchServicesAsync(CancellationToken.None);
        var entityResult = await this._dataService.FetchEntityRegistryAsync(CancellationToken.None);
        var deviceResult = await this._dataService.FetchDeviceRegistryAsync(CancellationToken.None);
        var areaResult = await this._dataService.FetchAreaRegistryAsync(CancellationToken.None);

        // Assert - All methods return same tuple structure
        AssertTupleStructure(statesResult);
        AssertTupleStructure(servicesResult);
        AssertTupleStructure(entityResult);
        AssertTupleStructure(deviceResult);
        AssertTupleStructure(areaResult);

        static void AssertTupleStructure((Boolean Success, String? Json, String? Error) result)
        {
            result.Success.Should().BeTrue();
            result.Json.Should().NotBeNull();
            result.Error.Should().BeNull();
        }
    }

    [Fact]
    public async Task ErrorResponses_FollowConsistentPattern()
    {
        // Arrange - Different types of failures
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(false); // Connection failure

        // Act
        var results = new[]
        {
            await this._dataService.FetchStatesAsync(CancellationToken.None),
            await this._dataService.FetchServicesAsync(CancellationToken.None),
            await this._dataService.FetchEntityRegistryAsync(CancellationToken.None),
            await this._dataService.FetchDeviceRegistryAsync(CancellationToken.None),
            await this._dataService.FetchAreaRegistryAsync(CancellationToken.None)
        };

        // Assert - All should follow same error pattern
        foreach (var (success, json, error) in results)
        {
            success.Should().BeFalse();
            json.Should().BeNull();
            error.Should().Be("Could not establish connection to Home Assistant");
        }
    }

    #endregion

    #region Edge Cases Tests

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData("undefined")]
    public async Task FetchMethods_WithVariousEmptyResponses_HandleCorrectly(String responseData)
    {
        // Arrange
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync(Arg.Any<String>(), Arg.Any<CancellationToken>())
            .Returns((true, responseData, null));

        // Act
        var (success, json, error) = await this._dataService.FetchStatesAsync(CancellationToken.None);

        // Assert
        success.Should().BeTrue();
        json.Should().Be(responseData); // Should preserve original response
        error.Should().BeNull();
    }

    [Fact]
    public async Task FetchMethods_WithUnicodeContent_HandleCorrectly()
    {
        // Arrange
        var unicodeJson = """{"entity_id": "light.тест", "state": "🔥", "friendly_name": "Тест 灯"}""";
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync(Arg.Any<String>(), Arg.Any<CancellationToken>())
            .Returns((true, unicodeJson, null));

        // Act
        var (success, json, error) = await this._dataService.FetchStatesAsync(CancellationToken.None);

        // Assert
        success.Should().BeTrue();
        json.Should().Be(unicodeJson);
        error.Should().BeNull();
    }

    [Fact]
    public async Task FetchMethods_WithMalformedJson_ReturnsDataAsIs()
    {
        // Arrange - HomeAssistantDataService doesn't validate JSON, just passes it through
        var malformedJson = """{"entity_id": "light.test", "state":}"""; // Malformed JSON
        this._mockHaClient.EnsureConnectedAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(true);
        this._mockHaClient.RequestAsync(Arg.Any<String>(), Arg.Any<CancellationToken>())
            .Returns((true, malformedJson, null));

        // Act
        var (success, json, error) = await this._dataService.FetchStatesAsync(CancellationToken.None);

        // Assert - Service should pass through data as-is (validation happens elsewhere)
        success.Should().BeTrue();
        json.Should().Be(malformedJson);
        error.Should().BeNull();
    }

    #endregion
}