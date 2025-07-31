using System.Drawing;

namespace CursorPhobia.Core.Models;

/// <summary>
/// Represents information about a display monitor
/// </summary>
public class MonitorInfo
{
    /// <summary>
    /// Handle to the monitor
    /// </summary>
    public IntPtr monitorHandle { get; set; }
    
    /// <summary>
    /// The display rectangle of the monitor in virtual screen coordinates
    /// </summary>
    public Rectangle monitorBounds { get; set; }
    
    /// <summary>
    /// The work area rectangle of the monitor in virtual screen coordinates
    /// (excludes taskbars, sidebars, and docked windows)
    /// </summary>
    public Rectangle workAreaBounds { get; set; }
    
    /// <summary>
    /// Indicates whether this is the primary monitor
    /// </summary>
    public bool isPrimary { get; set; }
    
    /// <summary>
    /// The monitor's device name
    /// </summary>
    public string deviceName { get; set; } = string.Empty;
    
    /// <summary>
    /// Monitor width in pixels
    /// </summary>
    public int width => monitorBounds.Width;
    
    /// <summary>
    /// Monitor height in pixels
    /// </summary>
    public int height => monitorBounds.Height;
    
    /// <summary>
    /// Monitor's horizontal position in virtual screen coordinates
    /// </summary>
    public int x => monitorBounds.X;
    
    /// <summary>
    /// Monitor's vertical position in virtual screen coordinates
    /// </summary>
    public int y => monitorBounds.Y;
    
    /// <summary>
    /// Creates a new MonitorInfo instance
    /// </summary>
    public MonitorInfo()
    {
    }
    
    /// <summary>
    /// Creates a new MonitorInfo instance with specified parameters
    /// </summary>
    /// <param name="handle">Monitor handle</param>
    /// <param name="monitorRect">Monitor bounds</param>
    /// <param name="workRect">Work area bounds</param>
    /// <param name="primary">Whether this is the primary monitor</param>
    /// <param name="name">Device name</param>
    public MonitorInfo(IntPtr handle, Rectangle monitorRect, Rectangle workRect, bool primary, string name = "")
    {
        monitorHandle = handle;
        monitorBounds = monitorRect;
        workAreaBounds = workRect;
        isPrimary = primary;
        deviceName = name;
    }
    
    /// <summary>
    /// Determines if a point is within this monitor's bounds
    /// </summary>
    /// <param name="point">Point to check</param>
    /// <returns>True if the point is within the monitor bounds</returns>
    public bool ContainsPoint(Point point)
    {
        return monitorBounds.Contains(point);
    }
    
    /// <summary>
    /// Determines if a rectangle intersects with this monitor's bounds
    /// </summary>
    /// <param name="rectangle">Rectangle to check</param>
    /// <returns>True if the rectangle intersects with the monitor bounds</returns>
    public bool IntersectsWith(Rectangle rectangle)
    {
        return monitorBounds.IntersectsWith(rectangle);
    }
    
    /// <summary>
    /// Returns a string representation of the monitor information
    /// </summary>
    public override string ToString()
    {
        return $"Monitor [{deviceName}]: {monitorBounds} (Primary: {isPrimary})";
    }
}