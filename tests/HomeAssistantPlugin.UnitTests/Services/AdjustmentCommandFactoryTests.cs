using System;
using System.Collections.Generic;
using NSubstitute;
using Xunit;
using Loupedeck.HomeAssistantPlugin.Services;
using Loupedeck.HomeAssistantPlugin.Services.Commands;
using Loupedeck.HomeAssistantPlugin.Models;
using Loupedeck.HomeAssistantPlugin;

namespace Loupedeck.HomeAssistantPlugin.Tests.Services
{
    /// <summary>
    /// Unit tests for AdjustmentCommandFactory with 100% coverage target
    /// Tests factory pattern implementation, command creation, dependency injection, and error handling
    /// </summary>
    public class AdjustmentCommandFactoryTests
    {
        private const string TestEntityId = "light.test_entity";

        private readonly ILightStateManager _mockStateManager;
        private readonly ILightControlService _mockControlService;
        private readonly Dictionary<string, LookMode> _lookModeByEntity;
        private readonly AdjustmentCommandContext _context;
        private readonly AdjustmentCommandFactory _factory;

        private bool _markCommandSentCalled;
        private bool _adjustmentValueChangedCalled;

        public AdjustmentCommandFactoryTests()
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

            _factory = new AdjustmentCommandFactory(_context);
        }

        private void MarkCommandSent(string entityId)
        {
            _markCommandSentCalled = true;
        }

        private void TriggerAdjustmentValueChanged(string parameter)
        {
            _adjustmentValueChangedCalled = true;
        }

        private LightCaps GetTestCapabilities(string entityId)
        {
            return new LightCaps(true, true, true, true); // Full capabilities
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_ValidContext_CreatesInstance()
        {
            // Act & Assert
            Assert.NotNull(_factory);
        }

        [Fact]
        public void Constructor_NullContext_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() => new AdjustmentCommandFactory(null!));
            Assert.Equal("context", exception.ParamName);
        }

        #endregion

        #region Factory Method Tests - Return Type Validation

        [Fact]
        public void CreateBrightnessCommand_ValidCall_ReturnsBrightnessAdjustmentCommand()
        {
            // Act
            var command = _factory.CreateBrightnessCommand();

            // Assert
            Assert.NotNull(command);
            Assert.IsType<BrightnessAdjustmentCommand>(command);
        }

        [Fact]
        public void CreateHueCommand_ValidCall_ReturnsHueAdjustmentCommand()
        {
            // Act
            var command = _factory.CreateHueCommand();

            // Assert
            Assert.NotNull(command);
            Assert.IsType<HueAdjustmentCommand>(command);
        }

        [Fact]
        public void CreateSaturationCommand_ValidCall_ReturnsSaturationAdjustmentCommand()
        {
            // Act
            var command = _factory.CreateSaturationCommand();

            // Assert
            Assert.NotNull(command);
            Assert.IsType<SaturationAdjustmentCommand>(command);
        }

        [Fact]
        public void CreateTemperatureCommand_ValidCall_ReturnsTemperatureAdjustmentCommand()
        {
            // Act
            var command = _factory.CreateTemperatureCommand();

            // Assert
            Assert.NotNull(command);
            Assert.IsType<TemperatureAdjustmentCommand>(command);
        }

        #endregion

        #region Command Interface Compliance

        [Theory]
        [InlineData("brightness")]
        [InlineData("hue")]
        [InlineData("saturation")]
        [InlineData("temperature")]
        public void CreateCommand_AllTypes_ImplementIAdjustmentCommand(string commandType)
        {
            // Act
            IAdjustmentCommand command = commandType switch
            {
                "brightness" => _factory.CreateBrightnessCommand(),
                "hue" => _factory.CreateHueCommand(),
                "saturation" => _factory.CreateSaturationCommand(),
                "temperature" => _factory.CreateTemperatureCommand(),
                _ => throw new ArgumentException("Invalid command type")
            };

            // Assert
            Assert.NotNull(command);
            Assert.IsAssignableFrom<IAdjustmentCommand>(command);
        }

        #endregion

        #region Dependency Injection Validation

        [Fact]
        public void CreateBrightnessCommand_ValidContext_InjectsContextProperly()
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((120.0, 50.0, 128));
            _mockStateManager.IsLightOn(TestEntityId).Returns(true);

            // Act
            var command = _factory.CreateBrightnessCommand();
            command.Execute(TestEntityId, 5); // Test that injected dependencies work

            // Assert - Verify the command can use the injected services
            _mockStateManager.Received(1).GetHsbValues(TestEntityId);
            _mockStateManager.Received(1).IsLightOn(TestEntityId);
            _mockControlService.Received(1).SetBrightness(TestEntityId, Arg.Any<int>());
        }

        [Fact]
        public void CreateHueCommand_ValidContext_InjectsContextProperly()
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((180.0, 75.0, 128));

            // Act
            var command = _factory.CreateHueCommand();
            command.Execute(TestEntityId, 10); // Test that injected dependencies work

            // Assert - Verify the command can use the injected services
            _mockStateManager.Received(1).GetHsbValues(TestEntityId);
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, Arg.Any<double>(), Arg.Any<double>());
            _mockControlService.Received(1).SetHueSat(TestEntityId, Arg.Any<double>(), Arg.Any<double>());
        }

        [Fact]
        public void CreateSaturationCommand_ValidContext_InjectsContextProperly()
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((240.0, 60.0, 128));

            // Act
            var command = _factory.CreateSaturationCommand();
            command.Execute(TestEntityId, 8); // Test that injected dependencies work

            // Assert - Verify the command can use the injected services
            _mockStateManager.Received(1).GetHsbValues(TestEntityId);
            _mockStateManager.Received(1).UpdateHsColor(TestEntityId, Arg.Any<double>(), Arg.Any<double>());
            _mockControlService.Received(1).SetHueSat(TestEntityId, Arg.Any<double>(), Arg.Any<double>());
        }

        [Fact]
        public void CreateTemperatureCommand_ValidContext_InjectsContextProperly()
        {
            // Arrange
            _mockStateManager.GetColorTempMired(TestEntityId).Returns((153, 500, 300));

            // Act
            var command = _factory.CreateTemperatureCommand();
            command.Execute(TestEntityId, 6); // Test that injected dependencies work

            // Assert - Verify the command can use the injected services
            _mockStateManager.Received(1).GetColorTempMired(TestEntityId);
            _mockStateManager.Received(1).SetCachedTempMired(TestEntityId, Arg.Any<int?>(), Arg.Any<int?>(), Arg.Any<int>());
            _mockControlService.Received(1).SetTempMired(TestEntityId, Arg.Any<int>());
        }

        #endregion

        #region Context Sharing and State Management

        [Fact]
        public void CreateMultipleCommands_SameContext_ShareLookModeState()
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((180.0, 75.0, 128));
            _mockStateManager.GetColorTempMired(TestEntityId).Returns((153, 500, 300));

            // Act - Create commands and execute to set look modes
            var hueCommand = _factory.CreateHueCommand();
            var tempCommand = _factory.CreateTemperatureCommand();

            hueCommand.Execute(TestEntityId, 5); // Should set to LookMode.Hs
            
            // Assert - Verify look mode was set
            Assert.True(_lookModeByEntity.ContainsKey(TestEntityId));
            Assert.Equal(LookMode.Hs, _lookModeByEntity[TestEntityId]);

            // Act - Execute temperature command
            tempCommand.Execute(TestEntityId, 3); // Should change to LookMode.Temp

            // Assert - Verify look mode was updated by the other command
            Assert.Equal(LookMode.Temp, _lookModeByEntity[TestEntityId]);
        }

        [Fact]
        public void CreateMultipleCommands_SameContext_ShareCallbackFunctions()
        {
            // Arrange
            _mockStateManager.GetHsbValues(TestEntityId).Returns((120.0, 50.0, 128));
            _mockStateManager.IsLightOn(TestEntityId).Returns(true);

            // Act - Create and execute brightness command
            var brightnessCommand = _factory.CreateBrightnessCommand();
            brightnessCommand.Execute(TestEntityId, 5);

            // Assert - Verify shared callback was invoked
            Assert.True(_markCommandSentCalled);
            Assert.True(_adjustmentValueChangedCalled);
        }

        #endregion

        #region Memory and Resource Management

        [Fact]
        public void CreateMultipleCommandsOfSameType_CreatesNewInstances()
        {
            // Act
            var command1 = _factory.CreateBrightnessCommand();
            var command2 = _factory.CreateBrightnessCommand();

            // Assert - Should create separate instances, not reuse
            Assert.NotNull(command1);
            Assert.NotNull(command2);
            Assert.NotSame(command1, command2); // Different instances
            Assert.Equal(command1.GetType(), command2.GetType()); // Same type
        }

        [Fact]
        public void CreateCommandsSequentially_AllTypesWork()
        {
            // Act - Create all command types in sequence
            var brightnessCommand = _factory.CreateBrightnessCommand();
            var hueCommand = _factory.CreateHueCommand();
            var saturationCommand = _factory.CreateSaturationCommand();
            var temperatureCommand = _factory.CreateTemperatureCommand();

            // Assert - All should be created successfully
            Assert.NotNull(brightnessCommand);
            Assert.NotNull(hueCommand);
            Assert.NotNull(saturationCommand);
            Assert.NotNull(temperatureCommand);

            // All should be different types
            Assert.IsType<BrightnessAdjustmentCommand>(brightnessCommand);
            Assert.IsType<HueAdjustmentCommand>(hueCommand);
            Assert.IsType<SaturationAdjustmentCommand>(saturationCommand);
            Assert.IsType<TemperatureAdjustmentCommand>(temperatureCommand);
        }

        #endregion

        #region Context Validation Through Commands

        [Fact]
        public void CreateBrightnessCommand_ContextWithNullStateManager_CommandHandlesGracefully()
        {
            // Arrange - Context with null state manager
            var contextWithNullManager = new AdjustmentCommandContext(
                null!,
                _mockControlService,
                _lookModeByEntity,
                MarkCommandSent,
                TriggerAdjustmentValueChanged,
                GetTestCapabilities);
            var factoryWithNullManager = new AdjustmentCommandFactory(contextWithNullManager);

            // Act
            var command = factoryWithNullManager.CreateBrightnessCommand();

            // Assert - Command should be created and handle null gracefully
            Assert.NotNull(command);
            
            // Should not throw when executed with null state manager
            command.Execute(TestEntityId, 5);
            _mockControlService.Received(1).SetBrightness(TestEntityId, Arg.Any<int>());
        }

        [Fact]
        public void CreateHueCommand_ContextWithNullControlService_CommandHandlesGracefully()
        {
            // Arrange - Context with null control service
            var contextWithNullService = new AdjustmentCommandContext(
                _mockStateManager,
                null!,
                _lookModeByEntity,
                MarkCommandSent,
                TriggerAdjustmentValueChanged,
                GetTestCapabilities);
            var factoryWithNullService = new AdjustmentCommandFactory(contextWithNullService);

            _mockStateManager.GetHsbValues(TestEntityId).Returns((180.0, 75.0, 128));

            // Act
            var command = factoryWithNullService.CreateHueCommand();

            // Assert - Command should be created and handle null service gracefully
            Assert.NotNull(command);
            
            // Should not throw when executed with null control service
            command.Execute(TestEntityId, 10);
            _mockStateManager.Received(1).GetHsbValues(TestEntityId);
        }

        #endregion

        #region Error Conditions and Edge Cases

        [Fact]
        public void CreateCommand_FactoryWithMinimalContext_WorksCorrectly()
        {
            // Arrange - Create context with minimal viable setup
            var minimalLookModes = new Dictionary<string, LookMode>();
            var minimalContext = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                minimalLookModes,
                _ => { }, // Empty callback
                _ => { }, // Empty callback
                _ => new LightCaps(false, false, false, false)); // No capabilities

            var minimalFactory = new AdjustmentCommandFactory(minimalContext);

            // Act & Assert - All factory methods should work
            Assert.NotNull(minimalFactory.CreateBrightnessCommand());
            Assert.NotNull(minimalFactory.CreateHueCommand());
            Assert.NotNull(minimalFactory.CreateSaturationCommand());
            Assert.NotNull(minimalFactory.CreateTemperatureCommand());
        }

        [Fact]
        public void CreateCommands_WithDifferentCapabilityConfigurations_AllWork()
        {
            // Arrange - Context that returns different capabilities based on entity
            var capabilityContext = new AdjustmentCommandContext(
                _mockStateManager,
                _mockControlService,
                _lookModeByEntity,
                MarkCommandSent,
                TriggerAdjustmentValueChanged,
                entityId => entityId switch
                {
                    "light.brightness_only" => new LightCaps(true, true, false, false),
                    "light.color_only" => new LightCaps(true, false, false, true),
                    "light.temp_only" => new LightCaps(true, false, true, false),
                    _ => new LightCaps(false, false, false, false)
                });

            var capabilityFactory = new AdjustmentCommandFactory(capabilityContext);

            // Act - Create all command types
            var brightnessCommand = capabilityFactory.CreateBrightnessCommand();
            var hueCommand = capabilityFactory.CreateHueCommand();
            var saturationCommand = capabilityFactory.CreateSaturationCommand();
            var temperatureCommand = capabilityFactory.CreateTemperatureCommand();

            // Assert - All commands should be created regardless of capability variations
            Assert.NotNull(brightnessCommand);
            Assert.NotNull(hueCommand);
            Assert.NotNull(saturationCommand);
            Assert.NotNull(temperatureCommand);
        }

        #endregion

        #region Factory Pattern Compliance

        [Fact]
        public void Factory_ImplementsIAdjustmentCommandFactory()
        {
            // Assert
            Assert.IsAssignableFrom<IAdjustmentCommandFactory>(_factory);
        }

        [Fact]
        public void Factory_AllMethodsReturnNonNullCommands()
        {
            // Act & Assert
            Assert.NotNull(_factory.CreateBrightnessCommand());
            Assert.NotNull(_factory.CreateHueCommand());
            Assert.NotNull(_factory.CreateSaturationCommand());
            Assert.NotNull(_factory.CreateTemperatureCommand());
        }

        [Fact]
        public void Factory_CommandsHaveCorrectInterface()
        {
            // Act
            var commands = new[]
            {
                _factory.CreateBrightnessCommand(),
                _factory.CreateHueCommand(),
                _factory.CreateSaturationCommand(),
                _factory.CreateTemperatureCommand()
            };

            // Assert - All commands should implement the expected interface
            foreach (var command in commands)
            {
                Assert.IsAssignableFrom<IAdjustmentCommand>(command);
            }
        }

        #endregion

        #region Threading and Concurrency

        [Fact]
        public void Factory_ConcurrentCommandCreation_ThreadSafe()
        {
            // Arrange
            var commands = new List<IAdjustmentCommand>();
            var tasks = new List<System.Threading.Tasks.Task>();

            // Act - Create commands concurrently
            for (int i = 0; i < 10; i++)
            {
                var task = System.Threading.Tasks.Task.Run(() =>
                {
                    var command = _factory.CreateBrightnessCommand();
                    lock (commands)
                    {
                        commands.Add(command);
                    }
                });
                tasks.Add(task);
            }

            System.Threading.Tasks.Task.WaitAll(tasks.ToArray());

            // Assert - All commands should be created successfully
            Assert.Equal(10, commands.Count);
            Assert.All(commands, cmd => Assert.NotNull(cmd));
            Assert.All(commands, cmd => Assert.IsType<BrightnessAdjustmentCommand>(cmd));
        }

        #endregion

        #region Integration Testing

        [Fact]
        public void Factory_CreatedCommandsWorkWithRealContext_IntegrationTest()
        {
            // Arrange - Setup realistic state for integration test
            _mockStateManager.GetHsbValues(TestEntityId).Returns((200.0, 80.0, 150));
            _mockStateManager.IsLightOn(TestEntityId).Returns(true);
            _mockStateManager.GetColorTempMired(TestEntityId).Returns((200, 400, 300));

            // Act - Create and execute all command types
            var brightnessCommand = _factory.CreateBrightnessCommand();
            var hueCommand = _factory.CreateHueCommand();
            var saturationCommand = _factory.CreateSaturationCommand();
            var temperatureCommand = _factory.CreateTemperatureCommand();

            // Execute all commands
            brightnessCommand.Execute(TestEntityId, 5);
            hueCommand.Execute(TestEntityId, 10);
            saturationCommand.Execute(TestEntityId, -3);
            temperatureCommand.Execute(TestEntityId, 7);

            // Assert - All commands should have executed successfully
            _mockControlService.Received(1).SetBrightness(TestEntityId, Arg.Any<int>());
            _mockControlService.Received(1).SetHueSat(TestEntityId, Arg.Any<double>(), Arg.Any<double>());
            _mockControlService.Received(1).SetTempMired(TestEntityId, Arg.Any<int>());

            // Verify state changes
            Assert.Contains(TestEntityId, _lookModeByEntity.Keys);
            Assert.Equal(LookMode.Temp, _lookModeByEntity[TestEntityId]); // Last command set this
        }

        #endregion
    }
}