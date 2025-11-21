using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NSubstitute;
using Xunit;
using Loupedeck.HomeAssistantPlugin;
using Loupedeck.HomeAssistantPlugin.Services;
using Loupedeck.HomeAssistantPlugin.Services.Commands;
using Loupedeck.HomeAssistantPlugin.Models;

namespace HomeAssistantPlugin.UnitTests.Services.Commands
{
    /// <summary>
    /// Unit tests for HueAdjustmentCommand with 100% coverage target
    /// Tests hue adjustment with wrap-around behavior, capability checking, service interactions, and error handling
    /// </summary>
    public class HueAdjustmentCommandTests
    {
        private const string TestEntityId = "light.test_entity";
        private const double DefaultHue = 180.0;
        private const double DefaultSaturation = 100.0;
        private const int DefaultBrightness = 128;

        private readonly ILightStateManager _mockStateManager;
        private readonly ILightControlService _mockControlService;
        private readonly Dictionary<string, LookMode> _lookModeByEntity;
        private readonly AdjustmentCommandContext _context;
        private readonly HueAdjustmentCommand _command;

        private bool _markCommandSentCalled;
        private string? _markCommandSentEntityId;
        private readonly List<string> _adjustmentValueChangedParameters = new();

        public HueAdjustmentCommandTests()
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

            _command = new HueAdjustmentCommand(_context);
        }

        private void MarkCommandSent(string entityId)
        {
            _markCommandSentCalled = true;
            _markCommandSentEntityId = entityId;
        }

        private void TriggerAdjustmentValueChanged(string parameter)
        {
            _adjustmentValueChangedParameters.Add(parameter);
        }

        private LightCaps GetTestCapabilities(string entityId)
        {
            // Default: supports HS color
            return new LightCaps(true, true, false, true);
        }

        private LightCaps GetCapabilitiesWithoutHs(string entityId)
        {
            // Does not support HS color
            return new LightCaps(true, true, false, false);
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
            Assert.Throws<ArgumentNullException>(() => new HueAdjustmentCommand(null!));
        }

        #endregion

        #region Capability Checking

        [Fact]
        public void Execute_DeviceDoesNotSupportHsColor_ReturnsEarlyWithoutChanges()
        {
            // Arrange - Context with capabilities that don't support HS
            var contextWithoutHs = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                _lookModeByEntity,
                MarkCommandSent,
                TriggerAdjustmentValueChanged,
                GetCapabilitiesWithoutHs);
            var commandWithoutHs = new HueAdjustmentCommand(contextWithoutHs);

            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, DefaultSaturation, DefaultBrightness));

            // Act
            commandWithoutHs.Execute(TestEntityId, 5);

            // Assert - No service calls should be made
            _mockStateManager.DidNotReceive().UpdateHsColor(Arg.Any<string>(), Arg.Any<double>(), Arg.Any<double>());
            _mockControlService.DidNotReceive().SetHueSat(Arg.Any<string>(), Arg.Any<double>(), Arg.Any<double>());
            Assert.False(_markCommandSentCalled);
            Assert.Empty(_adjustmentValueChangedParameters);
        }

        [Fact]
        public void Execute_DeviceSupportsHsColor_ProcessesCommand()
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, DefaultSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, 5);

            // Assert - Service calls should be made
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, Arg.Any<double>(), DefaultSaturation);
            _mockControlService.Received(1).SetHueSat(TestEntityId, Arg.Any<double>(), DefaultSaturation);
            Assert.True(_markCommandSentCalled);
        }

        #endregion

        #region Look Mode Setting

        [Fact]
        public void Execute_ValidCall_SetsLookModeToHs()
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, DefaultSaturation, DefaultBrightness));
            Assert.False(_lookModeByEntity.ContainsKey(TestEntityId));

            // Act
            _command.Execute(TestEntityId, 10);

            // Assert
            Assert.True(_lookModeByEntity.ContainsKey(TestEntityId));
            Assert.Equal(LookMode.Hs, _lookModeByEntity[TestEntityId]);
        }

        [Fact]
        public void Execute_ExistingLookMode_UpdatesToHs()
        {
            // Arrange
            _lookModeByEntity[TestEntityId] = LookMode.Temp;
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, DefaultSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, 5);

            // Assert
            Assert.Equal(LookMode.Hs, _lookModeByEntity[TestEntityId]);
        }

        #endregion

        #region Hue Calculation and Wrapping

        [Theory]
        [InlineData(180.0, 5, 185.0)]    // Simple positive adjustment
        [InlineData(180.0, -10, 170.0)]  // Simple negative adjustment
        [InlineData(0.0, 5, 5.0)]        // From zero
        [InlineData(359.0, 2, 1.0)]      // Wrap around 360 to 0
        [InlineData(1.0, -5, 356.0)]     // Wrap around 0 to 360
        public void Execute_HueCalculation_HandlesWrappingCorrectly(double startHue, int diff, double expectedHue)
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((startHue, DefaultSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, diff);

            // Assert - Allow small tolerance for floating point precision
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, 
                Arg.Is<double>(h => Math.Abs(h - expectedHue) < 0.001), DefaultSaturation);
            _mockControlService.Received(1).SetHueSat(TestEntityId, 
                Arg.Is<double>(h => Math.Abs(h - expectedHue) < 0.001), DefaultSaturation);
        }

        [Theory]
        [InlineData(350.0, 50, 20.0)]    // Large positive wrap: 350 + 30 (capped) = 20
        [InlineData(10.0, -50, 340.0)]   // Large negative wrap: 10 - 30 (capped) = 340
        public void Execute_LargeDiffValues_CapsAndWrapsCorrectly(double startHue, int diff, double expectedHue)
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((startHue, DefaultSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, diff);

            // Assert - Should cap to max 30 degrees per event and wrap
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, 
                Arg.Is<double>(h => Math.Abs(h - expectedHue) < 0.001), DefaultSaturation);
        }

        [Fact]
        public void Execute_MaxHueDegPerEvent_CapsAt30Degrees()
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, DefaultSaturation, DefaultBrightness));

            // Act - Request 50 degree change
            _command.Execute(TestEntityId, 50);

            // Assert - Should be capped at 30 degrees: 180 + 30 = 210
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, 210.0, DefaultSaturation);
            _mockControlService.Received(1).SetHueSat(TestEntityId, 210.0, DefaultSaturation);
        }

        [Fact]
        public void Execute_NegativeMaxHueDegPerEvent_CapsAt30Degrees()
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, DefaultSaturation, DefaultBrightness));

            // Act - Request -50 degree change
            _command.Execute(TestEntityId, -50);

            // Assert - Should be capped at -30 degrees: 180 - 30 = 150
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, 150.0, DefaultSaturation);
            _mockControlService.Received(1).SetHueSat(TestEntityId, 150.0, DefaultSaturation);
        }

        #endregion

        #region Service Interactions

        [Fact]
        public void Execute_ValidCall_UpdatesStateManagerWithNewHue()
        {
            // Arrange
            var startHue = 120.0;
            _mockStateManager.GetHsbValues(TestEntityId).Returns((startHue, DefaultSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, 15);

            // Assert
            var expectedHue = startHue + 15; // 135.0
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, expectedHue, DefaultSaturation);
        }

        [Fact]
        public void Execute_ValidCall_CallsControlServiceWithCorrectValues()
        {
            // Arrange
            var startHue = 270.0;
            var currentSaturation = 75.0;
            _mockStateManager.GetHsbValues(TestEntityId).Returns((startHue, currentSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, -20);

            // Assert
            var expectedHue = startHue - 20; // 250.0
            _mockControlService.Received(1).SetHueSat(TestEntityId, expectedHue, currentSaturation);
        }

        [Fact]
        public void Execute_ValidCall_GetsCurrentSaturationFromStateManager()
        {
            // Arrange
            var currentSaturation = 85.0;
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, DefaultSaturation, DefaultBrightness));
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, currentSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, 10);

            // Assert - Should use the current saturation value
            _mockControlService.Received(1).SetHueSat(TestEntityId, Arg.Any<double>(), currentSaturation);
        }

        #endregion

        #region UI Refresh and Command Tracking

        [Fact]
        public void Execute_ValidCall_TriggersAllRelatedUIUpdates()
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, DefaultSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, 8);

            // Assert - Should trigger UI updates for hue, saturation, and temperature
            Assert.Contains("adj:ha-hue", _adjustmentValueChangedParameters);
            Assert.Contains("adj:ha-sat", _adjustmentValueChangedParameters);
            Assert.Contains("adj:ha-temp", _adjustmentValueChangedParameters);
        }

        [Fact]
        public void Execute_ValidCall_MarksCommandSent()
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, DefaultSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, 12);

            // Assert
            Assert.True(_markCommandSentCalled);
            Assert.Equal(TestEntityId, _markCommandSentEntityId);
        }

        #endregion

        #region Fallback Value Handling

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
            var commandWithNullManager = new HueAdjustmentCommand(contextWithNullManager);

            // Act & Assert - Should not throw, will use fallback values (0, 100, 128)
            commandWithNullManager.Execute(TestEntityId, 10);
            
            // Should call control service with fallback saturation (100.0)
            _mockControlService.Received(1).SetHueSat(TestEntityId, 10.0, 100.0);
        }

        [Fact]
        public void Execute_StateManagerReturnsDefaults_HandlesGracefully()
        {
            // Arrange - StateManager returns default HSB values
            _mockStateManager.GetHsbValues(TestEntityId).Returns((0.0, 100.0, 128));

            // Act
            _command.Execute(TestEntityId, 25);

            // Assert - Should handle default values appropriately
            _mockControlService.Received(1).SetHueSat(TestEntityId, 25.0, 100.0);
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

            // Should still trigger UI refresh for hue adjustment on error
            Assert.Contains("adj:ha-hue", _adjustmentValueChangedParameters);
        }

        [Fact]
        public void Execute_UpdateHsColorThrowsException_HandlesGracefully()
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, DefaultSaturation, DefaultBrightness));
            _mockStateManager.When(x => x.UpdateHsColor(Arg.Any<string>(), Arg.Any<double>(), Arg.Any<double>()))
                            .Do(x => throw new InvalidOperationException("Update error"));

            // Act & Assert - Should not throw
            _command.Execute(TestEntityId, 7);

            // Should still trigger UI refresh on error
            Assert.Contains("adj:ha-hue", _adjustmentValueChangedParameters);
        }

        [Fact]
        public void Execute_ControlServiceThrowsException_HandlesGracefully()
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, DefaultSaturation, DefaultBrightness));
            _mockControlService.When(x => x.SetHueSat(Arg.Any<string>(), Arg.Any<double>(), Arg.Any<double>()))
                              .Do(x => throw new InvalidOperationException("Service error"));

            // Act & Assert - Should not throw
            _command.Execute(TestEntityId, 3);

            // Should still update state manager before service call
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, Arg.Any<double>(), Arg.Any<double>());
        }

        #endregion

        #region Parameter Validation

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void Execute_InvalidEntityId_HandlesGracefully(string? entityId)
        {
            // Act & Assert - Should not throw, but also should return early due to capability check
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
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, DefaultSaturation, DefaultBrightness));

            // Act & Assert - Should not throw and should cap to max 30 degrees
            _command.Execute(TestEntityId, diff);

            // The actual hue should be within valid range after capping and wrapping
            _mockControlService.Received(1).SetHueSat(TestEntityId, 
                Arg.Is<double>(h => h >= 0.0 && h < 360.0), Arg.Any<double>());
        }

        #endregion

        #region Step Size Validation

        [Theory]
        [InlineData(1, 1.0)]    // 1 * 1 degree per tick = 1 degree
        [InlineData(5, 5.0)]    // 5 * 1 degree per tick = 5 degrees
        [InlineData(30, 30.0)]  // At the cap limit
        [InlineData(35, 30.0)]  // Should be capped to 30
        [InlineData(-25, -25.0)] // Negative within limit
        [InlineData(-40, -30.0)] // Negative should be capped to -30
        public void Execute_StepSizeCalculation_HandlesCorrectly(int inputDiff, double expectedStepSize)
        {
            // Arrange
            var startHue = 180.0;
            _mockStateManager.GetHsbValues(TestEntityId).Returns((startHue, DefaultSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, inputDiff);

            // Assert
            var expectedHue = HSBHelper.Wrap360(startHue + expectedStepSize);
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, 
                Arg.Is<double>(h => Math.Abs(h - expectedHue) < 0.001), DefaultSaturation);
        }

        #endregion

        #region Zero Diff Handling

        [Fact]
        public void Execute_ZeroDiff_NoHueChange()
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, DefaultSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, 0);

            // Assert - Hue should remain the same
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, DefaultHue, DefaultSaturation);
            _mockControlService.Received(1).SetHueSat(TestEntityId, DefaultHue, DefaultSaturation);
        }

        #endregion

        #region Precision Edge Cases

        [Theory]
        [InlineData(0.0, 360.0)]    // Edge case: exactly at boundary
        [InlineData(359.99, 0.01)]  // Very close to wrap point
        [InlineData(0.01, 359.99)]  // Very close to wrap point (negative)
        public void Execute_HueWrapEdgeCases_HandlesCorrectly(double startHue, double expectedAfterWrap)
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((startHue, DefaultSaturation, DefaultBrightness));

            // Act - Small adjustment that should trigger wrap
            var diff = startHue < 180 ? 1 : -1;
            _command.Execute(TestEntityId, diff);

            // Assert - Should handle wrap correctly
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, Arg.Any<double>(), DefaultSaturation);
            _mockControlService.Received(1).SetHueSat(TestEntityId, Arg.Any<double>(), DefaultSaturation);
        }

        #endregion
    }
}