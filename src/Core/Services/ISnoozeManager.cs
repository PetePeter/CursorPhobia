namespace CursorPhobia.Core.Services;

/// <summary>
/// Service for managing temporary snooze functionality
/// </summary>
public interface ISnoozeManager : IDisposable
{
    /// <summary>
    /// Gets whether the application is currently snoozed
    /// </summary>
    bool IsSnoozing { get; }

    /// <summary>
    /// Gets the time when the current snooze period will end
    /// </summary>
    DateTime? SnoozeEndTime { get; }

    /// <summary>
    /// Gets the remaining snooze time
    /// </summary>
    TimeSpan? RemainingSnoozeTime { get; }

    /// <summary>
    /// Starts a snooze period for the specified duration
    /// </summary>
    /// <param name="duration">How long to snooze</param>
    Task SnoozeAsync(TimeSpan duration);

    /// <summary>
    /// Ends the current snooze period early
    /// </summary>
    Task EndSnoozeAsync();

    /// <summary>
    /// Gets the current snooze status as a formatted string
    /// </summary>
    /// <returns>Formatted snooze status or null if not snoozing</returns>
    string? GetSnoozeStatusText();

    /// <summary>
    /// Event fired when a snooze period starts
    /// </summary>
    event EventHandler<SnoozeEventArgs>? SnoozeStarted;

    /// <summary>
    /// Event fired when a snooze period ends
    /// </summary>
    event EventHandler<SnoozeEventArgs>? SnoozeEnded;

    /// <summary>
    /// Event fired every second during snooze for countdown updates
    /// </summary>
    event EventHandler<SnoozeEventArgs>? SnoozeCountdownUpdated;
}

/// <summary>
/// Event arguments for snooze-related events
/// </summary>
public class SnoozeEventArgs : EventArgs
{
    public DateTime? SnoozeEndTime { get; }
    public TimeSpan? RemainingTime { get; }
    public bool IsSnoozing { get; }
    public string? StatusText { get; }

    public SnoozeEventArgs(DateTime? snoozeEndTime, TimeSpan? remainingTime, bool isSnoozing, string? statusText = null)
    {
        SnoozeEndTime = snoozeEndTime;
        RemainingTime = remainingTime;
        IsSnoozing = isSnoozing;
        StatusText = statusText;
    }
}