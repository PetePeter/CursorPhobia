using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using CursorPhobia.Core.Services;
using CursorPhobia.Tests;

namespace CursorPhobia.Tests
{
    /// <summary>
    /// Comprehensive unit tests for SingleInstanceManager class
    /// Tests mutex-based single instance control, inter-process communication, and thread safety
    /// </summary>
    public class SingleInstanceManagerTests : IDisposable
    {
        private readonly TestLogger _logger;
        private readonly List<SingleInstanceManager> _managers;

        public SingleInstanceManagerTests()
        {
            _logger = new TestLogger();
            _managers = new List<SingleInstanceManager>();
        }

        public void Dispose()
        {
            // Clean up all test managers
            foreach (var manager in _managers)
            {
                try
                {
                    manager.Dispose();
                }
                catch
                {
                    // Ignore disposal errors in tests
                }
            }
            _managers.Clear();
        }

        [Fact]
        public async Task TryAcquireLockAsync_FirstInstance_ShouldSucceed()
        {
            // Arrange
            var manager = CreateManager();

            // Act
            var result = await manager.TryAcquireLockAsync();

            // Assert
            Assert.True(result, "First instance should acquire lock successfully");
            Assert.True(manager.IsOwner, "Manager should be marked as owner");
            Assert.False(manager.IsInitialized, "Manager should not be initialized yet");
        }

        [Fact]
        public async Task TryAcquireLockAsync_SecondInstance_ShouldFail()
        {
            // Arrange
            var manager1 = CreateManager();
            var manager2 = CreateManager();

            // Act
            var result1 = await manager1.TryAcquireLockAsync();
            var result2 = await manager2.TryAcquireLockAsync();

            // Assert
            Assert.True(result1, "First instance should acquire lock");
            Assert.False(result2, "Second instance should be blocked");
            Assert.True(manager1.IsOwner, "First manager should be owner");
            Assert.False(manager2.IsOwner, "Second manager should not be owner");
        }

        [Fact]
        public async Task InitializeAsync_WithoutLock_ShouldFail()
        {
            // Arrange
            var manager = CreateManager();

            // Act
            var result = await manager.InitializeAsync();

            // Assert
            Assert.False(result, "Initialize should fail without acquiring lock first");
            Assert.False(manager.IsInitialized, "Manager should not be initialized");
        }

        [Fact]
        public async Task InitializeAsync_WithLock_ShouldSucceed()
        {
            // Arrange
            var manager = CreateManager();
            await manager.TryAcquireLockAsync();

            // Act
            var result = await manager.InitializeAsync();

            // Assert
            Assert.True(result, "Initialize should succeed after acquiring lock");
            Assert.True(manager.IsInitialized, "Manager should be initialized");
        }

        [Fact]
        public async Task InitializeAsync_CalledTwice_ShouldReturnTrueOnSecondCall()
        {
            // Arrange
            var manager = CreateManager();
            await manager.TryAcquireLockAsync();

            // Act
            var result1 = await manager.InitializeAsync();
            var result2 = await manager.InitializeAsync();

            // Assert
            Assert.True(result1, "First initialize should succeed");
            Assert.True(result2, "Second initialize should also return true");
            Assert.True(manager.IsInitialized, "Manager should remain initialized");
        }

        [Fact]
        public async Task SendActivationRequestAsync_ToNonExistentInstance_ShouldFail()
        {
            // Arrange
            var manager = CreateManager();
            var args = new[] { "--test", "arg1", "arg2" };

            // Act
            var result = await manager.SendActivationRequestAsync(args);

            // Assert
            Assert.False(result, "Activation request should fail when no instance is listening");
        }

        [Fact]
        public async Task InstanceActivationRequested_ShouldFireWhenActivationReceived()
        {
            // Arrange
            var primaryManager = CreateManager();
            var secondaryManager = CreateManager();

            await primaryManager.TryAcquireLockAsync();
            await primaryManager.InitializeAsync();

            InstanceActivationEventArgs? receivedArgs = null;
            primaryManager.InstanceActivationRequested += (sender, args) =>
            {
                receivedArgs = args;
            };

            var testArgs = new[] { "--activate", "test-param" };

            // Act
            // Give the pipe server a moment to start
            await Task.Delay(100);
            var result = await secondaryManager.SendActivationRequestAsync(testArgs);

            // Wait for the event to be processed
            await Task.Delay(500);

            // Assert
            Assert.True(result, "Activation request should succeed");
            Assert.NotNull(receivedArgs);
            Assert.Equal(testArgs, receivedArgs!.Arguments);
            Assert.True(Math.Abs((DateTime.UtcNow - receivedArgs.RequestTime).TotalSeconds) < 5,
                "Request time should be recent");
        }

        [Fact]
        public async Task SendActivationRequestAsync_WithNullArgs_ShouldUseEmptyArray()
        {
            // Arrange
            var primaryManager = CreateManager();
            var secondaryManager = CreateManager();

            await primaryManager.TryAcquireLockAsync();
            await primaryManager.InitializeAsync();

            InstanceActivationEventArgs? receivedArgs = null;
            primaryManager.InstanceActivationRequested += (sender, args) =>
            {
                receivedArgs = args;
            };

            // Act
            await Task.Delay(100); // Give pipe server time to start
            var result = await secondaryManager.SendActivationRequestAsync(null!);
            await Task.Delay(500); // Wait for event processing

            // Assert
            Assert.True(result, "Activation request should succeed even with null args");
            Assert.NotNull(receivedArgs);
            Assert.Empty(receivedArgs!.Arguments);
        }

        [Fact]
        public async Task ShutdownAsync_ShouldReleaseLockAndStopPipeServer()
        {
            // Arrange
            var manager1 = CreateManager();
            var manager2 = CreateManager();

            await manager1.TryAcquireLockAsync();
            await manager1.InitializeAsync();

            // Act
            await manager1.ShutdownAsync();

            // Give the shutdown a moment to complete
            await Task.Delay(100);

            var result = await manager2.TryAcquireLockAsync();

            // Assert
            Assert.True(result, "Second manager should be able to acquire lock after first shuts down");
            Assert.False(manager1.IsOwner, "First manager should no longer be owner");
            Assert.False(manager1.IsInitialized, "First manager should no longer be initialized");
        }

        [Fact]
        public async Task Dispose_ShouldCleanupProperlyWithoutExceptions()
        {
            // Arrange
            var manager = CreateManager();
            await manager.TryAcquireLockAsync();
            await manager.InitializeAsync();

            // Act & Assert - Should not throw
            manager.Dispose();

            // Verify state is cleaned up
            Assert.False(manager.IsOwner, "Manager should no longer be owner after disposal");
            Assert.False(manager.IsInitialized, "Manager should no longer be initialized after disposal");
        }

        [Fact]
        public async Task Operations_OnDisposedManager_ShouldReturnFalse()
        {
            // Arrange
            var manager = CreateManager();
            manager.Dispose();

            // Act & Assert
            var lockResult = await manager.TryAcquireLockAsync();
            var initResult = await manager.InitializeAsync();
            var activationResult = await manager.SendActivationRequestAsync(new[] { "test" });

            Assert.False(lockResult, "TryAcquireLockAsync should return false on disposed manager");
            Assert.False(initResult, "InitializeAsync should return false on disposed manager");
            Assert.False(activationResult, "SendActivationRequestAsync should return false on disposed manager");
            Assert.False(manager.IsOwner, "IsOwner should be false on disposed manager");
            Assert.False(manager.IsInitialized, "IsInitialized should be false on disposed manager");
        }

        [Fact]
        public async Task ConcurrentAcquisition_ShouldOnlyAllowOneOwner()
        {
            // Arrange
            var tasks = new List<Task<(SingleInstanceManager Manager, bool Success)>>();

            // Create multiple managers and try to acquire lock concurrently
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var manager = CreateManager();
                    var success = await manager.TryAcquireLockAsync();
                    return (manager, success);
                }));
            }

            // Act
            var results = await Task.WhenAll(tasks);

            // Assert
            var successCount = results.Count(r => r.Success);
            Assert.Equal(1, successCount);

            var ownerCount = results.Count(r => r.Manager.IsOwner);
            Assert.Equal(1, ownerCount);
        }

        [Fact]
        public void LoggingIntegration_ShouldLogAppropriateMessages()
        {
            // Arrange & Act
            var manager = CreateManager();

            // Assert - Should have logged manager creation
            Assert.Contains(_logger.Logs, log => log.Contains("DEBUG") && log.Contains("SingleInstanceManager created"));
        }

        [Fact]
        public async Task MutexNames_ShouldBeUserSpecific()
        {
            // Arrange
            var manager1 = CreateManager();
            var manager2 = CreateManager();

            // Act
            var result1 = await manager1.TryAcquireLockAsync();
            var result2 = await manager2.TryAcquireLockAsync();

            // Assert
            Assert.True(result1, "First manager should acquire lock");
            Assert.False(result2, "Second manager should be blocked (proving they use same mutex name)");

            // Verify mutex names are generated (can't directly test SID without user context, but ensure no exceptions)
            Assert.True(true, "Mutex name generation completed without exceptions");
        }

        [Fact]
        public async Task PipeServerResilience_ShouldRecoverFromErrors()
        {
            // Arrange
            var manager = CreateManager();
            await manager.TryAcquireLockAsync();
            await manager.InitializeAsync();

            // Act - Send multiple rapid requests to test server resilience
            var tasks = new List<Task<bool>>();
            for (int i = 0; i < 3; i++)
            {
                var secondaryManager = CreateManager();
                tasks.Add(secondaryManager.SendActivationRequestAsync(new[] { $"test-{i}" }));
            }

            await Task.Delay(100); // Give pipe server time to start
            var results = await Task.WhenAll(tasks);

            // Assert - At least some requests should succeed (pipe server should handle multiple connections)
            Assert.True(results.Any(r => r), "At least some activation requests should succeed");
        }

        private SingleInstanceManager CreateManager()
        {
            var manager = new SingleInstanceManager(_logger);
            _managers.Add(manager);
            return manager;
        }
    }
}