using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;
using Xunit;

namespace CursorPhobia.Tests;

/// <summary>
/// Long-running stability tests for 24-hour validation scenarios
/// </summary>
public class StabilityTests
{
    private readonly TestLogger _logger;
    private readonly PerformanceMonitoringService _performanceMonitor;

    public StabilityTests()
    {
        _logger = new TestLogger();
        _performanceMonitor = new PerformanceMonitoringService(_logger);
    }

    [Fact(Timeout = 300000)] // 5 minutes timeout for CI
    public async Task MonitorManager_LongRunningOperations_ShouldRemainStable()
    {
        // Arrange
        var monitorManager = new MonitorManager();
        var testDuration = TimeSpan.FromMinutes(2); // Reduced for CI - can be increased for manual testing
        var operationInterval = TimeSpan.FromMilliseconds(100);
        var startTime = DateTime.UtcNow;
        var exceptions = new ConcurrentBag<Exception>();
        var operationCount = 0;
        var lastMemoryCheck = DateTime.UtcNow;
        var initialMemory = GC.GetTotalMemory(true);

        _logger.LogInformation($"Starting stability test for {testDuration.TotalMinutes} minutes");

        // Act
        var stabilityTask = Task.Run(async () =>
        {
            while (DateTime.UtcNow - startTime < testDuration)
            {
                try
                {
                    // Perform various monitor operations
                    var monitors = monitorManager.GetAllMonitors();
                    Interlocked.Increment(ref operationCount);

                    if (monitors.Any())
                    {
                        var firstMonitor = monitors.First();
                        var dpiInfo = monitorManager.GetMonitorDpi(firstMonitor);

                        var primary = monitorManager.GetPrimaryMonitor();
                        var adjacent = monitorManager.GetAdjacentMonitors(firstMonitor);

                        // Test point and rectangle queries
                        var testPoint = new Point(firstMonitor.monitorBounds.X + 100, firstMonitor.monitorBounds.Y + 100);
                        var containingMonitor = monitorManager.GetMonitorContaining(testPoint);

                        var testRect = new Rectangle(testPoint.X, testPoint.Y, 200, 200);
                        var rectMonitor = monitorManager.GetMonitorContaining(testRect);
                    }

                    // Memory check every 30 seconds
                    if (DateTime.UtcNow - lastMemoryCheck > TimeSpan.FromSeconds(30))
                    {
                        var currentMemory = GC.GetTotalMemory(false);
                        var memoryIncrease = currentMemory - initialMemory;

                        // Log memory usage
                        _logger.LogInformation($"Memory usage: {currentMemory / 1024}KB (increase: {memoryIncrease / 1024}KB), Operations: {operationCount}");

                        // Warn if memory usage is growing too fast
                        if (memoryIncrease > 50 * 1024 * 1024) // 50MB
                        {
                            _logger.LogWarning($"High memory usage detected: {memoryIncrease / 1024 / 1024}MB increase");
                        }

                        lastMemoryCheck = DateTime.UtcNow;
                    }

                    await Task.Delay(operationInterval);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                    _logger.LogError($"Exception during stability test: {ex.Message}");
                }
            }
        });

        await stabilityTask;
        var totalDuration = DateTime.UtcNow - startTime;

        // Force garbage collection to clean up
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalMemory = GC.GetTotalMemory(false);
        var totalMemoryIncrease = finalMemory - initialMemory;

        // Assert
        Assert.False(exceptions.Any(),
            $"Exceptions occurred during stability test: {string.Join("; ", exceptions.Select(e => e.Message))}");

        Assert.True(operationCount > 0, "No operations were performed during stability test");

        // Memory should not grow unbounded
        Assert.True(totalMemoryIncrease < 100 * 1024 * 1024,
            $"Memory usage grew too much: {totalMemoryIncrease / 1024 / 1024}MB");

        // Performance should remain consistent
        var stats = _performanceMonitor.GetStatistics();
        if (stats.ContainsKey("MonitorManager.GetAllMonitors"))
        {
            var getAllStats = stats["MonitorManager.GetAllMonitors"];
            Assert.True(getAllStats.AverageDuration.TotalMilliseconds < 50,
                $"Average operation duration too high: {getAllStats.AverageDuration.TotalMilliseconds}ms");
        }

        _logger.LogInformation($"Stability test completed successfully: {operationCount} operations in {totalDuration.TotalMinutes:F2} minutes");
        _logger.LogInformation($"Final memory usage: {finalMemory / 1024}KB (increase: {totalMemoryIncrease / 1024}KB)");
    }

    [Fact(Timeout = 180000)] // 3 minutes timeout
    public async Task PerformanceMonitoringService_ExtendedUsage_ShouldMaintainPerformance()
    {
        // Arrange
        var testDuration = TimeSpan.FromMinutes(1.5); // Reduced for CI
        var startTime = DateTime.UtcNow;
        var exceptions = new ConcurrentBag<Exception>();
        var metricCount = 0;
        var counterCount = 0;

        _logger.LogInformation($"Starting performance monitoring extended test for {testDuration.TotalMinutes} minutes");

        // Act
        var tasks = new List<Task>();

        // Task 1: Continuously track metrics
        tasks.Add(Task.Run(async () =>
        {
            while (DateTime.UtcNow - startTime < testDuration)
            {
                try
                {
                    using (var tracker = _performanceMonitor.TrackMetric("ExtendedTest.ContinuousMetric"))
                    {
                        await Task.Delay(Random.Shared.Next(1, 10));
                    }
                    Interlocked.Increment(ref metricCount);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        }));

        // Task 2: Continuously increment counters
        tasks.Add(Task.Run(async () =>
        {
            while (DateTime.UtcNow - startTime < testDuration)
            {
                try
                {
                    _performanceMonitor.IncrementCounter("ExtendedTest.ContinuousCounter");
                    Interlocked.Increment(ref counterCount);
                    await Task.Delay(Random.Shared.Next(5, 20));
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        }));

        // Task 3: Periodically get statistics
        tasks.Add(Task.Run(async () =>
        {
            while (DateTime.UtcNow - startTime < testDuration)
            {
                try
                {
                    var stats = _performanceMonitor.GetStatistics();
                    var memoryUsage = _performanceMonitor.GetMemoryUsage();
                    var gcCounts = _performanceMonitor.GetGCCounts();

                    await Task.Delay(TimeSpan.FromSeconds(10));
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        }));

        // Task 4: Periodically reset statistics to prevent unbounded growth
        tasks.Add(Task.Run(async () =>
        {
            while (DateTime.UtcNow - startTime < testDuration)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(20));
                    _performanceMonitor.ResetStatistics();
                    _logger.LogInformation("Performance statistics reset during extended test");
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        }));

        await Task.WhenAll(tasks);
        var totalDuration = DateTime.UtcNow - startTime;

        // Assert
        Assert.False(exceptions.Any(),
            $"Exceptions occurred during extended performance test: {string.Join("; ", exceptions.Select(e => e.Message))}");

        Assert.True(metricCount > 0, "No metrics were recorded during extended test");
        Assert.True(counterCount > 0, "No counters were incremented during extended test");

        _logger.LogInformation($"Extended performance test completed: {metricCount} metrics, {counterCount} counters in {totalDuration.TotalMinutes:F2} minutes");
    }

    [Fact]
    public async Task MonitorManager_RapidCacheInvalidation_ShouldRemainStable()
    {
        // Arrange
        var monitorManager = new MonitorManager();
        const int iterations = 1000;
        var exceptions = new ConcurrentBag<Exception>();

        // Act
        var tasks = new List<Task>();

        // Task to continuously query monitors
        tasks.Add(Task.Run(async () =>
        {
            for (int i = 0; i < iterations; i++)
            {
                try
                {
                    var monitors = monitorManager.GetAllMonitors();
                    await Task.Delay(1);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        }));

        // Task to simulate rapid cache invalidation
        tasks.Add(Task.Run(async () =>
        {
            for (int i = 0; i < iterations / 10; i++)
            {
                try
                {
                    // Simulate cache invalidation by creating new MonitorManager instances
                    using var tempManager = new MonitorManager();
                    var monitors = tempManager.GetAllMonitors();

                    await Task.Delay(10);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }
        }));

        await Task.WhenAll(tasks);

        // Assert
        Assert.False(exceptions.Any(),
            $"Exceptions occurred during rapid invalidation test: {string.Join("; ", exceptions.Select(e => e.Message))}");

        var stats = _performanceMonitor.GetStatistics();
        if (stats.ContainsKey("MonitorManager.GetAllMonitors"))
        {
            var getAllStats = stats["MonitorManager.GetAllMonitors"];
            _logger.LogInformation($"Rapid invalidation test - Average duration: {getAllStats.AverageDuration.TotalMilliseconds:F2}ms, Operations: {getAllStats.SampleCount}");
        }
    }

    [Fact]
    public void MonitorManager_ResourceCleanup_ShouldNotLeaveHandles()
    {
        // Arrange
        var initialHandleCount = GetCurrentProcessHandleCount();
        const int iterations = 100;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            using (var monitorManager = new MonitorManager())
            {
                var monitors = monitorManager.GetAllMonitors();

                if (monitors.Any())
                {
                    var dpiInfo = monitorManager.GetMonitorDpi(monitors.First());
                }
            }

            // Force cleanup every 10 iterations
            if (i % 10 == 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        // Final cleanup
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var finalHandleCount = GetCurrentProcessHandleCount();
        var handleIncrease = finalHandleCount - initialHandleCount;

        // Assert
        Assert.True(handleIncrease < 50,
            $"Handle count increased by {handleIncrease}, potential handle leak");

        _logger.LogInformation($"Resource cleanup test - Initial handles: {initialHandleCount}, Final: {finalHandleCount}, Increase: {handleIncrease}");
    }

    private static int GetCurrentProcessHandleCount()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.HandleCount;
        }
        catch
        {
            return 0; // Fallback if unable to get handle count
        }
    }
}