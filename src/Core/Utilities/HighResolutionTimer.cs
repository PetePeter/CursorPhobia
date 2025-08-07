using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CursorPhobia.Core.Utilities;

/// <summary>
/// High-resolution timer that provides more accurate timing than System.Timers.Timer
/// Uses multimedia timers on Windows for sub-millisecond precision
/// </summary>
public sealed class HighResolutionTimer : IDisposable
{
    private readonly object _lock = new();
    private readonly int _intervalMs;
    private readonly System.Threading.Timer _fallbackTimer;
    private volatile bool _isRunning = false;
    private volatile bool _disposed = false;

    // Windows multimedia timer imports for high resolution timing
    [DllImport("winmm.dll", SetLastError = true)]
    private static extern uint timeSetEvent(uint uDelay, uint uResolution, TimerCallback lpTimeProc, IntPtr dwUser, uint fuEvent);

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern uint timeKillEvent(uint uTimerID);

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern uint timeBeginPeriod(uint uPeriod);

    [DllImport("winmm.dll", SetLastError = true)]
    private static extern uint timeEndPeriod(uint uPeriod);

    private delegate void TimerCallback(uint uTimerID, uint uMsg, IntPtr dwUser, IntPtr dw1, IntPtr dw2);

    private uint _timerId = 0;
    private readonly TimerCallback _timerCallback;
    private GCHandle _callbackHandle; // Prevent garbage collection of callback

    /// <summary>
    /// Event fired when timer elapses
    /// </summary>
    public event EventHandler? Elapsed;

    /// <summary>
    /// Creates a new high-resolution timer
    /// </summary>
    /// <param name="intervalMs">Timer interval in milliseconds</param>
    public HighResolutionTimer(int intervalMs)
    {
        if (intervalMs <= 0)
            throw new ArgumentException("Interval must be greater than 0", nameof(intervalMs));

        _intervalMs = intervalMs;
        _timerCallback = OnTimerCallback;
        
        // Pin the callback to prevent garbage collection
        _callbackHandle = GCHandle.Alloc(_timerCallback);

        // Fallback timer for systems that don't support multimedia timers
        _fallbackTimer = new System.Threading.Timer(OnFallbackTimer, null, Timeout.Infinite, Timeout.Infinite);

        // Set system timer resolution for better accuracy
        try
        {
            timeBeginPeriod(1);
        }
        catch (Exception)
        {
            // Ignore errors, fallback to standard timer
        }
    }

    /// <summary>
    /// Starts the timer
    /// </summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_disposed || _isRunning) return;

            try
            {
                // Try to use high-resolution multimedia timer
                _timerId = timeSetEvent(
                    (uint)_intervalMs,
                    1, // 1ms resolution
                    _timerCallback,
                    IntPtr.Zero,
                    0x01 // TIME_PERIODIC
                );

                if (_timerId != 0)
                {
                    _isRunning = true;
                }
                else
                {
                    // Fallback to standard timer
                    _fallbackTimer.Change(_intervalMs, _intervalMs);
                    _isRunning = true;
                }
            }
            catch (Exception)
            {
                // Fallback to standard timer
                _fallbackTimer.Change(_intervalMs, _intervalMs);
                _isRunning = true;
            }
        }
    }

    /// <summary>
    /// Stops the timer
    /// </summary>
    public void Stop()
    {
        lock (_lock)
        {
            if (!_isRunning) return;

            if (_timerId != 0)
            {
                try
                {
                    timeKillEvent(_timerId);
                }
                catch (Exception)
                {
                    // Ignore cleanup errors
                }
                _timerId = 0;
            }

            _fallbackTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _isRunning = false;
        }
    }

    /// <summary>
    /// Gets whether the timer is currently running
    /// </summary>
    public bool IsRunning => _isRunning && !_disposed;

    /// <summary>
    /// Callback for multimedia timer
    /// </summary>
    private void OnTimerCallback(uint uTimerID, uint uMsg, IntPtr dwUser, IntPtr dw1, IntPtr dw2)
    {
        if (!_disposed && _isRunning)
        {
            try
            {
                Elapsed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception)
            {
                // Ignore callback errors to prevent timer from stopping
            }
        }
    }

    /// <summary>
    /// Callback for fallback timer
    /// </summary>
    private void OnFallbackTimer(object? state)
    {
        if (!_disposed && _isRunning)
        {
            try
            {
                Elapsed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception)
            {
                // Ignore callback errors to prevent timer from stopping
            }
        }
    }

    /// <summary>
    /// Disposes the timer and releases resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;

            Stop();

            try
            {
                timeEndPeriod(1);
            }
            catch (Exception)
            {
                // Ignore cleanup errors
            }

            _fallbackTimer?.Dispose();
            
            // Free the pinned callback handle
            if (_callbackHandle.IsAllocated)
            {
                _callbackHandle.Free();
            }
            
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}