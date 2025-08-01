using System.Drawing;

namespace CursorPhobia.Core.Models;

/// <summary>
/// Configuration settings for CursorPhobia proximity detection and window pushing behavior
/// </summary>
public class CursorPhobiaConfiguration
{
    /// <summary>
    /// Distance in pixels from cursor to window edge that triggers proximity detection
    /// Default: 50 pixels
    /// </summary>
    public int ProximityThreshold { get; set; } = 50;
    
    /// <summary>
    /// Distance in pixels to push the window away from the cursor
    /// Default: 100 pixels
    /// </summary>
    public int PushDistance { get; set; } = 100;
    
    /// <summary>
    /// Minimum time in milliseconds between cursor position checks
    /// Default: 16ms (~60 FPS)
    /// </summary>
    public int UpdateIntervalMs { get; set; } = 16;
    
    /// <summary>
    /// Maximum time in milliseconds between cursor position checks when system is busy
    /// Default: 50ms (20 FPS minimum)
    /// </summary>
    public int MaxUpdateIntervalMs { get; set; } = 50;
    
    /// <summary>
    /// Whether to enable the CTRL key override that disables cursor phobia temporarily
    /// Default: true
    /// </summary>
    public bool EnableCtrlOverride { get; set; } = true;
    
    /// <summary>
    /// Minimum distance in pixels from screen edges that windows must maintain
    /// Default: 20 pixels
    /// </summary>
    public int ScreenEdgeBuffer { get; set; } = 20;
    
    /// <summary>
    /// Whether to apply cursor phobia to all windows or only topmost windows
    /// Default: false (only topmost windows)
    /// </summary>
    public bool ApplyToAllWindows { get; set; } = false;
    
    /// <summary>
    /// Duration of window movement animation in milliseconds
    /// Default: 200ms for smooth but responsive movement
    /// </summary>
    public int AnimationDurationMs { get; set; } = 200;
    
    /// <summary>
    /// Whether to enable smooth window animations during push movements
    /// Default: true for better user experience
    /// </summary>
    public bool EnableAnimations { get; set; } = true;
    
    /// <summary>
    /// Easing curve type for window animations
    /// Default: EaseOut for natural deceleration
    /// </summary>
    public AnimationEasing AnimationEasing { get; set; } = AnimationEasing.EaseOut;
    
    /// <summary>
    /// Time in milliseconds that cursor must hover over window area before pushing stops
    /// Default: 5000ms (5 seconds)
    /// </summary>
    public int HoverTimeoutMs { get; set; } = 5000;
    
    /// <summary>
    /// Whether to enable hover timeout behavior (stop pushing after hovering for HoverTimeoutMs)
    /// Default: true
    /// </summary>
    public bool EnableHoverTimeout { get; set; } = true;
    
    /// <summary>
    /// Multi-monitor support and edge wrapping configuration
    /// </summary>
    public MultiMonitorConfiguration? MultiMonitor { get; set; } = new();
    
    /// <summary>
    /// Validates the configuration settings and returns any errors
    /// </summary>
    /// <returns>List of validation error messages, empty if valid</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();
        
        if (ProximityThreshold <= 0)
            errors.Add("ProximityThreshold must be greater than 0");
            
        if (ProximityThreshold > 500)
            errors.Add("ProximityThreshold should not exceed 500 pixels for usability");
            
        if (PushDistance <= 0)
            errors.Add("PushDistance must be greater than 0");
            
        if (PushDistance > 1000)
            errors.Add("PushDistance should not exceed 1000 pixels to prevent windows moving off-screen");
            
        if (UpdateIntervalMs < 1)
            errors.Add("UpdateIntervalMs must be at least 1ms");
            
        if (UpdateIntervalMs > MaxUpdateIntervalMs)
            errors.Add("UpdateIntervalMs cannot be greater than MaxUpdateIntervalMs");
            
        if (MaxUpdateIntervalMs < 10)
            errors.Add("MaxUpdateIntervalMs must be at least 10ms to prevent excessive CPU usage");
            
        if (MaxUpdateIntervalMs > 1000)
            errors.Add("MaxUpdateIntervalMs should not exceed 1000ms for responsiveness");
            
        if (ScreenEdgeBuffer < 0)
            errors.Add("ScreenEdgeBuffer cannot be negative");
            
        if (ScreenEdgeBuffer > 100)
            errors.Add("ScreenEdgeBuffer should not exceed 100 pixels for usability");
            
        if (AnimationDurationMs < 0)
            errors.Add("AnimationDurationMs cannot be negative");
            
        if (AnimationDurationMs > 2000)
            errors.Add("AnimationDurationMs should not exceed 2000ms for usability");
            
        if (HoverTimeoutMs < 100)
            errors.Add("HoverTimeoutMs must be at least 100ms");
            
        if (HoverTimeoutMs > 30000)
            errors.Add("HoverTimeoutMs should not exceed 30000ms (30 seconds)");
        
        // Validate multi-monitor configuration
        if (MultiMonitor != null)
        {
            errors.AddRange(MultiMonitor.Validate());
        }
            
        return errors;
    }
    
    /// <summary>
    /// Creates a default configuration with recommended settings
    /// </summary>
    /// <returns>Default configuration instance</returns>
    public static CursorPhobiaConfiguration CreateDefault()
    {
        return new CursorPhobiaConfiguration();
    }
    
    /// <summary>
    /// Creates a configuration optimized for performance (less frequent updates)
    /// </summary>
    /// <returns>Performance-optimized configuration instance</returns>
    public static CursorPhobiaConfiguration CreatePerformanceOptimized()
    {
        return new CursorPhobiaConfiguration
        {
            UpdateIntervalMs = 33, // ~30 FPS
            MaxUpdateIntervalMs = 100 // 10 FPS minimum
        };
    }
    
    /// <summary>
    /// Creates a configuration optimized for responsiveness (more frequent updates)
    /// </summary>
    /// <returns>Responsiveness-optimized configuration instance</returns>
    public static CursorPhobiaConfiguration CreateResponsivenessOptimized()
    {
        return new CursorPhobiaConfiguration
        {
            UpdateIntervalMs = 8, // ~120 FPS
            MaxUpdateIntervalMs = 25 // 40 FPS minimum
        };
    }
}

/// <summary>
/// Configuration for proximity detection algorithms and safety mechanisms
/// </summary>
public class ProximityConfiguration
{
    /// <summary>
    /// The algorithm to use for proximity detection
    /// </summary>
    public ProximityAlgorithm Algorithm { get; set; } = ProximityAlgorithm.EuclideanDistance;
    
    /// <summary>
    /// Whether to use the nearest edge distance for proximity calculation
    /// Default: true (more intuitive behavior)
    /// </summary>
    public bool UseNearestEdge { get; set; } = true;
    
    /// <summary>
    /// Multiplier for horizontal proximity detection (useful for widescreen monitors)
    /// Default: 1.0 (equal horizontal and vertical sensitivity)
    /// </summary>
    public double HorizontalSensitivityMultiplier { get; set; } = 1.0;
    
    /// <summary>
    /// Multiplier for vertical proximity detection
    /// Default: 1.0 (equal horizontal and vertical sensitivity)
    /// </summary>
    public double VerticalSensitivityMultiplier { get; set; } = 1.0;
    
    /// <summary>
    /// Validates the proximity configuration
    /// </summary>
    /// <returns>List of validation error messages, empty if valid</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();
        
        if (HorizontalSensitivityMultiplier <= 0)
            errors.Add("HorizontalSensitivityMultiplier must be greater than 0");
            
        if (VerticalSensitivityMultiplier <= 0)
            errors.Add("VerticalSensitivityMultiplier must be greater than 0");
            
        if (HorizontalSensitivityMultiplier > 10)
            errors.Add("HorizontalSensitivityMultiplier should not exceed 10 for usability");
            
        if (VerticalSensitivityMultiplier > 10)
            errors.Add("VerticalSensitivityMultiplier should not exceed 10 for usability");
            
        return errors;
    }
}

/// <summary>
/// Available algorithms for proximity detection
/// </summary>
public enum ProximityAlgorithm
{
    /// <summary>
    /// Standard Euclidean distance calculation: sqrt((x2-x1)² + (y2-y1)²)
    /// </summary>
    EuclideanDistance,
    
    /// <summary>
    /// Manhattan distance calculation: |x2-x1| + |y2-y1|
    /// More performance-friendly as it avoids square root calculation
    /// </summary>
    ManhattanDistance,
    
    /// <summary>
    /// Distance to nearest window edge (most intuitive for users)
    /// </summary>
    NearestEdgeDistance
}

/// <summary>
/// Animation easing types for window movement
/// </summary>
public enum AnimationEasing
{
    /// <summary>
    /// Linear interpolation - constant speed throughout animation
    /// </summary>
    Linear,
    
    /// <summary>
    /// Ease in - slow start, accelerating toward end
    /// </summary>
    EaseIn,
    
    /// <summary>
    /// Ease out - fast start, decelerating toward end (recommended for UI)
    /// </summary>
    EaseOut,
    
    /// <summary>
    /// Ease in-out - slow start and end, fast in middle
    /// </summary>
    EaseInOut
}

/// <summary>
/// Configuration for multi-monitor support and edge wrapping behavior
/// </summary>
public class MultiMonitorConfiguration
{
    /// <summary>
    /// Whether to enable edge wrapping when windows are pushed to screen boundaries
    /// Default: true
    /// </summary>
    public bool EnableWrapping { get; set; } = true;
    
    /// <summary>
    /// Preferred wrapping behavior when multiple options are available
    /// Default: Smart (adjacent if available, otherwise opposite edge)
    /// </summary>
    public WrapPreference PreferredWrapBehavior { get; set; } = WrapPreference.Smart;
    
    /// <summary>
    /// Whether to respect taskbar areas when calculating wrap destinations
    /// Default: true (wrap to work area, not full monitor bounds)
    /// </summary>
    public bool RespectTaskbarAreas { get; set; } = true;
    
    /// <summary>
    /// Per-monitor settings for customizing behavior on specific displays
    /// Key is monitor device name or handle
    /// </summary>
    public Dictionary<string, PerMonitorSettings> PerMonitorSettings { get; set; } = new();
    
    /// <summary>
    /// Validates the multi-monitor configuration
    /// </summary>
    /// <returns>List of validation error messages, empty if valid</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();
        
        // Validate per-monitor settings
        foreach (var kvp in PerMonitorSettings)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
            {
                errors.Add("Per-monitor settings cannot have empty monitor identifier");
            }
            
            if (kvp.Value != null)
            {
                errors.AddRange(kvp.Value.Validate().Select(error => $"Monitor '{kvp.Key}': {error}"));
            }
        }
        
        return errors;
    }
}

/// <summary>
/// Per-monitor configuration settings
/// </summary>
public class PerMonitorSettings
{
    /// <summary>
    /// Whether CursorPhobia is enabled for this specific monitor
    /// Default: true
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Custom proximity threshold for this monitor (overrides global setting if set)
    /// </summary>
    public int? CustomProximityThreshold { get; set; }
    
    /// <summary>
    /// Custom push distance for this monitor (overrides global setting if set)
    /// </summary>
    public int? CustomPushDistance { get; set; }
    
    /// <summary>
    /// Custom edge wrapping behavior for this monitor (overrides global setting if set)
    /// </summary>
    public bool? CustomEnableWrapping { get; set; }
    
    /// <summary>
    /// Custom wrap preference for this monitor (overrides global setting if set)
    /// </summary>
    public WrapPreference? CustomWrapPreference { get; set; }
    
    /// <summary>
    /// Validates the per-monitor settings
    /// </summary>
    /// <returns>List of validation error messages, empty if valid</returns>
    public List<string> Validate()
    {
        var errors = new List<string>();
        
        if (CustomProximityThreshold.HasValue)
        {
            if (CustomProximityThreshold.Value <= 0)
                errors.Add("CustomProximityThreshold must be greater than 0");
                
            if (CustomProximityThreshold.Value > 500)
                errors.Add("CustomProximityThreshold should not exceed 500 pixels");
        }
        
        if (CustomPushDistance.HasValue)
        {
            if (CustomPushDistance.Value <= 0)
                errors.Add("CustomPushDistance must be greater than 0");
                
            if (CustomPushDistance.Value > 1000)
                errors.Add("CustomPushDistance should not exceed 1000 pixels");
        }
        
        return errors;
    }
}

/// <summary>
/// Wrap behavior preferences for multi-monitor edge wrapping
/// </summary>
public enum WrapPreference
{
    /// <summary>
    /// Wrap to adjacent monitor if available
    /// </summary>
    Adjacent,
    
    /// <summary>
    /// Always wrap to opposite edge of current monitor
    /// </summary>
    Opposite,
    
    /// <summary>
    /// Smart wrapping: adjacent if available, otherwise opposite
    /// </summary>
    Smart
}