using System.Drawing;
using System.Windows.Forms;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Utilities;
using CursorPhobia.Core.WindowsAPI;
using static CursorPhobia.Core.WindowsAPI.WindowsStructures;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Service for implementing safety mechanisms to prevent windows from being moved off-screen
/// or into unsafe positions
/// </summary>
public class SafetyManager : ISafetyManager
{
    private readonly ILogger _logger;
    private readonly CursorPhobiaConfiguration _config;
    private readonly List<Rectangle> _screenBounds;
    private readonly object _lockObject = new();

    /// <summary>
    /// Creates a new SafetyManager instance
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="config">Configuration for safety settings</param>
    public SafetyManager(ILogger logger, CursorPhobiaConfiguration? config = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? CursorPhobiaConfiguration.CreateDefault();

        var validationErrors = _config.Validate();
        if (validationErrors.Count > 0)
        {
            throw new ArgumentException($"Invalid safety manager configuration: {string.Join(", ", validationErrors)}");
        }

        _screenBounds = new List<Rectangle>();
        RefreshScreenBounds();

        _logger.LogDebug("SafetyManager initialized with {ScreenCount} screens and {EdgeBuffer}px edge buffer",
            _screenBounds.Count, HardcodedDefaults.ScreenEdgeBuffer);
    }

    /// <summary>
    /// Validates a proposed window position and adjusts it to ensure it stays within safe boundaries
    /// Enhanced to support multi-monitor transitions by checking all monitors instead of constraining to a single target
    /// </summary>
    /// <param name="windowBounds">Current window bounds</param>
    /// <param name="proposedPosition">Proposed new position</param>
    /// <returns>Safe position that keeps the window within overall desktop boundaries</returns>
    public Point ValidateWindowPosition(Rectangle windowBounds, Point proposedPosition)
    {
        try
        {
            lock (_lockObject)
            {
                // Create the proposed window bounds
                var proposedBounds = new Rectangle(proposedPosition.X, proposedPosition.Y, windowBounds.Width, windowBounds.Height);

                // Check if cross-monitor movement is enabled in configuration
                var allowCrossMonitor = _config.MultiMonitor?.EnableCrossMonitorMovement ?? true;

                // Check if the proposed position is already safe within any monitor
                if (IsPositionSafeInAnyMonitor(proposedBounds))
                {
                    _logger.LogDebug("Position validation: proposed=({ProposedX},{ProposedY}) is already safe",
                        proposedPosition.X, proposedPosition.Y);
                    return proposedPosition;
                }

                // If cross-monitor movement is disabled, constrain to current monitor
                if (!allowCrossMonitor)
                {
                    var currentScreen = FindBestScreenForWindow(new Rectangle(windowBounds.X, windowBounds.Y, windowBounds.Width, windowBounds.Height));
                    var safeBounds = GetSafeBounds(currentScreen);
                    var constrainedPosition = ConstrainToSafeBounds(proposedBounds, safeBounds);
                    
                    _logger.LogDebug("Cross-monitor movement disabled, constraining to current monitor: " +
                                   "proposed=({ProposedX},{ProposedY}), constrained=({ConstrainedX},{ConstrainedY})",
                        proposedPosition.X, proposedPosition.Y, constrainedPosition.X, constrainedPosition.Y);
                    
                    return constrainedPosition;
                }

                // For cross-monitor enabled, try to find the best position across all monitors
                var safePosition = FindBestCrossMonitorPosition(windowBounds, proposedBounds);

                _logger.LogDebug("Position validation: proposed=({ProposedX},{ProposedY}), safe=({SafeX},{SafeY}) [Cross-monitor: {CrossMonitor}]",
                    proposedPosition.X, proposedPosition.Y, safePosition.X, safePosition.Y, allowCrossMonitor);

                return safePosition;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating window position for bounds {WindowBounds} at proposed position ({ProposedX},{ProposedY})",
                windowBounds, proposedPosition.X, proposedPosition.Y);

            // Return the original proposed position as fallback
            return proposedPosition;
        }
    }

    /// <summary>
    /// Finds the best position for a window across all monitors, allowing cross-monitor movement
    /// </summary>
    /// <param name="originalBounds">Original window bounds</param>
    /// <param name="proposedBounds">Proposed window bounds</param>
    /// <returns>Best safe position across all monitors</returns>
    private Point FindBestCrossMonitorPosition(Rectangle originalBounds, Rectangle proposedBounds)
    {
        // First, try the originally intended target screen
        var targetScreen = FindBestScreenForWindow(proposedBounds);
        var targetSafeBounds = GetSafeBounds(targetScreen);
        
        // If the window would fit entirely within the target screen's safe bounds, use it
        if (targetSafeBounds.Width >= proposedBounds.Width && targetSafeBounds.Height >= proposedBounds.Height)
        {
            var constrainedPosition = ConstrainToSafeBounds(proposedBounds, targetSafeBounds);
            
            // Check if this position provides reasonable visibility
            var constrainedBounds = new Rectangle(constrainedPosition.X, constrainedPosition.Y, proposedBounds.Width, proposedBounds.Height);
            if (IsPositionSafeInAnyMonitor(constrainedBounds, 0.7)) // Require 70% visibility
            {
                return constrainedPosition;
            }
        }

        // If target screen isn't suitable, try other screens in order of preference
        var currentScreen = FindBestScreenForWindow(originalBounds);
        var candidateScreens = _screenBounds
            .Where(screen => screen != targetScreen) // Skip the already-tried target screen
            .OrderBy(screen => CalculateScreenPreference(originalBounds, proposedBounds, screen))
            .ToList();

        foreach (var candidateScreen in candidateScreens)
        {
            var candidateSafeBounds = GetSafeBounds(candidateScreen);
            
            // Skip screens that are too small for the window
            if (candidateSafeBounds.Width < proposedBounds.Width || candidateSafeBounds.Height < proposedBounds.Height)
                continue;

            var candidatePosition = ConstrainToSafeBounds(proposedBounds, candidateSafeBounds);
            var candidateBounds = new Rectangle(candidatePosition.X, candidatePosition.Y, proposedBounds.Width, proposedBounds.Height);
            
            if (IsPositionSafeInAnyMonitor(candidateBounds, 0.8)) // Require 80% visibility for alternative screens
            {
                _logger.LogDebug("Found better position on alternative screen: ({X},{Y}) on screen {Screen}",
                    candidatePosition.X, candidatePosition.Y, candidateScreen);
                return candidatePosition;
            }
        }

        // Fallback: constrain to target screen even if not optimal
        return ConstrainToSafeBounds(proposedBounds, targetSafeBounds);
    }

    /// <summary>
    /// Calculates a preference score for placing a window on a specific screen
    /// Lower scores indicate higher preference
    /// </summary>
    /// <param name="originalBounds">Original window bounds</param>
    /// <param name="proposedBounds">Proposed window bounds</param>
    /// <param name="screen">Candidate screen</param>
    /// <returns>Preference score (lower is better)</returns>
    private double CalculateScreenPreference(Rectangle originalBounds, Rectangle proposedBounds, Rectangle screen)
    {
        double score = 0;

        // Prefer screens that have more overlap with the proposed position
        var intersection = Rectangle.Intersect(proposedBounds, screen);
        var overlapRatio = (double)(intersection.Width * intersection.Height) / (proposedBounds.Width * proposedBounds.Height);
        score += (1.0 - overlapRatio) * 100; // High penalty for low overlap

        // Prefer screens closer to the original position
        var originalCenter = new Point(originalBounds.X + originalBounds.Width / 2, originalBounds.Y + originalBounds.Height / 2);
        var screenCenter = new Point(screen.X + screen.Width / 2, screen.Y + screen.Height / 2);
        var distance = Math.Sqrt(Math.Pow(screenCenter.X - originalCenter.X, 2) + Math.Pow(screenCenter.Y - originalCenter.Y, 2));
        score += distance * 0.01; // Small penalty for distance

        // Prefer larger screens
        var screenArea = screen.Width * screen.Height;
        var maxScreenArea = _screenBounds.Max(s => s.Width * s.Height);
        score += (1.0 - (double)screenArea / maxScreenArea) * 10; // Small penalty for smaller screens

        return score;
    }

    /// <summary>
    /// Checks if a window position would be considered safe (fully visible and within screen bounds)
    /// </summary>
    /// <param name="windowBounds">Window bounds to check</param>
    /// <returns>True if the position is safe</returns>
    public bool IsPositionSafe(Rectangle windowBounds)
    {
        try
        {
            lock (_lockObject)
            {
                return IsPositionSafeInAnyMonitor(windowBounds);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if position is safe for window bounds {WindowBounds}", windowBounds);
            return false; // Conservative approach - assume unsafe on error
        }
    }

    /// <summary>
    /// Gets the safe usable area for all screens combined
    /// </summary>
    /// <returns>List of safe rectangles representing usable screen areas</returns>
    public List<Rectangle> GetSafeScreenAreas()
    {
        try
        {
            lock (_lockObject)
            {
                var safeAreas = new List<Rectangle>();

                foreach (var screen in _screenBounds)
                {
                    safeAreas.Add(GetSafeBounds(screen));
                }

                _logger.LogDebug("Retrieved {Count} safe screen areas", safeAreas.Count);
                return safeAreas;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting safe screen areas");
            return new List<Rectangle>();
        }
    }

    /// <summary>
    /// Refreshes the screen bounds information from the system
    /// </summary>
    public void RefreshScreenBounds()
    {
        try
        {
            lock (_lockObject)
            {
                _screenBounds.Clear();

                // Enumerate all display monitors
                var monitors = new List<Rectangle>();

                User32.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, MonitorEnumCallback, IntPtr.Zero);

                _screenBounds.AddRange(monitors);

                if (_screenBounds.Count == 0)
                {
                    // Fallback to primary screen if enumeration fails
                    var primaryScreen = Screen.PrimaryScreen?.WorkingArea ?? new Rectangle(0, 0, 1920, 1080);
                    _screenBounds.Add(primaryScreen);
                    _logger.LogWarning("Failed to enumerate monitors, using fallback primary screen: {PrimaryScreen}", primaryScreen);
                }

                _logger.LogInformation("Refreshed screen bounds: found {ScreenCount} screens", _screenBounds.Count);

                // Local callback method
                bool MonitorEnumCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData)
                {
                    var monitorInfo = MONITORINFO.Create();
                    if (User32.GetMonitorInfo(hMonitor, ref monitorInfo))
                    {
                        // Use the work area (excludes taskbar, etc.)
                        var workArea = monitorInfo.rcWork.ToRectangle();
                        monitors.Add(workArea);

                        _logger.LogDebug("Found monitor work area: {WorkArea}", workArea);
                    }
                    return true; // Continue enumeration
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing screen bounds");

            // Ensure we have at least one screen bound as fallback
            if (_screenBounds.Count == 0)
            {
                var fallbackScreen = new Rectangle(0, 0, 1920, 1080);
                _screenBounds.Add(fallbackScreen);
                _logger.LogWarning("Using fallback screen bounds: {FallbackScreen}", fallbackScreen);
            }
        }
    }

    /// <summary>
    /// Calculates the minimum safe distance a window should maintain from screen edges
    /// </summary>
    /// <param name="windowBounds">Window bounds to calculate for</param>
    /// <returns>Minimum distance in pixels</returns>
    public int CalculateMinimumSafeDistance(Rectangle windowBounds)
    {
        try
        {
            // Base distance is the configured edge buffer
            var baseDistance = HardcodedDefaults.ScreenEdgeBuffer;

            // For very small windows, use a smaller buffer
            if (windowBounds.Width < 200 || windowBounds.Height < 150)
            {
                baseDistance = Math.Max(5, baseDistance / 2);
            }

            // For very large windows, use a larger buffer
            if (windowBounds.Width > 1500 || windowBounds.Height > 1000)
            {
                baseDistance = Math.Min(50, baseDistance * 2);
            }

            _logger.LogDebug("Calculated minimum safe distance: {Distance}px for window size {Width}x{Height}",
                baseDistance, windowBounds.Width, windowBounds.Height);

            return baseDistance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating minimum safe distance for window bounds {WindowBounds}", windowBounds);
            return HardcodedDefaults.ScreenEdgeBuffer; // Return default on error
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Finds the best screen for a window based on where most of it would be displayed
    /// Enhanced to support virtual desktop coordinate systems properly
    /// </summary>
    private Rectangle FindBestScreenForWindow(Rectangle windowBounds)
    {
        if (_screenBounds.Count == 0)
        {
            throw new InvalidOperationException("No screen bounds available");
        }

        if (_screenBounds.Count == 1)
        {
            return _screenBounds[0];
        }

        Rectangle bestScreen = _screenBounds[0];
        var maxIntersectionArea = 0;

        // First pass: Find screen with maximum intersection area
        foreach (var screen in _screenBounds)
        {
            var intersection = Rectangle.Intersect(windowBounds, screen);
            var intersectionArea = intersection.Width * intersection.Height;

            if (intersectionArea > maxIntersectionArea)
            {
                maxIntersectionArea = intersectionArea;
                bestScreen = screen;
            }
        }

        // If we have a good intersection, use that screen
        if (maxIntersectionArea > (windowBounds.Width * windowBounds.Height * 0.1)) // At least 10% overlap
        {
            return bestScreen;
        }

        // Second pass: If no significant intersection, find the closest screen by center point
        var windowCenter = new Point(windowBounds.X + windowBounds.Width / 2, windowBounds.Y + windowBounds.Height / 2);
        var minDistance = double.MaxValue;

        foreach (var screen in _screenBounds)
        {
            var screenCenter = new Point(screen.X + screen.Width / 2, screen.Y + screen.Height / 2);
            var distance = Math.Sqrt(Math.Pow(windowCenter.X - screenCenter.X, 2) + Math.Pow(windowCenter.Y - screenCenter.Y, 2));
            
            if (distance < minDistance)
            {
                minDistance = distance;
                bestScreen = screen;
            }
        }

        return bestScreen;
    }

    /// <summary>
    /// Gets the safe bounds for a screen (screen bounds minus edge buffer)
    /// </summary>
    private Rectangle GetSafeBounds(Rectangle screenBounds)
    {
        var buffer = HardcodedDefaults.ScreenEdgeBuffer;

        return new Rectangle(
            screenBounds.X + buffer,
            screenBounds.Y + buffer,
            Math.Max(100, screenBounds.Width - (buffer * 2)), // Ensure minimum usable area
            Math.Max(100, screenBounds.Height - (buffer * 2))
        );
    }

    /// <summary>
    /// Constrains a window bounds to fit within safe bounds
    /// </summary>
    private Point ConstrainToSafeBounds(Rectangle windowBounds, Rectangle safeBounds)
    {
        var x = windowBounds.X;
        var y = windowBounds.Y;

        // Adjust X position
        if (windowBounds.Left < safeBounds.Left)
        {
            x = safeBounds.Left;
        }
        else if (windowBounds.Right > safeBounds.Right)
        {
            x = safeBounds.Right - windowBounds.Width;
        }

        // Adjust Y position
        if (windowBounds.Top < safeBounds.Top)
        {
            y = safeBounds.Top;
        }
        else if (windowBounds.Bottom > safeBounds.Bottom)
        {
            y = safeBounds.Bottom - windowBounds.Height;
        }

        // Final bounds check - if window is larger than safe bounds, center it
        if (windowBounds.Width > safeBounds.Width)
        {
            x = safeBounds.Left + (safeBounds.Width - windowBounds.Width) / 2;
        }

        if (windowBounds.Height > safeBounds.Height)
        {
            y = safeBounds.Top + (safeBounds.Height - windowBounds.Height) / 2;
        }

        return new Point(x, y);
    }

    /// <summary>
    /// Checks if a window position is safe within any monitor
    /// </summary>
    /// <param name="windowBounds">Window bounds to check</param>
    /// <param name="visibilityThreshold">Minimum visibility ratio required (default 0.8 = 80%)</param>
    /// <returns>True if the window is safe within any monitor</returns>
    private bool IsPositionSafeInAnyMonitor(Rectangle windowBounds, double visibilityThreshold = 0.8)
    {
        // Check if the window intersects with any screen and is mostly visible
        foreach (var screen in _screenBounds)
        {
            var safeBounds = GetSafeBounds(screen);

            // Window is safe if it's entirely within the safe bounds of any screen
            if (safeBounds.Contains(windowBounds))
            {
                _logger.LogDebug("Window position {WindowBounds} is safe within screen {Screen}", windowBounds, screen);
                return true;
            }

            // Also consider safe if window has significant overlap and is mostly visible
            var intersection = Rectangle.Intersect(windowBounds, safeBounds);
            var intersectionArea = intersection.Width * intersection.Height;
            var windowArea = windowBounds.Width * windowBounds.Height;
            
            if (windowArea > 0 && intersectionArea > windowArea * visibilityThreshold)
            {
                var visibilityRatio = (double)intersectionArea / windowArea;
                _logger.LogDebug("Window position {WindowBounds} is mostly safe within screen {Screen} ({Percentage:P1} visible)", 
                    windowBounds, screen, visibilityRatio);
                return true;
            }
        }

        _logger.LogDebug("Window position {WindowBounds} is not safe - insufficient visibility on all screens (threshold: {Threshold:P1})", 
            windowBounds, visibilityThreshold);
        return false;
    }

    /// <summary>
    /// Calculates the distance between two rectangles (0 if they intersect)
    /// </summary>
    private static double CalculateDistanceToRectangle(Rectangle rect1, Rectangle rect2)
    {
        if (rect1.IntersectsWith(rect2))
            return 0;

        var dx = Math.Max(0, Math.Max(rect1.Left - rect2.Right, rect2.Left - rect1.Right));
        var dy = Math.Max(0, Math.Max(rect1.Top - rect2.Bottom, rect2.Top - rect1.Bottom));

        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Gets the combined safe area of all monitors for overall desktop boundary checking
    /// </summary>
    /// <returns>Rectangle representing the overall safe desktop area</returns>
    public Rectangle GetOverallDesktopSafeArea()
    {
        try
        {
            lock (_lockObject)
            {
                if (_screenBounds.Count == 0)
                {
                    return Rectangle.Empty;
                }

                // Calculate the bounding rectangle of all monitors
                int minX = _screenBounds.Min(s => s.X);
                int minY = _screenBounds.Min(s => s.Y);
                int maxX = _screenBounds.Max(s => s.Right);
                int maxY = _screenBounds.Max(s => s.Bottom);

                // Apply buffer to the overall desktop area
                var buffer = HardcodedDefaults.ScreenEdgeBuffer;
                return new Rectangle(
                    minX + buffer,
                    minY + buffer,
                    Math.Max(100, (maxX - minX) - (buffer * 2)),
                    Math.Max(100, (maxY - minY) - (buffer * 2))
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating overall desktop safe area");
            return Rectangle.Empty;
        }
    }

    #endregion
}

/// <summary>
/// Interface for safety management service
/// </summary>
public interface ISafetyManager
{
    /// <summary>
    /// Validates a proposed window position and adjusts it to ensure it stays within safe boundaries
    /// </summary>
    /// <param name="windowBounds">Current window bounds</param>
    /// <param name="proposedPosition">Proposed new position</param>
    /// <returns>Safe position that keeps the window within screen boundaries</returns>
    Point ValidateWindowPosition(Rectangle windowBounds, Point proposedPosition);

    /// <summary>
    /// Checks if a window position would be considered safe (fully visible and within screen bounds)
    /// </summary>
    /// <param name="windowBounds">Window bounds to check</param>
    /// <returns>True if the position is safe</returns>
    bool IsPositionSafe(Rectangle windowBounds);

    /// <summary>
    /// Gets the safe usable area for all screens combined
    /// </summary>
    /// <returns>List of safe rectangles representing usable screen areas</returns>
    List<Rectangle> GetSafeScreenAreas();

    /// <summary>
    /// Refreshes the screen bounds information from the system
    /// </summary>
    void RefreshScreenBounds();

    /// <summary>
    /// Calculates the minimum safe distance a window should maintain from screen edges
    /// </summary>
    /// <param name="windowBounds">Window bounds to calculate for</param>
    /// <returns>Minimum distance in pixels</returns>
    int CalculateMinimumSafeDistance(Rectangle windowBounds);

    /// <summary>
    /// Gets the combined safe area of all monitors for overall desktop boundary checking
    /// </summary>
    /// <returns>Rectangle representing the overall safe desktop area</returns>
    Rectangle GetOverallDesktopSafeArea();
}