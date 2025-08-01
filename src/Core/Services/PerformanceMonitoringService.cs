using System.Collections.Concurrent;
using System.Diagnostics;
using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Thread-safe performance monitoring service for tracking system metrics
/// </summary>
public class PerformanceMonitoringService : IPerformanceMonitoringService
{
    private readonly ConcurrentDictionary<string, PerformanceMetricStats> _metrics = new();
    private readonly ILogger? _logger;
    private readonly object _resetLock = new();
    
    /// <summary>
    /// Creates a new performance monitoring service
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic output</param>
    public PerformanceMonitoringService(ILogger? logger = null)
    {
        _logger = logger;
    }
    
    /// <summary>
    /// Starts tracking a performance metric with the given name
    /// </summary>
    /// <param name="metricName">Name of the metric to track</param>
    /// <returns>A disposable tracking context</returns>
    public IDisposable TrackMetric(string metricName)
    {
        return new MetricTracker(this, metricName);
    }
    
    /// <summary>
    /// Records a metric value with the given name and duration
    /// </summary>
    /// <param name="metricName">Name of the metric</param>
    /// <param name="duration">Duration to record</param>
    public void RecordMetric(string metricName, TimeSpan duration)
    {
        if (string.IsNullOrWhiteSpace(metricName))
            return;
            
        try
        {
            _metrics.AddOrUpdate(metricName, 
                // Add new metric
                _ => new PerformanceMetricStats
                {
                    Name = metricName,
                    SampleCount = 1,
                    TotalDuration = duration,
                    MinDuration = duration,
                    MaxDuration = duration,
                    FirstSample = DateTime.UtcNow,
                    LastSample = DateTime.UtcNow
                },
                // Update existing metric
                (_, existing) =>
                {
                    var updated = new PerformanceMetricStats
                    {
                        Name = existing.Name,
                        SampleCount = existing.SampleCount + 1,
                        TotalDuration = existing.TotalDuration + duration,
                        MinDuration = duration < existing.MinDuration ? duration : existing.MinDuration,
                        MaxDuration = duration > existing.MaxDuration ? duration : existing.MaxDuration,
                        FirstSample = existing.FirstSample,
                        LastSample = DateTime.UtcNow,
                        CounterValue = existing.CounterValue
                    };
                    return updated;
                });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Error recording metric '{metricName}': {ex.Message}");
        }
    }
    
    /// <summary>
    /// Records a counter metric (increments by 1)
    /// </summary>
    /// <param name="counterName">Name of the counter</param>
    public void IncrementCounter(string counterName)
    {
        IncrementCounter(counterName, 1);
    }
    
    /// <summary>
    /// Records a counter metric with a specific value
    /// </summary>
    /// <param name="counterName">Name of the counter</param>
    /// <param name="value">Value to add to counter</param>
    public void IncrementCounter(string counterName, long value)
    {
        if (string.IsNullOrWhiteSpace(counterName))
            return;
            
        try
        {
            _metrics.AddOrUpdate(counterName,
                // Add new counter
                _ => new PerformanceMetricStats
                {
                    Name = counterName,
                    SampleCount = 1,
                    CounterValue = value,
                    FirstSample = DateTime.UtcNow,
                    LastSample = DateTime.UtcNow
                },
                // Update existing counter
                (_, existing) =>
                {
                    var updated = new PerformanceMetricStats
                    {
                        Name = existing.Name,
                        SampleCount = existing.SampleCount + 1,
                        TotalDuration = existing.TotalDuration,
                        MinDuration = existing.MinDuration,
                        MaxDuration = existing.MaxDuration,
                        FirstSample = existing.FirstSample,
                        LastSample = DateTime.UtcNow,
                        CounterValue = existing.CounterValue + value
                    };
                    return updated;
                });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Error incrementing counter '{counterName}': {ex.Message}");
        }
    }
    
    /// <summary>
    /// Gets the current statistics for all tracked metrics
    /// </summary>
    /// <returns>Dictionary of metric statistics</returns>
    public Dictionary<string, PerformanceMetricStats> GetStatistics()
    {
        try
        {
            return _metrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Error getting statistics: {ex.Message}");
            return new Dictionary<string, PerformanceMetricStats>();
        }
    }
    
    /// <summary>
    /// Gets statistics for a specific metric
    /// </summary>
    /// <param name="metricName">Name of the metric</param>
    /// <returns>Metric statistics or null if not found</returns>
    public PerformanceMetricStats? GetStatistics(string metricName)
    {
        if (string.IsNullOrWhiteSpace(metricName))
            return null;
            
        return _metrics.TryGetValue(metricName, out var stats) ? stats : null;
    }
    
    /// <summary>
    /// Resets all performance statistics
    /// </summary>
    public void ResetStatistics()
    {
        lock (_resetLock)
        {
            try
            {
                _metrics.Clear();
                _logger?.LogInformation("Performance statistics reset");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Error resetting statistics: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Gets the current memory usage in bytes
    /// </summary>
    /// <returns>Memory usage in bytes</returns>
    public long GetMemoryUsage()
    {
        try
        {
            return GC.GetTotalMemory(false);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Error getting memory usage: {ex.Message}");
            return 0;
        }
    }
    
    /// <summary>
    /// Gets the current GC generation counts
    /// </summary>
    /// <returns>Array of GC counts by generation</returns>
    public int[] GetGCCounts()
    {
        try
        {
            return new[]
            {
                GC.CollectionCount(0),
                GC.CollectionCount(1),
                GC.CollectionCount(2)
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Error getting GC counts: {ex.Message}");
            return new int[3];
        }
    }
    
    /// <summary>
    /// Internal metric tracker that records duration on disposal
    /// </summary>
    private class MetricTracker : IDisposable
    {
        private readonly PerformanceMonitoringService _service;
        private readonly string _metricName;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;
        
        public MetricTracker(PerformanceMonitoringService service, string metricName)
        {
            _service = service;
            _metricName = metricName;
            _stopwatch = Stopwatch.StartNew();
        }
        
        public void Dispose()
        {
            if (_disposed)
                return;
                
            _stopwatch.Stop();
            _service.RecordMetric(_metricName, _stopwatch.Elapsed);
            _disposed = true;
        }
    }
}