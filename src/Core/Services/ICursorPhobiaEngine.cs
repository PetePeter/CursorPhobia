using CursorPhobia.Core.Models;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Interface for the main CursorPhobia engine
/// </summary>
public interface ICursorPhobiaEngine
{
    /// <summary>
    /// Gets whether the engine is currently running
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the number of windows currently being tracked
    /// </summary>
    int TrackedWindowCount { get; }

    /// <summary>
    /// Gets the average update cycle time in milliseconds
    /// </summary>
    double AverageUpdateTimeMs { get; }

    /// <summary>
    /// Event raised when the engine starts
    /// </summary>
    event EventHandler? EngineStarted;

    /// <summary>
    /// Event raised when the engine stops
    /// </summary>
    event EventHandler? EngineStopped;

    /// <summary>
    /// Event raised when a window push operation occurs
    /// </summary>
    event EventHandler<WindowPushEventArgs>? WindowPushed;

    /// <summary>
    /// Event raised when the engine state changes (for tray notifications)
    /// </summary>
    event EventHandler<EngineStateChangedEventArgs>? EngineStateChanged;

    /// <summary>
    /// Event raised when performance issues are detected (for tray warnings)
    /// </summary>
    event EventHandler<EnginePerformanceEventArgs>? PerformanceIssueDetected;

    /// <summary>
    /// Event raised when configuration is updated (for UI updates and logging)
    /// </summary>
    event EventHandler<ConfigurationUpdatedEventArgs>? ConfigurationUpdated;

    /// <summary>
    /// Starts the cursor phobia engine
    /// </summary>
    /// <returns>True if started successfully, false otherwise</returns>
    Task<bool> StartAsync();

    /// <summary>
    /// Stops the cursor phobia engine
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Forces a refresh of the tracked windows list
    /// </summary>
    Task RefreshTrackedWindowsAsync();

    /// <summary>
    /// Updates the engine configuration with hot-swapping support
    /// </summary>
    /// <param name="newConfiguration">The new configuration to apply</param>
    /// <returns>Result of the configuration update operation</returns>
    Task<ConfigurationUpdateResult> UpdateConfigurationAsync(CursorPhobiaConfiguration newConfiguration);

    /// <summary>
    /// Gets current performance statistics
    /// </summary>
    /// <returns>Performance statistics object</returns>
    EnginePerformanceStats GetPerformanceStats();

    /// <summary>
    /// Gets the effective wrap behavior for a window on a specific monitor
    /// </summary>
    /// <param name="windowBounds">Window bounds to determine monitor</param>
    /// <returns>Wrap behavior configuration for the monitor</returns>
    WrapBehavior GetEffectiveWrapBehavior(Rectangle windowBounds);
}