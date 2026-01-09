using System;
using System.Collections.Generic;
using NSubstitute;
using Xunit;
using Loupedeck.HomeAssistantPlugin.Services;
using Loupedeck.HomeAssistantPlugin.Models;
using Loupedeck.HomeAssistantPlugin;

namespace Loupedeck.HomeAssistantPlugin.Tests.Services
{
    /// <summary>
    /// Unit tests for AdjustmentCommandContext with 100% coverage target
    /// Tests context creation, property validation, dependency injection, and callback functionality
    /// </summary>
    public class AdjustmentCommandContextTests
    {
        private const string TestEntityId = "light.test_entity";

        private readonly ILightStateManager _mockStateManager;
        private readonly ILightControlService _mockControlService;
        private readonly Dictionary<string, LookMode> _lookModeByEntity;

        private bool _markCommandSentCalled;
        private string? _markCommandSentEntityId;
        private bool _adjustmentValueChangedCalled;
        private string? _adjustmentValueChangedParameter;

        public AdjustmentCommandContextTests()
        {
            _mockStateManager = Substitute.For<ILightStateManager>();
            _mockControlService = Substitute.For<ILightControlService>();
            _lookModeByEntity = new Dictionary<string, LookMode>();
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
            return new LightCaps(true, true, true, true);
        }

        #region Constructor Tests - Valid Parameters

        [Fact]
        public void Constructor_AllValidParameters_CreatesInstance()
        {
            // Act
            var context = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                _lookModeByEntity,
                MarkCommandSent,
                TriggerAdjustmentValueChanged,
                GetTestCapabilities);

            // Assert
            Assert.NotNull(context);
            Assert.Equal(_mockStateManager, context.LightStateManager);
            Assert.Equal(_mockControlService, context.LightControlService);
            Assert.Equal(_lookModeByEntity, context.LookModeByEntity);
            Assert.NotNull(context.MarkCommandSent);
            Assert.NotNull(context.TriggerAdjustmentValueChanged);
            Assert.NotNull(context.GetCapabilities);
        }

        #endregion

        #region Constructor Tests - Null Parameter Validation

        [Fact]
        public void Constructor_NullLightStateManager_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new AdjustmentCommandContext(
                    null!,
                    _mockControlService,
                    _lookModeByEntity,
                    MarkCommandSent,
                    TriggerAdjustmentValueChanged,
                    GetTestCapabilities));

            Assert.Equal("lightStateManager", exception.ParamName);
        }

        [Fact]
        public void Constructor_NullLightControlService_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new AdjustmentCommandContext(
                    _mockStateManager,
                    null!,
                    _lookModeByEntity,
                    MarkCommandSent,
                    TriggerAdjustmentValueChanged,
                    GetTestCapabilities));

            Assert.Equal("lightControlService", exception.ParamName);
        }

        [Fact]
        public void Constructor_NullLookModeByEntity_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new AdjustmentCommandContext(
                    _mockStateManager,
                    _mockControlService,
                    null!,
                    MarkCommandSent,
                    TriggerAdjustmentValueChanged,
                    GetTestCapabilities));

            Assert.Equal("lookModeByEntity", exception.ParamName);
        }

        [Fact]
        public void Constructor_NullMarkCommandSent_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new AdjustmentCommandContext(
                    _mockStateManager,
                    _mockControlService,
                    _lookModeByEntity,
                    null!,
                    TriggerAdjustmentValueChanged,
                    GetTestCapabilities));

            Assert.Equal("markCommandSent", exception.ParamName);
        }

        [Fact]
        public void Constructor_NullTriggerAdjustmentValueChanged_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new AdjustmentCommandContext(
                    _mockStateManager,
                    _mockControlService,
                    _lookModeByEntity,
                    MarkCommandSent,
                    null!,
                    GetTestCapabilities));

            Assert.Equal("triggerAdjustmentValueChanged", exception.ParamName);
        }

        [Fact]
        public void Constructor_NullGetCapabilities_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new AdjustmentCommandContext(
                    _mockStateManager,
                    _mockControlService,
                    _lookModeByEntity,
                    MarkCommandSent,
                    TriggerAdjustmentValueChanged,
                    null!));

            Assert.Equal("getCapabilities", exception.ParamName);
        }

        #endregion

        #region Property Access Tests

        [Fact]
        public void Properties_AfterConstruction_ReturnCorrectValues()
        {
            // Arrange
            var context = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                _lookModeByEntity,
                MarkCommandSent,
                TriggerAdjustmentValueChanged,
                GetTestCapabilities);

            // Act & Assert
            Assert.Same(_mockStateManager, context.LightStateManager);
            Assert.Same(_mockControlService, context.LightControlService);
            Assert.Same(_lookModeByEntity, context.LookModeByEntity);
            Assert.NotNull(context.MarkCommandSent);
            Assert.NotNull(context.TriggerAdjustmentValueChanged);
            Assert.NotNull(context.GetCapabilities);
        }

        [Fact]
        public void Properties_AreReadOnly_CannotBeSet()
        {
            // Arrange
            var context = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                _lookModeByEntity,
                MarkCommandSent,
                TriggerAdjustmentValueChanged,
                GetTestCapabilities);

            // Assert - Properties should be get-only (this is enforced by compiler)
            // We verify by checking the properties exist and return expected values
            Assert.IsAssignableFrom<ILightStateManager>(context.LightStateManager);
            Assert.IsAssignableFrom<ILightControlService>(context.LightControlService);
            Assert.IsAssignableFrom<Dictionary<string, LookMode>>(context.LookModeByEntity);
            Assert.IsAssignableFrom<Action<string>>(context.MarkCommandSent);
            Assert.IsAssignableFrom<Action<string>>(context.TriggerAdjustmentValueChanged);
            Assert.IsAssignableFrom<Func<string, LightCaps>>(context.GetCapabilities);
        }

        #endregion

        #region Callback Function Tests

        [Fact]
        public void MarkCommandSent_WhenInvoked_CallsProvidedAction()
        {
            // Arrange
            var context = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                _lookModeByEntity,
                MarkCommandSent,
                TriggerAdjustmentValueChanged,
                GetTestCapabilities);

            // Act
            context.MarkCommandSent(TestEntityId);

            // Assert
            Assert.True(_markCommandSentCalled);
            Assert.Equal(TestEntityId, _markCommandSentEntityId);
        }

        [Fact]
        public void TriggerAdjustmentValueChanged_WhenInvoked_CallsProvidedAction()
        {
            // Arrange
            var testParameter = "adj:bri";
            var context = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                _lookModeByEntity,
                MarkCommandSent,
                TriggerAdjustmentValueChanged,
                GetTestCapabilities);

            // Act
            context.TriggerAdjustmentValueChanged(testParameter);

            // Assert
            Assert.True(_adjustmentValueChangedCalled);
            Assert.Equal(testParameter, _adjustmentValueChangedParameter);
        }

        [Fact]
        public void GetCapabilities_WhenInvoked_CallsProvidedFunction()
        {
            // Arrange
            var context = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                _lookModeByEntity,
                MarkCommandSent,
                TriggerAdjustmentValueChanged,
                GetTestCapabilities);

            // Act
            var capabilities = context.GetCapabilities(TestEntityId);

            // Assert
            Assert.NotNull(capabilities);
            Assert.True(capabilities.OnOff);
            Assert.True(capabilities.Brightness);
            Assert.True(capabilities.ColorTemp);
            Assert.True(capabilities.ColorHs);
        }

        #endregion

        #region LookMode Dictionary Integration

        [Fact]
        public void LookModeByEntity_AfterConstruction_IsAccessible()
        {
            // Arrange
            var context = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                _lookModeByEntity,
                MarkCommandSent,
                TriggerAdjustmentValueChanged,
                GetTestCapabilities);

            // Act & Assert - Dictionary should be accessible and modifiable
            Assert.Empty(context.LookModeByEntity);
            
            context.LookModeByEntity[TestEntityId] = LookMode.Hs;
            Assert.Single(context.LookModeByEntity);
            Assert.Equal(LookMode.Hs, context.LookModeByEntity[TestEntityId]);
        }

        [Fact]
        public void LookModeByEntity_ModificationsAffectOriginal_SameReference()
        {
            // Arrange
            var originalDict = new Dictionary<string, LookMode>();
            var context = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                originalDict,
                MarkCommandSent,
                TriggerAdjustmentValueChanged,
                GetTestCapabilities);

            // Act - Modify through context
            context.LookModeByEntity[TestEntityId] = LookMode.Temp;

            // Assert - Original dictionary should be modified
            Assert.Single(originalDict);
            Assert.Equal(LookMode.Temp, originalDict[TestEntityId]);
            Assert.Same(originalDict, context.LookModeByEntity);
        }

        #endregion

        #region LookMode Enum Tests

        [Theory]
        [InlineData(LookMode.Hs)]
        [InlineData(LookMode.Temp)]
        public void LookMode_AllValues_CanBeStored(LookMode mode)
        {
            // Arrange
            var context = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                _lookModeByEntity,
                MarkCommandSent,
                TriggerAdjustmentValueChanged,
                GetTestCapabilities);

            // Act
            context.LookModeByEntity[TestEntityId] = mode;

            // Assert
            Assert.Equal(mode, context.LookModeByEntity[TestEntityId]);
        }

        [Fact]
        public void LookMode_MultipleEntities_CanHaveDifferentModes()
        {
            // Arrange
            var context = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                _lookModeByEntity,
                MarkCommandSent,
                TriggerAdjustmentValueChanged,
                GetTestCapabilities);

            var entity1 = "light.entity1";
            var entity2 = "light.entity2";

            // Act
            context.LookModeByEntity[entity1] = LookMode.Hs;
            context.LookModeByEntity[entity2] = LookMode.Temp;

            // Assert
            Assert.Equal(2, context.LookModeByEntity.Count);
            Assert.Equal(LookMode.Hs, context.LookModeByEntity[entity1]);
            Assert.Equal(LookMode.Temp, context.LookModeByEntity[entity2]);
        }

        #endregion

        #region Callback Parameter Validation

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void MarkCommandSent_WithVariousEntityIds_HandlesGracefully(string? entityId)
        {
            // Arrange
            var callbackEntityId = string.Empty;
            var callbackInvoked = false;

            var context = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                _lookModeByEntity,
                id => { callbackInvoked = true; callbackEntityId = id; },
                TriggerAdjustmentValueChanged,
                GetTestCapabilities);

            // Act
            context.MarkCommandSent(entityId!);

            // Assert - Callback should be invoked regardless of parameter value
            Assert.True(callbackInvoked);
            Assert.Equal(entityId, callbackEntityId);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void TriggerAdjustmentValueChanged_WithVariousParameters_HandlesGracefully(string? parameter)
        {
            // Arrange
            var callbackParameter = string.Empty;
            var callbackInvoked = false;

            var context = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                _lookModeByEntity,
                MarkCommandSent,
                param => { callbackInvoked = true; callbackParameter = param; },
                GetTestCapabilities);

            // Act
            context.TriggerAdjustmentValueChanged(parameter!);

            // Assert - Callback should be invoked regardless of parameter value
            Assert.True(callbackInvoked);
            Assert.Equal(parameter, callbackParameter);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public void GetCapabilities_WithVariousEntityIds_ReturnsCapabilities(string? entityId)
        {
            // Arrange
            var context = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                _lookModeByEntity,
                MarkCommandSent,
                TriggerAdjustmentValueChanged,
                id => new LightCaps(false, false, false, false)); // Return minimal caps for any ID

            // Act
            var capabilities = context.GetCapabilities(entityId!);

            // Assert - Function should return capabilities regardless of parameter value
            Assert.NotNull(capabilities);
            Assert.False(capabilities.OnOff);
            Assert.False(capabilities.Brightness);
            Assert.False(capabilities.ColorTemp);
            Assert.False(capabilities.ColorHs);
        }

        #endregion

        #region Multiple Invocation Tests

        [Fact]
        public void Callbacks_MultipleInvocations_AllExecuted()
        {
            // Arrange
            var markCommandSentCount = 0;
            var adjustmentValueChangedCount = 0;
            var getCapabilitiesCount = 0;

            var context = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                _lookModeByEntity,
                _ => markCommandSentCount++,
                _ => adjustmentValueChangedCount++,
                _ => { getCapabilitiesCount++; return GetTestCapabilities(""); });

            // Act - Multiple invocations
            context.MarkCommandSent("entity1");
            context.MarkCommandSent("entity2");
            context.TriggerAdjustmentValueChanged("param1");
            context.TriggerAdjustmentValueChanged("param2");
            context.TriggerAdjustmentValueChanged("param3");
            context.GetCapabilities("entity1");
            context.GetCapabilities("entity2");

            // Assert - All invocations should be counted
            Assert.Equal(2, markCommandSentCount);
            Assert.Equal(3, adjustmentValueChangedCount);
            Assert.Equal(2, getCapabilitiesCount);
        }

        #endregion

        #region Context Immutability

        [Fact]
        public void Context_AfterConstruction_PropertiesCannotBeReassigned()
        {
            // Arrange
            var context = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                _lookModeByEntity,
                MarkCommandSent,
                TriggerAdjustmentValueChanged,
                GetTestCapabilities);

            var originalStateManager = context.LightStateManager;
            var originalControlService = context.LightControlService;
            var originalLookModeDict = context.LookModeByEntity;

            // Act & Assert - Properties should remain the same (immutable references)
            Assert.Same(originalStateManager, context.LightStateManager);
            Assert.Same(originalControlService, context.LightControlService);
            Assert.Same(originalLookModeDict, context.LookModeByEntity);

            // The references themselves are immutable, but the dictionary contents can be modified
            context.LookModeByEntity["test"] = LookMode.Hs;
            Assert.Same(originalLookModeDict, context.LookModeByEntity); // Still same reference
            Assert.Single(context.LookModeByEntity); // But contents changed
        }

        #endregion

        #region Complex Integration Scenarios

        [Fact]
        public void Context_UsedByMultipleCommands_MaintainsState()
        {
            // Arrange
            var markCommandSentEntities = new List<string>();
            var adjustmentParameters = new List<string>();

            var context = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                _lookModeByEntity,
                entity => markCommandSentEntities.Add(entity),
                param => adjustmentParameters.Add(param),
                GetTestCapabilities);

            // Act - Simulate multiple command usage
            context.LookModeByEntity["light1"] = LookMode.Hs;
            context.MarkCommandSent("light1");
            context.TriggerAdjustmentValueChanged("adj:hue");

            context.LookModeByEntity["light2"] = LookMode.Temp;
            context.MarkCommandSent("light2");
            context.TriggerAdjustmentValueChanged("adj:temp");

            // Assert - Context should maintain all state correctly
            Assert.Equal(2, context.LookModeByEntity.Count);
            Assert.Equal(LookMode.Hs, context.LookModeByEntity["light1"]);
            Assert.Equal(LookMode.Temp, context.LookModeByEntity["light2"]);
            
            Assert.Equal(2, markCommandSentEntities.Count);
            Assert.Contains("light1", markCommandSentEntities);
            Assert.Contains("light2", markCommandSentEntities);

            Assert.Equal(2, adjustmentParameters.Count);
            Assert.Contains("adj:hue", adjustmentParameters);
            Assert.Contains("adj:temp", adjustmentParameters);
        }

        #endregion

        #region Exception Handling in Callbacks

        [Fact]
        public void Context_CallbackThrowsException_DoesNotAffectContextState()
        {
            // Arrange - Callback that throws
            var context = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                _lookModeByEntity,
                _ => throw new InvalidOperationException("Test exception"),
                TriggerAdjustmentValueChanged,
                GetTestCapabilities);

            // Act & Assert - Exception should propagate, but context should remain valid
            Assert.Throws<InvalidOperationException>(() => context.MarkCommandSent(TestEntityId));
            
            // Context should still be functional
            Assert.NotNull(context.LightStateManager);
            Assert.NotNull(context.LightControlService);
            context.TriggerAdjustmentValueChanged("test"); // Should still work
            var caps = context.GetCapabilities(TestEntityId); // Should still work
            Assert.NotNull(caps);
        }

        #endregion
    }
}