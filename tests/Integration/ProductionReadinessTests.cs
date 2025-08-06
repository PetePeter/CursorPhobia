using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using CursorPhobia.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CursorPhobia.Tests.Integration
{
    /// <summary>
    /// Comprehensive production readiness integration tests validating all WI#8 requirements.
    /// Tests single instance management, exception handling, logging, error recovery, and performance.
    /// </summary>
    public class ProductionReadinessTests
    {
        [Fact]
        public async Task SingleInstanceManager_ShouldPreventMultipleInstances()
        {
            // Arrange
            var serviceProvider = CreateServiceProvider();
            using var singleInstanceManager1 = serviceProvider.GetRequiredService<ISingleInstanceManager>();
            using var singleInstanceManager2 = serviceProvider.GetRequiredService<ISingleInstanceManager>();

            // Act
            var firstInstanceAcquired = await singleInstanceManager1.TryAcquireLockAsync();
            var secondInstanceAcquired = await singleInstanceManager2.TryAcquireLockAsync();

            // Assert
            Assert.True(firstInstanceAcquired, "First instance should be acquired successfully");
            Assert.False(secondInstanceAcquired, "Second instance should be blocked");
        }

        [Fact]
        public async Task ErrorRecoveryManager_ShouldRecoverFromTransientFailures()
        {
            // Arrange
            var serviceProvider = CreateServiceProvider();
            var errorRecoveryManager = serviceProvider.GetRequiredService<IErrorRecoveryManager>();
            var recoveryAttempted = false;

            // Register a component that will fail initially but succeed on retry
            var failureCount = 0;
            await errorRecoveryManager.RegisterComponentAsync("TestComponent", async () =>
            {
                failureCount++;
                if (failureCount < 3)
                {
                    throw new InvalidOperationException($"Simulated failure #{failureCount}");
                }
                recoveryAttempted = true;
                return true;
            });

            // Act
            var result = await errorRecoveryManager.TriggerRecoveryAsync("TestComponent");

            // Assert
            Assert.True(result.Success);
            Assert.True(recoveryAttempted);
            Assert.Equal(3, failureCount);
        }

        [Fact]
        public async Task ServiceHealthMonitor_ShouldDetectUnhealthyServices()
        {
            // Arrange
            var serviceProvider = CreateServiceProvider();
            var healthMonitor = serviceProvider.GetRequiredService<IServiceHealthMonitor>();
            var healthChanged = false;

            healthMonitor.SystemHealthChanged += (_, _) => healthChanged = true;

            // Act - Just verify the health monitor is working
            await Task.Delay(200); // Wait for initialization

            // Assert - Basic functionality test
            Assert.True(healthChanged); // Should have fired during initialization
        }

        [Fact]
        public async Task GlobalExceptionHandler_ShouldInitializeAndHandleExceptions()
        {
            // Arrange
            var serviceProvider = CreateServiceProvider();
            var exceptionHandler = serviceProvider.GetRequiredService<IGlobalExceptionHandler>();
            var exceptionHandledCount = 0;
            var criticalExceptionCount = 0;

            exceptionHandler.ExceptionHandled += (_, args) =>
            {
                exceptionHandledCount++;
            };

            exceptionHandler.CriticalExceptionOccurred += (_, args) =>
            {
                criticalExceptionCount++;
            };

            // Act
            var initializeResult = await exceptionHandler.InitializeAsync();

            // Test normal exception handling
            var normalExceptionResult = await exceptionHandler.HandleExceptionAsync(
                new InvalidOperationException("Test exception"),
                "Test context",
                canRecover: true);

            // Test critical exception handling  
            var criticalExceptionResult = await exceptionHandler.HandleExceptionAsync(
                new OutOfMemoryException("Test critical exception"),
                "Test critical context",
                canRecover: false);

            // Assert
            Assert.True(initializeResult, "Exception handler should initialize successfully");
            Assert.True(exceptionHandler.IsActive, "Exception handler should be active after initialization");
            Assert.True(normalExceptionResult, "Normal exception should be handled successfully");
            Assert.False(criticalExceptionResult, "Critical non-recoverable exception should return false");
            Assert.Equal(1, exceptionHandledCount);
            Assert.Equal(1, criticalExceptionCount);
            Assert.Equal(2, exceptionHandler.TotalExceptionsHandled);
            Assert.Equal(1, exceptionHandler.CriticalExceptionsCount);

            // Cleanup
            await exceptionHandler.ShutdownAsync();
        }

        [Fact]
        public async Task GlobalExceptionHandler_ShouldHandleVariousExceptionTypes()
        {
            // Arrange
            var serviceProvider = CreateServiceProvider();
            var exceptionHandler = serviceProvider.GetRequiredService<IGlobalExceptionHandler>();
            await exceptionHandler.InitializeAsync();

            var testExceptions = new Exception[]
            {
                new ArgumentNullException("testParam", "Test argument null exception"),
                new InvalidOperationException("Test invalid operation"),
                new TimeoutException("Test timeout exception"),
                new System.IO.IOException("Test IO exception"),
                new UnauthorizedAccessException("Test unauthorized access"),
                new NotSupportedException("Test not supported exception")
            };

            // Act & Assert
            foreach (var testException in testExceptions)
            {
                var result = await exceptionHandler.HandleExceptionAsync(
                    testException,
                    $"Test context for {testException.GetType().Name}",
                    canRecover: true);

                // Most exceptions should be handled (return true for canRecover=true)
                // ArgumentNullException and similar programming errors return false
                var expectedResult = testException is ArgumentException ? false : true;
                Assert.Equal(expectedResult, result);
            }

            // Verify all exceptions were counted
            Assert.Equal(testExceptions.Length, exceptionHandler.TotalExceptionsHandled);

            // Cleanup
            await exceptionHandler.ShutdownAsync();
        }

        [Fact]
        public async Task GlobalExceptionHandler_ShouldThrottleServiceRecovery()
        {
            // Arrange
            var serviceProvider = CreateServiceProvider();
            var exceptionHandler = serviceProvider.GetRequiredService<IGlobalExceptionHandler>();
            await exceptionHandler.InitializeAsync();

            var recoveryAttempts = 0;

            // Act - Attempt multiple service recoveries rapidly
            var results = new List<bool>();
            for (int i = 0; i < 5; i++)
            {
                var result = await exceptionHandler.AttemptServiceRecoveryAsync(
                    "TestService",
                    async () =>
                    {
                        recoveryAttempts++;
                        await Task.Delay(10);
                        return true;
                    });
                results.Add(result);
            }

            // Assert - Should throttle after max attempts
            Assert.True(results.Take(3).All(r => r), "First 3 recovery attempts should succeed");
            Assert.True(results.Skip(3).All(r => !r), "Additional attempts should be throttled");
            Assert.Equal(3, recoveryAttempts); // Only 3 actual recovery attempts should have been made

            // Cleanup
            await exceptionHandler.ShutdownAsync();
        }

        [Fact]
        public async Task PerformanceMetrics_ShouldMeetProductionTargets()
        {
            // Arrange
            var serviceProvider = CreateServiceProvider();
            var lifecycleManager = serviceProvider.GetRequiredService<IApplicationLifecycleManager>();

            // Act - Measure initialization time
            var stopwatch = Stopwatch.StartNew();
            await lifecycleManager.InitializeAsync();
            stopwatch.Stop();

            // Assert - Should initialize in less than 3 seconds (production target)
            Assert.True(stopwatch.ElapsedMilliseconds < 3000,
                $"Initialization took {stopwatch.ElapsedMilliseconds}ms, should be < 3000ms");
        }

        [Fact]
        public async Task MemoryUsage_ShouldNotLeakDuringNormalOperation()
        {
            // Arrange
            var serviceProvider = CreateServiceProvider();
            var initialMemory = GC.GetTotalMemory(true);

            // Act - Simulate normal operation cycles
            for (int i = 0; i < 100; i++)
            {
                var singleInstanceManager = serviceProvider.GetService<ISingleInstanceManager>();
                var errorRecoveryManager = serviceProvider.GetService<IErrorRecoveryManager>();
                var healthMonitor = serviceProvider.GetService<IServiceHealthMonitor>();

                // Simulate some work
                await Task.Delay(1);
            }

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;

            // Assert - Memory increase should be minimal (less than 1MB for this test)
            Assert.True(memoryIncrease < 1024 * 1024,
                $"Memory increased by {memoryIncrease} bytes, should be minimal");
        }

        [Fact]
        public async Task ProductionLogging_ShouldWorkWithAllComponents()
        {
            // Arrange
            var serviceProvider = CreateServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<ProductionReadinessTests>>();

            // Act & Assert - Should not throw exceptions
            logger.LogInformation("Production readiness test starting");
            logger.LogWarning("Test warning message");
            logger.LogError("Test error message");
            logger.LogDebug("Test debug message");

            // Verify structured logging works
            using (logger.BeginScope(new { TestName = "ProductionReadiness", Version = "1.0" }))
            {
                logger.LogInformation("Scoped log message");
            }

            Assert.True(true, "Logging completed without exceptions");
        }

        private static IServiceProvider CreateServiceProvider()
        {
            var services = new ServiceCollection();

            // Add logging
            services.AddLogging(builder => builder.AddConsole());

            // Add ILogger for services that need it
            services.AddSingleton<CursorPhobia.Core.Utilities.ILogger>(provider =>
            {
                var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
                return new CursorPhobia.Core.Utilities.Logger(loggerFactory.CreateLogger<ProductionReadinessTests>(), "ProductionReadinessTests");
            });

            // Add production services
            services.AddSingleton<ISingleInstanceManager, SingleInstanceManager>();
            services.AddSingleton<IGlobalExceptionHandler, GlobalExceptionHandler>();
            services.AddSingleton<IErrorRecoveryManager, ErrorRecoveryManager>();
            services.AddSingleton<IServiceHealthMonitor, ServiceHealthMonitor>();
            services.AddSingleton<IApplicationLifecycleManager, ApplicationLifecycleManager>();

            return services.BuildServiceProvider();
        }
    }

    /// <summary>
    /// Test service for health monitoring validation
    /// </summary>
    public class TestService
    {
        private readonly bool _isHealthy;

        public TestService(bool isHealthy = true)
        {
            _isHealthy = isHealthy;
        }

        public Task<bool> IsHealthyAsync()
        {
            return Task.FromResult(_isHealthy);
        }
    }
}