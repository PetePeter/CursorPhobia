using System.Windows.Forms;
using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Manages temporary snooze functionality for CursorPhobia
/// </summary>
public class SnoozeManager : ISnoozeManager
{
    private readonly ILogger _logger;
    private readonly ICursorPhobiaEngine _engine;
    private readonly System.Windows.Forms.Timer _countdownTimer;
    private DateTime? _snoozeEndTime;
    private bool _wasEngineRunningBeforeSnooze;
    private volatile bool _disposed;

    public SnoozeManager(ILogger logger, ICursorPhobiaEngine engine)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));

        // Initialize countdown timer
        _countdownTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000, // Update every second
            Enabled = false
        };
        _countdownTimer.Tick += OnCountdownTimerTick;
    }

    /// <inheritdoc />
    public bool IsSnoozing => _snoozeEndTime.HasValue && DateTime.Now < _snoozeEndTime.Value;

    /// <inheritdoc />
    public DateTime? SnoozeEndTime => _snoozeEndTime;

    /// <inheritdoc />
    public TimeSpan? RemainingSnoozeTime
    {
        get
        {
            if (!IsSnoozing) return null;

            var remaining = _snoozeEndTime!.Value - DateTime.Now;
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    /// <inheritdoc />
    public async Task SnoozeAsync(TimeSpan duration)
    {
        if (_disposed) return;

        try
        {
            if (duration <= TimeSpan.Zero)
            {
                _logger.LogWarning("Invalid snooze duration: {Duration}", duration);
                return;
            }

            // If already snoozing, extend the current snooze
            if (IsSnoozing)
            {
                _snoozeEndTime = DateTime.Now.Add(duration);
                _logger.LogInformation("Extended snooze period to {EndTime}", _snoozeEndTime);
            }
            else
            {
                // Remember if the engine was running before snooze
                _wasEngineRunningBeforeSnooze = _engine.IsRunning;

                // Stop the engine if it's running
                if (_engine.IsRunning)
                {
                    await _engine.StopAsync();
                }

                _snoozeEndTime = DateTime.Now.Add(duration);
                _countdownTimer.Start();

                _logger.LogInformation("Started snooze period until {EndTime} (duration: {Duration})",
                    _snoozeEndTime, duration);
            }

            // Notify listeners
            var eventArgs = new SnoozeEventArgs(_snoozeEndTime, RemainingSnoozeTime, true, GetSnoozeStatusText());
            SnoozeStarted?.Invoke(this, eventArgs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting snooze period");
        }
    }

    /// <inheritdoc />
    public async Task EndSnoozeAsync()
    {
        if (_disposed) return;

        try
        {
            if (!IsSnoozing)
            {
                _logger.LogDebug("EndSnooze called but not currently snoozing");
                return;
            }

            var wasSnoozing = IsSnoozing;
            _snoozeEndTime = null;
            _countdownTimer.Stop();

            // Restart the engine if it was running before snooze
            if (_wasEngineRunningBeforeSnooze && !_engine.IsRunning)
            {
                var started = await _engine.StartAsync();
                if (started)
                {
                    _logger.LogInformation("Engine restarted after snooze ended");
                }
                else
                {
                    _logger.LogWarning("Failed to restart engine after snooze ended");
                }
            }

            _logger.LogInformation("Snooze period ended");

            // Notify listeners
            if (wasSnoozing)
            {
                var eventArgs = new SnoozeEventArgs(null, null, false, "Snooze ended");
                SnoozeEnded?.Invoke(this, eventArgs);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ending snooze period");
        }
    }

    /// <inheritdoc />
    public string? GetSnoozeStatusText()
    {
        if (!IsSnoozing) return null;

        var remaining = RemainingSnoozeTime;
        if (!remaining.HasValue) return "Snoozed";

        var totalMinutes = (int)remaining.Value.TotalMinutes;
        var seconds = remaining.Value.Seconds;

        if (totalMinutes >= 60)
        {
            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;
            return minutes > 0 ? $"Snoozed: {hours}h {minutes}m" : $"Snoozed: {hours}h";
        }
        else if (totalMinutes > 0)
        {
            return seconds > 0 ? $"Snoozed: {totalMinutes}m {seconds}s" : $"Snoozed: {totalMinutes}m";
        }
        else
        {
            return $"Snoozed: {seconds}s";
        }
    }

    /// <inheritdoc />
    public event EventHandler<SnoozeEventArgs>? SnoozeStarted;

    /// <inheritdoc />
    public event EventHandler<SnoozeEventArgs>? SnoozeEnded;

    /// <inheritdoc />
    public event EventHandler<SnoozeEventArgs>? SnoozeCountdownUpdated;

    private async void OnCountdownTimerTick(object? sender, EventArgs e)
    {
        try
        {
            if (!IsSnoozing)
            {
                _countdownTimer.Stop();
                await EndSnoozeAsync();
                return;
            }

            // Fire countdown update event
            var eventArgs = new SnoozeEventArgs(_snoozeEndTime, RemainingSnoozeTime, true, GetSnoozeStatusText());
            SnoozeCountdownUpdated?.Invoke(this, eventArgs);

            // Check if snooze period has ended
            if (RemainingSnoozeTime == TimeSpan.Zero)
            {
                await EndSnoozeAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in snooze countdown timer");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;

        try
        {
            _countdownTimer?.Stop();
            _countdownTimer?.Dispose();

            // End any active snooze
            if (IsSnoozing)
            {
                try
                {
                    // Use ConfigureAwait(false) and handle exceptions properly
                    Task.Run(async () => await EndSnoozeAsync().ConfigureAwait(false));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error ending snooze during disposal");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing SnoozeManager");
        }
    }

    /// <summary>
    /// Gets predefined snooze durations for quick access
    /// </summary>
    public static readonly Dictionary<string, TimeSpan> PredefinedDurations = new()
    {
        { "5 minutes", TimeSpan.FromMinutes(5) },
        { "15 minutes", TimeSpan.FromMinutes(15) },
        { "30 minutes", TimeSpan.FromMinutes(30) },
        { "1 hour", TimeSpan.FromHours(1) },
        { "2 hours", TimeSpan.FromHours(2) },
        { "Until restart", TimeSpan.FromDays(1) } // Long duration that effectively lasts until restart
    };
}