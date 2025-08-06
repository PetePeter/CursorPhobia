using System.Diagnostics;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Interface for performance monitoring service that tracks system metrics
/// </summary>
public interface IPerformanceMonitoringService
{
    /// <summary>
    /// Starts tracking a performance metric with the given name
    /// </summary>
    /// <param name="metricName">Name of the metric to track</param>
    /// <returns>A disposable tracking context</returns>
    IDisposable TrackMetric(string metricName);

    /// <summary>
    /// Records a metric value with the given name and duration
    /// </summary>
    /// <param name="metricName">Name of the metric</param>
    /// <param name="duration">Duration to record</param>
    void RecordMetric(string metricName, TimeSpan duration);

    /// <summary>
    /// Records a counter metric (increments by 1)
    /// </summary>
    /// <param name="counterName">Name of the counter</param>
    void IncrementCounter(string counterName);

    /// <summary>
    /// Records a counter metric with a specific value
    /// </summary>
    /// <param name="counterName">Name of the counter</param>
    /// <param name="value">Value to add to counter</param>
    void IncrementCounter(string counterName, long value);

    /// <summary>
    /// Gets the current statistics for all tracked metrics
    /// </summary>
    /// <returns>Dictionary of metric statistics</returns>
    Dictionary<string, PerformanceMetricStats> GetStatistics();

    /// <summary>
    /// Gets statistics for a specific metric
    /// </summary>
    /// <param name="metricName">Name of the metric</param>
    /// <returns>Metric statistics or null if not found</returns>
    PerformanceMetricStats? GetStatistics(string metricName);

    /// <summary>
    /// Resets all performance statistics
    /// </summary>
    void ResetStatistics();

    /// <summary>
    /// Gets the current memory usage in bytes
    /// </summary>
    /// <returns>Memory usage in bytes</returns>
    long GetMemoryUsage();

    /// <summary>
    /// Gets the current GC generation counts
    /// </summary>
    /// <returns>Array of GC counts by generation</returns>
    int[] GetGCCounts();
}

/// <summary>
/// Statistics for a performance metric
/// </summary>
public class PerformanceMetricStats
{
    /// <summary>
    /// Name of the metric
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Total number of samples recorded
    /// </summary>
    public long SampleCount { get; set; }

    /// <summary>
    /// Total duration across all samples
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// Average duration per sample
    /// </summary>
    public TimeSpan AverageDuration => SampleCount > 0 ? TimeSpan.FromTicks(TotalDuration.Ticks / SampleCount) : TimeSpan.Zero;

    /// <summary>
    /// Minimum duration recorded
    /// </summary>
    public TimeSpan MinDuration { get; set; } = TimeSpan.MaxValue;

    /// <summary>
    /// Maximum duration recorded
    /// </summary>
    public TimeSpan MaxDuration { get; set; } = TimeSpan.MinValue;

    /// <summary>
    /// When the first sample was recorded
    /// </summary>
    public DateTime FirstSample { get; set; }

    /// <summary>
    /// When the last sample was recorded
    /// </summary>
    public DateTime LastSample { get; set; }

    /// <summary>
    /// Counter value for counter-type metrics
    /// </summary>
    public long CounterValue { get; set; }
}