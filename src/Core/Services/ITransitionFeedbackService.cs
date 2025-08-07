using System.Drawing;
using CursorPhobia.Core.Models;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Interface for providing visual feedback during cross-monitor window transitions
/// </summary>
public interface ITransitionFeedbackService
{
    /// <summary>
    /// Shows visual feedback for a cross-monitor window transition
    /// </summary>
    /// <param name="windowHandle">Handle to the window being transitioned</param>
    /// <param name="sourceMonitor">Source monitor information</param>
    /// <param name="targetMonitor">Target monitor information</param>
    /// <param name="feedbackType">Type of feedback to display</param>
    /// <param name="durationMs">Duration of feedback in milliseconds</param>
    /// <returns>Task representing the feedback operation</returns>
    Task ShowTransitionFeedbackAsync(IntPtr windowHandle, MonitorInfo sourceMonitor, MonitorInfo targetMonitor, 
        TransitionFeedbackType feedbackType, int durationMs);

    /// <summary>
    /// Shows subtle border highlight around the target monitor during transition
    /// </summary>
    /// <param name="targetMonitor">Monitor to highlight</param>
    /// <param name="durationMs">Duration of highlight in milliseconds</param>
    /// <returns>Task representing the highlight operation</returns>
    Task ShowMonitorHighlightAsync(MonitorInfo targetMonitor, int durationMs);

    /// <summary>
    /// Shows animation feedback for window movement between monitors
    /// </summary>
    /// <param name="windowBounds">Current window bounds</param>
    /// <param name="sourcePosition">Source position</param>
    /// <param name="targetPosition">Target position</param>
    /// <param name="durationMs">Duration of animation feedback</param>
    /// <returns>Task representing the animation operation</returns>
    Task ShowMovementAnimationAsync(Rectangle windowBounds, Point sourcePosition, Point targetPosition, int durationMs);
}