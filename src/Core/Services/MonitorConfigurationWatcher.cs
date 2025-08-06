using System.Runtime.InteropServices;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Utilities;
using Microsoft.Win32;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Service that monitors for display configuration changes and raises events when changes are detected
/// </summary>
public class MonitorConfigurationWatcher : IMonitorConfigurationWatcher
{
    private readonly IMonitorManager _monitorManager;
    private readonly ILogger _logger;
    private readonly object _lockObject = new();

    private bool _isMonitoring;
    private bool _disposed;
    private List<MonitorInfo> _lastKnownConfiguration = new();

    /// <summary>
    /// Event raised when monitor configuration changes are detected
    /// </summary>
    public event EventHandler<MonitorChangeEventArgs>? MonitorConfigurationChanged;

    /// <summary>
    /// Gets whether the watcher is currently monitoring for changes
    /// </summary>
    public bool IsMonitoring
    {
        get
        {
            lock (_lockObject)
            {
                return _isMonitoring && !_disposed;
            }
        }
    }

    /// <summary>
    /// Gets the time when the last change was detected
    /// </summary>
    public DateTime? LastChangeDetected { get; private set; }

    /// <summary>
    /// Gets the interval (in milliseconds) at which changes are checked.
    /// Returns null since this implementation uses event-driven detection.
    /// </summary>
    public int? PollingIntervalMs => null;

    /// <summary>
    /// Creates a new monitor configuration watcher
    /// </summary>
    /// <param name="monitorManager">Monitor manager for getting configuration</param>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null</exception>
    public MonitorConfigurationWatcher(IMonitorManager monitorManager, ILogger logger)
    {
        _monitorManager = monitorManager ?? throw new ArgumentNullException(nameof(monitorManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Starts monitoring for display configuration changes
    /// </summary>
    public void StartMonitoring()
    {
        lock (_lockObject)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(MonitorConfigurationWatcher));

            if (_isMonitoring)
                return;

            try
            {
                // Store initial configuration
                UpdateLastKnownConfiguration();

                // Subscribe to Windows system events
                SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

                _isMonitoring = true;
                _logger.LogInformation("MonitorConfigurationWatcher started monitoring");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to start monitor configuration watching: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Stops monitoring for display configuration changes
    /// </summary>
    public void StopMonitoring()
    {
        lock (_lockObject)
        {
            if (!_isMonitoring || _disposed)
                return;

            try
            {
                // Unsubscribe from Windows system events
                SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

                _isMonitoring = false;
                _logger.LogInformation("MonitorConfigurationWatcher stopped monitoring");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while stopping monitor configuration watching: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Manually triggers a check for configuration changes
    /// </summary>
    /// <returns>True if changes were detected and event was raised</returns>
    public bool CheckForChanges()
    {
        lock (_lockObject)
        {
            if (_disposed)
                return false;

            try
            {
                var currentConfiguration = _monitorManager.GetAllMonitors();
                var changeArgs = AnalyzeConfigurationChange(_lastKnownConfiguration, currentConfiguration);

                if (changeArgs != null)
                {
                    _lastKnownConfiguration = new List<MonitorInfo>(currentConfiguration);
                    LastChangeDetected = DateTime.UtcNow;

                    // Raise event on a background thread to avoid blocking
                    Task.Run(() => RaiseConfigurationChanged(changeArgs));

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error checking for monitor configuration changes: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Event handler for Windows display settings changes
    /// </summary>
    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        try
        {
            _logger.LogDebug("Display settings changed event received");

            // Add a small delay to ensure Windows has finished updating
            Task.Delay(100).ContinueWith(_ => CheckForChanges());
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error handling display settings change: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates the last known configuration with current monitor setup
    /// </summary>
    private void UpdateLastKnownConfiguration()
    {
        try
        {
            _lastKnownConfiguration = _monitorManager.GetAllMonitors();
            _logger.LogDebug($"Updated last known configuration: {_lastKnownConfiguration.Count} monitors");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to update last known configuration: {ex.Message}");
            _lastKnownConfiguration = new List<MonitorInfo>();
        }
    }

    /// <summary>
    /// Analyzes changes between two monitor configurations
    /// </summary>
    /// <param name="previous">Previous configuration</param>
    /// <param name="current">Current configuration</param>
    /// <returns>Change event args if changes detected, null otherwise</returns>
    private static MonitorChangeEventArgs? AnalyzeConfigurationChange(
        List<MonitorInfo> previous,
        List<MonitorInfo> current)
    {
        // Quick check - if counts are different, definitely a change
        bool countChanged = previous.Count != current.Count;

        // Check for added/removed monitors
        var previousHandles = previous.Select(m => m.monitorHandle).ToHashSet();
        var currentHandles = current.Select(m => m.monitorHandle).ToHashSet();

        var addedHandles = currentHandles.Except(previousHandles).ToList();
        var removedHandles = previousHandles.Except(currentHandles).ToList();

        // Check for modified monitors (same handle, different properties)
        var modifiedMonitors = new List<(MonitorInfo Previous, MonitorInfo Current)>();
        foreach (var currentMonitor in current)
        {
            var previousMonitor = previous.FirstOrDefault(p => p.monitorHandle == currentMonitor.monitorHandle);
            if (previousMonitor != null && !MonitorsEqual(previousMonitor, currentMonitor))
            {
                modifiedMonitors.Add((previousMonitor, currentMonitor));
            }
        }

        // Check for primary monitor changes
        var previousPrimary = previous.FirstOrDefault(m => m.isPrimary);
        var currentPrimary = current.FirstOrDefault(m => m.isPrimary);
        bool primaryChanged = previousPrimary?.monitorHandle != currentPrimary?.monitorHandle;

        // Determine if any changes occurred
        bool hasChanges = countChanged || addedHandles.Count > 0 || removedHandles.Count > 0 ||
                         modifiedMonitors.Count > 0 || primaryChanged;

        if (!hasChanges)
            return null;

        // Determine change type
        var changeType = DetermineChangeType(addedHandles.Count > 0, removedHandles.Count > 0,
                                           modifiedMonitors.Count > 0, primaryChanged);

        return new MonitorChangeEventArgs(changeType, previous.AsReadOnly(), current.AsReadOnly());
    }

    /// <summary>
    /// Determines the type of monitor configuration change
    /// </summary>
    private static MonitorChangeType DetermineChangeType(bool hasAdded, bool hasRemoved,
                                                       bool hasModified, bool primaryChanged)
    {
        int changeCount = (hasAdded ? 1 : 0) + (hasRemoved ? 1 : 0) +
                         (hasModified ? 1 : 0) + (primaryChanged ? 1 : 0);

        if (changeCount > 1)
            return MonitorChangeType.ComplexChange;

        if (hasAdded)
            return MonitorChangeType.MonitorsAdded;

        if (hasRemoved)
            return MonitorChangeType.MonitorsRemoved;

        if (primaryChanged)
            return MonitorChangeType.PrimaryMonitorChanged;

        if (hasModified)
            return MonitorChangeType.MonitorsRepositioned;

        return MonitorChangeType.Unknown;
    }

    /// <summary>
    /// Checks if two monitor configurations are equal
    /// </summary>
    private static bool MonitorsEqual(MonitorInfo monitor1, MonitorInfo monitor2)
    {
        return monitor1.monitorHandle == monitor2.monitorHandle &&
               monitor1.monitorBounds == monitor2.monitorBounds &&
               monitor1.workAreaBounds == monitor2.workAreaBounds &&
               monitor1.isPrimary == monitor2.isPrimary;
    }

    /// <summary>
    /// Raises the MonitorConfigurationChanged event safely
    /// </summary>
    private void RaiseConfigurationChanged(MonitorChangeEventArgs args)
    {
        try
        {
            _logger.LogInformation($"Monitor configuration changed: {args.ChangeType}");
            MonitorConfigurationChanged?.Invoke(this, args);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error raising MonitorConfigurationChanged event: {ex.Message}");
        }
    }

    /// <summary>
    /// Disposes the monitor configuration watcher
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected dispose method
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
            return;

        if (disposing)
        {
            lock (_lockObject)
            {
                try
                {
                    StopMonitoring();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error during MonitorConfigurationWatcher disposal: {ex.Message}");
                }

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Finalizer to ensure resources are cleaned up
    /// </summary>
    ~MonitorConfigurationWatcher()
    {
        Dispose(false);
    }
}