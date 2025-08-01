using CursorPhobia.Core.Models;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Interface for monitoring display configuration changes
/// </summary>
public interface IMonitorConfigurationWatcher : IDisposable
{
    /// <summary>
    /// Event raised when monitor configuration changes are detected
    /// </summary>
    event EventHandler<MonitorChangeEventArgs>? MonitorConfigurationChanged;
    
    /// <summary>
    /// Gets whether the watcher is currently monitoring for changes
    /// </summary>
    bool IsMonitoring { get; }
    
    /// <summary>
    /// Starts monitoring for display configuration changes
    /// </summary>
    void StartMonitoring();
    
    /// <summary>
    /// Stops monitoring for display configuration changes
    /// </summary>
    void StopMonitoring();
    
    /// <summary>
    /// Manually triggers a check for configuration changes
    /// </summary>
    /// <returns>True if changes were detected and event was raised</returns>
    bool CheckForChanges();
    
    /// <summary>
    /// Gets the time when the last change was detected
    /// </summary>
    DateTime? LastChangeDetected { get; }
    
    /// <summary>
    /// Gets the interval (in milliseconds) at which changes are checked.
    /// Returns null if using event-driven detection only.
    /// </summary>
    int? PollingIntervalMs { get; }
}