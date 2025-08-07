using System.Drawing;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Service for detecting proximity between cursor and windows using configurable algorithms
/// </summary>
public class ProximityDetector : IProximityDetector
{
    private readonly ILogger _logger;
    private readonly ProximityConfiguration _config;
    private readonly IMonitorManager? _monitorManager;
    
    // Spatial caching for performance optimization
    private readonly Dictionary<(Point cursor, Rectangle window, int threshold), (bool result, DateTime timestamp)> _proximityCache = new();
    private readonly object _cacheLock = new();
    private const int CacheTimeoutMs = 16; // ~60fps cache timeout
    private const int MaxCacheSize = 1000; // Prevent memory bloat

    /// <summary>
    /// Creates a new ProximityDetector instance
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="config">Configuration for proximity detection algorithms</param>
    /// <param name="monitorManager">Monitor manager for DPI-aware calculations (optional)</param>
    public ProximityDetector(ILogger logger, ProximityConfiguration? config = null, IMonitorManager? monitorManager = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? new ProximityConfiguration();
        _monitorManager = monitorManager;

        var validationErrors = _config.Validate();
        if (validationErrors.Count > 0)
        {
            throw new ArgumentException($"Invalid proximity configuration: {string.Join(", ", validationErrors)}");
        }

        _logger.LogDebug("ProximityDetector initialized with algorithm: {Algorithm}, DPI-aware: {DpiAware}",
            _config.Algorithm, _monitorManager != null);
    }

    /// <summary>
    /// Calculates the proximity distance between a cursor position and a window
    /// </summary>
    /// <param name="cursorPosition">Current cursor position in screen coordinates</param>
    /// <param name="windowBounds">Window bounds rectangle</param>
    /// <returns>Distance value (interpretation depends on selected algorithm)</returns>
    public double CalculateProximity(Point cursorPosition, Rectangle windowBounds)
    {
        try
        {
            // Optimized calculation using switch expression and avoiding repeated calculations
            var distance = _config.Algorithm switch
            {
                ProximityAlgorithm.EuclideanDistance => CalculateEuclideanDistanceOptimized(cursorPosition, windowBounds),
                ProximityAlgorithm.ManhattanDistance => CalculateManhattanDistanceOptimized(cursorPosition, windowBounds),
                ProximityAlgorithm.NearestEdgeDistance => CalculateNearestEdgeDistanceOptimized(cursorPosition, windowBounds),
                _ => throw new NotSupportedException($"Proximity algorithm {_config.Algorithm} is not supported")
            };

            // Apply sensitivity multipliers only if they're not default values
            var adjustedDistance = Math.Abs(_config.HorizontalSensitivityMultiplier - 1.0) < 0.001 &&
                                 Math.Abs(_config.VerticalSensitivityMultiplier - 1.0) < 0.001
                ? distance
                : ApplySensitivityMultipliers(distance, cursorPosition, windowBounds);

            return adjustedDistance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating proximity between cursor ({CursorX},{CursorY}) and window {WindowBounds}",
                cursorPosition.X, cursorPosition.Y, windowBounds);
            return double.MaxValue; // Return maximum distance on error to avoid false positives
        }
    }

    /// <summary>
    /// Determines if a cursor is within the proximity threshold of a window with spatial caching
    /// </summary>
    /// <param name="cursorPosition">Current cursor position in screen coordinates</param>
    /// <param name="windowBounds">Window bounds rectangle</param>
    /// <param name="proximityThreshold">Distance threshold for proximity detection</param>
    /// <returns>True if cursor is within proximity threshold</returns>
    public bool IsWithinProximity(Point cursorPosition, Rectangle windowBounds, int proximityThreshold)
    {
        if (proximityThreshold <= 0)
        {
            _logger.LogWarning("Invalid proximity threshold: {Threshold}. Must be greater than 0", proximityThreshold);
            return false;
        }

        try
        {
            // Check cache first for performance optimization
            var cacheKey = (cursorPosition, windowBounds, proximityThreshold);
            var currentTime = DateTime.UtcNow;

            lock (_cacheLock)
            {
                if (_proximityCache.TryGetValue(cacheKey, out var cachedResult))
                {
                    var age = (currentTime - cachedResult.timestamp).TotalMilliseconds;
                    if (age < CacheTimeoutMs)
                    {
                        return cachedResult.result;
                    }
                    // Remove expired entry
                    _proximityCache.Remove(cacheKey);
                }

                // Cleanup cache if it's getting too large
                if (_proximityCache.Count > MaxCacheSize)
                {
                    CleanupExpiredCacheEntries(currentTime);
                }
            }

            // Convert threshold to physical pixels if DPI-aware mode is enabled
            var effectiveThreshold = GetDpiAwareThreshold(proximityThreshold, cursorPosition, windowBounds);

            var distance = CalculateProximity(cursorPosition, windowBounds);
            var isWithin = distance <= effectiveThreshold;

            // Cache the result
            lock (_cacheLock)
            {
                _proximityCache[cacheKey] = (isWithin, currentTime);
            }

            _logger.LogDebug("Proximity check: distance={Distance:F2}, threshold={Threshold} (effective: {EffectiveThreshold}), within={IsWithin}",
                distance, proximityThreshold, effectiveThreshold, isWithin);

            return isWithin;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking proximity between cursor ({CursorX},{CursorY}) and window {WindowBounds}",
                cursorPosition.X, cursorPosition.Y, windowBounds);
            return false;
        }
    }

    /// <summary>
    /// Calculates the optimal push direction and distance to move a window away from the cursor
    /// </summary>
    /// <param name="cursorPosition">Current cursor position</param>
    /// <param name="windowBounds">Current window bounds</param>
    /// <param name="pushDistance">Desired distance to push the window</param>
    /// <returns>Point representing the offset to apply to the window position</returns>
    public Point CalculatePushVector(Point cursorPosition, Rectangle windowBounds, int pushDistance)
    {
        if (pushDistance <= 0)
        {
            _logger.LogWarning("Invalid push distance: {Distance}. Must be greater than 0", pushDistance);
            return Point.Empty;
        }

        try
        {
            // Convert push distance to physical pixels if DPI-aware
            var effectivePushDistance = GetDpiAwarePushDistance(pushDistance, cursorPosition, windowBounds);

            // Find the closest point on the window to the cursor
            var closestPoint = GetClosestPointOnRectangle(cursorPosition, windowBounds);

            // Calculate direction vector from cursor to closest point
            var directionX = closestPoint.X - cursorPosition.X;
            var directionY = closestPoint.Y - cursorPosition.Y;

            // Handle case where cursor is inside the window
            if (windowBounds.Contains(cursorPosition))
            {
                // Push away from the center of the window
                var centerX = windowBounds.Left + windowBounds.Width / 2;
                var centerY = windowBounds.Top + windowBounds.Height / 2;

                directionX = centerX - cursorPosition.X;
                directionY = centerY - cursorPosition.Y;

                _logger.LogDebug("Cursor inside window, pushing away from center");
            }

            // Normalize the direction vector
            var magnitude = Math.Sqrt(directionX * directionX + directionY * directionY);

            if (magnitude == 0)
            {
                // If cursor is exactly at the closest point, push right by default
                _logger.LogDebug("Zero magnitude vector, defaulting to rightward push");
                return new Point(effectivePushDistance, 0);
            }

            var normalizedX = directionX / magnitude;
            var normalizedY = directionY / magnitude;

            // Scale by effective push distance (DPI-aware)
            var pushX = (int)Math.Round(normalizedX * effectivePushDistance);
            var pushY = (int)Math.Round(normalizedY * effectivePushDistance);

            var pushVector = new Point(pushX, pushY);

            _logger.LogDebug("Push vector calculated: ({PushX},{PushY}) from cursor ({CursorX},{CursorY}) to window {WindowBounds}",
                pushX, pushY, cursorPosition.X, cursorPosition.Y, windowBounds);

            return pushVector;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating push vector for cursor ({CursorX},{CursorY}) and window {WindowBounds}",
                cursorPosition.X, cursorPosition.Y, windowBounds);
            return Point.Empty;
        }
    }

    #region Private Helper Methods

    /// <summary>
    /// Converts a logical proximity threshold to physical pixels based on monitor DPI
    /// </summary>
    /// <param name="logicalThreshold">Threshold in logical pixels</param>
    /// <param name="cursorPosition">Cursor position to determine monitor</param>
    /// <param name="windowBounds">Window bounds to determine monitor</param>
    /// <returns>Threshold in physical pixels</returns>
    private double GetDpiAwareThreshold(int logicalThreshold, Point cursorPosition, Rectangle windowBounds)
    {
        if (_monitorManager == null)
        {
            // No DPI conversion if monitor manager is not available
            return logicalThreshold;
        }

        try
        {
            // Use cursor position to determine DPI (more responsive to user's current monitor)
            var dpiInfo = _monitorManager.GetDpiForPoint(cursorPosition);

            // Convert logical threshold to physical pixels
            var physicalThreshold = (int)Math.Round(logicalThreshold * dpiInfo.ScaleFactorX);

            _logger.LogDebug("DPI-aware threshold conversion: {LogicalThreshold} -> {PhysicalThreshold} (scale: {ScaleFactor})",
                logicalThreshold, physicalThreshold, dpiInfo.ScaleFactorX);

            return physicalThreshold;
        }
        catch (Exception)
        {
            _logger.LogWarning("Failed to get DPI-aware threshold, using logical threshold: {Threshold}", logicalThreshold);
            return logicalThreshold;
        }
    }

    /// <summary>
    /// Converts a logical push distance to physical pixels based on monitor DPI
    /// </summary>
    /// <param name="logicalDistance">Distance in logical pixels</param>
    /// <param name="cursorPosition">Cursor position to determine monitor</param>
    /// <param name="windowBounds">Window bounds to determine monitor</param>
    /// <returns>Distance in physical pixels</returns>
    private int GetDpiAwarePushDistance(int logicalDistance, Point cursorPosition, Rectangle windowBounds)
    {
        if (_monitorManager == null)
        {
            // No DPI conversion if monitor manager is not available
            return logicalDistance;
        }

        try
        {
            // Use cursor position to determine DPI
            var dpiInfo = _monitorManager.GetDpiForPoint(cursorPosition);

            // Convert logical distance to physical pixels
            var physicalDistance = (int)Math.Round(logicalDistance * dpiInfo.ScaleFactorX);

            _logger.LogDebug("DPI-aware distance conversion: {LogicalDistance} -> {PhysicalDistance} (scale: {ScaleFactor})",
                logicalDistance, physicalDistance, dpiInfo.ScaleFactorX);

            return physicalDistance;
        }
        catch (Exception)
        {
            _logger.LogWarning("Failed to get DPI-aware distance, using logical distance: {Distance}", logicalDistance);
            return logicalDistance;
        }
    }

    /// <summary>
    /// Optimized Euclidean distance calculation with fewer allocations
    /// </summary>
    private double CalculateEuclideanDistanceOptimized(Point cursor, Rectangle window)
    {
        // Calculate closest point inline to avoid method call overhead
        var closestX = Math.Max(window.Left, Math.Min(cursor.X, window.Right));
        var closestY = Math.Max(window.Top, Math.Min(cursor.Y, window.Bottom));
        
        var dx = cursor.X - closestX;
        var dy = cursor.Y - closestY;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Optimized Manhattan distance calculation with fewer allocations
    /// </summary>
    private double CalculateManhattanDistanceOptimized(Point cursor, Rectangle window)
    {
        // Calculate closest point inline to avoid method call overhead
        var closestX = Math.Max(window.Left, Math.Min(cursor.X, window.Right));
        var closestY = Math.Max(window.Top, Math.Min(cursor.Y, window.Bottom));
        
        return Math.Abs(cursor.X - closestX) + Math.Abs(cursor.Y - closestY);
    }

    /// <summary>
    /// Optimized nearest edge distance calculation
    /// </summary>
    private double CalculateNearestEdgeDistanceOptimized(Point cursor, Rectangle window)
    {
        // If cursor is inside window, return 0
        if (cursor.X >= window.Left && cursor.X <= window.Right &&
            cursor.Y >= window.Top && cursor.Y <= window.Bottom)
            return 0;

        // Calculate distances efficiently without creating intermediate objects
        if (cursor.X < window.Left)
        {
            if (cursor.Y < window.Top)
                return Math.Min(window.Left - cursor.X, window.Top - cursor.Y);
            else if (cursor.Y > window.Bottom)
                return Math.Min(window.Left - cursor.X, cursor.Y - window.Bottom);
            else
                return window.Left - cursor.X;
        }
        else if (cursor.X > window.Right)
        {
            if (cursor.Y < window.Top)
                return Math.Min(cursor.X - window.Right, window.Top - cursor.Y);
            else if (cursor.Y > window.Bottom)
                return Math.Min(cursor.X - window.Right, cursor.Y - window.Bottom);
            else
                return cursor.X - window.Right;
        }
        else
        {
            if (cursor.Y < window.Top)
                return window.Top - cursor.Y;
            else
                return cursor.Y - window.Bottom;
        }
    }


    /// <summary>
    /// Finds the closest point on a rectangle to a given point
    /// </summary>
    private static Point GetClosestPointOnRectangle(Point point, Rectangle rectangle)
    {
        var x = Math.Max(rectangle.Left, Math.Min(point.X, rectangle.Right));
        var y = Math.Max(rectangle.Top, Math.Min(point.Y, rectangle.Bottom));
        return new Point(x, y);
    }

    /// <summary>
    /// Applies horizontal and vertical sensitivity multipliers to distance calculation
    /// </summary>
    private double ApplySensitivityMultipliers(double distance, Point cursor, Rectangle window)
    {
        // If using default multipliers (both 1.0), skip calculation
        if (Math.Abs(_config.HorizontalSensitivityMultiplier - 1.0) < 0.001 &&
            Math.Abs(_config.VerticalSensitivityMultiplier - 1.0) < 0.001)
        {
            return distance;
        }

        // For algorithms that use coordinates, we need to decompose and recompose
        if (_config.Algorithm == ProximityAlgorithm.EuclideanDistance)
        {
            var closestPoint = GetClosestPointOnRectangle(cursor, window);
            var dx = Math.Abs(cursor.X - closestPoint.X) * _config.HorizontalSensitivityMultiplier;
            var dy = Math.Abs(cursor.Y - closestPoint.Y) * _config.VerticalSensitivityMultiplier;
            return Math.Sqrt(dx * dx + dy * dy);
        }
        else if (_config.Algorithm == ProximityAlgorithm.ManhattanDistance)
        {
            var closestPoint = GetClosestPointOnRectangle(cursor, window);
            var dx = Math.Abs(cursor.X - closestPoint.X) * _config.HorizontalSensitivityMultiplier;
            var dy = Math.Abs(cursor.Y - closestPoint.Y) * _config.VerticalSensitivityMultiplier;
            return dx + dy;
        }

        // For NearestEdgeDistance, apply based on which edge is closest
        return distance * Math.Min(_config.HorizontalSensitivityMultiplier, _config.VerticalSensitivityMultiplier);
    }

    /// <summary>
    /// Cleans up expired cache entries to prevent memory bloat
    /// </summary>
    /// <param name="currentTime">Current timestamp for comparison</param>
    private void CleanupExpiredCacheEntries(DateTime currentTime)
    {
        var expiredKeys = new List<(Point, Rectangle, int)>();
        
        foreach (var kvp in _proximityCache)
        {
            var age = (currentTime - kvp.Value.timestamp).TotalMilliseconds;
            if (age >= CacheTimeoutMs)
            {
                expiredKeys.Add(kvp.Key);
            }
        }
        
        foreach (var key in expiredKeys)
        {
            _proximityCache.Remove(key);
        }
        
        _logger.LogDebug("Cleaned up {Count} expired proximity cache entries", expiredKeys.Count);
    }

    #endregion
}

/// <summary>
/// Interface for proximity detection service
/// </summary>
public interface IProximityDetector
{
    /// <summary>
    /// Calculates the proximity distance between a cursor position and a window
    /// </summary>
    /// <param name="cursorPosition">Current cursor position in screen coordinates</param>
    /// <param name="windowBounds">Window bounds rectangle</param>
    /// <returns>Distance value (interpretation depends on selected algorithm)</returns>
    double CalculateProximity(Point cursorPosition, Rectangle windowBounds);

    /// <summary>
    /// Determines if a cursor is within the proximity threshold of a window
    /// </summary>
    /// <param name="cursorPosition">Current cursor position in screen coordinates</param>
    /// <param name="windowBounds">Window bounds rectangle</param>
    /// <param name="proximityThreshold">Distance threshold for proximity detection</param>
    /// <returns>True if cursor is within proximity threshold</returns>
    bool IsWithinProximity(Point cursorPosition, Rectangle windowBounds, int proximityThreshold);

    /// <summary>
    /// Calculates the optimal push direction and distance to move a window away from the cursor
    /// </summary>
    /// <param name="cursorPosition">Current cursor position</param>
    /// <param name="windowBounds">Current window bounds</param>
    /// <param name="pushDistance">Desired distance to push the window</param>
    /// <returns>Point representing the offset to apply to the window position</returns>
    Point CalculatePushVector(Point cursorPosition, Rectangle windowBounds, int pushDistance);
}