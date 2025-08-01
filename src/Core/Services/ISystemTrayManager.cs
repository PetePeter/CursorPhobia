using CursorPhobia.Core.Models;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Interface for managing the system tray icon and context menu
/// Provides tray icon state management and menu interaction handling
/// </summary>
public interface ISystemTrayManager : IDisposable
{
    /// <summary>
    /// Event raised when the user requests to toggle enable/disable from the tray menu
    /// </summary>
    event EventHandler? ToggleEngineRequested;
    
    /// <summary>
    /// Event raised when the user requests to open settings from the tray menu
    /// </summary>
    event EventHandler? SettingsRequested;
    
    /// <summary>
    /// Event raised when the user requests to view about information from the tray menu
    /// </summary>
    event EventHandler? AboutRequested;
    
    /// <summary>
    /// Event raised when the user requests to exit the application from the tray menu
    /// </summary>
    event EventHandler? ExitRequested;
    
    /// <summary>
    /// Gets whether the tray manager is currently initialized and visible
    /// </summary>
    bool IsInitialized { get; }
    
    /// <summary>
    /// Gets the current tray icon state
    /// </summary>
    TrayIconState CurrentState { get; }
    
    /// <summary>
    /// Initializes the system tray icon and context menu
    /// </summary>
    /// <returns>True if initialization was successful, false otherwise</returns>
    Task<bool> InitializeAsync();
    
    /// <summary>
    /// Updates the tray icon state and tooltip
    /// </summary>
    /// <param name="state">New tray icon state</param>
    /// <param name="tooltipText">Optional custom tooltip text, uses default if null</param>
    Task UpdateStateAsync(TrayIconState state, string? tooltipText = null);
    
    /// <summary>
    /// Updates the menu state (enabled/disabled text) based on engine status
    /// </summary>
    /// <param name="isEngineEnabled">Whether the engine is currently running</param>
    Task UpdateMenuStateAsync(bool isEngineEnabled);
    
    /// <summary>
    /// Shows a balloon notification from the tray icon
    /// </summary>
    /// <param name="title">Notification title</param>
    /// <param name="message">Notification message</param>
    /// <param name="isError">Whether this is an error notification</param>
    Task ShowNotificationAsync(string title, string message, bool isError = false);
    
    /// <summary>
    /// Hides the tray icon and cleans up resources
    /// </summary>
    Task HideAsync();
}