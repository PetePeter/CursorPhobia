using System.Drawing;
using CursorPhobia.Core.Models;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Handles edge wrapping logic for windows at monitor boundaries
/// </summary>
public class EdgeWrapHandler
{
    private readonly MonitorManager _monitorManager;
    
    /// <summary>
    /// Initializes a new instance of the EdgeWrapHandler
    /// </summary>
    /// <param name="monitorManager">Monitor manager for multi-monitor detection</param>
    public EdgeWrapHandler(MonitorManager monitorManager)
    {
        _monitorManager = monitorManager ?? throw new ArgumentNullException(nameof(monitorManager));
    }
    
    /// <summary>
    /// Calculates the wrap destination for a window being pushed to a screen edge
    /// </summary>
    /// <param name="windowRect">Current window rectangle</param>
    /// <param name="pushVector">Direction and magnitude of push</param>
    /// <param name="wrapBehavior">Wrapping behavior configuration</param>
    /// <returns>New position for the window, or null if no wrapping should occur</returns>
    public Point? CalculateWrapDestination(Rectangle windowRect, Point pushVector, WrapBehavior wrapBehavior)
    {
        if (!wrapBehavior.EnableWrapping) return null;
        
        var currentMonitor = _monitorManager.GetMonitorContaining(windowRect);
        if (currentMonitor == null) return null;
        
        var edgeType = DetectCrossedEdge(windowRect, pushVector, currentMonitor);
        if (edgeType == EdgeType.None) return null;
        
        return wrapBehavior.PreferredBehavior switch
        {
            WrapPreference.Adjacent => CalculateAdjacentWrap(windowRect, edgeType, currentMonitor),
            WrapPreference.Opposite => CalculateOppositeWrap(windowRect, edgeType, currentMonitor),
            WrapPreference.Smart => CalculateSmartWrap(windowRect, edgeType, currentMonitor),
            _ => null
        };
    }
    
    /// <summary>
    /// Determines which edge of the monitor is being crossed
    /// </summary>
    /// <param name="windowRect">Window rectangle</param>
    /// <param name="pushVector">Push direction</param>
    /// <param name="monitor">Current monitor</param>
    /// <returns>Type of edge being crossed</returns>
    private EdgeType DetectCrossedEdge(Rectangle windowRect, Point pushVector, MonitorInfo monitor)
    {
        var workArea = monitor.workAreaBounds;
        var windowCenter = new Point(windowRect.X + windowRect.Width / 2, windowRect.Y + windowRect.Height / 2);
        
        // Check if window is at or beyond edges
        if (pushVector.X < 0 && windowRect.Left <= workArea.Left)
            return EdgeType.Left;
            
        if (pushVector.X > 0 && windowRect.Right >= workArea.Right)
            return EdgeType.Right;
            
        if (pushVector.Y < 0 && windowRect.Top <= workArea.Top)
            return EdgeType.Top;
            
        if (pushVector.Y > 0 && windowRect.Bottom >= workArea.Bottom)
            return EdgeType.Bottom;
        
        return EdgeType.None;
    }
    
    /// <summary>
    /// Calculates wrap destination using adjacent monitor logic
    /// </summary>
    /// <param name="windowRect">Current window rectangle</param>
    /// <param name="edgeType">Edge being crossed</param>
    /// <param name="currentMonitor">Current monitor</param>
    /// <returns>New window position or null</returns>
    private Point? CalculateAdjacentWrap(Rectangle windowRect, EdgeType edgeType, MonitorInfo currentMonitor)
    {
        var direction = EdgeTypeToDirection(edgeType);
        var adjacentMonitor = _monitorManager.GetMonitorInDirection(currentMonitor, direction);
        
        if (adjacentMonitor == null)
        {
            // No adjacent monitor, wrap to opposite edge of current monitor
            return CalculateOppositeEdgeWrap(windowRect, edgeType, currentMonitor);
        }
        
        return CalculateAdjacentMonitorWrap(windowRect, edgeType, adjacentMonitor);
    }
    
    /// <summary>
    /// Calculates wrap destination using opposite edge logic
    /// </summary>
    /// <param name="windowRect">Current window rectangle</param>
    /// <param name="edgeType">Edge being crossed</param>
    /// <param name="currentMonitor">Current monitor</param>
    /// <returns>New window position</returns>
    private Point CalculateOppositeWrap(Rectangle windowRect, EdgeType edgeType, MonitorInfo currentMonitor)
    {
        return CalculateOppositeEdgeWrap(windowRect, edgeType, currentMonitor);
    }
    
    /// <summary>
    /// Calculates wrap destination using smart logic (adjacent if available, otherwise opposite)
    /// </summary>
    /// <param name="windowRect">Current window rectangle</param>
    /// <param name="edgeType">Edge being crossed</param>
    /// <param name="currentMonitor">Current monitor</param>
    /// <returns>New window position</returns>
    private Point CalculateSmartWrap(Rectangle windowRect, EdgeType edgeType, MonitorInfo currentMonitor)
    {
        var direction = EdgeTypeToDirection(edgeType);
        var adjacentMonitor = _monitorManager.GetMonitorInDirection(currentMonitor, direction);
        
        if (adjacentMonitor != null)
        {
            return CalculateAdjacentMonitorWrap(windowRect, edgeType, adjacentMonitor);
        }
        
        return CalculateOppositeEdgeWrap(windowRect, edgeType, currentMonitor);
    }
    
    /// <summary>
    /// Calculates position for wrapping to opposite edge of current monitor
    /// </summary>
    /// <param name="windowRect">Current window rectangle</param>
    /// <param name="edgeType">Edge being crossed</param>
    /// <param name="monitor">Current monitor</param>
    /// <returns>New window position</returns>
    private Point CalculateOppositeEdgeWrap(Rectangle windowRect, EdgeType edgeType, MonitorInfo monitor)
    {
        var workArea = monitor.workAreaBounds;
        
        return edgeType switch
        {
            EdgeType.Left => new Point(workArea.Right - windowRect.Width, windowRect.Y),
            EdgeType.Right => new Point(workArea.Left, windowRect.Y),
            EdgeType.Top => new Point(windowRect.X, workArea.Bottom - windowRect.Height),
            EdgeType.Bottom => new Point(windowRect.X, workArea.Top),
            _ => windowRect.Location
        };
    }
    
    /// <summary>
    /// Calculates position for wrapping to adjacent monitor
    /// </summary>
    /// <param name="windowRect">Current window rectangle</param>
    /// <param name="edgeType">Edge being crossed</param>
    /// <param name="targetMonitor">Target monitor</param>
    /// <returns>New window position</returns>
    private Point CalculateAdjacentMonitorWrap(Rectangle windowRect, EdgeType edgeType, MonitorInfo targetMonitor)
    {
        var targetWorkArea = targetMonitor.workAreaBounds;
        
        return edgeType switch
        {
            EdgeType.Left => new Point(targetWorkArea.Right - windowRect.Width, 
                                     Math.Max(targetWorkArea.Top, 
                                     Math.Min(targetWorkArea.Bottom - windowRect.Height, windowRect.Y))),
            
            EdgeType.Right => new Point(targetWorkArea.Left,
                                      Math.Max(targetWorkArea.Top,
                                      Math.Min(targetWorkArea.Bottom - windowRect.Height, windowRect.Y))),
            
            EdgeType.Top => new Point(Math.Max(targetWorkArea.Left,
                                             Math.Min(targetWorkArea.Right - windowRect.Width, windowRect.X)),
                                    targetWorkArea.Bottom - windowRect.Height),
            
            EdgeType.Bottom => new Point(Math.Max(targetWorkArea.Left,
                                                Math.Min(targetWorkArea.Right - windowRect.Width, windowRect.X)),
                                       targetWorkArea.Top),
            
            _ => windowRect.Location
        };
    }
    
    /// <summary>
    /// Converts edge type to direction
    /// </summary>
    /// <param name="edgeType">Edge type</param>
    /// <returns>Corresponding direction</returns>
    private EdgeDirection EdgeTypeToDirection(EdgeType edgeType)
    {
        return edgeType switch
        {
            EdgeType.Left => EdgeDirection.Left,
            EdgeType.Right => EdgeDirection.Right,
            EdgeType.Top => EdgeDirection.Up,
            EdgeType.Bottom => EdgeDirection.Down,
            _ => throw new ArgumentException($"Invalid edge type: {edgeType}")
        };
    }
    
    /// <summary>
    /// Validates that a wrap destination is safe and doesn't cause infinite loops
    /// </summary>
    /// <param name="originalPosition">Original window position</param>
    /// <param name="newPosition">Proposed new position</param>
    /// <param name="windowSize">Window size</param>
    /// <returns>True if the wrap is safe</returns>
    public bool IsWrapSafe(Point originalPosition, Point newPosition, Size windowSize)
    {
        // Ensure minimum movement distance to prevent rapid oscillation
        const int minimumWrapDistance = 50;
        
        var distance = Math.Sqrt(Math.Pow(newPosition.X - originalPosition.X, 2) + 
                                Math.Pow(newPosition.Y - originalPosition.Y, 2));
        
        if (distance < minimumWrapDistance) return false;
        
        // Ensure new position is within some monitor's work area
        var newRect = new Rectangle(newPosition, windowSize);
        var targetMonitor = _monitorManager.GetMonitorContaining(newRect);
        
        return targetMonitor != null;
    }
}

/// <summary>
/// Types of monitor edges
/// </summary>
public enum EdgeType
{
    None,
    Left,
    Right,
    Top,
    Bottom
}

/// <summary>
/// Wrap behavior configuration
/// </summary>
public class WrapBehavior
{
    /// <summary>
    /// Whether wrapping is enabled
    /// </summary>
    public bool EnableWrapping { get; set; } = true;
    
    /// <summary>
    /// Preferred wrapping behavior
    /// </summary>
    public WrapPreference PreferredBehavior { get; set; } = WrapPreference.Smart;
    
    /// <summary>
    /// Whether to respect taskbar areas when wrapping
    /// </summary>
    public bool RespectTaskbarAreas { get; set; } = true;
}

