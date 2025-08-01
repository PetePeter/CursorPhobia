using System.Drawing;
using CursorPhobia.Core.Models;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Interface for monitor management operations
/// </summary>
public interface IMonitorManager
{
    /// <summary>
    /// Gets all connected monitors with their information
    /// </summary>
    /// <returns>List of monitor information</returns>
    List<MonitorInfo> GetAllMonitors();
    
    /// <summary>
    /// Gets the monitor containing the specified point
    /// </summary>
    /// <param name="point">Point to check</param>
    /// <returns>Monitor containing the point, or null if not found</returns>
    MonitorInfo? GetMonitorContaining(Point point);
    
    /// <summary>
    /// Gets the monitor containing the specified rectangle
    /// </summary>
    /// <param name="windowRect">Rectangle to check</param>
    /// <returns>Monitor with largest intersection, or null if not found</returns>
    MonitorInfo? GetMonitorContaining(Rectangle windowRect);
    
    /// <summary>
    /// Gets the primary monitor
    /// </summary>
    /// <returns>Primary monitor or null if not found</returns>
    MonitorInfo? GetPrimaryMonitor();
    
    /// <summary>
    /// Gets all monitors adjacent to the specified monitor
    /// </summary>
    /// <param name="monitor">Source monitor</param>
    /// <returns>List of adjacent monitors</returns>
    List<MonitorInfo> GetAdjacentMonitors(MonitorInfo monitor);
    
    /// <summary>
    /// Gets the monitor in the specified direction from the source monitor
    /// </summary>
    /// <param name="sourceMonitor">Source monitor</param>
    /// <param name="direction">Direction to look</param>
    /// <returns>Monitor in the specified direction, or null if not found</returns>
    MonitorInfo? GetMonitorInDirection(MonitorInfo sourceMonitor, EdgeDirection direction);
    
    /// <summary>
    /// Gets DPI information for the specified monitor
    /// </summary>
    /// <param name="monitor">Monitor to get DPI for</param>
    /// <returns>DPI information for the monitor</returns>
    DpiInfo GetMonitorDpi(MonitorInfo monitor);
    
    /// <summary>
    /// Gets DPI information for the monitor containing the specified point
    /// </summary>
    /// <param name="point">Point to check</param>
    /// <returns>DPI information for the containing monitor, or default if not found</returns>
    DpiInfo GetDpiForPoint(Point point);
    
    /// <summary>
    /// Gets DPI information for the monitor containing the specified rectangle
    /// </summary>
    /// <param name="windowRect">Rectangle to check</param>
    /// <returns>DPI information for the containing monitor, or default if not found</returns>
    DpiInfo GetDpiForRectangle(Rectangle windowRect);
}