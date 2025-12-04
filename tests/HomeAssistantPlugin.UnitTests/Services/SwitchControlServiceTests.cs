using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Loupedeck.HomeAssistantPlugin;
using Loupedeck.HomeAssistantPlugin.Services;

using NSubstitute;

using Xunit;

namespace HomeAssistantPlugin.UnitTests.Services;

/// <summary>
/// Comprehensive tests for SwitchControlService focusing on switch control operations,
/// service integration, and error handling with 85% coverage target.
/// </summary>
public class SwitchControlServiceTests : IDisposable
{
    private readonly IHaClient _mockHaClient;
    private readonly SwitchControlService _service;
    private Boolean _disposed = false;

    public SwitchControlServiceTests()
    {
        this._mockHaClient = Substitute.For<IHaClient>();
        this._mockHaClient.IsAuthenticated.Returns(true);
        
        // Setup successful service calls by default
        this._mockHaClient.CallServiceAsync(Arg.Any<String>(), Arg.Any<String>(), Arg.Any<String>(), 
            Arg.Any<JsonElement?>(), Arg.Any<CancellationToken>())
            .Returns((true, null));

        this._service = new SwitchControlService(this._mockHaClient);
    }

    public void Dispose()
    {
        if (!this._disposed)
        {
            this._service?.Dispose();
            this._disposed = true;
        }
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidHaClient_InitializesSuccessfully()
    {
        // Arrange & Act
        using var service = new SwitchControlService(this._mockHaClient);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullHaClient_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var action = () => new SwitchControlService(null!);
        action.Should().Throw<ArgumentNullException>()
            .WithMessage("*ha*");
    }

    #endregion

    #region TurnOnAsync Tests

    [Fact]
    public async Task TurnOnAsync_WithValidEntity_CallsServiceSuccessfully()
    {
        // Arrange
        var entityId = "switch.test";

        // Act
        var result = await this._service.TurnOnAsync(entityId);

        // Assert
        result.Should().BeTrue();
        await this._mockHaClient.Received(1).CallServiceAsync(
            "switch", "turn_on", entityId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TurnOnAsync_WithJsonData_CallsServiceWithData()
    {
        // Arrange
        var entityId = "switch.test";
        var data = JsonSerializer.SerializeToElement(new { delay = 5 });

        // Act
        var result = await this._service.TurnOnAsync(entityId, data);

        // Assert
        result.Should().BeTrue();
        await this._mockHaClient.Received(1).CallServiceAsync(
            "switch", "turn_on", entityId, data, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TurnOnAsync_WithServiceFailure_ReturnsFalse()
    {
        // Arrange
        var entityId = "switch.test";
        this._mockHaClient.CallServiceAsync(Arg.Any<String>(), Arg.Any<String>(), Arg.Any<String>(),
            Arg.Any<JsonElement?>(), Arg.Any<CancellationToken>())
            .Returns((false, "Service call failed"));

        // Act
        var result = await this._service.TurnOnAsync(entityId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TurnOnAsync_WithCancellation_HandlesCancellation()
    {
        // Arrange
        var entityId = "switch.test";
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        this._mockHaClient.CallServiceAsync(Arg.Any<String>(), Arg.Any<String>(), Arg.Any<String>(),
            Arg.Any<JsonElement?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(Boolean, String?)>(new OperationCanceledException()));

        // Act
        var result = await this._service.TurnOnAsync(entityId, ct: cts.Token);

        // Assert
        result.Should().BeFalse(); // Should handle cancellation gracefully
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TurnOnAsync_WithInvalidEntityId_StillCallsService(String? entityId)
    {
        // Act
        var result = await this._service.TurnOnAsync(entityId!);

        // Assert - Service should still attempt the call (validation is done by HA)
        result.Should().BeTrue();
        await this._mockHaClient.Received(1).CallServiceAsync(
            "switch", "turn_on", entityId!, null, Arg.Any<CancellationToken>());
    }

    #endregion

    #region TurnOffAsync Tests

    [Fact]
    public async Task TurnOffAsync_WithValidEntity_CallsServiceSuccessfully()
    {
        // Arrange
        var entityId = "switch.test";

        // Act
        var result = await this._service.TurnOffAsync(entityId);

        // Assert
        result.Should().BeTrue();
        await this._mockHaClient.Received(1).CallServiceAsync(
            "switch", "turn_off", entityId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TurnOffAsync_WithServiceFailure_ReturnsFalse()
    {
        // Arrange
        var entityId = "switch.test";
        this._mockHaClient.CallServiceAsync("switch", "turn_off", entityId, null, Arg.Any<CancellationToken>())
            .Returns((false, "Turn off failed"));

        // Act
        var result = await this._service.TurnOffAsync(entityId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task TurnOffAsync_WithCancellation_HandlesCancellation()
    {
        // Arrange
        var entityId = "switch.test";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        this._mockHaClient.CallServiceAsync("switch", "turn_off", entityId, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(Boolean, String?)>(new OperationCanceledException()));

        // Act
        var result = await this._service.TurnOffAsync(entityId, cts.Token);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task TurnOffAsync_WithInvalidEntityId_StillCallsService(String? entityId)
    {
        // Act
        var result = await this._service.TurnOffAsync(entityId!);

        // Assert - Service should still attempt the call
        result.Should().BeTrue();
        await this._mockHaClient.Received(1).CallServiceAsync(
            "switch", "turn_off", entityId!, null, Arg.Any<CancellationToken>());
    }

    #endregion

    #region ToggleAsync Tests

    [Fact]
    public async Task ToggleAsync_WithValidEntity_CallsServiceSuccessfully()
    {
        // Arrange
        var entityId = "switch.test";

        // Act
        var result = await this._service.ToggleAsync(entityId);

        // Assert
        result.Should().BeTrue();
        await this._mockHaClient.Received(1).CallServiceAsync(
            "switch", "toggle", entityId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleAsync_WithServiceFailure_ReturnsFalse()
    {
        // Arrange
        var entityId = "switch.test";
        this._mockHaClient.CallServiceAsync("switch", "toggle", entityId, null, Arg.Any<CancellationToken>())
            .Returns((false, "Toggle failed"));

        // Act
        var result = await this._service.ToggleAsync(entityId);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleAsync_WithCancellation_HandlesCancellation()
    {
        // Arrange
        var entityId = "switch.test";
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        this._mockHaClient.CallServiceAsync("switch", "toggle", entityId, null, Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(Boolean, String?)>(new OperationCanceledException()));

        // Act
        var result = await this._service.ToggleAsync(entityId, cts.Token);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ToggleAsync_WithInvalidEntityId_StillCallsService(String? entityId)
    {
        // Act
        var result = await this._service.ToggleAsync(entityId!);

        // Assert - Service should still attempt the call
        result.Should().BeTrue();
        await this._mockHaClient.Received(1).CallServiceAsync(
            "switch", "toggle", entityId!, null, Arg.Any<CancellationToken>());
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ServiceMethods_WithException_HandleGracefully()
    {
        // Arrange
        var entityId = "switch.test";
        this._mockHaClient.CallServiceAsync(Arg.Any<String>(), Arg.Any<String>(), Arg.Any<String>(),
            Arg.Any<JsonElement?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(Boolean, String?)>(new InvalidOperationException("Test exception")));

        // Act & Assert - Should not propagate exceptions
        var result1 = await this._service.TurnOnAsync(entityId);
        var result2 = await this._service.TurnOffAsync(entityId);
        var result3 = await this._service.ToggleAsync(entityId);

        result1.Should().BeFalse();
        result2.Should().BeFalse();
        result3.Should().BeFalse();
    }

    [Fact]
    public async Task ServiceMethods_WithTimeout_HandleGracefully()
    {
        // Arrange
        var entityId = "switch.test";
        this._mockHaClient.CallServiceAsync(Arg.Any<String>(), Arg.Any<String>(), Arg.Any<String>(),
            Arg.Any<JsonElement?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<(Boolean, String?)>(new TaskCanceledException("Request timeout")));

        // Act & Assert
        var result1 = await this._service.TurnOnAsync(entityId);
        var result2 = await this._service.TurnOffAsync(entityId);
        var result3 = await this._service.ToggleAsync(entityId);

        result1.Should().BeFalse();
        result2.Should().BeFalse();
        result3.Should().BeFalse();
    }

    #endregion

    #region Integration Tests

    [Fact]
    public async Task MultipleOperations_WithDifferentEntities_HandledIndependently()
    {
        // Arrange
        var entity1 = "switch.living_room";
        var entity2 = "switch.kitchen";

        // Act - Call different operations on different entities
        var result1 = await this._service.TurnOnAsync(entity1);
        var result2 = await this._service.TurnOffAsync(entity2);
        var result3 = await this._service.ToggleAsync(entity1);

        // Assert - Each entity should receive its appropriate calls
        result1.Should().BeTrue();
        result2.Should().BeTrue();
        result3.Should().BeTrue();

        await this._mockHaClient.Received(1).CallServiceAsync(
            "switch", "turn_on", entity1, null, Arg.Any<CancellationToken>());

        await this._mockHaClient.Received(1).CallServiceAsync(
            "switch", "turn_off", entity2, null, Arg.Any<CancellationToken>());

        await this._mockHaClient.Received(1).CallServiceAsync(
            "switch", "toggle", entity1, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SequentialOperations_OnSameEntity_AllExecute()
    {
        // Arrange
        var entityId = "switch.test";

        // Act - Perform sequential operations on same entity
        var result1 = await this._service.TurnOnAsync(entityId);
        var result2 = await this._service.TurnOffAsync(entityId);
        var result3 = await this._service.ToggleAsync(entityId);

        // Assert - All operations should execute
        result1.Should().BeTrue();
        result2.Should().BeTrue();
        result3.Should().BeTrue();

        await this._mockHaClient.Received(1).CallServiceAsync(
            "switch", "turn_on", entityId, null, Arg.Any<CancellationToken>());

        await this._mockHaClient.Received(1).CallServiceAsync(
            "switch", "turn_off", entityId, null, Arg.Any<CancellationToken>());

        await this._mockHaClient.Received(1).CallServiceAsync(
            "switch", "toggle", entityId, null, Arg.Any<CancellationToken>());
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task HighFrequencyOperations_HandleCorrectly()
    {
        // Arrange
        var entityIds = new[] { "switch.test1", "switch.test2", "switch.test3" };
        var tasks = new List<Task<Boolean>>();

        // Act - Send many rapid operations
        for (var i = 0; i < 100; i++)
        {
            var entityId = entityIds[i % entityIds.Length];
            switch (i % 3)
            {
                case 0:
                    tasks.Add(this._service.TurnOnAsync(entityId));
                    break;
                case 1:
                    tasks.Add(this._service.TurnOffAsync(entityId));
                    break;
                case 2:
                    tasks.Add(this._service.ToggleAsync(entityId));
                    break;
            }
        }

        var results = await Task.WhenAll(tasks);

        // Assert - All operations should complete successfully
        results.Should().AllSatisfy(r => r.Should().BeTrue());
    }

    #endregion

    #region Service Call Verification Tests

    [Fact]
    public async Task TurnOnAsync_CallsCorrectServiceDomain()
    {
        // Arrange
        var entityId = "switch.test";

        // Act
        await this._service.TurnOnAsync(entityId);

        // Assert - Should call switch domain, not light domain
        await this._mockHaClient.Received(1).CallServiceAsync(
            "switch", "turn_on", entityId, null, Arg.Any<CancellationToken>());

        // Verify it doesn't call light domain
        await this._mockHaClient.DidNotReceive().CallServiceAsync(
            "light", Arg.Any<String>(), Arg.Any<String>(), Arg.Any<JsonElement?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ServiceCalls_UseCorrectServiceNames()
    {
        // Arrange
        var entityId = "switch.test";

        // Act
        await this._service.TurnOnAsync(entityId);
        await this._service.TurnOffAsync(entityId);
        await this._service.ToggleAsync(entityId);

        // Assert - Verify correct service names are used
        await this._mockHaClient.Received(1).CallServiceAsync(
            "switch", "turn_on", Arg.Any<String>(), Arg.Any<JsonElement?>(), Arg.Any<CancellationToken>());

        await this._mockHaClient.Received(1).CallServiceAsync(
            "switch", "turn_off", Arg.Any<String>(), Arg.Any<JsonElement?>(), Arg.Any<CancellationToken>());

        await this._mockHaClient.Received(1).CallServiceAsync(
            "switch", "toggle", Arg.Any<String>(), Arg.Any<JsonElement?>(), Arg.Any<CancellationToken>());
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_CleansUpResources()
    {
        // Arrange
        using var service = new SwitchControlService(this._mockHaClient);

        // Act & Assert - Should not throw
        var action = () => service.Dispose();
        action.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        using var service = new SwitchControlService(this._mockHaClient);

        // Act & Assert - Multiple disposes should not throw
        service.Dispose();
        var action = () => service.Dispose();
        action.Should().NotThrow();
    }

    [Fact]
    public async Task OperationsAfterDispose_HandleGracefully()
    {
        // Arrange
        var service = new SwitchControlService(this._mockHaClient);
        service.Dispose();

        // Act & Assert - Operations after dispose should still work (no internal state to clean up)
        var result1 = await service.TurnOnAsync("switch.test");
        var result2 = await service.TurnOffAsync("switch.test");
        var result3 = await service.ToggleAsync("switch.test");

        // Results may be true or false depending on implementation, but should not throw
        result1.Should().BeOfType<Boolean>();
        result2.Should().BeOfType<Boolean>();
        result3.Should().BeOfType<Boolean>();
    }

    #endregion

    #region Real-world Scenarios

    [Fact]
    public async Task TypicalSwitchOperations_WorkAsExpected()
    {
        // Arrange - Simulate typical switch usage patterns
        var wallSwitch = "switch.living_room_lights";
        var outlet = "switch.coffee_maker";
        var relay = "switch.garden_sprinkler";

        // Act - Typical operations
        var turnOnWallSwitch = await this._service.TurnOnAsync(wallSwitch);
        var turnOnOutlet = await this._service.TurnOnAsync(outlet);
        var toggleRelay = await this._service.ToggleAsync(relay);
        var turnOffOutlet = await this._service.TurnOffAsync(outlet);

        // Assert
        turnOnWallSwitch.Should().BeTrue();
        turnOnOutlet.Should().BeTrue();
        toggleRelay.Should().BeTrue();
        turnOffOutlet.Should().BeTrue();

        // Verify correct service calls
        await this._mockHaClient.Received(1).CallServiceAsync(
            "switch", "turn_on", wallSwitch, null, Arg.Any<CancellationToken>());
        await this._mockHaClient.Received(1).CallServiceAsync(
            "switch", "turn_on", outlet, null, Arg.Any<CancellationToken>());
        await this._mockHaClient.Received(1).CallServiceAsync(
            "switch", "toggle", relay, null, Arg.Any<CancellationToken>());
        await this._mockHaClient.Received(1).CallServiceAsync(
            "switch", "turn_off", outlet, null, Arg.Any<CancellationToken>());
    }

    #endregion
}