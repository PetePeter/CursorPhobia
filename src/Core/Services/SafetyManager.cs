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
            _screenBounds.Count, _config.ScreenEdgeBuffer);
    }

    /// <summary>
    /// Validates a proposed window position and adjusts it to ensure it stays within safe boundaries
    /// </summary>
    /// <param name="windowBounds">Current window bounds</param>
    /// <param name="proposedPosition">Proposed new position</param>
    /// <returns>Safe position that keeps the window within screen boundaries</returns>
    public Point ValidateWindowPosition(Rectangle windowBounds, Point proposedPosition)
    {
        try
        {
            lock (_lockObject)
            {
                // Create the proposed window bounds
                var proposedBounds = new Rectangle(proposedPosition.X, proposedPosition.Y, windowBounds.Width, windowBounds.Height);

                // Find the best screen for this window
                var targetScreen = FindBestScreenForWindow(proposedBounds);

                // Calculate the safe bounds within the target screen
                var safeBounds = GetSafeBounds(targetScreen);

                // Adjust the position to keep the window within safe bounds
                var safePosition = ConstrainToSafeBounds(proposedBounds, safeBounds);

                _logger.LogDebug("Position validation: proposed=({ProposedX},{ProposedY}), safe=({SafeX},{SafeY}), screen={Screen}",
                    proposedPosition.X, proposedPosition.Y, safePosition.X, safePosition.Y, targetScreen);

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
                // Check if the window intersects with any screen
                foreach (var screen in _screenBounds)
                {
                    var safeBounds = GetSafeBounds(screen);

                    // Window is safe if it's entirely within the safe bounds of any screen
                    if (safeBounds.Contains(windowBounds))
                    {
                        _logger.LogDebug("Window position {WindowBounds} is safe within screen {Screen}", windowBounds, screen);
                        return true;
                    }
                }

                _logger.LogDebug("Window position {WindowBounds} is not safe - outside all screen safe bounds", windowBounds);
                return false;
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
            var baseDistance = _config.ScreenEdgeBuffer;

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
            return _config.ScreenEdgeBuffer; // Return default on error
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Finds the best screen for a window based on where most of it would be displayed
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

        // If no intersection, find the closest screen
        if (maxIntersectionArea == 0)
        {
            var minDistance = double.MaxValue;

            foreach (var screen in _screenBounds)
            {
                var distance = CalculateDistanceToRectangle(windowBounds, screen);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    bestScreen = screen;
                }
            }
        }

        return bestScreen;
    }

    /// <summary>
    /// Gets the safe bounds for a screen (screen bounds minus edge buffer)
    /// </summary>
    private Rectangle GetSafeBounds(Rectangle screenBounds)
    {
        var buffer = _config.ScreenEdgeBuffer;

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
}