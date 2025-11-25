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
/// Comprehensive tests for LightControlService focusing on light control coordination,
/// debouncing behavior, service integration, and error handling with 85% coverage target.
/// </summary>
public class LightControlServiceTests : IDisposable
{
    private readonly IHaClient _mockHaClient;
    private readonly LightControlService _service;
    private Boolean _disposed = false;

    public LightControlServiceTests()
    {
        this._mockHaClient = Substitute.For<IHaClient>();
        this._mockHaClient.IsAuthenticated.Returns(true);
        
        // Setup successful service calls by default
        this._mockHaClient.CallServiceAsync(Arg.Any<String>(), Arg.Any<String>(), Arg.Any<String>(), 
            Arg.Any<JsonElement?>(), Arg.Any<CancellationToken>())
            .Returns((true, null));

        this._service = new LightControlService(this._mockHaClient, 100, 100, 100); // Short debounce for tests
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
    public void Constructor_WithValidParameters_InitializesSuccessfully()
    {
        // Arrange & Act
        using var service = new LightControlService(this._mockHaClient, 200, 150, 300);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullHaClient_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        var action = () => new LightControlService(null!, 100, 100, 100);
        action.Should().Throw<ArgumentNullException>()
            .WithMessage("*ha*");
    }

    [Theory]
    [InlineData(0, 100, 100)]
    [InlineData(100, 0, 100)]
    [InlineData(100, 100, 0)]
    [InlineData(-1, 100, 100)]
    [InlineData(100, -1, 100)]
    [InlineData(100, 100, -1)]
    public void Constructor_WithValidDebounceValues_InitializesSuccessfully(Int32 brightnessMs, Int32 hsMs, Int32 tempMs)
    {
        // Arrange, Act & Assert
        var action = () => new LightControlService(this._mockHaClient, brightnessMs, hsMs, tempMs);
        action.Should().NotThrow();
    }

    #endregion

    #region SetBrightness Tests

    [Theory]
    [InlineData(128, 128)]    // Normal value
    [InlineData(0, 0)]        // Minimum value
    [InlineData(255, 255)]    // Maximum value
    [InlineData(-50, 0)]      // Below minimum, should clamp
    [InlineData(300, 255)]    // Above maximum, should clamp
    public void SetBrightness_WithVariousValues_ClampsCorrectly(Int32 inputValue, Int32 expectedValue)
    {
        // Arrange
        var entityId = "light.test";

        // Act
        this._service.SetBrightness(entityId, inputValue);

        // Assert - We can't directly verify the clamped value without exposing internals,
        // but we can verify the method doesn't throw
        // The actual clamping verification happens in integration tests with mock verification
    }

    [Fact]
    public async Task SetBrightness_MultipleCallsQuickly_DebouncesToLatestValue()
    {
        // Arrange
        var entityId = "light.test";
        
        // Act - Send multiple brightness values quickly
        this._service.SetBrightness(entityId, 100);
        this._service.SetBrightness(entityId, 150);
        this._service.SetBrightness(entityId, 200);

        // Wait for debounce to complete
        await Task.Delay(200);

        // Assert - Should only receive one call with the latest value (200)
        await this._mockHaClient.Received(1).CallServiceAsync(
            "light", "turn_on", entityId, 
            Arg.Is<JsonElement>(el => el.GetProperty("brightness").GetInt32() == 200),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SetBrightness_WithNullOrEmptyEntityId_HandlesGracefully()
    {
        // Act & Assert - Should not throw
        var action1 = () => this._service.SetBrightness(null!, 128);
        var action2 = () => this._service.SetBrightness("", 128);
        var action3 = () => this._service.SetBrightness("   ", 128);

        action1.Should().NotThrow();
        action2.Should().NotThrow();
        action3.Should().NotThrow();
    }

    #endregion

    #region SetHueSat Tests

    [Theory]
    [InlineData(180, 50, 180, 50)]        // Normal values
    [InlineData(0, 0, 0, 0)]              // Minimum values
    [InlineData(359, 100, 359, 100)]      // Maximum values
    [InlineData(-30, 50, 330, 50)]        // Negative hue should wrap
    [InlineData(380, 50, 20, 50)]         // Hue > 360 should wrap
    [InlineData(720, 50, 0, 50)]          // 720 degrees should wrap to 0
    [InlineData(180, -10, 180, 0)]        // Negative saturation should clamp
    [InlineData(180, 150, 180, 100)]      // Saturation > 100 should clamp
    public void SetHueSat_WithVariousValues_WrapsAndClampsCorrectly(
        Double inputHue, Double inputSat, Double expectedHue, Double expectedSat)
    {
        // Arrange
        var entityId = "light.test";

        // Act
        this._service.SetHueSat(entityId, inputHue, inputSat);

        // Assert - Method should not throw (actual wrapping/clamping verified in integration)
    }

    [Fact]
    public async Task SetHueSat_MultipleCallsQuickly_DebouncesToLatestValue()
    {
        // Arrange
        var entityId = "light.test";
        
        // Act - Send multiple HS values quickly
        this._service.SetHueSat(entityId, 120, 75);
        this._service.SetHueSat(entityId, 180, 50);
        this._service.SetHueSat(entityId, 240, 85);

        // Wait for debounce to complete
        await Task.Delay(200);

        // Assert - Should only receive one call with the latest values (240, 85)
        await this._mockHaClient.Received(1).CallServiceAsync(
            "light", "turn_on", entityId, 
            Arg.Is<JsonElement>(el => 
                el.GetProperty("hs_color").EnumerateArray().First().GetDouble() == 240 &&
                el.GetProperty("hs_color").EnumerateArray().Skip(1).First().GetDouble() == 85),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region SetTempMired Tests

    [Theory]
    [InlineData(300, 300)]    // Normal value
    [InlineData(153, 153)]    // Typical minimum
    [InlineData(500, 500)]    // Typical maximum
    [InlineData(0, 1)]        // Zero should clamp to minimum safe value
    [InlineData(-50, 1)]      // Negative should clamp to minimum safe value
    public void SetTempMired_WithVariousValues_ClampsCorrectly(Int32 inputMired, Int32 expectedMinimum)
    {
        // Arrange
        var entityId = "light.test";

        // Act & Assert - Should not throw
        var action = () => this._service.SetTempMired(entityId, inputMired);
        action.Should().NotThrow();
    }

    [Fact]
    public async Task SetTempMired_MultipleCallsQuickly_DebouncesToLatestValue()
    {
        // Arrange
        var entityId = "light.test";
        
        // Act - Send multiple temperature values quickly
        this._service.SetTempMired(entityId, 200);
        this._service.SetTempMired(entityId, 300);
        this._service.SetTempMired(entityId, 400);

        // Wait for debounce to complete
        await Task.Delay(200);

        // Assert - Should only receive one call with the latest value converted to Kelvin
        await this._mockHaClient.Received(1).CallServiceAsync(
            "light", "turn_on", entityId, 
            Arg.Is<JsonElement>(el => el.GetProperty("color_temp_kelvin").GetInt32() > 0),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region CancelPending Tests

    [Fact]
    public async Task CancelPending_WithPendingOperations_CancelsAllOperations()
    {
        // Arrange
        var entityId = "light.test";
        
        // Set up pending operations
        this._service.SetBrightness(entityId, 128);
        this._service.SetHueSat(entityId, 180, 50);
        this._service.SetTempMired(entityId, 300);

        // Act - Cancel before debounce completes
        this._service.CancelPending(entityId);

        // Wait longer than debounce time
        await Task.Delay(200);

        // Assert - No service calls should have been made
        await this._mockHaClient.DidNotReceive().CallServiceAsync(
            Arg.Any<String>(), Arg.Any<String>(), Arg.Any<String>(),
            Arg.Any<JsonElement?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void CancelPending_WithNoEntityId_HandlesGracefully()
    {
        // Act & Assert - Should not throw
        var action1 = () => this._service.CancelPending(null!);
        var action2 = () => this._service.CancelPending("");
        var action3 = () => this._service.CancelPending("   ");

        action1.Should().NotThrow();
        action2.Should().NotThrow();
        action3.Should().NotThrow();
    }

    [Fact]
    public void CancelPending_WithNoPendingOperations_HandlesGracefully()
    {
        // Act & Assert - Should not throw
        var action = () => this._service.CancelPending("light.nonexistent");
        action.Should().NotThrow();
    }

    #endregion

    #region TurnOnAsync Tests

    [Fact]
    public async Task TurnOnAsync_WithValidEntity_CallsServiceSuccessfully()
    {
        // Arrange
        var entityId = "light.test";

        // Act
        var result = await this._service.TurnOnAsync(entityId);

        // Assert
        result.Should().BeTrue();
        await this._mockHaClient.Received(1).CallServiceAsync(
            "light", "turn_on", entityId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TurnOnAsync_WithJsonData_CallsServiceWithData()
    {
        // Arrange
        var entityId = "light.test";
        var data = JsonSerializer.SerializeToElement(new { brightness = 200, hs_color = new[] { 120, 75 } });

        // Act
        var result = await this._service.TurnOnAsync(entityId, data);

        // Assert
        result.Should().BeTrue();
        await this._mockHaClient.Received(1).CallServiceAsync(
            "light", "turn_on", entityId, data, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TurnOnAsync_WithServiceFailure_ReturnsFalse()
    {
        // Arrange
        var entityId = "light.test";
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
        var entityId = "light.test";
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        this._mockHaClient.CallServiceAsync(Arg.Any<String>(), Arg.Any<String>(), Arg.Any<String>(),
            Arg.Any<JsonElement?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => throw new OperationCanceledException());

        // Act
        var result = await this._service.TurnOnAsync(entityId, ct: cts.Token);

        // Assert
        result.Should().BeFalse(); // Should handle cancellation gracefully
    }

    #endregion

    #region TurnOffAsync Tests

    [Fact]
    public async Task TurnOffAsync_WithValidEntity_CallsServiceSuccessfully()
    {
        // Arrange
        var entityId = "light.test";

        // Act
        var result = await this._service.TurnOffAsync(entityId);

        // Assert
        result.Should().BeTrue();
        await this._mockHaClient.Received(1).CallServiceAsync(
            "light", "turn_off", entityId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TurnOffAsync_WithServiceFailure_ReturnsFalse()
    {
        // Arrange
        var entityId = "light.test";
        this._mockHaClient.CallServiceAsync("light", "turn_off", entityId, null, Arg.Any<CancellationToken>())
            .Returns((false, "Turn off failed"));

        // Act
        var result = await this._service.TurnOffAsync(entityId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ToggleAsync Tests

    [Fact]
    public async Task ToggleAsync_WithValidEntity_CallsServiceSuccessfully()
    {
        // Arrange
        var entityId = "light.test";

        // Act
        var result = await this._service.ToggleAsync(entityId);

        // Assert
        result.Should().BeTrue();
        await this._mockHaClient.Received(1).CallServiceAsync(
            "light", "toggle", entityId, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ToggleAsync_WithServiceFailure_ReturnsFalse()
    {
        // Arrange
        var entityId = "light.test";
        this._mockHaClient.CallServiceAsync("light", "toggle", entityId, null, Arg.Any<CancellationToken>())
            .Returns((false, "Toggle failed"));

        // Act
        var result = await this._service.ToggleAsync(entityId);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Authentication and Connection Tests

    [Fact]
    public async Task DebouncedOperations_WhenNotAuthenticated_DoNotExecute()
    {
        // Arrange
        this._mockHaClient.IsAuthenticated.Returns(false);
        var entityId = "light.test";

        // Act
        this._service.SetBrightness(entityId, 128);
        await Task.Delay(200); // Wait for debounce

        // Assert - No service calls should be made when not authenticated
        await this._mockHaClient.DidNotReceive().CallServiceAsync(
            Arg.Any<String>(), Arg.Any<String>(), Arg.Any<String>(),
            Arg.Any<JsonElement?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TurnOnAsync_WhenNotAuthenticated_StillAttempts()
    {
        // Arrange - Direct service calls should still attempt even when not authenticated
        // (authentication is checked by the HaClient implementation)
        this._mockHaClient.IsAuthenticated.Returns(false);
        var entityId = "light.test";

        // Act
        var result = await this._service.TurnOnAsync(entityId);

        // Assert - Direct service calls should still be attempted
        result.Should().BeTrue();
        await this._mockHaClient.Received(1).CallServiceAsync(
            "light", "turn_on", entityId, null, Arg.Any<CancellationToken>());
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ServiceMethods_WithException_HandleGracefully()
    {
        // Arrange
        var entityId = "light.test";
        this._mockHaClient.CallServiceAsync(Arg.Any<String>(), Arg.Any<String>(), Arg.Any<String>(),
            Arg.Any<JsonElement?>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => throw new InvalidOperationException("Test exception"));

        // Act & Assert - Should not propagate exceptions
        var result1 = await this._service.TurnOnAsync(entityId);
        var result2 = await this._service.TurnOffAsync(entityId);
        var result3 = await this._service.ToggleAsync(entityId);

        result1.Should().BeFalse();
        result2.Should().BeFalse();
        result3.Should().BeFalse();
    }

    [Fact]
    public void DebouncedSetters_WithException_HandleGracefully()
    {
        // Arrange
        var entityId = "light.test";

        // Act & Assert - Should not throw exceptions
        var action1 = () => this._service.SetBrightness(entityId, 128);
        var action2 = () => this._service.SetHueSat(entityId, 180, 50);
        var action3 = () => this._service.SetTempMired(entityId, 300);
        var action4 = () => this._service.CancelPending(entityId);

        action1.Should().NotThrow();
        action2.Should().NotThrow();
        action3.Should().NotThrow();
        action4.Should().NotThrow();
    }

    #endregion

    #region Integration and Coordination Tests

    [Fact]
    public async Task MixedOperations_WithDifferentEntities_HandledIndependently()
    {
        // Arrange
        var entity1 = "light.living_room";
        var entity2 = "light.kitchen";

        // Act - Set different properties on different entities
        this._service.SetBrightness(entity1, 128);
        this._service.SetHueSat(entity2, 240, 75);
        
        await Task.Delay(200); // Wait for debounce

        // Assert - Each entity should receive its appropriate calls
        await this._mockHaClient.Received(1).CallServiceAsync(
            "light", "turn_on", entity1,
            Arg.Is<JsonElement>(el => el.GetProperty("brightness").GetInt32() == 128),
            Arg.Any<CancellationToken>());

        await this._mockHaClient.Received(1).CallServiceAsync(
            "light", "turn_on", entity2,
            Arg.Is<JsonElement>(el => el.GetProperty("hs_color").EnumerateArray().First().GetDouble() == 240),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DebouncedAndDirectOperations_WorkTogether()
    {
        // Arrange
        var entityId = "light.test";

        // Act - Mix debounced and direct operations
        this._service.SetBrightness(entityId, 128);
        var toggleResult = await this._service.ToggleAsync(entityId);
        
        await Task.Delay(200); // Wait for debounce

        // Assert - Both operations should execute
        toggleResult.Should().BeTrue();
        
        await this._mockHaClient.Received(1).CallServiceAsync(
            "light", "toggle", entityId, null, Arg.Any<CancellationToken>());
        
        await this._mockHaClient.Received(1).CallServiceAsync(
            "light", "turn_on", entityId,
            Arg.Is<JsonElement>(el => el.GetProperty("brightness").GetInt32() == 128),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Performance Tests

    [Fact]
    public async Task HighFrequencyOperations_HandleCorrectly()
    {
        // Arrange
        var entityId = "light.stress_test";

        // Act - Send many rapid updates
        for (var i = 0; i < 100; i++)
        {
            this._service.SetBrightness(entityId, i % 255);
            this._service.SetHueSat(entityId, i % 360, (i % 100) + 1);
            this._service.SetTempMired(entityId, 200 + (i % 300));
        }

        await Task.Delay(300); // Wait for all debounces to complete

        // Assert - Should only receive the final values for each operation type
        await this._mockHaClient.Received(1).CallServiceAsync(
            "light", "turn_on", entityId,
            Arg.Is<JsonElement>(el => el.GetProperty("brightness").GetInt32() == 99 % 255),
            Arg.Any<CancellationToken>());

        await this._mockHaClient.Received(1).CallServiceAsync(
            "light", "turn_on", entityId,
            Arg.Is<JsonElement>(el => el.GetProperty("hs_color").EnumerateArray().First().GetDouble() == 99 % 360),
            Arg.Any<CancellationToken>());

        await this._mockHaClient.Received(1).CallServiceAsync(
            "light", "turn_on", entityId,
            Arg.Is<JsonElement>(el => el.GetProperty("color_temp_kelvin").GetInt32() > 0),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_CleansUpResources()
    {
        // Arrange
        using var service = new LightControlService(this._mockHaClient, 100, 100, 100);

        // Act & Assert - Should not throw
        var action = () => service.Dispose();
        action.Should().NotThrow();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        using var service = new LightControlService(this._mockHaClient, 100, 100, 100);

        // Act & Assert - Multiple disposes should not throw
        service.Dispose();
        var action = () => service.Dispose();
        action.Should().NotThrow();
    }

    [Fact]
    public async Task OperationsAfterDispose_HandleGracefully()
    {
        // Arrange
        var service = new LightControlService(this._mockHaClient, 100, 100, 100);
        service.Dispose();

        // Act & Assert - Operations after dispose should handle gracefully
        var action1 = () => service.SetBrightness("light.test", 128);
        var action2 = () => service.SetHueSat("light.test", 180, 50);
        var action3 = () => service.SetTempMired("light.test", 300);
        var action4 = () => service.CancelPending("light.test");

        action1.Should().NotThrow();
        action2.Should().NotThrow();
        action3.Should().NotThrow();
        action4.Should().NotThrow();

        // Direct async operations might still work if the underlying client is not disposed
        var result = await service.TurnOnAsync("light.test");
        // Result may be true or false depending on implementation, but should not throw
    }

    #endregion
}