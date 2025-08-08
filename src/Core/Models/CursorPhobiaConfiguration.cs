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
    public int ProximityThreshold { get; set; } = HardcodedDefaults.ProximityThreshold;

    /// <summary>
    /// Distance in pixels to push the window away from the cursor
    /// Default: 100 pixels
    /// </summary>
    public int PushDistance { get; set; } = HardcodedDefaults.PushDistance;

    /// <summary>
    /// Minimum time in milliseconds between cursor position checks
    /// Default: 16ms (~60 FPS)
    /// NOTE: This property is now hardcoded for optimal performance and is no longer user-configurable.
    /// </summary>
    [Obsolete("This property is now hardcoded and no longer user-configurable. Use HardcodedDefaults.UpdateIntervalMs for the current value.", false)]
    public int UpdateIntervalMs { get; set; } = HardcodedDefaults.UpdateIntervalMs;

    /// <summary>
    /// Maximum time in milliseconds between cursor position checks when system is busy
    /// Default: 33ms (~30 FPS minimum)
    /// NOTE: This property is now hardcoded for optimal performance and is no longer user-configurable.
    /// </summary>
    [Obsolete("This property is now hardcoded and no longer user-configurable. Use HardcodedDefaults.MaxUpdateIntervalMs for the current value.", false)]
    public int MaxUpdateIntervalMs { get; set; } = HardcodedDefaults.MaxUpdateIntervalMs;

    /// <summary>
    /// Whether to enable the CTRL key override that disables cursor phobia temporarily
    /// Default: true
    /// </summary>
    public bool EnableCtrlOverride { get; set; } = true;

    /// <summary>
    /// Tolerance distance in pixels around windows after releasing CTRL key
    /// When CTRL is released while hovering over a window, cursor phobia won't activate
    /// until the cursor moves this distance away from the window
    /// Default: 50 pixels
    /// NOTE: This property is now hardcoded for optimal user experience and is no longer user-configurable.
    /// </summary>
    [Obsolete("This property is now hardcoded and no longer user-configurable. Use HardcodedDefaults.CtrlReleaseToleranceDistance for the current value.", false)]
    public int CtrlReleaseToleranceDistance { get; set; } = HardcodedDefaults.CtrlReleaseToleranceDistance;

    /// <summary>
    /// Repel border distance in pixels for always-on-top windows
    /// When cursor leaves an always-on-top window, cursor phobia won't activate
    /// until the cursor moves this distance away from the window bounds
    /// Default: 30 pixels
    /// NOTE: This property is now hardcoded for optimal user experience and is no longer user-configurable.
    /// </summary>
    [Obsolete("This property is now hardcoded and no longer user-configurable. Use HardcodedDefaults.AlwaysOnTopRepelBorderDistance for the current value.", false)]
    public int AlwaysOnTopRepelBorderDistance { get; set; } = HardcodedDefaults.AlwaysOnTopRepelBorderDistance;

    /// <summary>
    /// Minimum distance in pixels from screen edges that windows must maintain
    /// Default: 20 pixels
    /// NOTE: This property is now hardcoded for optimal user experience and is no longer user-configurable.
    /// </summary>
    [Obsolete("This property is now hardcoded and no longer user-configurable. Use HardcodedDefaults.ScreenEdgeBuffer for the current value.", false)]
    public int ScreenEdgeBuffer { get; set; } = HardcodedDefaults.ScreenEdgeBuffer;

    /// <summary>
    /// Whether to apply cursor phobia to all windows or only topmost windows
    /// Default: false (only topmost windows)
    /// </summary>
    public bool ApplyToAllWindows { get; set; } = false;

    /// <summary>
    /// Duration of window movement animation in milliseconds
    /// Default: 200ms for smooth but responsive movement
    /// NOTE: This property is now hardcoded based on UX research and is no longer user-configurable.
    /// </summary>
    [Obsolete("This property is now hardcoded and no longer user-configurable. Use HardcodedDefaults.AnimationDurationMs for the current value.", false)]
    public int AnimationDurationMs { get; set; } = HardcodedDefaults.AnimationDurationMs;

    /// <summary>
    /// Whether to enable smooth window animations during push movements
    /// Default: true for better user experience
    /// NOTE: This property is now hardcoded based on UX research and is no longer user-configurable.
    /// </summary>
    [Obsolete("This property is now hardcoded and no longer user-configurable. Use HardcodedDefaults.EnableAnimations for the current value.", false)]
    public bool EnableAnimations { get; set; } = HardcodedDefaults.EnableAnimations;

    /// <summary>
    /// Easing curve type for window animations
    /// Default: EaseOut for natural deceleration
    /// NOTE: This property is now hardcoded based on UX research and is no longer user-configurable.
    /// </summary>
    [Obsolete("This property is now hardcoded and no longer user-configurable. Use HardcodedDefaults.AnimationEasing for the current value.", false)]
    public AnimationEasing AnimationEasing { get; set; } = HardcodedDefaults.AnimationEasing;

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

        // Note: UpdateIntervalMs and MaxUpdateIntervalMs are now hardcoded and no longer validated

        // Note: ScreenEdgeBuffer is now hardcoded and no longer validated

        // Note: CtrlReleaseToleranceDistance is now hardcoded and no longer validated

        // Note: AlwaysOnTopRepelBorderDistance is now hardcoded and no longer validated

        // Note: AnimationDurationMs and EnableAnimations are now hardcoded and no longer validated

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

    /// <summary>
    /// Creates a configuration with optimal defaults derived from research and user feedback.
    /// These are the recommended smart defaults that balance performance, responsiveness, and user experience.
    /// All values have been carefully tuned based on UX evaluation and performance testing.
    /// </summary>
    /// <returns>Configuration instance with optimal smart defaults</returns>
    public static CursorPhobiaConfiguration CreateOptimalDefaults()
    {
        return new CursorPhobiaConfiguration
        {
            // Performance settings - 60 FPS target with 30 FPS minimum
            UpdateIntervalMs = HardcodedDefaults.UpdateIntervalMs,
            MaxUpdateIntervalMs = HardcodedDefaults.MaxUpdateIntervalMs,
            
            // Spatial settings - balanced for most screen sizes and use cases
            ProximityThreshold = HardcodedDefaults.ProximityThreshold,
            PushDistance = HardcodedDefaults.PushDistance,
            ScreenEdgeBuffer = HardcodedDefaults.ScreenEdgeBuffer,
            CtrlReleaseToleranceDistance = HardcodedDefaults.CtrlReleaseToleranceDistance,
            AlwaysOnTopRepelBorderDistance = HardcodedDefaults.AlwaysOnTopRepelBorderDistance,
            
            // Animation settings - smooth but responsive
            EnableAnimations = HardcodedDefaults.EnableAnimations,
            AnimationDurationMs = HardcodedDefaults.AnimationDurationMs,
            AnimationEasing = HardcodedDefaults.AnimationEasing,
            
            // Default feature settings
            EnableCtrlOverride = true,
            ApplyToAllWindows = false,
            HoverTimeoutMs = 5000,
            EnableHoverTimeout = true,
            MultiMonitor = new MultiMonitorConfiguration()
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
    /// Whether to enable cross-monitor window movement
    /// Default: true (allows windows to move between monitors)
    /// </summary>
    public bool EnableCrossMonitorMovement { get; set; } = true;

    /// <summary>
    /// Whether to show visual feedback during cross-monitor transitions
    /// Default: true (recommended by UX evaluation)
    /// </summary>
    public bool ShowTransitionFeedback { get; set; } = true;

    /// <summary>
    /// Duration of transition feedback in milliseconds
    /// Default: 800ms (brief visual indicator)
    /// </summary>
    public int TransitionFeedbackDurationMs { get; set; } = 800;

    /// <summary>
    /// Type of visual feedback to show during transitions
    /// Default: Subtle (non-intrusive indicators)
    /// </summary>
    public TransitionFeedbackType FeedbackType { get; set; } = TransitionFeedbackType.Subtle;

    /// <summary>
    /// Minimum distance between monitors for cross-monitor movement (pixels)
    /// Default: 10 (prevents excessive jumping between adjacent monitors)
    /// </summary>
    public int CrossMonitorThreshold { get; set; } = 10;

    /// <summary>
    /// Whether to automatically adjust DPI scaling for cross-monitor movements
    /// Default: true (ensures consistent visual sizing across monitors)
    /// </summary>
    public bool EnableAutoDpiAdjustment { get; set; } = true;

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

        // Validate transition feedback settings
        if (TransitionFeedbackDurationMs < 100)
            errors.Add("TransitionFeedbackDurationMs must be at least 100ms");

        if (TransitionFeedbackDurationMs > 5000)
            errors.Add("TransitionFeedbackDurationMs should not exceed 5000ms to avoid UI disruption");

        if (CrossMonitorThreshold < 0)
            errors.Add("CrossMonitorThreshold cannot be negative");

        if (CrossMonitorThreshold > 100)
            errors.Add("CrossMonitorThreshold should not exceed 100 pixels for responsiveness");

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

/// <summary>
/// Visual feedback types for cross-monitor transitions
/// </summary>
public enum TransitionFeedbackType
{
    /// <summary>
    /// No visual feedback
    /// </summary>
    None,

    /// <summary>
    /// Subtle visual feedback (brief highlight or border)
    /// </summary>
    Subtle,

    /// <summary>
    /// Animation feedback (fade/slide effects)
    /// </summary>
    Animation,

    /// <summary>
    /// Toast notification feedback
    /// </summary>
    Toast,

    /// <summary>
    /// System tray notification
    /// </summary>
    TrayNotification
}

/// <summary>
/// Hardcoded optimal defaults for CursorPhobia configuration.
/// These constants represent the best-practice values derived from extensive testing,
/// user feedback, and performance analysis. They provide a solid foundation for
/// creating configurations that work well across different hardware and use cases.
/// </summary>
public static class HardcodedDefaults
{
    /// <summary>
    /// Optimal update interval in milliseconds (~60 FPS)
    /// Provides smooth tracking without excessive CPU usage
    /// </summary>
    public const int UpdateIntervalMs = 16;

    /// <summary>
    /// Maximum update interval in milliseconds (~30 FPS minimum)
    /// Ensures responsiveness even under system load
    /// </summary>
    public const int MaxUpdateIntervalMs = 33;

    /// <summary>
    /// Optimal screen edge buffer in pixels
    /// Prevents windows from being pushed too close to screen edges
    /// </summary>
    public const int ScreenEdgeBuffer = 20;

    /// <summary>
    /// Optimal proximity threshold in pixels
    /// Balanced to avoid accidental triggers while maintaining responsiveness
    /// </summary>
    public const int ProximityThreshold = 50;

    /// <summary>
    /// Optimal push distance in pixels
    /// Moves windows far enough to be useful but not disruptive
    /// </summary>
    public const int PushDistance = 100;

    /// <summary>
    /// Optimal CTRL release tolerance distance in pixels
    /// Allows fine cursor movements without re-triggering phobia
    /// </summary>
    public const int CtrlReleaseToleranceDistance = 50;

    /// <summary>
    /// Optimal always-on-top repel border distance in pixels
    /// Provides smooth interaction with always-on-top windows
    /// </summary>
    public const int AlwaysOnTopRepelBorderDistance = 30;

    /// <summary>
    /// Optimal animation enablement state
    /// Smooth animations improve user experience
    /// </summary>
    public const bool EnableAnimations = true;

    /// <summary>
    /// Optimal animation duration in milliseconds
    /// Fast enough to be responsive, slow enough to be smooth
    /// </summary>
    public const int AnimationDurationMs = 200;

    /// <summary>
    /// Optimal animation easing type
    /// EaseOut provides natural deceleration that feels intuitive
    /// </summary>
    public static readonly AnimationEasing AnimationEasing = AnimationEasing.EaseOut;
}