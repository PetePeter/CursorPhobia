using System.Drawing;
using CursorPhobia.Core.Utilities;
using CursorPhobia.Core.WindowsAPI;
using static CursorPhobia.Core.WindowsAPI.WindowsStructures;
using ILogger = CursorPhobia.Core.Utilities.ILogger;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Interface for window manipulation operations with async support
/// </summary>
public interface IWindowManipulationService
{
    /// <summary>
    /// Moves a window to the specified coordinates (position only)
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="x">New X coordinate</param>
    /// <param name="y">New Y coordinate</param>
    /// <returns>True if successful, false otherwise</returns>
    bool MoveWindow(IntPtr hWnd, int x, int y);

    /// <summary>
    /// Moves a window to the specified coordinates asynchronously
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="x">New X coordinate</param>
    /// <param name="y">New Y coordinate</param>
    /// <returns>Task that returns true if successful, false otherwise</returns>
    Task<bool> MoveWindowAsync(IntPtr hWnd, int x, int y);

    /// <summary>
    /// Gets the bounding rectangle of a window
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <returns>Rectangle representing the window bounds</returns>
    Rectangle GetWindowBounds(IntPtr hWnd);

    /// <summary>
    /// Gets the bounding rectangle of a window asynchronously
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <returns>Task that returns the window bounds</returns>
    Task<Rectangle> GetWindowBoundsAsync(IntPtr hWnd);

    /// <summary>
    /// Determines if a window is visible
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <returns>True if the window is visible, false otherwise</returns>
    bool IsWindowVisible(IntPtr hWnd);
}

/// <summary>
/// Service for manipulating window positions and properties with batched operations
/// </summary>
public class WindowManipulationService : IWindowManipulationService, IDisposable
{
    private readonly ILogger _logger;
    private readonly IBatchedWindowOperations _batchedOperations;
    private volatile bool _disposed = false;

    /// <summary>
    /// Creates a new WindowManipulationService instance with batched operations
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="batchedOperations">Optional batched operations service for performance</param>
    public WindowManipulationService(ILogger logger, IBatchedWindowOperations? batchedOperations = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _batchedOperations = batchedOperations ?? new BatchedWindowOperations(logger);
    }

    /// <summary>
    /// Moves a window to the specified coordinates (position only, preserves size)
    /// Uses direct API call for synchronous compatibility
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="x">New X coordinate</param>
    /// <param name="y">New Y coordinate</param>
    /// <returns>True if successful, false otherwise</returns>
    public bool MoveWindow(IntPtr hWnd, int x, int y)
    {
        if (_disposed)
            return false;

        if (hWnd == IntPtr.Zero)
        {
            _logger.LogWarning("Invalid window handle provided to MoveWindow");
            return false;
        }

        try
        {
            // Direct synchronous call for backwards compatibility
            if (User32.GetWindowRect(hWnd, out var rect))
            {
                var currentBounds = rect.ToRectangle();
                var success = User32.SetWindowPos(
                    hWnd,
                    IntPtr.Zero,
                    x, y,
                    currentBounds.Width, currentBounds.Height,
                    WindowsStructures.SWP_NOZORDER | WindowsStructures.SWP_NOACTIVATE | WindowsStructures.SWP_NOREDRAW
                );
                
                if (success)
                {
                    _logger.LogDebug("Successfully moved window {Handle:X} to ({X}, {Y})",
                        hWnd.ToInt64(), x, y);
                }
                return success;
            }
            else
            {
                var errorCode = Kernel32.GetLastError();
                var errorMessage = Kernel32.GetErrorMessage(errorCode);
                _logger.LogError("Failed to get window bounds before move for {Handle:X}. Error: {Error}",
                    hWnd.ToInt64(), errorMessage);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while moving window {Handle:X} to ({X}, {Y})",
                hWnd.ToInt64(), x, y);
            return false;
        }
    }

    /// <summary>
    /// Moves a window to the specified coordinates asynchronously
    /// Uses batched operations for improved performance during animations
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="x">New X coordinate</param>
    /// <param name="y">New Y coordinate</param>
    /// <returns>Task that returns true if successful, false otherwise</returns>
    public async Task<bool> MoveWindowAsync(IntPtr hWnd, int x, int y)
    {
        if (_disposed)
            return false;

        if (hWnd == IntPtr.Zero)
        {
            _logger.LogWarning("Invalid window handle provided to MoveWindowAsync");
            return false;
        }

        try
        {
            // Use batched operations for better performance during animations
            var success = await _batchedOperations.QueueMoveWindowAsync(hWnd, x, y);
            if (success)
            {
                _logger.LogDebug("Successfully queued async move for window {Handle:X} to ({X}, {Y})",
                    hWnd.ToInt64(), x, y);
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while queuing async move for window {Handle:X} to ({X}, {Y})",
                hWnd.ToInt64(), x, y);
            return false;
        }
    }

    /// <summary>
    /// Gets the bounding rectangle of a window using direct API call
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <returns>Rectangle representing the window bounds, or empty rectangle if failed</returns>
    public Rectangle GetWindowBounds(IntPtr hWnd)
    {
        if (_disposed || hWnd == IntPtr.Zero)
        {
            if (hWnd == IntPtr.Zero)
                _logger.LogWarning("Invalid window handle provided to GetWindowBounds");
            return Rectangle.Empty;
        }

        try
        {
            // Direct API call for immediate results
            if (User32.GetWindowRect(hWnd, out var rect))
            {
                var bounds = rect.ToRectangle();
                _logger.LogDebug("Retrieved bounds for window {Handle:X}: {Bounds}",
                    hWnd.ToInt64(), bounds);
                return bounds;
            }
            else
            {
                var errorCode = Kernel32.GetLastError();
                var errorMessage = Kernel32.GetErrorMessage(errorCode);
                _logger.LogError("Failed to get window bounds for {Handle:X}. Error: {Error}",
                    hWnd.ToInt64(), errorMessage);
                return Rectangle.Empty;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while getting window bounds for {Handle:X}",
                hWnd.ToInt64());
            return Rectangle.Empty;
        }
    }

    /// <summary>
    /// Gets the bounding rectangle of a window asynchronously using batched operations
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <returns>Task that returns the window bounds, or empty rectangle if failed</returns>
    public async Task<Rectangle> GetWindowBoundsAsync(IntPtr hWnd)
    {
        if (_disposed || hWnd == IntPtr.Zero)
        {
            if (hWnd == IntPtr.Zero)
                _logger.LogWarning("Invalid window handle provided to GetWindowBoundsAsync");
            return Rectangle.Empty;
        }

        try
        {
            // Use batched operations for async bounds retrieval
            var bounds = await _batchedOperations.QueueGetWindowBoundsAsync(hWnd);
            _logger.LogDebug("Retrieved async bounds for window {Handle:X}: {Bounds}",
                hWnd.ToInt64(), bounds);
            return bounds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while getting async window bounds for {Handle:X}",
                hWnd.ToInt64());
            return Rectangle.Empty;
        }
    }

    /// <summary>
    /// Determines if a window is visible
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <returns>True if the window is visible, false otherwise</returns>
    public bool IsWindowVisible(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            _logger.LogWarning("Invalid window handle provided to IsWindowVisible");
            return false;
        }

        try
        {
            var isVisible = User32.IsWindowVisible(hWnd);
            _logger.LogDebug("Window {Handle:X} visibility: {IsVisible}",
                hWnd.ToInt64(), isVisible);
            return isVisible;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while checking visibility for window {Handle:X}",
                hWnd.ToInt64());
            return false;
        }
    }

    /// <summary>
    /// Disposes the window manipulation service
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        _batchedOperations?.Dispose();
        
        GC.SuppressFinalize(this);
    }
}
