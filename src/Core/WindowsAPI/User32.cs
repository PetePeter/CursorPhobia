using System.Runtime.InteropServices;
using System.Text;
using static CursorPhobia.Core.WindowsAPI.WindowsStructures;

namespace CursorPhobia.Core.WindowsAPI;

/// <summary>
/// P/Invoke declarations for User32.dll Windows API functions
/// </summary>
public static class User32
{
    private const string User32Dll = "user32.dll";
    
    #region Window Enumeration
    
    /// <summary>
    /// Enumerates all top-level windows on the screen
    /// </summary>
    /// <param name="enumFunc">Callback function for each window</param>
    /// <param name="lParam">Application-defined value passed to callback</param>
    /// <returns>True if successful, false otherwise</returns>
    [DllImport(User32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumWindows(EnumWindowsProc enumFunc, IntPtr lParam);
    
    /// <summary>
    /// Enumerates child windows that belong to the specified parent window
    /// </summary>
    /// <param name="hWndParent">Handle to parent window</param>
    /// <param name="enumFunc">Callback function for each child window</param>
    /// <param name="lParam">Application-defined value passed to callback</param>
    /// <returns>True if successful, false otherwise</returns>
    [DllImport(User32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc enumFunc, IntPtr lParam);
    
    #endregion
    
    #region Window Information
    
    /// <summary>
    /// Retrieves information about the specified window
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="index">The zero-based offset to the value to be retrieved</param>
    /// <returns>The requested value</returns>
    [DllImport(User32Dll, SetLastError = true)]
    public static extern uint GetWindowLong(IntPtr hWnd, int index);
    
    /// <summary>
    /// Retrieves information about the specified window (64-bit compatible)
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="index">The zero-based offset to the value to be retrieved</param>
    /// <returns>The requested value</returns>
    [DllImport(User32Dll, SetLastError = true, EntryPoint = "GetWindowLongPtr")]
    public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);
    
    /// <summary>
    /// Platform-neutral way to get window long value
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="index">The zero-based offset to the value to be retrieved</param>
    /// <returns>The requested value</returns>
    public static IntPtr GetWindowLongPtrSafe(IntPtr hWnd, int index)
    {
        if (IntPtr.Size == 8)
            return GetWindowLongPtr(hWnd, index);
        else
            return new IntPtr(GetWindowLong(hWnd, index));
    }
    
    /// <summary>
    /// Retrieves the dimensions of the bounding rectangle of the specified window
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="lpRect">Pointer to a RECT structure that receives the coordinates</param>
    /// <returns>True if successful, false otherwise</returns>
    [DllImport(User32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    
    /// <summary>
    /// Retrieves the coordinates of a window's client area
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="lpRect">Pointer to a RECT structure that receives the coordinates</param>
    /// <returns>True if successful, false otherwise</returns>
    [DllImport(User32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);
    
    /// <summary>
    /// Determines the visibility state of the specified window
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <returns>True if the window is visible, false otherwise</returns>
    [DllImport(User32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsWindowVisible(IntPtr hWnd);
    
    /// <summary>
    /// Determines whether the specified window is minimized (iconic)
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <returns>True if the window is minimized, false otherwise</returns>
    [DllImport(User32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsIconic(IntPtr hWnd);
    
    /// <summary>
    /// Determines whether a window is maximized
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <returns>True if the window is maximized, false otherwise</returns>
    [DllImport(User32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsZoomed(IntPtr hWnd);
    
    /// <summary>
    /// Retrieves information about the specified window
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="pwi">Pointer to a WINDOWINFO structure to receive the information</param>
    /// <returns>True if successful, false otherwise</returns>
    [DllImport(User32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowInfo(IntPtr hWnd, ref WINDOWINFO pwi);
    
    #endregion
    
    #region Window Text and Class
    
    /// <summary>
    /// Copies the text of the specified window's title bar into a buffer
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="lpString">Buffer that receives the text</param>
    /// <param name="nMaxCount">Maximum number of characters to copy</param>
    /// <returns>Length of the copied string</returns>
    [DllImport(User32Dll, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    
    /// <summary>
    /// Retrieves the length of the specified window's title bar text
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <returns>Length of the text in characters</returns>
    [DllImport(User32Dll, SetLastError = true)]
    public static extern int GetWindowTextLength(IntPtr hWnd);
    
    /// <summary>
    /// Retrieves the name of the class to which the specified window belongs
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="lpClassName">Buffer that receives the class name</param>
    /// <param name="nMaxCount">Maximum number of characters to copy</param>
    /// <returns>Number of characters copied</returns>
    [DllImport(User32Dll, SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);
    
    #endregion
    
    #region Window Positioning and Manipulation
    
    /// <summary>
    /// Changes the size, position, and Z order of a child, pop-up, or top-level window
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="hWndInsertAfter">Handle to the window to precede the positioned window in the Z order</param>
    /// <param name="x">New position of the left side of the window</param>
    /// <param name="y">New position of the top of the window</param>
    /// <param name="cx">New width of the window</param>
    /// <param name="cy">New height of the window</param>
    /// <param name="uFlags">Window sizing and positioning flags</param>
    /// <returns>True if successful, false otherwise</returns>
    [DllImport(User32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
    
    /// <summary>
    /// Changes the position and dimensions of the specified window
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="x">New position of the left side of the window</param>
    /// <param name="y">New position of the top of the window</param>
    /// <param name="nWidth">New width of the window</param>
    /// <param name="nHeight">New height of the window</param>
    /// <param name="bRepaint">Indicates whether the window is to be repainted</param>
    /// <returns>True if successful, false otherwise</returns>
    [DllImport(User32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int nWidth, int nHeight, bool bRepaint);
    
    /// <summary>
    /// Sets the show state of a window
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="nCmdShow">Controls how the window is to be shown</param>
    /// <returns>True if the window was previously visible, false otherwise</returns>
    [DllImport(User32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    
    /// <summary>
    /// Retrieves information about the current placement of a window
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="lpwndpl">Pointer to a WINDOWPLACEMENT structure</param>
    /// <returns>True if successful, false otherwise</returns>
    [DllImport(User32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);
    
    /// <summary>
    /// Sets the show state and the restored, minimized, and maximized positions of the specified window
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="lpwndpl">Pointer to a WINDOWPLACEMENT structure</param>
    /// <returns>True if successful, false otherwise</returns>
    [DllImport(User32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);
    
    #endregion
    
    #region Process and Thread Information
    
    /// <summary>
    /// Retrieves the identifier of the thread that created the specified window
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="lpdwProcessId">Pointer to a variable that receives the process identifier</param>
    /// <returns>The thread identifier</returns>
    [DllImport(User32Dll, SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    
    #endregion
    
    #region Monitor Functions
    
    /// <summary>
    /// Retrieves a handle to the display monitor that contains a specified point
    /// </summary>
    /// <param name="pt">Point for which to retrieve the monitor handle</param>
    /// <param name="dwFlags">Determines the function's return value if the point is not contained within any display monitor</param>
    /// <returns>Handle to the monitor</returns>
    [DllImport(User32Dll, SetLastError = true)]
    public static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);
    
    /// <summary>
    /// Retrieves a handle to the display monitor that has the largest area of intersection with a specified rectangle
    /// </summary>
    /// <param name="lprc">Pointer to a RECT structure that specifies the rectangle of interest</param>
    /// <param name="dwFlags">Determines the function's return value if the rectangle does not intersect any display monitor</param>
    /// <returns>Handle to the monitor</returns>
    [DllImport(User32Dll, SetLastError = true)]
    public static extern IntPtr MonitorFromRect(ref RECT lprc, uint dwFlags);
    
    /// <summary>
    /// Retrieves a handle to the display monitor that contains the specified window
    /// </summary>
    /// <param name="hWnd">Handle to the window of interest</param>
    /// <param name="dwFlags">Determines the function's return value if the window does not intersect any display monitor</param>
    /// <returns>Handle to the monitor</returns>
    [DllImport(User32Dll, SetLastError = true)]
    public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);
    
    /// <summary>
    /// Enumerates display monitors
    /// </summary>
    /// <param name="hdc">Handle to a display device context</param>
    /// <param name="lprcClip">Pointer to a RECT structure that specifies a clipping rectangle</param>
    /// <param name="lpfnEnum">Pointer to a MonitorEnumProc application-defined callback function</param>
    /// <param name="dwData">Application-defined data that EnumDisplayMonitors passes directly to the callback function</param>
    /// <returns>True if successful, false otherwise</returns>
    [DllImport(User32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);
    
    /// <summary>
    /// Retrieves information about a display monitor
    /// </summary>
    /// <param name="hMonitor">Handle to the display monitor of interest</param>
    /// <param name="lpmi">Pointer to a MONITORINFO structure that receives information about the specified display monitor</param>
    /// <returns>True if successful, false otherwise</returns>
    [DllImport(User32Dll, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
    
    #endregion
    
    #region Helper Methods
    
    /// <summary>
    /// Helper method to safely get window text
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <returns>Window text or empty string if failed</returns>
    public static string GetWindowTextSafe(IntPtr hWnd)
    {
        try
        {
            int length = GetWindowTextLength(hWnd);
            if (length == 0) return string.Empty;
            
            var sb = new StringBuilder(length + 1);
            int actualLength = GetWindowText(hWnd, sb, sb.Capacity);
            return actualLength > 0 ? sb.ToString() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
    
    /// <summary>
    /// Helper method to safely get window class name
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <returns>Window class name or empty string if failed</returns>
    public static string GetClassNameSafe(IntPtr hWnd)
    {
        try
        {
            var sb = new StringBuilder(256);
            int length = GetClassName(hWnd, sb, sb.Capacity);
            return length > 0 ? sb.ToString() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }
    
    #endregion
}