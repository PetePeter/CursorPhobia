using System.Drawing;
using System.Runtime.InteropServices;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Utilities;
using CursorPhobia.Core.WindowsAPI;
using static CursorPhobia.Core.WindowsAPI.WindowsStructures;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Service for detecting and gathering information about windows
/// </summary>
public class WindowDetectionService : IWindowDetectionService
{
    private readonly Logger _logger;
    
    /// <summary>
    /// Creates a new WindowDetectionService instance
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    public WindowDetectionService(Logger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Gets all windows that have the topmost flag set
    /// </summary>
    /// <returns>List of topmost windows</returns>
    public List<WindowInfo> GetAllTopMostWindows()
    {
        _logger.LogDebug("Starting enumeration of all topmost windows");
        
        var topMostWindows = new List<WindowInfo>();
        var allWindows = EnumerateVisibleWindows();
        
        foreach (var window in allWindows)
        {
            if (IsWindowAlwaysOnTop(window.windowHandle))
            {
                topMostWindows.Add(window);
            }
        }
        
        _logger.LogInformation("Found {Count} topmost windows out of {Total} visible windows", 
            topMostWindows.Count, allWindows.Count);
        
        return topMostWindows;
    }
    
    /// <summary>
    /// Determines if a window has the always-on-top (topmost) flag set
    /// </summary>
    /// <param name="hWnd">Handle to the window to check</param>
    /// <returns>True if the window is always on top, false otherwise</returns>
    public bool IsWindowAlwaysOnTop(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            _logger.LogWarning("Invalid window handle provided to IsWindowAlwaysOnTop");
            return false;
        }
        
        try
        {
            // Get the extended window styles
            var exStyle = User32.GetWindowLongPtrSafe(hWnd, GWL_EXSTYLE);
            var isTopmost = (exStyle.ToInt64() & WS_EX_TOPMOST) == WS_EX_TOPMOST;
            
            _logger.LogDebug("Window {Handle:X} topmost status: {IsTopmost}", hWnd.ToInt64(), isTopmost);
            return isTopmost;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking topmost status for window {Handle:X}", hWnd.ToInt64());
            return false;
        }
    }
    
    /// <summary>
    /// Gets comprehensive information about a specific window
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <returns>WindowInfo object with details about the window, or null if failed</returns>
    public WindowInfo? GetWindowInformation(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
        {
            _logger.LogWarning("Invalid window handle provided to GetWindowInformation");
            return null;
        }
        
        try
        {
            // Get basic window properties
            var title = User32.GetWindowTextSafe(hWnd);
            var className = User32.GetClassNameSafe(hWnd);
            var isVisible = User32.IsWindowVisible(hWnd);
            var isMinimized = User32.IsIconic(hWnd);
            var isTopmost = IsWindowAlwaysOnTop(hWnd);
            
            // Get window bounds
            if (!User32.GetWindowRect(hWnd, out var rect))
            {
                _logger.LogWarning("Failed to get window rectangle for window {Handle:X}", hWnd.ToInt64());
                return null;
            }
            
            var bounds = rect.ToRectangle();
            
            // Get process and thread information
            var threadId = User32.GetWindowThreadProcessId(hWnd, out var processId);
            
            var windowInfo = new WindowInfo
            {
                windowHandle = hWnd,
                title = title,
                className = className,
                bounds = bounds,
                processId = (int)processId,
                threadId = (int)threadId,
                isVisible = isVisible,
                isTopmost = isTopmost,
                isMinimized = isMinimized
            };
            
            _logger.LogDebug("Retrieved information for window {Handle:X}: '{Title}' ({ClassName})", 
                hWnd.ToInt64(), title, className);
            
            return windowInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting window information for handle {Handle:X}", hWnd.ToInt64());
            return null;
        }
    }
    
    /// <summary>
    /// Enumerates all visible windows on the system
    /// </summary>
    /// <returns>List of visible windows</returns>
    public List<WindowInfo> EnumerateVisibleWindows()
    {
        _logger.LogDebug("Starting enumeration of all visible windows");
        
        var windows = new List<WindowInfo>();
        var windowHandles = new List<IntPtr>();
        
        try
        {
            // First pass: collect all window handles
            User32.EnumWindows((hWnd, lParam) =>
            {
                if (User32.IsWindowVisible(hWnd))
                {
                    windowHandles.Add(hWnd);
                }
                return true; // Continue enumeration
            }, IntPtr.Zero);
            
            _logger.LogDebug("Found {Count} visible window handles", windowHandles.Count);
            
            // Second pass: get detailed information for each window
            foreach (var hWnd in windowHandles)
            {
                var windowInfo = GetWindowInformation(hWnd);
                if (windowInfo != null)
                {
                    windows.Add(windowInfo);
                }
            }
            
            // Filter out windows that shouldn't be included
            var filteredWindows = FilterRelevantWindows(windows);
            
            _logger.LogInformation("Enumerated {Count} visible windows after filtering", filteredWindows.Count);
            return filteredWindows;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during window enumeration");
            return new List<WindowInfo>();
        }
    }
    
    /// <summary>
    /// Filters the window list to remove irrelevant windows (like hidden system windows)
    /// </summary>
    /// <param name="windows">List of windows to filter</param>
    /// <returns>Filtered list of relevant windows</returns>
    private List<WindowInfo> FilterRelevantWindows(List<WindowInfo> windows)
    {
        return windows.Where(window =>
        {
            // Skip windows with empty titles and certain system classes
            if (string.IsNullOrWhiteSpace(window.title) && IsSystemWindowClass(window.className))
            {
                return false;
            }
            
            // Skip very small windows (likely system windows)
            if (window.bounds.Width < 50 || window.bounds.Height < 50)
            {
                return false;
            }
            
            // Skip windows positioned far off-screen (negative coordinates beyond reasonable bounds)
            if (window.bounds.X < -1000 || window.bounds.Y < -1000)
            {
                return false;
            }
            
            return true;
        }).ToList();
    }
    
    /// <summary>
    /// Determines if a window class name represents a system window that should be filtered out
    /// </summary>
    /// <param name="className">The window class name</param>
    /// <returns>True if this is a system window class</returns>
    private static bool IsSystemWindowClass(string className)
    {
        if (string.IsNullOrEmpty(className))
            return false;
            
        var systemClasses = new[]
        {
            "Shell_TrayWnd",           // Taskbar
            "Shell_SecondaryTrayWnd",  // Secondary taskbar
            "Progman",                 // Desktop
            "WorkerW",                 // Desktop worker
            "DV2ControlHost",          // Windows 10 start menu
            "Windows.UI.Core.CoreWindow", // Windows 10 apps background
            "ApplicationFrameWindow",  // UWP app frame (but these usually have titles)
            "ImmersiveLauncher",      // Start screen
            "ImmersiveBackground",    // Background windows
            "EdgeUiInputTopWndClass", // Edge UI
            "NativeHWNDHost",         // Various system hosts
            "Chrome_RenderWidgetHostHWND", // Chrome internal windows
            "Intermediate D3D Window" // Graphics subsystem windows
        };
        
        return systemClasses.Any(sysClass => 
            className.Equals(sysClass, StringComparison.OrdinalIgnoreCase) ||
            className.StartsWith(sysClass, StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>
/// Interface for window detection service
/// </summary>
public interface IWindowDetectionService
{
    /// <summary>
    /// Gets all windows that have the topmost flag set
    /// </summary>
    /// <returns>List of topmost windows</returns>
    List<WindowInfo> GetAllTopMostWindows();
    
    /// <summary>
    /// Determines if a window has the always-on-top (topmost) flag set
    /// </summary>
    /// <param name="hWnd">Handle to the window to check</param>
    /// <returns>True if the window is always on top, false otherwise</returns>
    bool IsWindowAlwaysOnTop(IntPtr hWnd);
    
    /// <summary>
    /// Gets comprehensive information about a specific window
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <returns>WindowInfo object with details about the window, or null if failed</returns>
    WindowInfo? GetWindowInformation(IntPtr hWnd);
    
    /// <summary>
    /// Enumerates all visible windows on the system
    /// </summary>
    /// <returns>List of visible windows</returns>
    List<WindowInfo> EnumerateVisibleWindows();
}