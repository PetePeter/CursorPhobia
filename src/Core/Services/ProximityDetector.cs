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

    /// <summary>
    /// Creates a new ProximityDetector instance
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="config">Configuration for proximity detection algorithms</param>
    public ProximityDetector(ILogger logger, ProximityConfiguration? config = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? new ProximityConfiguration();
        
        var validationErrors = _config.Validate();
        if (validationErrors.Count > 0)
        {
            throw new ArgumentException($"Invalid proximity configuration: {string.Join(", ", validationErrors)}");
        }
        
        _logger.LogDebug("ProximityDetector initialized with algorithm: {Algorithm}", _config.Algorithm);
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
            var distance = _config.Algorithm switch
            {
                ProximityAlgorithm.EuclideanDistance => CalculateEuclideanDistance(cursorPosition, windowBounds),
                ProximityAlgorithm.ManhattanDistance => CalculateManhattanDistance(cursorPosition, windowBounds),
                ProximityAlgorithm.NearestEdgeDistance => CalculateNearestEdgeDistance(cursorPosition, windowBounds),
                _ => throw new NotSupportedException($"Proximity algorithm {_config.Algorithm} is not supported")
            };

            // Apply sensitivity multipliers
            var adjustedDistance = ApplySensitivityMultipliers(distance, cursorPosition, windowBounds);
            
            _logger.LogDebug("Proximity calculated: {Distance:F2} (algorithm: {Algorithm}, cursor: {CursorX},{CursorY}, window: {WindowBounds})", 
                adjustedDistance, _config.Algorithm, cursorPosition.X, cursorPosition.Y, windowBounds);
                
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
    /// Determines if a cursor is within the proximity threshold of a window
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
            var distance = CalculateProximity(cursorPosition, windowBounds);
            var isWithin = distance <= proximityThreshold;
            
            _logger.LogDebug("Proximity check: distance={Distance:F2}, threshold={Threshold}, within={IsWithin}", 
                distance, proximityThreshold, isWithin);
                
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
                return new Point(pushDistance, 0);
            }
            
            var normalizedX = directionX / magnitude;
            var normalizedY = directionY / magnitude;
            
            // Scale by push distance
            var pushX = (int)Math.Round(normalizedX * pushDistance);
            var pushY = (int)Math.Round(normalizedY * pushDistance);
            
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
    /// Calculates Euclidean distance between cursor and nearest point on window
    /// </summary>
    private double CalculateEuclideanDistance(Point cursor, Rectangle window)
    {
        var closestPoint = GetClosestPointOnRectangle(cursor, window);
        var dx = cursor.X - closestPoint.X;
        var dy = cursor.Y - closestPoint.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Calculates Manhattan distance between cursor and nearest point on window
    /// </summary>
    private double CalculateManhattanDistance(Point cursor, Rectangle window)
    {
        var closestPoint = GetClosestPointOnRectangle(cursor, window);
        var dx = Math.Abs(cursor.X - closestPoint.X);
        var dy = Math.Abs(cursor.Y - closestPoint.Y);
        return dx + dy;
    }

    /// <summary>
    /// Calculates distance to nearest window edge
    /// </summary>
    private double CalculateNearestEdgeDistance(Point cursor, Rectangle window)
    {
        // If cursor is inside window, return 0
        if (window.Contains(cursor))
            return 0;

        // Calculate distance to each edge
        var leftDistance = Math.Abs(cursor.X - window.Left);
        var rightDistance = Math.Abs(cursor.X - window.Right);
        var topDistance = Math.Abs(cursor.Y - window.Top);
        var bottomDistance = Math.Abs(cursor.Y - window.Bottom);

        // Return minimum distance considering cursor position relative to window
        if (cursor.X < window.Left)
        {
            if (cursor.Y < window.Top)
                return Math.Min(leftDistance, topDistance);
            else if (cursor.Y > window.Bottom)
                return Math.Min(leftDistance, bottomDistance);
            else
                return leftDistance;
        }
        else if (cursor.X > window.Right)
        {
            if (cursor.Y < window.Top)
                return Math.Min(rightDistance, topDistance);
            else if (cursor.Y > window.Bottom)
                return Math.Min(rightDistance, bottomDistance);
            else
                return rightDistance;
        }
        else
        {
            if (cursor.Y < window.Top)
                return topDistance;
            else
                return bottomDistance;
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