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
    /// Unit tests for TemperatureAdjustmentCommand with 100% coverage target
    /// Tests color temperature adjustments, mired/Kelvin conversion, range validation, and error handling
    /// </summary>
    public class TemperatureAdjustmentCommandTests
    {
        private const string TestEntityId = "light.test_entity";
        private const int DefaultMinMireds = 153;
        private const int DefaultMaxMireds = 500;
        private const int DefaultCurrentMireds = 370;

        private readonly ILightStateManager _mockStateManager;
        private readonly ILightControlService _mockControlService;
        private readonly Dictionary<string, LookMode> _lookModeByEntity;
        private readonly AdjustmentCommandContext _context;
        private readonly TemperatureAdjustmentCommand _command;

        private bool _markCommandSentCalled;
        private string? _markCommandSentEntityId;
        private readonly List<string> _adjustmentValueChangedParameters = new();

        public TemperatureAdjustmentCommandTests()
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

            _command = new TemperatureAdjustmentCommand(_context);
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
            // Default: supports color temperature
            return new LightCaps(true, true, true, false);
        }

        private LightCaps GetCapabilitiesWithoutTemp(string entityId)
        {
            // Does not support color temperature
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
            Assert.Throws<ArgumentNullException>(() => new TemperatureAdjustmentCommand(null!));
        }

        #endregion

        #region Capability Checking

        [Fact]
        public void Execute_DeviceDoesNotSupportColorTemp_ReturnsEarlyWithoutChanges()
        {
            // Arrange - Context with capabilities that don't support color temperature
            var contextWithoutTemp = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                _lookModeByEntity,
                MarkCommandSent,
                TriggerAdjustmentValueChanged,
                GetCapabilitiesWithoutTemp);
            var commandWithoutTemp = new TemperatureAdjustmentCommand(contextWithoutTemp);

            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((DefaultMinMireds, DefaultMaxMireds, DefaultCurrentMireds));

            // Act
            commandWithoutTemp.Execute(TestEntityId, 5);

            // Assert - No service calls should be made
            _mockStateManager.DidNotReceive().SetCachedTempMired(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int>());
            _mockControlService.DidNotReceive().SetTempMired(Arg.Any<string>(), Arg.Any<int>());
            Assert.False(_markCommandSentCalled);
            Assert.Empty(_adjustmentValueChangedParameters);
        }

        [Fact]
        public void Execute_DeviceSupportsColorTemp_ProcessesCommand()
        {
            // Arrange
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((DefaultMinMireds, DefaultMaxMireds, DefaultCurrentMireds));

            // Act
            _command.Execute(TestEntityId, 5);

            // Assert - Service calls should be made
            _mockStateManager.Received(1).SetCachedTempMired(TestEntityId, null, null, Arg.Any<int>());
            _mockControlService.Received(1).SetTempMired(TestEntityId, Arg.Any<int>());
            Assert.True(_markCommandSentCalled);
        }

        #endregion

        #region Look Mode Setting

        [Fact]
        public void Execute_ValidCall_SetsLookModeToTemp()
        {
            // Arrange
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((DefaultMinMireds, DefaultMaxMireds, DefaultCurrentMireds));
            Assert.False(_lookModeByEntity.ContainsKey(TestEntityId));

            // Act
            _command.Execute(TestEntityId, 10);

            // Assert
            Assert.True(_lookModeByEntity.ContainsKey(TestEntityId));
            Assert.Equal(LookMode.Temp, _lookModeByEntity[TestEntityId]);
        }

        [Fact]
        public void Execute_ExistingLookMode_UpdatesToTemp()
        {
            // Arrange
            _lookModeByEntity[TestEntityId] = LookMode.Hs;
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((DefaultMinMireds, DefaultMaxMireds, DefaultCurrentMireds));

            // Act
            _command.Execute(TestEntityId, 5);

            // Assert
            Assert.Equal(LookMode.Temp, _lookModeByEntity[TestEntityId]);
        }

        #endregion

        #region Temperature Calculation and Clamping

        [Theory]
        [InlineData(300, 5, 310)]    // Simple positive adjustment: 300 + (5*2) = 310
        [InlineData(400, -10, 380)]  // Simple negative adjustment: 400 + (-10*2) = 380
        [InlineData(200, 0, 200)]    // Zero adjustment
        [InlineData(250, 15, 280)]   // 15 * 2 = 30 step
        public void Execute_TemperatureCalculation_HandlesCorrectly(int startMireds, int diff, int expectedMireds)
        {
            // Arrange
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((DefaultMinMireds, DefaultMaxMireds, startMireds));

            // Act
            _command.Execute(TestEntityId, diff);

            // Assert
            _mockStateManager.Received(1).SetCachedTempMired(TestEntityId, null, null, expectedMireds);
            _mockControlService.Received(1).SetTempMired(TestEntityId, expectedMireds);
        }

        [Theory]
        [InlineData(480, 15, 500)]   // Near max, clamp: 480 + 30 = 510 -> 500 (max)
        [InlineData(450, 50, 500)]   // Large positive clamp: 450 + 60 (capped) = 510 -> 500
        [InlineData(490, 10, 500)]   // Just over maximum: 490 + 20 = 510 -> 500
        public void Execute_PositiveTemperatureOverflow_ClampsToMaximum(int startMireds, int diff, int expectedMireds)
        {
            // Arrange
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((DefaultMinMireds, DefaultMaxMireds, startMireds));

            // Act
            _command.Execute(TestEntityId, diff);

            // Assert - Should clamp to maximum
            _mockStateManager.Received(1).SetCachedTempMired(TestEntityId, null, null, expectedMireds);
            _mockControlService.Received(1).SetTempMired(TestEntityId, expectedMireds);
        }

        [Theory]
        [InlineData(170, -15, 153)]  // Near min, clamp: 170 - 30 = 140 -> 153 (min)
        [InlineData(200, -50, 153)]  // Large negative clamp: 200 - 60 (capped) = 140 -> 153
        [InlineData(160, -10, 153)]  // Just under minimum: 160 - 20 = 140 -> 153
        public void Execute_NegativeTemperatureUnderflow_ClampsToMinimum(int startMireds, int diff, int expectedMireds)
        {
            // Arrange
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((DefaultMinMireds, DefaultMaxMireds, startMireds));

            // Act
            _command.Execute(TestEntityId, diff);

            // Assert - Should clamp to minimum
            _mockStateManager.Received(1).SetCachedTempMired(TestEntityId, null, null, expectedMireds);
            _mockControlService.Received(1).SetTempMired(TestEntityId, expectedMireds);
        }

        [Fact]
        public void Execute_MaxMiredsPerEvent_CapsAt60Mireds()
        {
            // Arrange - TempStepMireds = 2, so 50 * 2 = 100, capped at 60
            var startMireds = 300;
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((DefaultMinMireds, DefaultMaxMireds, startMireds));

            // Act - Request 50 step change (100 mireds), should be capped at 60
            _command.Execute(TestEntityId, 50);

            // Assert - Should be capped at +60 mireds: 300 + 60 = 360
            _mockStateManager.Received(1).SetCachedTempMired(TestEntityId, null, null, 360);
            _mockControlService.Received(1).SetTempMired(TestEntityId, 360);
        }

        [Fact]
        public void Execute_NegativeMaxMiredsPerEvent_CapsAt60Mireds()
        {
            // Arrange
            var startMireds = 350;
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((DefaultMinMireds, DefaultMaxMireds, startMireds));

            // Act - Request -40 step change (-80 mireds), should be capped at -60
            _command.Execute(TestEntityId, -40);

            // Assert - Should be capped at -60 mireds: 350 - 60 = 290
            _mockStateManager.Received(1).SetCachedTempMired(TestEntityId, null, null, 290);
            _mockControlService.Received(1).SetTempMired(TestEntityId, 290);
        }

        #endregion

        #region Custom Range Handling

        [Fact]
        public void Execute_CustomMinMaxRange_RespectsLightSpecificLimits()
        {
            // Arrange - Light with narrower temperature range
            var customMin = 200;
            var customMax = 400;
            var startMireds = 350;
            
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((customMin, customMax, startMireds));

            // Act - Request change that would exceed custom max
            _command.Execute(TestEntityId, 30); // 350 + 60 = 410, should clamp to 400

            // Assert - Should clamp to custom maximum
            _mockStateManager.Received(1).SetCachedTempMired(TestEntityId, null, null, customMax);
            _mockControlService.Received(1).SetTempMired(TestEntityId, customMax);
        }

        [Fact]
        public void Execute_CustomMinMaxRange_RespectsLightSpecificMinimum()
        {
            // Arrange - Light with narrower temperature range
            var customMin = 250;
            var customMax = 450;
            var startMireds = 280;
            
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((customMin, customMax, startMireds));

            // Act - Request change that would go below custom min
            _command.Execute(TestEntityId, -20); // 280 - 40 = 240, should clamp to 250

            // Assert - Should clamp to custom minimum
            _mockStateManager.Received(1).SetCachedTempMired(TestEntityId, null, null, customMin);
            _mockControlService.Received(1).SetTempMired(TestEntityId, customMin);
        }

        #endregion

        #region Service Interactions

        [Fact]
        public void Execute_ValidCall_UpdatesStateManagerWithNewTemperature()
        {
            // Arrange
            var startMireds = 250;
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((DefaultMinMireds, DefaultMaxMireds, startMireds));

            // Act
            _command.Execute(TestEntityId, 8); // 8 * 2 = 16 mireds

            // Assert
            var expectedMireds = startMireds + 16; // 266
            _mockStateManager.Received(1).SetCachedTempMired(TestEntityId, null, null, expectedMireds);
        }

        [Fact]
        public void Execute_ValidCall_CallsControlServiceWithCorrectValue()
        {
            // Arrange
            var startMireds = 300;
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((DefaultMinMireds, DefaultMaxMireds, startMireds));

            // Act
            _command.Execute(TestEntityId, -12); // -12 * 2 = -24 mireds

            // Assert
            var expectedMireds = startMireds - 24; // 276
            _mockControlService.Received(1).SetTempMired(TestEntityId, expectedMireds);
        }

        [Fact]
        public void Execute_ValidCall_PreservesMinMaxInStateManager()
        {
            // Arrange
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((DefaultMinMireds, DefaultMaxMireds, DefaultCurrentMireds));

            // Act
            _command.Execute(TestEntityId, 5);

            // Assert - Should pass null for min/max to preserve existing values
            _mockStateManager.Received(1).SetCachedTempMired(TestEntityId, null, null, Arg.Any<int>());
        }

        #endregion

        #region UI Refresh and Command Tracking

        [Fact]
        public void Execute_ValidCall_TriggersAllRelatedUIUpdates()
        {
            // Arrange
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((DefaultMinMireds, DefaultMaxMireds, DefaultCurrentMireds));

            // Act
            _command.Execute(TestEntityId, 3);

            // Assert - Should trigger UI updates for temperature, hue, and saturation
            Assert.Contains("adj:ha-temp", _adjustmentValueChangedParameters);
            Assert.Contains("adj:ha-hue", _adjustmentValueChangedParameters);
            Assert.Contains("adj:ha-sat", _adjustmentValueChangedParameters);
        }

        [Fact]
        public void Execute_ValidCall_MarksCommandSent()
        {
            // Arrange
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((DefaultMinMireds, DefaultMaxMireds, DefaultCurrentMireds));

            // Act
            _command.Execute(TestEntityId, 7);

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
            var commandWithNullManager = new TemperatureAdjustmentCommand(contextWithNullManager);

            // Act & Assert - Should not throw, will use fallback values
            commandWithNullManager.Execute(TestEntityId, 5);
            
            // Should call control service with calculated value from fallbacks
            // Default fallback: (153, 500, 370) + (5 * 2) = 380
            _mockControlService.Received(1).SetTempMired(TestEntityId, 380);
        }

        [Fact]
        public void Execute_StateManagerReturnsNull_UsesFallbackValues()
        {
            // Arrange - StateManager returns null for temperature data
            _mockStateManager.GetColorTempMired(TestEntityId).Returns((ValueTuple<int, int, int>?)null);

            // Act
            _command.Execute(TestEntityId, -10);

            // Assert - Should handle null gracefully with fallback values
            // Default fallback: (153, 500, 370) + (-10 * 2) = 350
            _mockControlService.Received(1).SetTempMired(TestEntityId, 350);
        }

        [Fact]
        public void Execute_StateManagerReturnsDefaults_HandlesGracefully()
        {
            // Arrange - StateManager returns specific default values
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((DefaultMinMireds, DefaultMaxMireds, DefaultCurrentMireds));

            // Act
            _command.Execute(TestEntityId, 8);

            // Assert - Should handle default values appropriately: 370 + 16 = 386
            _mockControlService.Received(1).SetTempMired(TestEntityId, 386);
        }

        #endregion

        #region Error Handling

        [Fact]
        public void Execute_StateManagerThrowsException_HandlesGracefullyWithUIUpdate()
        {
            // Arrange
            _mockStateManager.When(x => x.GetColorTempMired(Arg.Any<string>())).Do(x => throw new InvalidOperationException("Test exception"));

            // Act & Assert - Should not throw
            _command.Execute(TestEntityId, 5);

            // Should still trigger UI refresh for temperature adjustment on error
            Assert.Contains("adj:ha-temp", _adjustmentValueChangedParameters);
        }

        [Fact]
        public void Execute_SetCachedTempMiredThrowsException_HandlesGracefully()
        {
            // Arrange
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((DefaultMinMireds, DefaultMaxMireds, DefaultCurrentMireds));
            _mockStateManager.When(x => x.SetCachedTempMired(Arg.Any<string>(), Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int>()))
                            .Do(x => throw new InvalidOperationException("Cache error"));

            // Act & Assert - Should not throw
            _command.Execute(TestEntityId, 4);

            // Should still trigger UI refresh on error
            Assert.Contains("adj:ha-temp", _adjustmentValueChangedParameters);
        }

        [Fact]
        public void Execute_ControlServiceThrowsException_HandlesGracefully()
        {
            // Arrange
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((DefaultMinMireds, DefaultMaxMireds, DefaultCurrentMireds));
            _mockControlService.When(x => x.SetTempMired(Arg.Any<string>(), Arg.Any<int>()))
                              .Do(x => throw new InvalidOperationException("Service error"));

            // Act & Assert - Should not throw
            _command.Execute(TestEntityId, 6);

            // Should still update state manager before service call
            _mockStateManager.Received(1).SetCachedTempMired(TestEntityId, null, null, Arg.Any<int>());
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
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((DefaultMinMireds, DefaultMaxMireds, DefaultCurrentMireds));

            // Act & Assert - Should not throw and should clamp appropriately
            _command.Execute(TestEntityId, diff);

            // The temperature should be within the valid range after clamping
            _mockControlService.Received(1).SetTempMired(TestEntityId,
                Arg.Is<int>(t => t >= DefaultMinMireds && t <= DefaultMaxMireds));
        }

        #endregion

        #region Step Size Validation

        [Theory]
        [InlineData(1, 2)]     // 1 * 2 mireds per tick = 2 mireds
        [InlineData(5, 10)]    // 5 * 2 mireds per tick = 10 mireds
        [InlineData(30, 60)]   // At the cap limit: 30 * 2 = 60 mireds
        [InlineData(35, 60)]   // Should be capped to 60 mireds
        [InlineData(-25, -50)] // Negative within limit: -25 * 2 = -50 mireds
        [InlineData(-40, -60)] // Negative should be capped to -60 mireds
        public void Execute_StepSizeCalculation_HandlesCorrectly(int inputDiff, int expectedStepSize)
        {
            // Arrange
            var startMireds = 300; // Safe middle value for testing
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((DefaultMinMireds, DefaultMaxMireds, startMireds));

            // Act
            _command.Execute(TestEntityId, inputDiff);

            // Assert
            var expectedMireds = HSBHelper.Clamp(startMireds + expectedStepSize, DefaultMinMireds, DefaultMaxMireds);
            _mockStateManager.Received(1).SetCachedTempMired(TestEntityId, null, null, expectedMireds);
        }

        #endregion

        #region Zero Diff Handling

        [Fact]
        public void Execute_ZeroDiff_NoTemperatureChange()
        {
            // Arrange
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((DefaultMinMireds, DefaultMaxMireds, DefaultCurrentMireds));

            // Act
            _command.Execute(TestEntityId, 0);

            // Assert - Temperature should remain the same
            _mockStateManager.Received(1).SetCachedTempMired(TestEntityId, null, null, DefaultCurrentMireds);
            _mockControlService.Received(1).SetTempMired(TestEntityId, DefaultCurrentMireds);
        }

        #endregion

        #region Boundary Edge Cases

        [Theory]
        [InlineData(153, -1, 153)] // Already at minimum, negative adjustment
        [InlineData(500, 1, 500)]  // Already at maximum, positive adjustment
        [InlineData(155, -2, 153)] // Near minimum, goes below
        [InlineData(498, 2, 500)]  // Near maximum, goes above
        public void Execute_BoundaryEdgeCases_HandlesCorrectly(int startMireds, int diff, int expectedMireds)
        {
            // Arrange
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((DefaultMinMireds, DefaultMaxMireds, startMireds));

            // Act
            _command.Execute(TestEntityId, diff);

            // Assert - Should handle boundary conditions correctly
            _mockStateManager.Received(1).SetCachedTempMired(TestEntityId, null, null, expectedMireds);
            _mockControlService.Received(1).SetTempMired(TestEntityId, expectedMireds);
        }

        #endregion

        #region Wide Range Testing

        [Theory]
        [InlineData(100, 600, 350, 25, 400)] // Wide range light: 350 + 50 = 400
        [InlineData(300, 400, 350, -30, 300)] // Narrow range light: 350 - 60 = 290, clamped to 300
        public void Execute_VariousLightRanges_HandlesCorrectly(int minMireds, int maxMireds, int startMireds, int diff, int expectedMireds)
        {
            // Arrange
            _mockStateManager.GetColorTempMired(TestEntityId)
                           .Returns((minMireds, maxMireds, startMireds));

            // Act
            _command.Execute(TestEntityId, diff);

            // Assert - Should respect the specific light's range
            _mockStateManager.Received(1).SetCachedTempMired(TestEntityId, null, null, expectedMireds);
            _mockControlService.Received(1).SetTempMired(TestEntityId, expectedMireds);
        }

        #endregion
    }
}