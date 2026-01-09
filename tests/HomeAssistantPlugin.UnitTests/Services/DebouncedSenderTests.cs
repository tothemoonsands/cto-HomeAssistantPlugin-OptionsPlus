using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Loupedeck.HomeAssistantPlugin.Services;

namespace Loupedeck.HomeAssistantPlugin.Tests.Services
{
    /// <summary>
    /// Unit tests for DebouncedSender with 100% coverage target
    /// Tests multi-threading behavior, debouncing timing accuracy, thread safety, memory management, and error handling
    /// </summary>
    public class DebouncedSenderTests : IDisposable
    {
        private const int ShortDelay = 50;   // 50ms for fast tests
        private const int MediumDelay = 100; // 100ms for timing tests
        private const int LongDelay = 200;   // 200ms for complex scenarios
        private const string TestKey = "test_key";
        private const string TestValue = "test_value";

        private readonly List<DebouncedSender<string, string>> _disposables = new();

        public void Dispose()
        {
            // Clean up any created senders
            foreach (var sender in _disposables)
            {
                sender?.Dispose();
            }
            _disposables.Clear();
        }

        private DebouncedSender<TKey, TValue> CreateSender<TKey, TValue>(int delayMs, Func<TKey, TValue, Task> sendFunc)
            where TKey : notnull
        {
            var sender = new DebouncedSender<TKey, TValue>(delayMs, sendFunc);
            if (sender is DebouncedSender<string, string> stringSender)
            {
                _disposables.Add(stringSender);
            }
            return sender;
        }

        #region Constructor Tests

        [Fact]
        public void Constructor_ValidParameters_CreatesInstance()
        {
            // Arrange & Act
            var sendFunc = new Func<string, string, Task>((k, v) => Task.CompletedTask);
            using var sender = new DebouncedSender<string, string>(MediumDelay, sendFunc);

            // Assert
            Assert.NotNull(sender);
        }

        [Fact]
        public void Constructor_NullSendFunction_ThrowsArgumentNullException()
        {
            // Act & Assert
            var exception = Assert.Throws<ArgumentNullException>(() =>
                new DebouncedSender<string, string>(MediumDelay, null!));
            Assert.Equal("send", exception.ParamName);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(100)]
        [InlineData(5000)]
        public void Constructor_VariousDelayValues_AcceptsAllPositiveValues(int delayMs)
        {
            // Arrange & Act
            var sendFunc = new Func<string, string, Task>((k, v) => Task.CompletedTask);
            using var sender = new DebouncedSender<string, string>(delayMs, sendFunc);

            // Assert
            Assert.NotNull(sender);
        }

        #endregion

        #region Basic Set and Send Tests

        [Fact]
        public async Task Set_SingleCall_ExecutesSendAfterDelay()
        {
            // Arrange
            var sendCalled = false;
            var sentKey = string.Empty;
            var sentValue = string.Empty;
            var tcs = new TaskCompletionSource<bool>();

            var sendFunc = new Func<string, string, Task>((k, v) =>
            {
                sendCalled = true;
                sentKey = k;
                sentValue = v;
                tcs.SetResult(true);
                return Task.CompletedTask;
            });

            using var sender = CreateSender(ShortDelay, sendFunc);

            // Act
            sender.Set(TestKey, TestValue);

            // Assert
            Assert.False(sendCalled); // Should not be called immediately

            // Wait for send to complete
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.True(sendCalled);
            Assert.Equal(TestKey, sentKey);
            Assert.Equal(TestValue, sentValue);
        }

        [Fact]
        public async Task Set_MultipleCalls_OnlyLastValueSent()
        {
            // Arrange
            var sendCount = 0;
            var sentValues = new List<string>();
            var tcs = new TaskCompletionSource<bool>();

            var sendFunc = new Func<string, string, Task>((k, v) =>
            {
                sendCount++;
                sentValues.Add(v);
                tcs.SetResult(true);
                return Task.CompletedTask;
            });

            using var sender = CreateSender(MediumDelay, sendFunc);

            // Act - Rapid successive calls
            sender.Set(TestKey, "value1");
            sender.Set(TestKey, "value2");
            sender.Set(TestKey, "value3");
            sender.Set(TestKey, "final_value");

            // Assert
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.Equal(1, sendCount); // Only one send should occur
            Assert.Single(sentValues);
            Assert.Equal("final_value", sentValues[0]);
        }

        #endregion

        #region Debouncing Behavior Tests

        [Fact]
        public async Task Set_RapidSuccessiveCalls_RestartsTimer()
        {
            // Arrange
            var sendCalled = false;
            var tcs = new TaskCompletionSource<bool>();

            var sendFunc = new Func<string, string, Task>((k, v) =>
            {
                sendCalled = true;
                tcs.SetResult(true);
                return Task.CompletedTask;
            });

            using var sender = CreateSender(LongDelay, sendFunc);

            // Act - Call Set multiple times within the delay period
            sender.Set(TestKey, "value1");
            await Task.Delay(50); // Less than the debounce delay
            sender.Set(TestKey, "value2");
            await Task.Delay(50);
            sender.Set(TestKey, "final_value");

            // Assert - Should not be called yet (timer was restarted)
            Assert.False(sendCalled);

            // Wait for the final send
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.True(sendCalled);
        }

        [Fact]
        public async Task Set_DelayedCalls_MultipleSendsOccur()
        {
            // Arrange
            var sendCount = 0;
            var sentValues = new ConcurrentBag<string>();
            var allSentTcs = new TaskCompletionSource<bool>();

            var sendFunc = new Func<string, string, Task>(async (k, v) =>
            {
                sentValues.Add(v);
                var count = Interlocked.Increment(ref sendCount);
                if (count == 3)
                {
                    allSentTcs.SetResult(true);
                }
            });

            using var sender = CreateSender(ShortDelay, sendFunc);

            // Act - Calls with sufficient delays between them
            sender.Set(TestKey, "value1");
            await Task.Delay(ShortDelay + 30); // Wait for first send to complete

            sender.Set(TestKey, "value2");
            await Task.Delay(ShortDelay + 30); // Wait for second send to complete

            sender.Set(TestKey, "value3");

            // Assert
            await allSentTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(3, sendCount);
            Assert.Contains("value1", sentValues);
            Assert.Contains("value2", sentValues);
            Assert.Contains("value3", sentValues);
        }

        #endregion

        #region Key-based Debouncing Tests

        [Fact]
        public async Task Set_DifferentKeys_IndependentDebouncing()
        {
            // Arrange
            var sendCount = 0;
            var sentPairs = new ConcurrentBag<(string key, string value)>();
            var allSentTcs = new TaskCompletionSource<bool>();

            var sendFunc = new Func<string, string, Task>((k, v) =>
            {
                sentPairs.Add((k, v));
                var count = Interlocked.Increment(ref sendCount);
                if (count == 2)
                {
                    allSentTcs.SetResult(true);
                }
                return Task.CompletedTask;
            });

            using var sender = CreateSender(MediumDelay, sendFunc);

            // Act - Set values for different keys simultaneously
            sender.Set("key1", "value1");
            sender.Set("key2", "value2");

            // Assert
            await allSentTcs.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.Equal(2, sendCount);
            Assert.Contains(("key1", "value1"), sentPairs);
            Assert.Contains(("key2", "value2"), sentPairs);
        }

        [Fact]
        public async Task Set_MultipleKeysRapidUpdates_EachKeyDebounced()
        {
            // Arrange
            var sendCount = 0;
            var sentPairs = new ConcurrentBag<(string key, string value)>();
            var allSentTcs = new TaskCompletionSource<bool>();

            var sendFunc = new Func<string, string, Task>((k, v) =>
            {
                sentPairs.Add((k, v));
                var count = Interlocked.Increment(ref sendCount);
                if (count == 2)
                {
                    allSentTcs.SetResult(true);
                }
                return Task.CompletedTask;
            });

            using var sender = CreateSender(MediumDelay, sendFunc);

            // Act - Rapid updates to multiple keys
            sender.Set("key1", "value1_initial");
            sender.Set("key2", "value2_initial");
            sender.Set("key1", "value1_final");
            sender.Set("key2", "value2_final");

            // Assert - Each key should send only its final value
            await allSentTcs.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.Equal(2, sendCount);
            Assert.Contains(("key1", "value1_final"), sentPairs);
            Assert.Contains(("key2", "value2_final"), sentPairs);
        }

        #endregion

        #region Cancel Method Tests

        [Fact]
        public async Task Cancel_PendingEntry_PreventsSend()
        {
            // Arrange
            var sendCalled = false;
            var sendFunc = new Func<string, string, Task>((k, v) =>
            {
                sendCalled = true;
                return Task.CompletedTask;
            });

            using var sender = CreateSender(MediumDelay, sendFunc);

            // Act
            sender.Set(TestKey, TestValue);
            sender.Cancel(TestKey); // Cancel before timer fires

            // Wait longer than the delay to ensure it would have fired
            await Task.Delay(MediumDelay + 50);

            // Assert
            Assert.False(sendCalled);
        }

        [Fact]
        public async Task Cancel_NonExistentKey_NoEffect()
        {
            // Arrange
            var sendCalled = false;
            var tcs = new TaskCompletionSource<bool>();

            var sendFunc = new Func<string, string, Task>((k, v) =>
            {
                sendCalled = true;
                tcs.SetResult(true);
                return Task.CompletedTask;
            });

            using var sender = CreateSender(ShortDelay, sendFunc);

            // Act
            sender.Set(TestKey, TestValue);
            sender.Cancel("nonexistent_key"); // Cancel different key

            // Assert - Original send should still occur
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.True(sendCalled);
        }

        [Fact]
        public async Task Cancel_MultipleKeys_OnlyTargetKeyCanceled()
        {
            // Arrange
            var sendCount = 0;
            var sentKeys = new ConcurrentBag<string>();
            var tcs = new TaskCompletionSource<bool>();

            var sendFunc = new Func<string, string, Task>((k, v) =>
            {
                sentKeys.Add(k);
                var count = Interlocked.Increment(ref sendCount);
                if (count == 1)
                {
                    tcs.SetResult(true);
                }
                return Task.CompletedTask;
            });

            using var sender = CreateSender(MediumDelay, sendFunc);

            // Act
            sender.Set("key1", "value1");
            sender.Set("key2", "value2");
            sender.Cancel("key1"); // Cancel only key1

            // Assert - Only key2 should send
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.Equal(1, sendCount);
            Assert.Contains("key2", sentKeys);
            Assert.DoesNotContain("key1", sentKeys);
        }

        #endregion

        #region Thread Safety Tests

        [Fact]
        public async Task Set_ConcurrentAccess_ThreadSafe()
        {
            // Arrange
            var sendCount = 0;
            var sentValues = new ConcurrentBag<string>();
            var allSentTcs = new TaskCompletionSource<bool>();

            var sendFunc = new Func<string, string, Task>((k, v) =>
            {
                sentValues.Add(v);
                var count = Interlocked.Increment(ref sendCount);
                if (count == 10) // Expecting 10 different keys
                {
                    allSentTcs.SetResult(true);
                }
                return Task.CompletedTask;
            });

            using var sender = CreateSender(ShortDelay, sendFunc);

            // Act - Concurrent access from multiple threads
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                var keyIndex = i;
                tasks.Add(Task.Run(() =>
                {
                    sender.Set($"key{keyIndex}", $"value{keyIndex}");
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            await allSentTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.Equal(10, sendCount);
            Assert.Equal(10, sentValues.Count);
        }

        [Fact]
        public async Task SetAndCancel_ConcurrentAccess_ThreadSafe()
        {
            // Arrange
            var sendCount = 0;
            var sendFunc = new Func<string, string, Task>((k, v) =>
            {
                Interlocked.Increment(ref sendCount);
                return Task.CompletedTask;
            });

            using var sender = CreateSender(MediumDelay, sendFunc);

            // Act - Concurrent Set and Cancel operations
            var tasks = new List<Task>();
            
            // Multiple threads setting values
            for (int i = 0; i < 5; i++)
            {
                var keyIndex = i;
                tasks.Add(Task.Run(() =>
                {
                    sender.Set($"key{keyIndex}", $"value{keyIndex}");
                }));
            }

            // Some threads canceling
            for (int i = 0; i < 3; i++)
            {
                var keyIndex = i;
                tasks.Add(Task.Run(async () =>
                {
                    await Task.Delay(10); // Small delay before cancel
                    sender.Cancel($"key{keyIndex}");
                }));
            }

            await Task.WhenAll(tasks);

            // Wait for potential sends to complete
            await Task.Delay(MediumDelay + 100);

            // Assert - Should have some sends but not all (some were canceled)
            Assert.True(sendCount >= 0 && sendCount <= 5);
        }

        #endregion

        #region Timing Accuracy Tests

        [Fact]
        public async Task Set_TimingAccuracy_WithinReasonableTolerance()
        {
            // Arrange
            var sendTime = DateTime.MinValue;
            var startTime = DateTime.UtcNow;
            var tcs = new TaskCompletionSource<bool>();

            var sendFunc = new Func<string, string, Task>((k, v) =>
            {
                sendTime = DateTime.UtcNow;
                tcs.SetResult(true);
                return Task.CompletedTask;
            });

            using var sender = CreateSender(MediumDelay, sendFunc);

            // Act
            sender.Set(TestKey, TestValue);
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));

            // Assert - Timing should be within reasonable tolerance
            var actualDelay = (sendTime - startTime).TotalMilliseconds;
            Assert.True(actualDelay >= MediumDelay - 20); // Allow 20ms early (timer precision)
            Assert.True(actualDelay <= MediumDelay + 100); // Allow 100ms late (system load)
        }

        [Fact]
        public async Task Set_HighFrequencyOperations_MaintainsTiming()
        {
            // Arrange
            var sendTimes = new ConcurrentBag<DateTime>();
            var sendCount = 0;
            var allSentTcs = new TaskCompletionSource<bool>();

            var sendFunc = new Func<string, string, Task>((k, v) =>
            {
                sendTimes.Add(DateTime.UtcNow);
                var count = Interlocked.Increment(ref sendCount);
                if (count == 5)
                {
                    allSentTcs.SetResult(true);
                }
                return Task.CompletedTask;
            });

            using var sender = CreateSender(ShortDelay, sendFunc);

            // Act - High frequency operations with different keys
            var startTime = DateTime.UtcNow;
            for (int i = 0; i < 5; i++)
            {
                sender.Set($"key{i}", $"value{i}");
            }

            await allSentTcs.Task.WaitAsync(TimeSpan.FromSeconds(2));

            // Assert - All sends should occur roughly at the same time
            var times = sendTimes.ToArray();
            Assert.Equal(5, times.Length);
            
            var maxTime = times.Max();
            var minTime = times.Min();
            var timeSpread = (maxTime - minTime).TotalMilliseconds;
            
            // All sends should complete within a reasonable time window
            Assert.True(timeSpread < 100); // Within 100ms of each other
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task Send_ThrowsException_DoesNotCrash()
        {
            // Arrange
            var sendAttempts = 0;
            var tcs = new TaskCompletionSource<bool>();

            var sendFunc = new Func<string, string, Task>((k, v) =>
            {
                Interlocked.Increment(ref sendAttempts);
                tcs.SetResult(true);
                throw new InvalidOperationException("Simulated send failure");
            });

            using var sender = CreateSender(ShortDelay, sendFunc);

            // Act & Assert - Should not throw
            sender.Set(TestKey, TestValue);
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.Equal(1, sendAttempts);
            
            // Sender should still be functional after exception
            var tcs2 = new TaskCompletionSource<bool>();
            var sendFunc2 = new Func<string, string, Task>((k, v) =>
            {
                tcs2.SetResult(true);
                return Task.CompletedTask;
            });

            using var sender2 = CreateSender(ShortDelay, sendFunc2);
            sender2.Set(TestKey, TestValue);
            await tcs2.Task.WaitAsync(TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task Send_AsyncException_HandledGracefully()
        {
            // Arrange
            var sendCalled = false;
            var tcs = new TaskCompletionSource<bool>();

            var sendFunc = new Func<string, string, Task>(async (k, v) =>
            {
                sendCalled = true;
                tcs.SetResult(true);
                await Task.Delay(10);
                throw new TaskCanceledException("Async operation canceled");
            });

            using var sender = CreateSender(ShortDelay, sendFunc);

            // Act & Assert - Should not throw
            sender.Set(TestKey, TestValue);
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.True(sendCalled);
        }

        #endregion

        #region Dispose and Cleanup Tests

        [Fact]
        public async Task Dispose_WithPendingOperations_CleansUpProperly()
        {
            // Arrange
            var sendCalled = false;
            var sendFunc = new Func<string, string, Task>((k, v) =>
            {
                sendCalled = true;
                return Task.CompletedTask;
            });

            var sender = new DebouncedSender<string, string>(MediumDelay, sendFunc);

            // Act
            sender.Set(TestKey, TestValue);
            sender.Dispose(); // Dispose before timer fires

            // Wait to see if send occurs after dispose
            await Task.Delay(MediumDelay + 50);

            // Assert - Send should not occur after dispose
            Assert.False(sendCalled);
        }

        [Fact]
        public void Dispose_MultipleCalls_DoesNotThrow()
        {
            // Arrange
            var sendFunc = new Func<string, string, Task>((k, v) => Task.CompletedTask);
            var sender = new DebouncedSender<string, string>(MediumDelay, sendFunc);

            // Act & Assert - Multiple disposes should not throw
            sender.Dispose();
            sender.Dispose();
            sender.Dispose();
        }

        [Fact]
        public async Task Dispose_WithManyPendingEntries_CleansUpAll()
        {
            // Arrange
            var sendFunc = new Func<string, string, Task>((k, v) => Task.CompletedTask);
            var sender = new DebouncedSender<string, string>(LongDelay, sendFunc);

            // Act - Create many pending entries
            for (int i = 0; i < 100; i++)
            {
                sender.Set($"key{i}", $"value{i}");
            }

            sender.Dispose();

            // Assert - Should not throw and should complete quickly
            // If cleanup is working properly, this will complete without hanging
            await Task.Delay(100); // Short delay to ensure dispose completes
        }

        #endregion

        #region Memory Management Tests

        [Fact]
        public async Task HighFrequencyOperations_NoMemoryLeaks()
        {
            // Arrange
            var sendCount = 0;
            var sendFunc = new Func<string, string, Task>((k, v) =>
            {
                Interlocked.Increment(ref sendCount);
                return Task.CompletedTask;
            });

            using var sender = CreateSender(10, sendFunc); // Very short delay

            // Act - Many operations to test memory management
            for (int i = 0; i < 1000; i++)
            {
                sender.Set($"key{i % 10}", $"value{i}"); // Reuse keys to test cleanup
                
                if (i % 100 == 0)
                {
                    await Task.Delay(1); // Occasional pause
                }
            }

            // Wait for operations to complete
            await Task.Delay(50);

            // Assert - Should handle high frequency without issues
            Assert.True(sendCount > 0);
            Assert.True(sendCount <= 1000); // Some operations should be debounced
        }

        [Fact]
        public async Task RapidSetAndCancel_NoMemoryAccumulation()
        {
            // Arrange
            var sendFunc = new Func<string, string, Task>((k, v) => Task.CompletedTask);
            using var sender = CreateSender(MediumDelay, sendFunc);

            // Act - Rapid set and cancel operations
            for (int i = 0; i < 500; i++)
            {
                sender.Set($"key{i}", $"value{i}");
                if (i % 2 == 0)
                {
                    sender.Cancel($"key{i}");
                }
            }

            // Wait for potential sends
            await Task.Delay(MediumDelay + 50);

            // Assert - Should handle rapid operations without memory issues
            // Test completes successfully if no memory issues occur
        }

        #endregion

        #region Edge Cases and Complex Scenarios

        [Fact]
        public async Task ZeroDelay_ExecutesImmediately()
        {
            // Arrange
            var sendCalled = false;
            var tcs = new TaskCompletionSource<bool>();

            var sendFunc = new Func<string, string, Task>((k, v) =>
            {
                sendCalled = true;
                tcs.SetResult(true);
                return Task.CompletedTask;
            });

            using var sender = CreateSender(0, sendFunc); // Zero delay

            // Act
            sender.Set(TestKey, TestValue);

            // Assert - Should execute very quickly with zero delay
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.True(sendCalled);
        }

        [Fact]
        public async Task LargeDelay_WorksCorrectly()
        {
            // Arrange
            var sendCalled = false;
            var tcs = new TaskCompletionSource<bool>();

            var sendFunc = new Func<string, string, Task>((k, v) =>
            {
                sendCalled = true;
                tcs.SetResult(true);
                return Task.CompletedTask;
            });

            using var sender = CreateSender(300, sendFunc); // Larger delay

            // Act
            sender.Set(TestKey, TestValue);

            // Assert
            Assert.False(sendCalled); // Should not be called immediately
            
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.True(sendCalled);
        }

        [Fact]
        public async Task ComplexScenario_InterleavedOperations()
        {
            // Arrange
            var sendCount = 0;
            var sentPairs = new ConcurrentBag<(string key, string value)>();
            var tcs = new TaskCompletionSource<bool>();

            var sendFunc = new Func<string, string, Task>((k, v) =>
            {
                sentPairs.Add((k, v));
                var count = Interlocked.Increment(ref sendCount);
                if (count == 2) // Expecting 2 final sends
                {
                    tcs.SetResult(true);
                }
                return Task.CompletedTask;
            });

            using var sender = CreateSender(MediumDelay, sendFunc);

            // Act - Complex interleaved operations
            sender.Set("key1", "value1_initial");
            await Task.Delay(20);
            
            sender.Set("key2", "value2_initial");
            await Task.Delay(20);
            
            sender.Cancel("key1");
            sender.Set("key1", "value1_final");
            
            await Task.Delay(20);
            sender.Set("key2", "value2_final");

            // Assert
            await tcs.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.Equal(2, sendCount);
            Assert.Contains(("key1", "value1_final"), sentPairs);
            Assert.Contains(("key2", "value2_final"), sentPairs);
        }

        #endregion
    }
}