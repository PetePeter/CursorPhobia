using System.Drawing;
using System.Runtime.InteropServices;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.WindowsAPI;
using static CursorPhobia.Core.WindowsAPI.WindowsStructures;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Manages multi-monitor detection and information retrieval
/// </summary>
public class MonitorManager
{
    private readonly List<MonitorInfo> _cachedMonitors = new();
    private DateTime _lastCacheUpdate = DateTime.MinValue;
    private readonly TimeSpan _cacheTimeout = TimeSpan.FromSeconds(5);
    
    /// <summary>
    /// Gets all connected monitors with their information
    /// </summary>
    /// <returns>List of monitor information</returns>
    public List<MonitorInfo> GetAllMonitors()
    {
        if (DateTime.Now - _lastCacheUpdate > _cacheTimeout)
        {
            RefreshMonitors();
        }
        
        return new List<MonitorInfo>(_cachedMonitors);
    }
    
    /// <summary>
    /// Gets the monitor containing the specified point
    /// </summary>
    /// <param name="point">Point to check</param>
    /// <returns>Monitor containing the point, or null if not found</returns>
    public MonitorInfo? GetMonitorContaining(Point point)
    {
        var monitors = GetAllMonitors();
        return monitors.FirstOrDefault(m => m.ContainsPoint(point));
    }
    
    /// <summary>
    /// Gets the monitor containing the specified rectangle
    /// </summary>
    /// <param name="windowRect">Rectangle to check</param>
    /// <returns>Monitor with largest intersection, or null if not found</returns>
    public MonitorInfo? GetMonitorContaining(Rectangle windowRect)
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
    public MonitorInfo? GetMonitorInDirection(MonitorInfo sourceMonitor, EdgeDirection direction)
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
    /// </summary>
    private void RefreshMonitors()
    {
        _cachedMonitors.Clear();
        
        var monitors = new List<MonitorInfo>();
        var callback = new MonitorEnumProc(EnumerateMonitorsCallback);
        
        bool EnumerateMonitorsCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
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
            
            return true; // Continue enumeration
        }
        
        User32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        
        _cachedMonitors.AddRange(monitors);
        _lastCacheUpdate = DateTime.Now;
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