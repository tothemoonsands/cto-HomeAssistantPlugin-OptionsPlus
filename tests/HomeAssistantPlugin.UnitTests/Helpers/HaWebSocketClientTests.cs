using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Xunit;
using Loupedeck.HomeAssistantPlugin;

namespace HomeAssistantPlugin.UnitTests.Helpers
{
    public class HaWebSocketClientTests
    {
        private readonly HaWebSocketClient _client;
        private readonly MethodInfo _splitMessagesMethod;

        public HaWebSocketClientTests()
        {
            _client = new HaWebSocketClient();
            
            // Get access to the private SplitCombinedJsonMessages method using reflection
            _splitMessagesMethod = typeof(HaWebSocketClient)
                .GetMethod("SplitCombinedJsonMessages", BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("SplitCombinedJsonMessages method not found");
        }

        private List<string> SplitMessages(string combinedData)
        {
            return (List<string>)_splitMessagesMethod.Invoke(_client, new object[] { combinedData })!;
        }

        [Fact]
        public void SplitCombinedJsonMessages_SingleMessage_ReturnsOneMessage()
        {
            // Arrange
            var input = @"{""id"":1,""type"":""result"",""success"":true}";

            // Act
            var result = SplitMessages(input);

            // Assert
            Assert.Single(result);
            Assert.Equal(input, result[0]);
            
            // Validate returned JSON is well-formed
            Assert.True(IsValidJson(result[0]));
        }

        [Fact]
        public void SplitCombinedJsonMessages_TwoSimpleMessages_ReturnsTwoMessages()
        {
            // Arrange
            var message1 = @"{""id"":1,""type"":""result""}";
            var message2 = @"{""id"":2,""type"":""pong""}";
            var input = message1 + message2;

            // Act
            var result = SplitMessages(input);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(message1, result[0]);
            Assert.Equal(message2, result[1]);
            
            // Validate both messages are well-formed JSON
            Assert.All(result, msg => Assert.True(IsValidJson(msg)));
        }

        [Fact]
        public void SplitCombinedJsonMessages_NestedComplexMessages_HandlesProperly()
        {
            // Arrange
            var message1 = @"{""id"":1,""result"":{""entities"":[{""state"":""on"",""attributes"":{""friendly_name"":""Living Room Light""}}]}}";
            var message2 = @"{""id"":2,""type"":""pong""}";
            var input = message1 + message2;

            // Act
            var result = SplitMessages(input);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(message1, result[0]);
            Assert.Equal(message2, result[1]);
            
            // Validate nested structure is preserved
            using var doc = JsonDocument.Parse(result[0]);
            Assert.True(doc.RootElement.TryGetProperty("result", out var resultProp));
            Assert.True(resultProp.TryGetProperty("entities", out var entitiesProp));
            Assert.Equal(JsonValueKind.Array, entitiesProp.ValueKind);
        }

        [Fact]
        public void SplitCombinedJsonMessages_MessagesWithStringEscapes_PreservesEscapes()
        {
            // Arrange
            var message1 = @"{""message"":""Value with \""quotes\"" and {braces}""}";
            var message2 = @"{""id"":2,""path"":""C:\\Users\\test""}";
            var input = message1 + message2;

            // Act
            var result = SplitMessages(input);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(message1, result[0]);
            Assert.Equal(message2, result[1]);
            
            // Validate string escapes are preserved
            using var doc1 = JsonDocument.Parse(result[0]);
            var messageValue = doc1.RootElement.GetProperty("message").GetString();
            Assert.Contains("\"quotes\"", messageValue);
            Assert.Contains("{braces}", messageValue);

            using var doc2 = JsonDocument.Parse(result[1]);
            var pathValue = doc2.RootElement.GetProperty("path").GetString();
            Assert.Contains("\\", pathValue);
        }

        [Fact]
        public void SplitCombinedJsonMessages_WithWhitespace_HandlesCorrectly()
        {
            // Arrange
            var message1 = @"{""id"":1,""type"":""result""}";
            var message2 = @"{""id"":2,""type"":""pong""}";
            var input = "  " + message1 + "\n\t  " + message2 + "  ";

            // Act
            var result = SplitMessages(input);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(message1, result[0]);
            Assert.Equal(message2, result[1]);
        }

        [Fact]
        public void SplitCombinedJsonMessages_EmptyInput_ReturnsEmpty()
        {
            // Arrange
            var input = "";

            // Act
            var result = SplitMessages(input);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void SplitCombinedJsonMessages_WhitespaceOnly_ReturnsEmpty()
        {
            // Arrange
            var input = "   \n\t  ";

            // Act
            var result = SplitMessages(input);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public void SplitCombinedJsonMessages_MalformedJson_SkipsMalformed()
        {
            // Arrange - First message is malformed, second is valid
            var malformed = @"{""id"":1,""incomplete"":}";
            var valid = @"{""id"":2,""type"":""pong""}";
            var input = malformed + valid;

            // Act
            var result = SplitMessages(input);

            // Assert
            // Should only return the valid message, skipping the malformed one
            Assert.Single(result);
            Assert.Equal(valid, result[0]);
            Assert.True(IsValidJson(result[0]));
        }

        [Fact]
        public void SplitCombinedJsonMessages_LargeMessage_HandlesCorrectly()
        {
            // Arrange - Create a large message with many entities
            var largeEntities = string.Join(",", Enumerable.Range(1, 100).Select(i => 
                $@"{{""entity_id"":""light.test_{i}"",""state"":""on"",""attributes"":{{""friendly_name"":""Test Light {i}""}}}}"));
            var message1 = $@"{{""id"":1,""result"":[{largeEntities}]}}";
            var message2 = @"{""id"":2,""type"":""pong""}";
            var input = message1 + message2;

            // Act
            var result = SplitMessages(input);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal(message1, result[0]);
            Assert.Equal(message2, result[1]);
            
            // Validate large message structure
            using var doc = JsonDocument.Parse(result[0]);
            var resultArray = doc.RootElement.GetProperty("result");
            Assert.Equal(100, resultArray.GetArrayLength());
        }

        [Fact]
        public void SplitCombinedJsonMessages_RealHomeAssistantData_HandlesCorrectly()
        {
            // Arrange - Simulate real Home Assistant response messages
            var statesMessage = @"{""id"":2000015,""type"":""result"",""success"":true,""result"":[{""entity_id"":""light.living_room"",""state"":""on"",""attributes"":{""brightness"":255,""friendly_name"":""Living Room Light""}}]}";
            var deviceRegistryMessage = @"{""id"":2000016,""type"":""result"",""success"":true,""result"":[{""area_id"":""living_room"",""name"":""Living Room Light"",""manufacturer"":""Philips""}]}";
            var pongMessage = @"{""id"":2000017,""type"":""pong""}";
            
            var input = statesMessage + deviceRegistryMessage + pongMessage;

            // Act
            var result = SplitMessages(input);

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Equal(statesMessage, result[0]);
            Assert.Equal(deviceRegistryMessage, result[1]);
            Assert.Equal(pongMessage, result[2]);
            
            // Validate each message is well-formed and has expected structure
            using var statesDoc = JsonDocument.Parse(result[0]);
            Assert.Equal(2000015, statesDoc.RootElement.GetProperty("id").GetInt32());
            Assert.Equal("result", statesDoc.RootElement.GetProperty("type").GetString());
            
            using var deviceDoc = JsonDocument.Parse(result[1]);
            Assert.Equal(2000016, deviceDoc.RootElement.GetProperty("id").GetInt32());
            
            using var pongDoc = JsonDocument.Parse(result[2]);
            Assert.Equal("pong", pongDoc.RootElement.GetProperty("type").GetString());
        }

        [Fact]
        public void SplitCombinedJsonMessages_IncompleteMessage_DoesNotCrash()
        {
            // Arrange - Message cut off mid-way (simulates partial frame reception)
            var complete = @"{""id"":1,""type"":""result""}";
            var incomplete = @"{""id"":2,""type"":""res";
            var input = complete + incomplete;

            // Act
            var result = SplitMessages(input);

            // Assert
            // Should return the complete message only
            Assert.Single(result);
            Assert.Equal(complete, result[0]);
        }

        [Theory]
        [InlineData(@"{""test"":""value""}", 1)]
        [InlineData(@"{""id"":1}{""id"":2}", 2)]
        [InlineData(@"{""nested"":{""value"":true}}{""simple"":""msg""}", 2)]
        [InlineData(@"   {""id"":1}   {""id"":2}   ", 2)]
        [InlineData(@"{""array"":[1,2,3]}{""object"":{""key"":""value""}}", 2)]
        public void SplitCombinedJsonMessages_VariousInputs_ReturnsCorrectCount(string input, int expectedCount)
        {
            // Act
            var result = SplitMessages(input);

            // Assert
            Assert.Equal(expectedCount, result.Count);
            Assert.All(result, msg => Assert.True(IsValidJson(msg)));
        }

        private static bool IsValidJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(json);
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _client?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}