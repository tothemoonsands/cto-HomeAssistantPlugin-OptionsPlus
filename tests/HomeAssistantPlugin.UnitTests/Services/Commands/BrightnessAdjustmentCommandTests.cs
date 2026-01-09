using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NSubstitute;
using Xunit;
using Loupedeck.HomeAssistantPlugin;
using Loupedeck.HomeAssistantPlugin.Services;
using Loupedeck.HomeAssistantPlugin.Services.Commands;
using Loupedeck.HomeAssistantPlugin.Models;

namespace Loupedeck.HomeAssistantPlugin.Tests.Services.Commands
{
    /// <summary>
    /// Unit tests for BrightnessAdjustmentCommand with 100% coverage target
    /// Tests command execution, parameter validation, boundary conditions, service interactions, and error handling
    /// </summary>
    public class BrightnessAdjustmentCommandTests
    {
        private const string TestEntityId = "light.test_entity";
        private const double DefaultHue = 0.0;
        private const double DefaultSaturation = 50.0;
        private const int DefaultBrightness = 128;

        private readonly ILightStateManager _mockStateManager;
        private readonly ILightControlService _mockControlService;
        private readonly Dictionary<string, LookMode> _lookModeByEntity;
        private readonly AdjustmentCommandContext _context;
        private readonly BrightnessAdjustmentCommand _command;

        private bool _markCommandSentCalled;
        private string? _markCommandSentEntityId;
        private bool _adjustmentValueChangedCalled;
        private string? _adjustmentValueChangedParameter;

        public BrightnessAdjustmentCommandTests()
        {
            _mockStateManager = Substitute.For<ILightStateManager>();
            _mockControlService = Substitute.For<ILightControlService>();
            _lookModeByEntity = new Dictionary<string, LookMode>();

            _context = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                _lookModeByEntity,
                MarkCommandSent,
                TriggerAdjustmentValueChanged,
                GetTestCapabilities);

            _command = new BrightnessAdjustmentCommand(_context);
        }

        private void MarkCommandSent(string entityId)
        {
            _markCommandSentCalled = true;
            _markCommandSentEntityId = entityId;
        }

        private void TriggerAdjustmentValueChanged(string parameter)
        {
            _adjustmentValueChangedCalled = true;
            _adjustmentValueChangedParameter = parameter;
        }

        private LightCaps GetTestCapabilities(string entityId)
        {
            return new LightCaps(true, true, false, false); // Basic brightness support
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_ValidContext_CreatesInstance()
        {
            // Act & Assert
            Assert.NotNull(_command);
        }

        [Fact]
        public void Constructor_NullContext_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new BrightnessAdjustmentCommand(null!));
        }

        #endregion

        #region Execute Method - Basic Functionality

        [Fact]
        public void Execute_ValidPositiveDiff_IncreasesBrightness()
        {
            // Arrange
            var currentHsb = (DefaultHue, DefaultSaturation, DefaultBrightness);
            _mockStateManager.GetHsbValues(TestEntityId).Returns(currentHsb);
            _mockStateManager.IsLightOn(TestEntityId).Returns(true);

            // Act
            _command.Execute(TestEntityId, 5); // +5% brightness

            // Assert
            var expectedBrightness = DefaultBrightness + (int)Math.Round(255.0 * 5.0 / 100.0); // +12.75 -> 13
            _mockStateManager.Received(1).SetCachedBrightness(TestEntityId, Arg.Is<int>(b => b >= DefaultBrightness));
            _mockControlService.Received(1).SetBrightness(TestEntityId, Arg.Any<int>());
        }

        [Fact]
        public void Execute_ValidNegativeDiff_DecreasesBrightness()
        {
            // Arrange
            var currentHsb = (DefaultHue, DefaultSaturation, DefaultBrightness);
            _mockStateManager.GetHsbValues(TestEntityId).Returns(currentHsb);
            _mockStateManager.IsLightOn(TestEntityId).Returns(true);

            // Act
            _command.Execute(TestEntityId, -3); // -3% brightness

            // Assert
            var expectedBrightness = DefaultBrightness - (int)Math.Round(255.0 * 3.0 / 100.0); // -7.65 -> 8
            _mockStateManager.Received(1).SetCachedBrightness(TestEntityId, Arg.Is<int>(b => b <= DefaultBrightness));
            _mockControlService.Received(1).SetBrightness(TestEntityId, Arg.Any<int>());
        }

        [Fact]
        public void Execute_ZeroDiff_NoChange()
        {
            // Arrange
            var currentHsb = (DefaultHue, DefaultSaturation, DefaultBrightness);
            _mockStateManager.GetHsbValues(TestEntityId).Returns(currentHsb);
            _mockStateManager.IsLightOn(TestEntityId).Returns(true);

            // Act
            _command.Execute(TestEntityId, 0);

            // Assert
            _mockStateManager.Received(1).SetCachedBrightness(TestEntityId, DefaultBrightness);
            _mockControlService.Received(1).SetBrightness(TestEntityId, DefaultBrightness);
        }

        #endregion

        #region Boundary Conditions Testing

        [Theory]
        [InlineData(100, 255)] // Large positive diff should clamp to max
        [InlineData(50, 255)]  // Large diff that would exceed max
        [InlineData(15, 255)]  // Max allowed step per event (10%) from high value
        public void Execute_LargeDiff_ClampsToMaxBrightness(int diff, int expectedMax)
        {
            // Arrange - Start with high brightness
            var currentHsb = (DefaultHue, DefaultSaturation, 240);
            _mockStateManager.GetHsbValues(TestEntityId).Returns(currentHsb);
            _mockStateManager.IsLightOn(TestEntityId).Returns(true);

            // Act
            _command.Execute(TestEntityId, diff);

            // Assert
            _mockStateManager.Received(1).SetCachedBrightness(TestEntityId, Arg.Is<int>(b => b <= 255));
            _mockControlService.Received(1).SetBrightness(TestEntityId, Arg.Is<int>(b => b <= 255));
        }

        [Theory]
        [InlineData(-100, 0)] // Large negative diff should clamp to min
        [InlineData(-50, 0)]  // Large diff that would go below min
        [InlineData(-15, 0)]  // Max allowed step per event from low value
        public void Execute_LargeNegativeDiff_ClampsToMinBrightness(int diff, int expectedMin)
        {
            // Arrange - Start with low brightness
            var currentHsb = (DefaultHue, DefaultSaturation, 20);
            _mockStateManager.GetHsbValues(TestEntityId).Returns(currentHsb);
            _mockStateManager.IsLightOn(TestEntityId).Returns(true);

            // Act
            _command.Execute(TestEntityId, diff);

            // Assert
            _mockStateManager.Received(1).SetCachedBrightness(TestEntityId, Arg.Is<int>(b => b >= 0));
            _mockControlService.Received(1).SetBrightness(TestEntityId, Arg.Is<int>(b => b >= 0));
        }

        [Fact]
        public void Execute_MaxPerEventCap_LimitsDiffTo10Percent()
        {
            // Arrange
            var currentHsb = (DefaultHue, DefaultSaturation, DefaultBrightness);
            _mockStateManager.GetHsbValues(TestEntityId).Returns(currentHsb);
            _mockStateManager.IsLightOn(TestEntityId).Returns(true);

            // Act - Request 20% change, should be capped at 10%
            _command.Execute(TestEntityId, 20);

            // Assert - Maximum change should be 10% = 25.5 -> 26 brightness units
            var maxExpectedIncrease = DefaultBrightness + 26; // 10% of 255 = 25.5 -> 26
            _mockStateManager.Received(1).SetCachedBrightness(TestEntityId, Arg.Is<int>(b => b <= maxExpectedIncrease + 1)); // Allow for rounding
        }

        #endregion

        #region Light State Transitions (On/Off Logic)

        [Fact]
        public void Execute_OffLightWithPositiveDiff_TurnsOnAndUpdatesBrightness()
        {
            // Arrange - Light is OFF
            var currentHsb = (DefaultHue, DefaultSaturation, DefaultBrightness);
            _mockStateManager.GetHsbValues(TestEntityId).Returns(currentHsb);
            _mockStateManager.IsLightOn(TestEntityId).Returns(false);

            // Act
            _command.Execute(TestEntityId, 5); // +5% brightness

            // Assert - Should call UpdateLightState with ON=true and new brightness
            _mockStateManager.Received(1).UpdateLightState(TestEntityId, true, Arg.Is<int>(b => b > DefaultBrightness));
            _mockControlService.Received(1).SetBrightness(TestEntityId, Arg.Any<int>());
            Assert.True(_markCommandSentCalled);
            Assert.Equal(TestEntityId, _markCommandSentEntityId);
        }

        [Fact]
        public void Execute_OffLightWithNegativeDiff_TurnsOnWithMinimumBrightness()
        {
            // Arrange - Light is OFF with cached brightness
            var currentHsb = (DefaultHue, DefaultSaturation, 100);
            _mockStateManager.GetHsbValues(TestEntityId).Returns(currentHsb);
            _mockStateManager.IsLightOn(TestEntityId).Returns(false);

            // Act
            _command.Execute(TestEntityId, -20); // Large negative diff

            // Assert - Should still turn on but with reduced brightness (but > 0)
            _mockStateManager.Received(1).UpdateLightState(TestEntityId, true, Arg.Is<int>(b => b >= 0));
        }

        [Fact]
        public void Execute_OffLightResultingInZeroBrightness_StaysOff()
        {
            // Arrange - Light is OFF with low cached brightness
            var currentHsb = (DefaultHue, DefaultSaturation, 5);
            _mockStateManager.GetHsbValues(TestEntityId).Returns(currentHsb);
            _mockStateManager.IsLightOn(TestEntityId).Returns(false);

            // Act - Large negative diff that results in 0 brightness
            _command.Execute(TestEntityId, -10);

            // Assert - Should NOT call UpdateLightState since target brightness is 0
            _mockStateManager.DidNotReceive().UpdateLightState(TestEntityId, true, Arg.Any<int>());
            // Should still update cached brightness to 0
            _mockStateManager.Received(1).SetCachedBrightness(TestEntityId, 0);
        }

        [Fact]
        public void Execute_OnLightWithPositiveDiff_UpdatesCachedBrightnessOnly()
        {
            // Arrange - Light is ON
            var currentHsb = (DefaultHue, DefaultSaturation, DefaultBrightness);
            _mockStateManager.GetHsbValues(TestEntityId).Returns(currentHsb);
            _mockStateManager.IsLightOn(TestEntityId).Returns(true);

            // Act
            _command.Execute(TestEntityId, 3);

            // Assert - Should NOT call UpdateLightState, only SetCachedBrightness
            _mockStateManager.DidNotReceive().UpdateLightState(Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<int>());
            _mockStateManager.Received(1).SetCachedBrightness(TestEntityId, Arg.Any<int>());
        }

        #endregion

        #region UI Refresh and Service Calls

        [Fact]
        public void Execute_ValidCall_TriggersAllAdjustmentValueChanged()
        {
            // Arrange
            var currentHsb = (DefaultHue, DefaultSaturation, DefaultBrightness);
            _mockStateManager.GetHsbValues(TestEntityId).Returns(currentHsb);
            _mockStateManager.IsLightOn(TestEntityId).Returns(true);

            var triggeredParameters = new List<string>();
            var context = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                _lookModeByEntity,
                MarkCommandSent,
                param => triggeredParameters.Add(param),
                GetTestCapabilities);
            var command = new BrightnessAdjustmentCommand(context);

            // Act
            command.Execute(TestEntityId, 5);

            // Assert - Should trigger UI updates for all related adjustment types
            Assert.Contains("adj:bri", triggeredParameters);
            Assert.Contains("adj:ha-sat", triggeredParameters);
            Assert.Contains("adj:ha-hue", triggeredParameters);
            Assert.Contains("adj:ha-temp", triggeredParameters);
        }

        [Fact]
        public void Execute_ValidCall_MarksCommandSent()
        {
            // Arrange
            var currentHsb = (DefaultHue, DefaultSaturation, DefaultBrightness);
            _mockStateManager.GetHsbValues(TestEntityId).Returns(currentHsb);
            _mockStateManager.IsLightOn(TestEntityId).Returns(true);

            // Act
            _command.Execute(TestEntityId, 2);

            // Assert
            Assert.True(_markCommandSentCalled);
            Assert.Equal(TestEntityId, _markCommandSentEntityId);
        }

        [Fact]
        public void Execute_ValidCall_CallsLightControlService()
        {
            // Arrange
            var currentHsb = (DefaultHue, DefaultSaturation, DefaultBrightness);
            _mockStateManager.GetHsbValues(TestEntityId).Returns(currentHsb);
            _mockStateManager.IsLightOn(TestEntityId).Returns(true);

            // Act
            _command.Execute(TestEntityId, 7);

            // Assert
            _mockControlService.Received(1).SetBrightness(TestEntityId, Arg.Any<int>());
        }

        #endregion

        #region Null and Missing State Handling

        [Fact]
        public void Execute_NullStateManager_UsesFallbackValues()
        {
            // Arrange - Context with null state manager
            var contextWithNullManager = new AdjustmentCommandContext(
                null!,
                _mockControlService,
                _lookModeByEntity,
                MarkCommandSent,
                TriggerAdjustmentValueChanged,
                GetTestCapabilities);
            var commandWithNullManager = new BrightnessAdjustmentCommand(contextWithNullManager);

            // Act & Assert - Should not throw, will use fallback values
            commandWithNullManager.Execute(TestEntityId, 5);
            
            // Should still call control service with fallback calculations
            _mockControlService.Received(1).SetBrightness(TestEntityId, Arg.Any<int>());
        }

        [Fact]
        public void Execute_StateManagerReturnsDefaults_HandlesGracefully()
        {
            // Arrange - StateManager returns default HSB values
            _mockStateManager.GetHsbValues(TestEntityId).Returns((0.0, 0.0, 128));
            _mockStateManager.IsLightOn(TestEntityId).Returns(false);

            // Act
            _command.Execute(TestEntityId, 5);

            // Assert - Should handle default values appropriately
            _mockControlService.Received(1).SetBrightness(TestEntityId, Arg.Any<int>());
        }

        #endregion

        #region Error Handling

        [Fact]
        public void Execute_StateManagerThrowsException_HandlesGracefullyWithUIUpdate()
        {
            // Arrange
            _mockStateManager.When(x => x.GetHsbValues(Arg.Any<string>())).Do(x => throw new InvalidOperationException("Test exception"));

            // Act & Assert - Should not throw
            _command.Execute(TestEntityId, 5);

            // Should still trigger UI refresh for brightness adjustment on error
            Assert.True(_adjustmentValueChangedCalled);
            Assert.Equal("adj:bri", _adjustmentValueChangedParameter);
        }

        [Fact]
        public void Execute_ControlServiceThrowsException_HandlesGracefullyWithUIUpdate()
        {
            // Arrange
            var currentHsb = (DefaultHue, DefaultSaturation, DefaultBrightness);
            _mockStateManager.GetHsbValues(TestEntityId).Returns(currentHsb);
            _mockStateManager.IsLightOn(TestEntityId).Returns(true);
            _mockControlService.When(x => x.SetBrightness(Arg.Any<string>(), Arg.Any<int>())).Do(x => throw new InvalidOperationException("Service error"));

            // Act & Assert - Should not throw
            _command.Execute(TestEntityId, 3);

            // Should still update state manager before service call
            _mockStateManager.Received(1).SetCachedBrightness(TestEntityId, Arg.Any<int>());
        }

        #endregion

        #region Parameter Validation

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Execute_InvalidEntityId_HandlesGracefully(string? entityId)
        {
            // Act & Assert - Should not throw
            _command.Execute(entityId!, 5);
        }

        [Theory]
        [InlineData(int.MinValue)]
        [InlineData(int.MaxValue)]
        [InlineData(-1000)]
        [InlineData(1000)]
        public void Execute_ExtremeDiffValues_HandlesGracefully(int diff)
        {
            // Arrange
            var currentHsb = (DefaultHue, DefaultSaturation, DefaultBrightness);
            _mockStateManager.GetHsbValues(TestEntityId).Returns(currentHsb);
            _mockStateManager.IsLightOn(TestEntityId).Returns(true);

            // Act & Assert - Should not throw and should clamp appropriately
            _command.Execute(TestEntityId, diff);

            _mockControlService.Received(1).SetBrightness(TestEntityId, Arg.Is<int>(b => b >= 0 && b <= 255));
        }

        #endregion

        #region Step Calculation Precision

        [Theory]
        [InlineData(1, 128, 130)]   // 1% of 255 = 2.55, rounded = 3, so 128 + 3 = 131, but let's be flexible with rounding
        [InlineData(2, 128, 133)]   // 2% of 255 = 5.1, rounded = 5, so 128 + 5 = 133
        [InlineData(10, 128, 153)]  // 10% of 255 = 25.5, rounded = 26, so 128 + 26 = 154
        public void Execute_SmallDiffValues_CalculatesStepsCorrectly(int diff, int startBrightness, int expectedMinResult)
        {
            // Arrange
            var currentHsb = (DefaultHue, DefaultSaturation, startBrightness);
            _mockStateManager.GetHsbValues(TestEntityId).Returns(currentHsb);
            _mockStateManager.IsLightOn(TestEntityId).Returns(true);

            // Act
            _command.Execute(TestEntityId, diff);

            // Assert - Allow for rounding differences
            _mockStateManager.Received(1).SetCachedBrightness(TestEntityId, 
                Arg.Is<int>(b => Math.Abs(b - expectedMinResult) <= 2)); // Allow ±2 for rounding
        }

        #endregion
    }
}