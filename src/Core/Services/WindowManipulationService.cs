using System.Drawing;
using CursorPhobia.Core.Utilities;
using CursorPhobia.Core.WindowsAPI;
using static CursorPhobia.Core.WindowsAPI.WindowsStructures;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Interface for window manipulation operations
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
    /// Gets the bounding rectangle of a window
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <returns>Rectangle representing the window bounds</returns>
    Rectangle GetWindowBounds(IntPtr hWnd);
    
    /// <summary>
    /// Determines if a window is visible
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <returns>True if the window is visible, false otherwise</returns>
    bool IsWindowVisible(IntPtr hWnd);
}

/// <summary>
/// Service for manipulating window positions and properties
/// </summary>
public class WindowManipulationService : IWindowManipulationService
{
    private readonly Logger _logger;

    /// <summary>
    /// Creates a new WindowManipulationService instance
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    public WindowManipulationService(Logger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Moves a window to the specified coordinates (position only, preserves size)
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="x">New X coordinate</param>
    /// <param name="y">New Y coordinate</param>
    /// <returns>True if successful, false otherwise</returns>
    public bool MoveWindow(IntPtr hWnd, int x, int y)
    {
        if (hWnd == IntPtr.Zero)
        {
            _logger.LogWarning("Invalid window handle provided to MoveWindow");
            return false;
        }
        
        try
        {
            // First get the current window bounds to preserve size
            var currentBounds = GetWindowBounds(hWnd);
            if (currentBounds.IsEmpty)
            {
                _logger.LogWarning("Could not get current bounds for window {Handle:X}", hWnd.ToInt64());
                return false;
            }
            
            // Use SetWindowPos for more precise control than MoveWindow
            var success = User32.SetWindowPos(
                hWnd, 
                IntPtr.Zero, // No Z-order change
                x, y, 
                currentBounds.Width, currentBounds.Height,
                SWP_NOZORDER | SWP_NOACTIVATE | SWP_NOREDRAW
            );
            
            if (success)
            {
                _logger.LogDebug("Successfully moved window {Handle:X} to ({X}, {Y})", 
                    hWnd.ToInt64(), x, y);
            }
            else
            {
                var errorCode = Kernel32.GetLastError();
                var errorMessage = Kernel32.GetErrorMessage(errorCode);
                _logger.LogError("Failed to move window {Handle:X} to ({X}, {Y}). Error: {Error}", 
                    hWnd.ToInt64(), x, y, errorMessage);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while moving window {Handle:X} to ({X}, {Y})", 
                hWnd.ToInt64(), x, y);
            return false;
        }
    }
    
    /// <summary>
    /// Gets the bounding rectangle of a window
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <returns>Rectangle representing the window bounds, or empty rectangle if failed</returns>
    public Rectangle GetWindowBounds(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            _logger.LogWarning("Invalid window handle provided to GetWindowBounds");
            return Rectangle.Empty;
        }
        
        try
        {
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
}
