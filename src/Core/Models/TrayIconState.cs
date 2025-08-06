namespace CursorPhobia.Core.Models;

/// <summary>
/// Represents the visual state of the system tray icon
/// Corresponds to different engine states and conditions
/// </summary>
public enum TrayIconState
{
    /// <summary>
    /// Engine is running and functioning normally (green icon)
    /// </summary>
    Enabled,

    /// <summary>
    /// Engine is stopped or disabled (red icon)
    /// </summary>
    Disabled,

    /// <summary>
    /// Engine is running but has performance issues or warnings (yellow icon)
    /// </summary>
    Warning,

    /// <summary>
    /// Engine has encountered an error or cannot function (gray icon)
    /// </summary>
    Error
}