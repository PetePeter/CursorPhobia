using System.Drawing;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Service for providing visual feedback during cross-monitor window transitions
/// Implements the recommendations from UX evaluation for better user experience
/// </summary>
public class TransitionFeedbackService : ITransitionFeedbackService, IDisposable
{
    private readonly ILogger _logger;
    private readonly ISystemTrayManager? _trayManager;
    private readonly object _lockObject = new();
    private volatile bool _disposed = false;
    
    // Track active feedback operations to prevent overlap
    private readonly HashSet<IntPtr> _activeFeedbackWindows = new();
    private readonly HashSet<IntPtr> _activeMonitorHighlights = new();

    public TransitionFeedbackService(ILogger logger, ISystemTrayManager? trayManager = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _trayManager = trayManager;

        _logger.LogDebug("TransitionFeedbackService initialized");
    }

    /// <summary>
    /// Shows visual feedback for a cross-monitor window transition
    /// </summary>
    /// <param name="windowHandle">Handle to the window being transitioned</param>
    /// <param name="sourceMonitor">Source monitor information</param>
    /// <param name="targetMonitor">Target monitor information</param>
    /// <param name="feedbackType">Type of feedback to display</param>
    /// <param name="durationMs">Duration of feedback in milliseconds</param>
    /// <returns>Task representing the feedback operation</returns>
    public async Task ShowTransitionFeedbackAsync(IntPtr windowHandle, MonitorInfo sourceMonitor, 
        MonitorInfo targetMonitor, TransitionFeedbackType feedbackType, int durationMs)
    {
        if (_disposed || windowHandle == IntPtr.Zero || sourceMonitor == null || targetMonitor == null)
            return;

        // Prevent overlapping feedback for the same window
        lock (_lockObject)
        {
            if (_activeFeedbackWindows.Contains(windowHandle))
            {
                _logger.LogDebug("Feedback already active for window {Handle:X}, skipping", windowHandle.ToInt64());
                return;
            }
            _activeFeedbackWindows.Add(windowHandle);
        }

        try
        {
            _logger.LogDebug("Showing transition feedback: {WindowHandle:X} from {SourceMonitor} to {TargetMonitor}, type: {FeedbackType}",
                windowHandle.ToInt64(), sourceMonitor.deviceName, targetMonitor.deviceName, feedbackType);

            switch (feedbackType)
            {
                case TransitionFeedbackType.None:
                    break;

                case TransitionFeedbackType.Subtle:
                    await ShowSubtleFeedbackAsync(targetMonitor, durationMs);
                    break;

                case TransitionFeedbackType.Animation:
                    // Animation feedback will be handled by WindowPusher during the actual move
                    await ShowMonitorHighlightAsync(targetMonitor, durationMs / 2);
                    break;

                case TransitionFeedbackType.Toast:
                    await ShowToastFeedbackAsync(sourceMonitor, targetMonitor, durationMs);
                    break;

                case TransitionFeedbackType.TrayNotification:
                    await ShowTrayNotificationFeedbackAsync(sourceMonitor, targetMonitor);
                    break;

                default:
                    _logger.LogWarning("Unknown feedback type: {FeedbackType}", feedbackType);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing transition feedback for window {Handle:X}", windowHandle.ToInt64());
        }
        finally
        {
            lock (_lockObject)
            {
                _activeFeedbackWindows.Remove(windowHandle);
            }
        }
    }

    /// <summary>
    /// Shows subtle border highlight around the target monitor during transition
    /// </summary>
    /// <param name="targetMonitor">Monitor to highlight</param>
    /// <param name="durationMs">Duration of highlight in milliseconds</param>
    /// <returns>Task representing the highlight operation</returns>
    public async Task ShowMonitorHighlightAsync(MonitorInfo targetMonitor, int durationMs)
    {
        if (_disposed || targetMonitor == null)
            return;

        // Prevent overlapping highlights for the same monitor
        lock (_lockObject)
        {
            if (_activeMonitorHighlights.Contains(targetMonitor.monitorHandle))
            {
                _logger.LogDebug("Highlight already active for monitor {MonitorName}, skipping", targetMonitor.deviceName);
                return;
            }
            _activeMonitorHighlights.Add(targetMonitor.monitorHandle);
        }

        try
        {
            _logger.LogDebug("Showing monitor highlight for {Monitor} for {Duration}ms", 
                targetMonitor.deviceName, durationMs);

            // Create a subtle highlight effect around the monitor bounds
            // This would typically involve creating a semi-transparent overlay window
            // For now, we'll simulate with logging and optional tray notification
            
            await SimulateMonitorHighlightAsync(targetMonitor, durationMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing monitor highlight for {Monitor}", targetMonitor.deviceName);
        }
        finally
        {
            lock (_lockObject)
            {
                _activeMonitorHighlights.Remove(targetMonitor.monitorHandle);
            }
        }
    }

    /// <summary>
    /// Shows animation feedback for window movement between monitors
    /// </summary>
    /// <param name="windowBounds">Current window bounds</param>
    /// <param name="sourcePosition">Source position</param>
    /// <param name="targetPosition">Target position</param>
    /// <param name="durationMs">Duration of animation feedback</param>
    /// <returns>Task representing the animation operation</returns>
    public async Task ShowMovementAnimationAsync(Rectangle windowBounds, Point sourcePosition, 
        Point targetPosition, int durationMs)
    {
        if (_disposed)
            return;

        try
        {
            _logger.LogDebug("Showing movement animation from ({SourceX},{SourceY}) to ({TargetX},{TargetY}) over {Duration}ms",
                sourcePosition.X, sourcePosition.Y, targetPosition.X, targetPosition.Y, durationMs);

            // This could be enhanced with actual visual effects like:
            // - Phantom window that follows the movement path
            // - Trail effect showing the movement direction
            // - Brief screen flash at the destination
            
            await SimulateMovementAnimationAsync(sourcePosition, targetPosition, durationMs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing movement animation from ({SourceX},{SourceY}) to ({TargetX},{TargetY})",
                sourcePosition.X, sourcePosition.Y, targetPosition.X, targetPosition.Y);
        }
    }

    /// <summary>
    /// Shows subtle feedback for cross-monitor transitions
    /// </summary>
    private async Task ShowSubtleFeedbackAsync(MonitorInfo targetMonitor, int durationMs)
    {
        // Brief highlight of the target monitor
        await ShowMonitorHighlightAsync(targetMonitor, Math.Min(durationMs, 300));
    }

    /// <summary>
    /// Shows toast notification feedback
    /// </summary>
    private async Task ShowToastFeedbackAsync(MonitorInfo sourceMonitor, MonitorInfo targetMonitor, int durationMs)
    {
        try
        {
            // This would show a toast notification near the target monitor
            _logger.LogInformation("Window moved from {SourceMonitor} to {TargetMonitor}", 
                sourceMonitor.deviceName, targetMonitor.deviceName);
            
            // Simulate toast with delay
            await Task.Delay(Math.Min(durationMs, 2000));
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error showing toast feedback: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Shows system tray notification feedback
    /// </summary>
    private async Task ShowTrayNotificationFeedbackAsync(MonitorInfo sourceMonitor, MonitorInfo targetMonitor)
    {
        if (_trayManager == null)
        {
            _logger.LogDebug("No tray manager available for notification feedback");
            return;
        }

        try
        {
            var message = $"Window moved from {GetMonitorDisplayName(sourceMonitor)} to {GetMonitorDisplayName(targetMonitor)}";
            await _trayManager.ShowNotificationAsync("CursorPhobia", message, false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error showing tray notification feedback: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Simulates monitor highlight effect
    /// </summary>
    private async Task SimulateMonitorHighlightAsync(MonitorInfo monitor, int durationMs)
    {
        // In a full implementation, this would create a subtle overlay window
        // For now, we simulate with a brief delay and logging
        _logger.LogDebug("Highlighting monitor {Monitor} bounds: {Bounds}", 
            monitor.deviceName, monitor.monitorBounds);
        
        await Task.Delay(Math.Min(durationMs, 500));
    }

    /// <summary>
    /// Simulates movement animation effect
    /// </summary>
    private async Task SimulateMovementAnimationAsync(Point sourcePosition, Point targetPosition, int durationMs)
    {
        // In a full implementation, this would show a visual trail or phantom window
        // For now, we simulate with logging and delay
        var distance = Math.Sqrt(Math.Pow(targetPosition.X - sourcePosition.X, 2) + 
                                Math.Pow(targetPosition.Y - sourcePosition.Y, 2));
        
        _logger.LogDebug("Animating movement over {Distance:F0} pixels in {Duration}ms", distance, durationMs);
        
        await Task.Delay(Math.Min(durationMs / 4, 200)); // Brief animation preview
    }

    /// <summary>
    /// Gets a user-friendly display name for a monitor
    /// </summary>
    private string GetMonitorDisplayName(MonitorInfo monitor)
    {
        if (!string.IsNullOrEmpty(monitor.deviceName))
        {
            // Simplify device name for user display
            var displayName = monitor.deviceName.Replace(@"\\.\", "").Replace("DISPLAY", "Monitor ");
            if (monitor.isPrimary)
                displayName += " (Primary)";
            return displayName;
        }
        
        return monitor.isPrimary ? "Primary Monitor" : $"Monitor at ({monitor.x}, {monitor.y})";
    }

    /// <summary>
    /// Disposes the transition feedback service
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _logger.LogDebug("Disposing TransitionFeedbackService");

        lock (_lockObject)
        {
            _activeFeedbackWindows.Clear();
            _activeMonitorHighlights.Clear();
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}