using System.Collections.Concurrent;
using System.Drawing;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;
using Xunit;

namespace CursorPhobia.Tests;

/// <summary>
/// Tests for thread safety in multi-monitor components
/// </summary>
public class ThreadSafetyTests
{
    private readonly TestLogger _logger;
    private readonly PerformanceMonitoringService _performanceMonitor;

    public ThreadSafetyTests()
    {
        _logger = new TestLogger();
        _performanceMonitor = new PerformanceMonitoringService(_logger);
    }

    [Fact]
    public async Task MonitorManager_ConcurrentReads_ShouldNotBlock()
    {
        // Arrange
        var monitorManager = new MonitorManager();
        const int threadCount = 10;
        const int operationsPerThread = 100;
        var results = new ConcurrentBag<List<MonitorInfo>>();
        var exceptions = new ConcurrentBag<Exception>();

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(async _ =>
        {
            try
            {
                for (int i = 0; i < operationsPerThread; i++)
                {
                    var monitors = monitorManager.GetAllMonitors();
                    results.Add(monitors);
                    
                    // Small delay to allow interleaving
                    await Task.Delay(1);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        await Task.WhenAll(tasks);

        // Assert
        Assert.False(exceptions.Any(), $"Exceptions occurred: {string.Join(", ", exceptions.Select(e => e.Message))}");
        Assert.Equal(threadCount * operationsPerThread, results.Count);
        
        // All results should be consistent (same monitor count)
        if (results.Any())
        {
            var firstResult = results.First();
            Assert.True(results.All(r => r.Count == firstResult.Count), 
                "Monitor counts should be consistent across concurrent reads");
        }
    }

    [Fact]
    public async Task MonitorManager_ConcurrentDpiQueries_ShouldBeThreadSafe()
    {
        // Arrange
        var monitorManager = new MonitorManager();
        var monitors = monitorManager.GetAllMonitors();
        
        if (!monitors.Any())
        {
            // Skip test if no monitors available
            return;
        }

        const int threadCount = 8;
        const int operationsPerThread = 50;
        var testMonitor = monitors.First();
        var results = new ConcurrentBag<DpiInfo>();
        var exceptions = new ConcurrentBag<Exception>();

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(async _ =>
        {
            try
            {
                for (int i = 0; i < operationsPerThread; i++)
                {
                    var dpiInfo = monitorManager.GetMonitorDpi(testMonitor);
                    results.Add(dpiInfo);
                    
                    await Task.Delay(1);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        await Task.WhenAll(tasks);

        // Assert
        Assert.False(exceptions.Any(), $"Exceptions occurred: {string.Join(", ", exceptions.Select(e => e.Message))}");
        Assert.Equal(threadCount * operationsPerThread, results.Count);
        
        // All DPI results should be consistent for the same monitor
        if (results.Any())
        {
            var firstResult = results.First();
            Assert.True(results.All(r => r.DpiX == firstResult.DpiX && r.DpiY == firstResult.DpiY), 
                "DPI values should be consistent for the same monitor");
        }
    }

    [Fact]
    public async Task PerformanceMonitoringService_ConcurrentOperations_ShouldBeThreadSafe()
    {
        // Arrange
        const int threadCount = 10;
        const int operationsPerThread = 100;
        var exceptions = new ConcurrentBag<Exception>();

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(async threadId =>
        {
            try
            {
                for (int i = 0; i < operationsPerThread; i++)
                {
                    // Mix of different operations
                    _performanceMonitor.IncrementCounter($"Thread{threadId}.Counter");
                    
                    using (var tracker = _performanceMonitor.TrackMetric($"Thread{threadId}.Metric"))
                    {
                        await Task.Delay(1);
                    }
                    
                    var stats = _performanceMonitor.GetStatistics();
                    Assert.NotNull(stats);
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        await Task.WhenAll(tasks);

        // Assert
        Assert.False(exceptions.Any(), $"Exceptions occurred: {string.Join(", ", exceptions.Select(e => e.Message))}");
        
        var finalStats = _performanceMonitor.GetStatistics();
        
        // Should have metrics for all threads
        for (int i = 0; i < threadCount; i++)
        {
            Assert.True(finalStats.ContainsKey($"Thread{i}.Counter"), $"Missing counter for thread {i}");
            Assert.True(finalStats.ContainsKey($"Thread{i}.Metric"), $"Missing metric for thread {i}");
            
            var counterStats = finalStats[$"Thread{i}.Counter"];
            Assert.Equal(operationsPerThread, counterStats.CounterValue);
        }
    }

    [Fact]
    public async Task MonitorManager_StressTest_ShouldMaintainPerformance()
    {
        // Arrange
        var monitorManager = new MonitorManager();
        const int threadCount = 20;
        const int operationsPerThread = 200;
        var exceptions = new ConcurrentBag<Exception>();
        var startTime = DateTime.UtcNow;

        // Act
        var tasks = Enumerable.Range(0, threadCount).Select(async threadId =>
        {
            try
            {
                for (int i = 0; i < operationsPerThread; i++)
                {
                    var monitors = monitorManager.GetAllMonitors();
                    
                    if (monitors.Any())
                    {
                        var firstMonitor = monitors.First();
                        var dpiInfo = monitorManager.GetMonitorDpi(firstMonitor);
                        
                        // Query different monitor methods
                        var primary = monitorManager.GetPrimaryMonitor();
                        var adjacent = monitorManager.GetAdjacentMonitors(firstMonitor);
                    }
                    
                    // Small delay to allow some interleaving
                    if (i % 10 == 0)
                    {
                        await Task.Delay(1);
                    }
                }
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        await Task.WhenAll(tasks);
        var totalTime = DateTime.UtcNow - startTime;

        // Assert
        Assert.False(exceptions.Any(), $"Exceptions occurred: {string.Join(", ", exceptions.Select(e => e.Message))}");
        
        var stats = _performanceMonitor.GetStatistics();
        
        // Performance assertions
        if (stats.ContainsKey("MonitorManager.GetAllMonitors"))
        {
            var getAllStats = stats["MonitorManager.GetAllMonitors"];
            var avgDuration = getAllStats.AverageDuration;
            
            // Average operation should be under 10ms even under stress
            Assert.True(avgDuration.TotalMilliseconds < 10, 
                $"Average GetAllMonitors duration too high: {avgDuration.TotalMilliseconds}ms");
        }
        
        // Cache hit ratio should be reasonable under stress
        var cacheHits = stats.ContainsKey("MonitorManager.CacheHits") ? stats["MonitorManager.CacheHits"].CounterValue : 0;
        var cacheMisses = stats.ContainsKey("MonitorManager.CacheMisses") ? stats["MonitorManager.CacheMisses"].CounterValue : 0;
        
        if (cacheHits + cacheMisses > 0)
        {
            var hitRatio = (double)cacheHits / (cacheHits + cacheMisses);
            Assert.True(hitRatio > 0.8, $"Cache hit ratio too low: {hitRatio:P2}");
        }
        
        _logger.LogInformation($"Stress test completed in {totalTime.TotalSeconds:F2}s with {threadCount} threads and {operationsPerThread} operations each");
    }

    [Fact]
    public async Task MonitorManager_ConcurrentDisposal_ShouldBeThreadSafe()
    {
        // Arrange
        var exceptions = new ConcurrentBag<Exception>();
        const int testRuns = 10;

        for (int run = 0; run < testRuns; run++)
        {
            var monitorManager = new MonitorManager();
            const int readerThreads = 5;
            
            // Act - Start reader threads
            var readerTasks = Enumerable.Range(0, readerThreads).Select(async _ =>
            {
                try
                {
                    while (true)
                    {
                        var monitors = monitorManager.GetAllMonitors();
                        await Task.Delay(1);
                    }
                }
                catch (ObjectDisposedException)
                {
                    // Expected when dispose happens
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
            
            // Let readers run for a bit
            await Task.Delay(50);
            
            // Dispose while readers are active
            try
            {
                monitorManager.Dispose();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
            
            // Wait a bit more to ensure all readers exit
            await Task.Delay(100);
        }

        // Assert
        Assert.False(exceptions.Any(), $"Unexpected exceptions occurred: {string.Join(", ", exceptions.Select(e => e.Message))}");
    }

    [Fact]
    public async Task PerformanceMonitoringService_MemoryStressTest_ShouldNotLeak()
    {
        // Arrange
        var initialMemory = GC.GetTotalMemory(true);
        const int iterations = 1000;
        const int metricsPerIteration = 100;

        // Act
        for (int i = 0; i < iterations; i++)
        {
            for (int j = 0; j < metricsPerIteration; j++)
            {
                using (var tracker = _performanceMonitor.TrackMetric($"MemoryTest.Iteration{i}.Metric{j}"))
                {
                    await Task.Delay(1);
                }
                
                _performanceMonitor.IncrementCounter($"MemoryTest.Iteration{i}.Counter{j}");
            }
            
            // Periodically reset to prevent unbounded growth
            if (i % 100 == 0)
            {
                _performanceMonitor.ResetStatistics();
            }
        }

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var finalMemory = GC.GetTotalMemory(false);
        var memoryIncrease = finalMemory - initialMemory;

        // Assert
        // Memory increase should be reasonable (less than 10MB for this test)
        Assert.True(memoryIncrease < 10 * 1024 * 1024, 
            $"Memory usage increased by {memoryIncrease / 1024 / 1024}MB, potential memory leak");
        
        _logger.LogInformation($"Memory test: Initial={initialMemory / 1024}KB, Final={finalMemory / 1024}KB, Increase={memoryIncrease / 1024}KB");
    }
}