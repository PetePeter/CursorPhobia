using System.Drawing;
using System.Runtime.InteropServices;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Utilities;
using CursorPhobia.Core.WindowsAPI;
using static CursorPhobia.Core.WindowsAPI.WindowsStructures;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Service for tracking global mouse cursor position and movement using Windows hooks
/// </summary>
public class CursorTracker : ICursorTracker, IDisposable
{
    private readonly ILogger _logger;
    private readonly CursorPhobiaConfiguration _config;
    private readonly object _lockObject = new();

    private IntPtr _hookHandle = IntPtr.Zero;
    private LowLevelMouseProc? _mouseHookDelegate;
    private volatile bool _isTracking = false;
    private volatile bool _disposed = false;

    // Cursor state
    private Point _lastCursorPosition = Point.Empty;
    private DateTime _lastUpdateTime = DateTime.MinValue;
    private readonly System.Timers.Timer _fallbackTimer;

    // Events
    public event EventHandler<CursorMovedEventArgs>? CursorMoved;
    public event EventHandler<Point>? CursorPositionChanged;

    /// <summary>
    /// Gets the current cursor position in screen coordinates
    /// </summary>
    public Point CurrentPosition
    {
        get
        {
            lock (_lockObject)
            {
                return _lastCursorPosition;
            }
        }
    }

    /// <summary>
    /// Gets whether the cursor tracker is currently active
    /// </summary>
    public bool IsTracking => _isTracking && !_disposed;

    /// <summary>
    /// Creates a new CursorTracker instance
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="config">Configuration for tracking behavior</param>
    public CursorTracker(ILogger logger, CursorPhobiaConfiguration? config = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? CursorPhobiaConfiguration.CreateDefault();

        var validationErrors = _config.Validate();
        if (validationErrors.Count > 0)
        {
            throw new ArgumentException($"Invalid cursor tracker configuration: {string.Join(", ", validationErrors)}");
        }

        // Initialize fallback timer for when hooks fail
        _fallbackTimer = new System.Timers.Timer(_config.MaxUpdateIntervalMs);
        _fallbackTimer.Elapsed += FallbackTimer_Elapsed;
        _fallbackTimer.AutoReset = true;

        _logger.LogDebug("CursorTracker initialized with update interval: {UpdateInterval}ms, max interval: {MaxInterval}ms",
            _config.UpdateIntervalMs, _config.MaxUpdateIntervalMs);
    }

    /// <summary>
    /// Starts tracking cursor position globally
    /// </summary>
    /// <returns>True if tracking started successfully, false otherwise</returns>
    public bool StartTracking()
    {
        if (_disposed)
        {
            _logger.LogWarning("Cannot start tracking on disposed CursorTracker");
            return false;
        }

        lock (_lockObject)
        {
            if (_isTracking)
            {
                _logger.LogDebug("CursorTracker is already tracking");
                return true;
            }

            try
            {
                // Get initial cursor position
                if (User32.GetCursorPos(out var point))
                {
                    _lastCursorPosition = point.ToPoint();
                    _lastUpdateTime = DateTime.UtcNow;
                    _logger.LogDebug("Initial cursor position: ({X},{Y})", _lastCursorPosition.X, _lastCursorPosition.Y);
                }
                else
                {
                    _logger.LogWarning("Failed to get initial cursor position");
                }

                // Install mouse hook
                if (InstallMouseHook())
                {
                    _isTracking = true;
                    _logger.LogInformation("CursorTracker started successfully with Windows hook");
                    return true;
                }
                else
                {
                    // Fall back to timer-based polling
                    _logger.LogWarning("Failed to install mouse hook, falling back to timer-based tracking");
                    _fallbackTimer.Start();
                    _isTracking = true;
                    _logger.LogInformation("CursorTracker started with fallback timer polling");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start cursor tracking");
                return false;
            }
        }
    }

    /// <summary>
    /// Stops tracking cursor position
    /// </summary>
    public void StopTracking()
    {
        lock (_lockObject)
        {
            if (!_isTracking)
            {
                _logger.LogDebug("CursorTracker is already stopped");
                return;
            }

            try
            {
                _isTracking = false;

                // Stop fallback timer
                _fallbackTimer.Stop();

                // Uninstall mouse hook
                UninstallMouseHook();

                _logger.LogInformation("CursorTracker stopped successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping cursor tracking");
            }
        }
    }

    /// <summary>
    /// Gets the current screen cursor position without relying on cached values
    /// </summary>
    /// <returns>Current cursor position, or Point.Empty if failed</returns>
    public Point GetCurrentCursorPosition()
    {
        try
        {
            if (User32.GetCursorPos(out var point))
            {
                return point.ToPoint();
            }
            else
            {
                _logger.LogWarning("Failed to get current cursor position from Windows API");
                return Point.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting current cursor position");
            return Point.Empty;
        }
    }

    /// <summary>
    /// Checks if the CTRL key is currently pressed (safety override)
    /// </summary>
    /// <returns>True if any CTRL key is pressed</returns>
    public bool IsCtrlKeyPressed()
    {
        if (!_config.EnableCtrlOverride)
            return false;

        try
        {
            // Check both left and right CTRL keys
            var leftCtrl = (User32.GetAsyncKeyState(VK_LCONTROL) & 0x8000) != 0;
            var rightCtrl = (User32.GetAsyncKeyState(VK_RCONTROL) & 0x8000) != 0;
            var genericCtrl = (User32.GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;

            return leftCtrl || rightCtrl || genericCtrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking CTRL key state");
            return false; // Default to not pressed on error for safety
        }
    }

    #region Private Methods

    /// <summary>
    /// Installs the low-level mouse hook
    /// </summary>
    private bool InstallMouseHook()
    {
        try
        {
            // Create the hook delegate (must be kept alive)
            _mouseHookDelegate = MouseHookProc;

            // Get the module handle for the current process
            var moduleHandle = Kernel32.GetModuleHandle(null);
            if (moduleHandle == IntPtr.Zero)
            {
                _logger.LogError("Failed to get module handle for mouse hook");
                return false;
            }

            // Install the hook
            _hookHandle = User32.SetWindowsHookEx(
                WH_MOUSE_LL,
                _mouseHookDelegate,
                moduleHandle,
                0);

            if (_hookHandle == IntPtr.Zero)
            {
                var lastError = Kernel32.GetLastError();
                _logger.LogError("Failed to install mouse hook. Error code: {ErrorCode}", lastError);
                return false;
            }

            _logger.LogDebug("Mouse hook installed successfully with handle: {Handle:X}", _hookHandle.ToInt64());
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception installing mouse hook");
            return false;
        }
    }

    /// <summary>
    /// Uninstalls the mouse hook
    /// </summary>
    private void UninstallMouseHook()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            try
            {
                if (User32.UnhookWindowsHookEx(_hookHandle))
                {
                    _logger.LogDebug("Mouse hook uninstalled successfully");
                }
                else
                {
                    var lastError = Kernel32.GetLastError();
                    _logger.LogWarning("Failed to uninstall mouse hook. Error code: {ErrorCode}", lastError);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception uninstalling mouse hook");
            }
            finally
            {
                _hookHandle = IntPtr.Zero;
                _mouseHookDelegate = null;
            }
        }
    }

    /// <summary>
    /// Low-level mouse hook procedure
    /// </summary>
    private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        try
        {
            if (nCode >= HC_ACTION && _isTracking && !_disposed)
            {
                var messageType = wParam.ToInt32();

                // We're primarily interested in mouse move events
                if (messageType == WM_MOUSEMOVE)
                {
                    // Extract mouse data from lParam
                    var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                    var newPosition = hookStruct.pt.ToPoint();

                    // Throttle updates based on configuration
                    var now = DateTime.UtcNow;
                    var timeSinceLastUpdate = (now - _lastUpdateTime).TotalMilliseconds;

                    if (timeSinceLastUpdate >= _config.UpdateIntervalMs)
                    {
                        UpdateCursorPosition(newPosition);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in mouse hook procedure");
        }

        // Always call the next hook in the chain
        return User32.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    /// <summary>
    /// Fallback timer handler for when mouse hook is not available
    /// </summary>
    private void FallbackTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (!_isTracking || _disposed)
            return;

        try
        {
            var currentPosition = GetCurrentCursorPosition();
            if (currentPosition != Point.Empty && currentPosition != _lastCursorPosition)
            {
                UpdateCursorPosition(currentPosition);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in fallback timer cursor update");
        }
    }

    /// <summary>
    /// Updates the cursor position and raises appropriate events
    /// </summary>
    private void UpdateCursorPosition(Point newPosition)
    {
        Point oldPosition;

        lock (_lockObject)
        {
            if (newPosition == _lastCursorPosition)
                return;

            oldPosition = _lastCursorPosition;
            _lastCursorPosition = newPosition;
            _lastUpdateTime = DateTime.UtcNow;
        }

        try
        {
            // Raise events
            CursorPositionChanged?.Invoke(this, newPosition);

            var moveArgs = new CursorMovedEventArgs(oldPosition, newPosition);
            CursorMoved?.Invoke(this, moveArgs);

            _logger.LogDebug("Cursor moved from ({OldX},{OldY}) to ({NewX},{NewY})",
                oldPosition.X, oldPosition.Y, newPosition.X, newPosition.Y);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error raising cursor position events");
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the cursor tracker and releases all resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Protected dispose method
    /// </summary>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                try
                {
                    // Stop tracking first
                    StopTracking();

                    // Dispose timer
                    _fallbackTimer?.Dispose();

                    _logger.LogDebug("CursorTracker disposed successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during CursorTracker disposal");
                }
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Finalizer
    /// </summary>
    ~CursorTracker()
    {
        Dispose(false);
    }

    #endregion
}

/// <summary>
/// Interface for cursor tracking service
/// </summary>
public interface ICursorTracker
{
    /// <summary>
    /// Gets the current cursor position in screen coordinates
    /// </summary>
    Point CurrentPosition { get; }

    /// <summary>
    /// Gets whether the cursor tracker is currently active
    /// </summary>
    bool IsTracking { get; }

    /// <summary>
    /// Event raised when cursor position changes
    /// </summary>
    event EventHandler<Point>? CursorPositionChanged;

    /// <summary>
    /// Event raised when cursor moves (includes old and new positions)
    /// </summary>
    event EventHandler<CursorMovedEventArgs>? CursorMoved;

    /// <summary>
    /// Starts tracking cursor position globally
    /// </summary>
    /// <returns>True if tracking started successfully</returns>
    bool StartTracking();

    /// <summary>
    /// Stops tracking cursor position
    /// </summary>
    void StopTracking();

    /// <summary>
    /// Gets the current screen cursor position without relying on cached values
    /// </summary>
    /// <returns>Current cursor position</returns>
    Point GetCurrentCursorPosition();

    /// <summary>
    /// Checks if the CTRL key is currently pressed (safety override)
    /// </summary>
    /// <returns>True if any CTRL key is pressed</returns>
    bool IsCtrlKeyPressed();
}

/// <summary>
/// Event arguments for cursor movement events
/// </summary>
public class CursorMovedEventArgs : EventArgs
{
    /// <summary>
    /// Previous cursor position
    /// </summary>
    public Point OldPosition { get; }

    /// <summary>
    /// New cursor position
    /// </summary>
    public Point NewPosition { get; }

    /// <summary>
    /// Distance moved (Euclidean distance)
    /// </summary>
    public double Distance { get; }

    /// <summary>
    /// Creates new cursor moved event arguments
    /// </summary>
    /// <param name="oldPosition">Previous position</param>
    /// <param name="newPosition">New position</param>
    public CursorMovedEventArgs(Point oldPosition, Point newPosition)
    {
        OldPosition = oldPosition;
        NewPosition = newPosition;

        var dx = newPosition.X - oldPosition.X;
        var dy = newPosition.Y - oldPosition.Y;
        Distance = Math.Sqrt(dx * dx + dy * dy);
    }
}