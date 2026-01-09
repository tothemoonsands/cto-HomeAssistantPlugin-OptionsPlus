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
    /// Unit tests for SaturationAdjustmentCommand with 100% coverage target
    /// Tests saturation boundary clamping, capability checking, service interactions, and error handling
    /// </summary>
    public class SaturationAdjustmentCommandTests
    {
        private const string TestEntityId = "light.test_entity";
        private const double DefaultHue = 120.0;
        private const double DefaultSaturation = 100.0;
        private const int DefaultBrightness = 128;

        private readonly ILightStateManager _mockStateManager;
        private readonly ILightControlService _mockControlService;
        private readonly Dictionary<string, LookMode> _lookModeByEntity;
        private readonly AdjustmentCommandContext _context;
        private readonly SaturationAdjustmentCommand _command;

        private bool _markCommandSentCalled;
        private string? _markCommandSentEntityId;
        private readonly List<string> _adjustmentValueChangedParameters = new();

        public SaturationAdjustmentCommandTests()
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

            _command = new SaturationAdjustmentCommand(_context);
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
            Assert.Throws<ArgumentNullException>(() => new SaturationAdjustmentCommand(null!));
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
            var commandWithoutHs = new SaturationAdjustmentCommand(contextWithoutHs);

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
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, DefaultHue, Arg.Any<double>());
            _mockControlService.Received(1).SetHueSat(TestEntityId, DefaultHue, Arg.Any<double>());
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

        #region Saturation Calculation and Clamping

        [Theory]
        [InlineData(50.0, 5, 55.0)]    // Simple positive adjustment
        [InlineData(60.0, -10, 50.0)]  // Simple negative adjustment
        [InlineData(0.0, 5, 5.0)]      // From minimum
        [InlineData(95.0, 5, 100.0)]   // To maximum
        public void Execute_SaturationCalculation_HandlesCorrectly(double startSaturation, int diff, double expectedSaturation)
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, startSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, diff);

            // Assert
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, DefaultHue, expectedSaturation);
            _mockControlService.Received(1).SetHueSat(TestEntityId, DefaultHue, expectedSaturation);
        }

        [Theory]
        [InlineData(95.0, 10, 100.0)]   // Clamp at maximum: 95 + 10 = 105 -> 100
        [InlineData(90.0, 20, 100.0)]   // Large positive clamp: 90 + 15 (capped) = 105 -> 100
        [InlineData(98.0, 5, 100.0)]    // Just over maximum: 98 + 5 = 103 -> 100
        public void Execute_PositiveSaturationOverflow_ClampsToMaximum(double startSaturation, int diff, double expectedSaturation)
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, startSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, diff);

            // Assert - Should clamp to 100%
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, DefaultHue, expectedSaturation);
            _mockControlService.Received(1).SetHueSat(TestEntityId, DefaultHue, expectedSaturation);
        }

        [Theory]
        [InlineData(5.0, -10, 0.0)]    // Clamp at minimum: 5 - 10 = -5 -> 0
        [InlineData(10.0, -20, 0.0)]   // Large negative clamp: 10 - 15 (capped) = -5 -> 0
        [InlineData(3.0, -5, 0.0)]     // Just under minimum: 3 - 5 = -2 -> 0
        public void Execute_NegativeSaturationUnderflow_ClampsToMinimum(double startSaturation, int diff, double expectedSaturation)
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, startSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, diff);

            // Assert - Should clamp to 0%
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, DefaultHue, expectedSaturation);
            _mockControlService.Received(1).SetHueSat(TestEntityId, DefaultHue, expectedSaturation);
        }

        [Fact]
        public void Execute_MaxSatPctPerEvent_CapsAt15Percent()
        {
            // Arrange
            var startSaturation = 50.0;
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, startSaturation, DefaultBrightness));

            // Act - Request 20% change, should be capped at 15%
            _command.Execute(TestEntityId, 20);

            // Assert - Should be capped at +15%: 50 + 15 = 65
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, DefaultHue, 65.0);
            _mockControlService.Received(1).SetHueSat(TestEntityId, DefaultHue, 65.0);
        }

        [Fact]
        public void Execute_NegativeMaxSatPctPerEvent_CapsAt15Percent()
        {
            // Arrange
            var startSaturation = 70.0;
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, startSaturation, DefaultBrightness));

            // Act - Request -25% change, should be capped at -15%
            _command.Execute(TestEntityId, -25);

            // Assert - Should be capped at -15%: 70 - 15 = 55
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, DefaultHue, 55.0);
            _mockControlService.Received(1).SetHueSat(TestEntityId, DefaultHue, 55.0);
        }

        #endregion

        #region Service Interactions

        [Fact]
        public void Execute_ValidCall_UpdatesStateManagerWithNewSaturation()
        {
            // Arrange
            var startSaturation = 40.0;
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, startSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, 8);

            // Assert
            var expectedSaturation = startSaturation + 8; // 48.0
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, DefaultHue, expectedSaturation);
        }

        [Fact]
        public void Execute_ValidCall_CallsControlServiceWithCorrectValues()
        {
            // Arrange
            var currentHue = 240.0;
            var startSaturation = 30.0;
            _mockStateManager.GetHsbValues(TestEntityId).Returns((currentHue, startSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, -5);

            // Assert
            var expectedSaturation = startSaturation - 5; // 25.0
            _mockControlService.Received(1).SetHueSat(TestEntityId, currentHue, expectedSaturation);
        }

        [Fact]
        public void Execute_ValidCall_GetsCurrentHueFromStateManager()
        {
            // Arrange
            var currentHue = 300.0;
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, DefaultSaturation, DefaultBrightness));
            _mockStateManager.GetHsbValues(TestEntityId).Returns((currentHue, DefaultSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, 5);

            // Assert - Should use the current hue value
            _mockControlService.Received(1).SetHueSat(TestEntityId, currentHue, Arg.Any<double>());
        }

        #endregion

        #region UI Refresh and Command Tracking

        [Fact]
        public void Execute_ValidCall_TriggersAllRelatedUIUpdates()
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, DefaultSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, 3);

            // Assert - Should trigger UI updates for saturation, hue, and temperature
            Assert.Contains("adj:ha-sat", _adjustmentValueChangedParameters);
            Assert.Contains("adj:ha-hue", _adjustmentValueChangedParameters);
            Assert.Contains("adj:ha-temp", _adjustmentValueChangedParameters);
        }

        [Fact]
        public void Execute_ValidCall_MarksCommandSent()
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, DefaultSaturation, DefaultBrightness));

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
            var commandWithNullManager = new SaturationAdjustmentCommand(contextWithNullManager);

            // Act & Assert - Should not throw, will use fallback values (0, 100, 128)
            commandWithNullManager.Execute(TestEntityId, 5);
            
            // Should call control service with adjusted saturation from fallback (100 + 5 = 100, clamped)
            _mockControlService.Received(1).SetHueSat(TestEntityId, 0.0, 100.0);
        }

        [Fact]
        public void Execute_StateManagerReturnsDefaults_HandlesGracefully()
        {
            // Arrange - StateManager returns default HSB values
            _mockStateManager.GetHsbValues(TestEntityId).Returns((0.0, 100.0, 128));

            // Act
            _command.Execute(TestEntityId, -10);

            // Assert - Should handle default values appropriately: 100 - 10 = 90
            _mockControlService.Received(1).SetHueSat(TestEntityId, 0.0, 90.0);
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

            // Should still trigger UI refresh for saturation adjustment on error
            Assert.Contains("adj:ha-sat", _adjustmentValueChangedParameters);
        }

        [Fact]
        public void Execute_UpdateHsColorThrowsException_HandlesGracefully()
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, DefaultSaturation, DefaultBrightness));
            _mockStateManager.When(x => x.UpdateHsColor(Arg.Any<string>(), Arg.Any<double>(), Arg.Any<double>()))
                            .Do(x => throw new InvalidOperationException("Update error"));

            // Act & Assert - Should not throw
            _command.Execute(TestEntityId, 4);

            // Should still trigger UI refresh on error
            Assert.Contains("adj:ha-sat", _adjustmentValueChangedParameters);
        }

        [Fact]
        public void Execute_ControlServiceThrowsException_HandlesGracefully()
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, DefaultSaturation, DefaultBrightness));
            _mockControlService.When(x => x.SetHueSat(Arg.Any<string>(), Arg.Any<double>(), Arg.Any<double>()))
                              .Do(x => throw new InvalidOperationException("Service error"));

            // Act & Assert - Should not throw
            _command.Execute(TestEntityId, 6);

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

            // Act & Assert - Should not throw and should clamp appropriately
            _command.Execute(TestEntityId, diff);

            // The saturation should be within valid range (0-100) after clamping
            _mockControlService.Received(1).SetHueSat(TestEntityId, Arg.Any<double>(),
                Arg.Is<double>(s => s >= 0.0 && s <= 100.0));
        }

        #endregion

        #region Step Size Validation

        [Theory]
        [InlineData(1, 1.0)]    // 1 * 1% per tick = 1%
        [InlineData(5, 5.0)]    // 5 * 1% per tick = 5%
        [InlineData(15, 15.0)]  // At the cap limit
        [InlineData(20, 15.0)]  // Should be capped to 15
        [InlineData(-10, -10.0)] // Negative within limit
        [InlineData(-25, -15.0)] // Negative should be capped to -15
        public void Execute_StepSizeCalculation_HandlesCorrectly(int inputDiff, double expectedStepSize)
        {
            // Arrange
            var startSaturation = 50.0; // Safe middle value for testing
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, startSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, inputDiff);

            // Assert
            var expectedSaturation = HSBHelper.Clamp(startSaturation + expectedStepSize, 0.0, 100.0);
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, DefaultHue, expectedSaturation);
        }

        #endregion

        #region Zero Diff Handling

        [Fact]
        public void Execute_ZeroDiff_NoSaturationChange()
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, DefaultSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, 0);

            // Assert - Saturation should remain the same
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, DefaultHue, DefaultSaturation);
            _mockControlService.Received(1).SetHueSat(TestEntityId, DefaultHue, DefaultSaturation);
        }

        #endregion

        #region Boundary Edge Cases

        [Theory]
        [InlineData(0.0, -1, 0.0)]     // Already at minimum, negative adjustment
        [InlineData(100.0, 1, 100.0)]  // Already at maximum, positive adjustment
        [InlineData(0.5, -1, 0.0)]     // Near minimum, goes below
        [InlineData(99.5, 1, 100.0)]   // Near maximum, goes above
        public void Execute_BoundaryEdgeCases_HandlesCorrectly(double startSaturation, int diff, double expectedSaturation)
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, startSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, diff);

            // Assert - Should handle boundary conditions correctly
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, DefaultHue, expectedSaturation);
            _mockControlService.Received(1).SetHueSat(TestEntityId, DefaultHue, expectedSaturation);
        }

        #endregion

        #region Precision Testing

        [Theory]
        [InlineData(25.7, 3, 28.7)]    // Floating point precision
        [InlineData(33.3, -2, 31.3)]   // Decimal values
        [InlineData(66.66, 5, 71.66)]  // Multiple decimals
        public void Execute_FloatingPointPrecision_HandlesCorrectly(double startSaturation, int diff, double expectedSaturation)
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((DefaultHue, startSaturation, DefaultBrightness));

            // Act
            _command.Execute(TestEntityId, diff);

            // Assert - Should maintain precision
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, DefaultHue, 
                Arg.Is<double>(s => Math.Abs(s - expectedSaturation) < 0.001));
            _mockControlService.Received(1).SetHueSat(TestEntityId, DefaultHue, 
                Arg.Is<double>(s => Math.Abs(s - expectedSaturation) < 0.001));
        }

        #endregion
    }
}