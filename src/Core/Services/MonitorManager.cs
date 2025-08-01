using System.Drawing;
using System.Runtime.InteropServices;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.WindowsAPI;
using CursorPhobia.Core.Utilities;
using static CursorPhobia.Core.WindowsAPI.WindowsStructures;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Manages multi-monitor detection and information retrieval
/// </summary>
public class MonitorManager : IMonitorManager
{
    private readonly List<MonitorInfo> _cachedMonitors = new();
    private DateTime _lastCacheUpdate = DateTime.MinValue;
    private readonly TimeSpan _cacheTimeout = TimeSpan.FromSeconds(5);
    private readonly IMonitorConfigurationWatcher? _configurationWatcher;
    private readonly ILogger? _logger;
    private readonly IPerformanceMonitoringService? _performanceMonitor;
    private readonly ReaderWriterLockSlim _cacheLock = new();
    private bool _disposed;
    
    /// <summary>
    /// Event raised when monitor configuration changes are detected
    /// </summary>
    public event EventHandler<MonitorChangeEventArgs>? MonitorConfigurationChanged;
    
    /// <summary>
    /// Gets whether the monitor manager is currently monitoring for changes
    /// </summary>
    public bool IsMonitoring => _configurationWatcher?.IsMonitoring ?? false;
    
    /// <summary>
    /// Gets the time when the last monitor configuration change was detected
    /// </summary>
    public DateTime? LastConfigurationChangeDetected => _configurationWatcher?.LastChangeDetected;
    
    /// <summary>
    /// Default constructor for basic monitor management without change detection
    /// </summary>
    public MonitorManager()
    {
        // No configuration watcher - basic functionality only
    }
    
    /// <summary>
    /// Constructor with configuration watcher for automatic cache invalidation
    /// </summary>
    /// <param name="configurationWatcher">Watcher for monitor configuration changes</param>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <param name="performanceMonitor">Optional performance monitoring service</param>
    public MonitorManager(IMonitorConfigurationWatcher configurationWatcher, ILogger logger, IPerformanceMonitoringService? performanceMonitor = null)
    {
        _configurationWatcher = configurationWatcher ?? throw new ArgumentNullException(nameof(configurationWatcher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _performanceMonitor = performanceMonitor;
        
        // Subscribe to configuration changes
        _configurationWatcher.MonitorConfigurationChanged += OnMonitorConfigurationChanged;
    }
    
    /// <summary>
    /// Gets all connected monitors with their information
    /// </summary>
    /// <returns>List of monitor information</returns>
    public virtual List<MonitorInfo> GetAllMonitors()
    {
        using var tracker = _performanceMonitor?.TrackMetric("MonitorManager.GetAllMonitors");
        
        if (_disposed)
            throw new ObjectDisposedException(nameof(MonitorManager));
        
        try
        {
            _cacheLock.EnterReadLock();
            
            if (_disposed)
                throw new ObjectDisposedException(nameof(MonitorManager));
                
            // Check if cache needs refresh under read lock first
            bool needsRefresh = DateTime.Now - _lastCacheUpdate > _cacheTimeout;
            
            if (!needsRefresh)
            {
                _performanceMonitor?.IncrementCounter("MonitorManager.CacheHits");
                return new List<MonitorInfo>(_cachedMonitors);
            }
        }
        finally
        {
            _cacheLock.ExitReadLock();
        }
        
        // Need to refresh - upgrade to write lock
        _performanceMonitor?.IncrementCounter("MonitorManager.CacheMisses");
        
        if (_disposed)
            throw new ObjectDisposedException(nameof(MonitorManager));
        
        try
        {
            _cacheLock.EnterWriteLock();
            
            if (_disposed)
                throw new ObjectDisposedException(nameof(MonitorManager));
                
            // Double-check pattern - another thread might have refreshed while we waited
            if (DateTime.Now - _lastCacheUpdate > _cacheTimeout)
            {
                RefreshMonitors();
            }
            
            return new List<MonitorInfo>(_cachedMonitors);
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
    }
    
    /// <summary>
    /// Gets the monitor containing the specified point
    /// </summary>
    /// <param name="point">Point to check</param>
    /// <returns>Monitor containing the point, or null if not found</returns>
    public virtual MonitorInfo? GetMonitorContaining(Point point)
    {
        var monitors = GetAllMonitors();
        return monitors.FirstOrDefault(m => m.ContainsPoint(point));
    }
    
    /// <summary>
    /// Gets the monitor containing the specified rectangle
    /// </summary>
    /// <param name="windowRect">Rectangle to check</param>
    /// <returns>Monitor with largest intersection, or null if not found</returns>
    public virtual MonitorInfo? GetMonitorContaining(Rectangle windowRect)
    {
        var monitors = GetAllMonitors();
        MonitorInfo? bestMonitor = null;
        int largestIntersection = 0;
        
        foreach (var monitor in monitors)
        {
            var intersection = Rectangle.Intersect(windowRect, monitor.monitorBounds);
            int area = intersection.Width * intersection.Height;
            
            if (area > largestIntersection)
            {
                largestIntersection = area;
                bestMonitor = monitor;
            }
        }
        
        return bestMonitor;
    }
    
    /// <summary>
    /// Gets the primary monitor
    /// </summary>
    /// <returns>Primary monitor or null if not found</returns>
    public MonitorInfo? GetPrimaryMonitor()
    {
        var monitors = GetAllMonitors();
        return monitors.FirstOrDefault(m => m.isPrimary);
    }
    
    /// <summary>
    /// Gets all monitors adjacent to the specified monitor
    /// </summary>
    /// <param name="monitor">Source monitor</param>
    /// <returns>List of adjacent monitors</returns>
    public List<MonitorInfo> GetAdjacentMonitors(MonitorInfo monitor)
    {
        var allMonitors = GetAllMonitors();
        var adjacent = new List<MonitorInfo>();
        
        foreach (var other in allMonitors)
        {
            if (other.monitorHandle == monitor.monitorHandle) continue;
            
            // Check if monitors share an edge
            if (SharesEdge(monitor.monitorBounds, other.monitorBounds))
            {
                adjacent.Add(other);
            }
        }
        
        return adjacent;
    }
    
    /// <summary>
    /// Gets the monitor in the specified direction from the source monitor
    /// </summary>
    /// <param name="sourceMonitor">Source monitor</param>
    /// <param name="direction">Direction to look</param>
    /// <returns>Monitor in the specified direction, or null if not found</returns>
    public virtual MonitorInfo? GetMonitorInDirection(MonitorInfo sourceMonitor, EdgeDirection direction)
    {
        var allMonitors = GetAllMonitors();
        var sourceBounds = sourceMonitor.monitorBounds;
        
        return direction switch
        {
            EdgeDirection.Left => allMonitors.Where(m => m.monitorHandle != sourceMonitor.monitorHandle && 
                                                      m.monitorBounds.Right == sourceBounds.Left)
                                            .OrderBy(m => Math.Abs(m.monitorBounds.Y - sourceBounds.Y))
                                            .FirstOrDefault(),
            
            EdgeDirection.Right => allMonitors.Where(m => m.monitorHandle != sourceMonitor.monitorHandle && 
                                                       m.monitorBounds.Left == sourceBounds.Right)
                                             .OrderBy(m => Math.Abs(m.monitorBounds.Y - sourceBounds.Y))
                                             .FirstOrDefault(),
            
            EdgeDirection.Up => allMonitors.Where(m => m.monitorHandle != sourceMonitor.monitorHandle && 
                                                    m.monitorBounds.Bottom == sourceBounds.Top)
                                          .OrderBy(m => Math.Abs(m.monitorBounds.X - sourceBounds.X))
                                          .FirstOrDefault(),
            
            EdgeDirection.Down => allMonitors.Where(m => m.monitorHandle != sourceMonitor.monitorHandle && 
                                                      m.monitorBounds.Top == sourceBounds.Bottom)
                                            .OrderBy(m => Math.Abs(m.monitorBounds.X - sourceBounds.X))
                                            .FirstOrDefault(),
            
            _ => null
        };
    }
    
    /// <summary>
    /// Refreshes the monitor cache by enumerating all displays
    /// NOTE: This method must be called while holding a write lock on _cacheLock
    /// </summary>
    private void RefreshMonitors()
    {
        using var tracker = _performanceMonitor?.TrackMetric("MonitorManager.RefreshMonitors");
        
        try
        {
            _cachedMonitors.Clear();
            
            var monitors = new List<MonitorInfo>();
            var errorCount = 0;
            var callback = new MonitorEnumProc(EnumerateMonitorsCallback);
            
            bool EnumerateMonitorsCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
            {
                try
                {
                    var monitorInfo = MONITORINFO.Create();
                    if (User32.GetMonitorInfo(hMonitor, ref monitorInfo))
                    {
                        var monitor = new MonitorInfo(
                            hMonitor,
                            monitorInfo.rcMonitor.ToRectangle(),
                            monitorInfo.rcWork.ToRectangle(),
                            (monitorInfo.dwFlags & MonitorFlags.MONITORINFOF_PRIMARY) != 0
                        );
                        
                        monitors.Add(monitor);
                    }
                    else
                    {
                        errorCount++;
                        _logger?.LogWarning($"Failed to get monitor info for handle {hMonitor}");
                    }
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _logger?.LogWarning($"Error enumerating monitor {hMonitor}: {ex.Message}");
                }
                
                return true; // Continue enumeration
            }
            
            bool enumResult = User32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
            if (!enumResult)
            {
                _logger?.LogWarning("EnumDisplayMonitors returned false - monitor enumeration may be incomplete");
                _performanceMonitor?.IncrementCounter("MonitorManager.EnumerationFailures");
            }
            
            _cachedMonitors.AddRange(monitors);
            _lastCacheUpdate = DateTime.Now;
            
            _performanceMonitor?.IncrementCounter("MonitorManager.CacheRefreshes");
            _performanceMonitor?.IncrementCounter("MonitorManager.MonitorsDetected", monitors.Count);
            
            if (errorCount > 0)
            {
                _performanceMonitor?.IncrementCounter("MonitorManager.EnumerationErrors", errorCount);
            }
            
            _logger?.LogDebug($"Monitor cache refreshed with {monitors.Count} monitors ({errorCount} errors)");
        }
        catch (Exception ex)
        {
            _performanceMonitor?.IncrementCounter("MonitorManager.CriticalRefreshErrors");
            _logger?.LogError($"Critical error during monitor refresh: {ex.Message}");
            // Don't update _lastCacheUpdate on error so we'll retry on next call
        }
    }
    
    /// <summary>
    /// Checks if two rectangles share an edge
    /// </summary>
    /// <param name="rect1">First rectangle</param>
    /// <param name="rect2">Second rectangle</param>
    /// <returns>True if rectangles share an edge</returns>
    private static bool SharesEdge(Rectangle rect1, Rectangle rect2)
    {
        // Check vertical edges
        if ((rect1.Right == rect2.Left || rect1.Left == rect2.Right) &&
            !(rect1.Bottom <= rect2.Top || rect1.Top >= rect2.Bottom))
        {
            return true;
        }
        
        // Check horizontal edges
        if ((rect1.Bottom == rect2.Top || rect1.Top == rect2.Bottom) &&
            !(rect1.Right <= rect2.Left || rect1.Left >= rect2.Right))
        {
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Gets DPI information for the specified monitor
    /// </summary>
    /// <param name="monitor">Monitor to get DPI for</param>
    /// <returns>DPI information for the monitor</returns>
    public virtual DpiInfo GetMonitorDpi(MonitorInfo monitor)
    {
        using var tracker = _performanceMonitor?.TrackMetric("MonitorManager.GetMonitorDpi");
        
        try
        {
            var dpiInfo = DpiInfo.FromMonitor(monitor.monitorHandle);
            _performanceMonitor?.IncrementCounter("MonitorManager.DpiQueriesSuccess");
            return dpiInfo;
        }
        catch (Exception ex)
        {
            _performanceMonitor?.IncrementCounter("MonitorManager.DpiQueriesFailure");
            _logger?.LogWarning($"Failed to get DPI for monitor {monitor.monitorHandle}: {ex.Message}");
            return new DpiInfo(); // Return default DPI on error
        }
    }
    
    /// <summary>
    /// Gets DPI information for the monitor containing the specified point
    /// </summary>
    /// <param name="point">Point to check</param>
    /// <returns>DPI information for the containing monitor, or default if not found</returns>
    public virtual DpiInfo GetDpiForPoint(Point point)
    {
        var monitor = GetMonitorContaining(point);
        return monitor != null ? GetMonitorDpi(monitor) : new DpiInfo();
    }
    
    /// <summary>
    /// Gets DPI information for the monitor containing the specified rectangle
    /// </summary>
    /// <param name="windowRect">Rectangle to check</param>
    /// <returns>DPI information for the containing monitor, or default if not found</returns>
    public virtual DpiInfo GetDpiForRectangle(Rectangle windowRect)
    {
        var monitor = GetMonitorContaining(windowRect);
        return monitor != null ? GetMonitorDpi(monitor) : new DpiInfo();
    }
    
    /// <summary>
    /// Starts monitoring for display configuration changes
    /// </summary>
    public void StartMonitoring()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MonitorManager));
            
        if (_configurationWatcher == null)
        {
            _logger?.LogWarning("Cannot start monitoring: No configuration watcher available");
            return;
        }
        
        _configurationWatcher.StartMonitoring();
        _logger?.LogInformation("MonitorManager started monitoring for configuration changes");
    }
    
    /// <summary>
    /// Stops monitoring for display configuration changes
    /// </summary>
    public void StopMonitoring()
    {
        if (_disposed || _configurationWatcher == null)
            return;
            
        _configurationWatcher.StopMonitoring();
        _logger?.LogInformation("MonitorManager stopped monitoring for configuration changes");
    }
    
    /// <summary>
    /// Raises the MonitorConfigurationChanged event
    /// </summary>
    /// <param name="eventArgs">Event arguments</param>
    protected virtual void OnMonitorConfigurationChanged(MonitorChangeEventArgs eventArgs)
    {
        if (_disposed)
            return;
            
        try
        {
            _cacheLock.EnterWriteLock();
            
            if (_disposed)
                return;
            
            // Invalidate cache immediately
            _lastCacheUpdate = DateTime.MinValue;
            _logger?.LogInformation($"Monitor cache invalidated due to configuration change: {eventArgs.ChangeType}");
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error invalidating monitor cache: {ex.Message}");
        }
        finally
        {
            _cacheLock.ExitWriteLock();
        }
        
        try
        {
            // Forward the event to subscribers outside of lock to prevent deadlocks
            MonitorConfigurationChanged?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error notifying monitor configuration change subscribers: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Event handler for monitor configuration changes from watcher
    /// </summary>
    private void OnMonitorConfigurationChanged(object? sender, MonitorChangeEventArgs e)
    {
        OnMonitorConfigurationChanged(e);
    }
    
    /// <summary>
    /// Disposes the monitor manager
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
            try
            {
                _cacheLock.EnterWriteLock();
                
                try
                {
                    if (_configurationWatcher != null)
                    {
                        _configurationWatcher.MonitorConfigurationChanged -= OnMonitorConfigurationChanged;
                        _configurationWatcher.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError($"Error during MonitorManager disposal: {ex.Message}");
                }
                
                _disposed = true;
            }
            finally
            {
                _cacheLock.ExitWriteLock();
                _cacheLock.Dispose();
            }
        }
    }
}

/// <summary>
/// Directions for monitor edge detection
/// </summary>
public enum EdgeDirection
{
    Left,
    Right,
    Up,
    Down
}

/// <summary>
/// Monitor information flags
/// </summary>
public static class MonitorFlags
{
    public const uint MONITORINFOF_PRIMARY = 0x00000001;
}