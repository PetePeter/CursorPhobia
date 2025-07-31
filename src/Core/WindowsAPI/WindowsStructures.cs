using System.Runtime.InteropServices;

namespace CursorPhobia.Core.WindowsAPI;

/// <summary>
/// Windows API structures and constants for window management
/// </summary>
public static class WindowsStructures
{
    #region Constants
    
    // Window Extended Styles
    public const uint WS_EX_TOPMOST = 0x00000008;
    public const uint WS_EX_TOOLWINDOW = 0x00000080;
    public const uint WS_EX_APPWINDOW = 0x00040000;
    
    // Window Styles
    public const uint WS_VISIBLE = 0x10000000;
    public const uint WS_MINIMIZE = 0x20000000;
    public const uint WS_MAXIMIZE = 0x01000000;
    
    // GetWindowLong constants
    public const int GWL_STYLE = -16;
    public const int GWL_EXSTYLE = -20;
    public const int GWL_WNDPROC = -4;
    public const int GWL_HINSTANCE = -6;
    public const int GWL_HWNDPARENT = -8;
    public const int GWL_ID = -12;
    public const int GWL_USERDATA = -21;
    
    // SetWindowPos flags
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOREDRAW = 0x0008;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_FRAMECHANGED = 0x0020;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public const uint SWP_HIDEWINDOW = 0x0080;
    public const uint SWP_NOCOPYBITS = 0x0100;
    public const uint SWP_NOOWNERZORDER = 0x0200;
    public const uint SWP_NOSENDCHANGING = 0x0400;
    
    // Special HWND values for SetWindowPos
    public static readonly IntPtr HWND_TOP = new(0);
    public static readonly IntPtr HWND_BOTTOM = new(1);
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);
    
    // Window Show States
    public const int SW_HIDE = 0;
    public const int SW_SHOWNORMAL = 1;
    public const int SW_NORMAL = 1;
    public const int SW_SHOWMINIMIZED = 2;
    public const int SW_SHOWMAXIMIZED = 3;
    public const int SW_MAXIMIZE = 3;
    public const int SW_SHOWNOACTIVATE = 4;
    public const int SW_SHOW = 5;
    public const int SW_MINIMIZE = 6;
    public const int SW_SHOWMINNOACTIVE = 7;
    public const int SW_SHOWNA = 8;
    public const int SW_RESTORE = 9;
    
    // Error codes
    public const uint ERROR_SUCCESS = 0;
    public const uint ERROR_INVALID_WINDOW_HANDLE = 1400;
    public const uint ERROR_INVALID_PARAMETER = 87;
    
    #endregion
    
    #region Structures
    
    /// <summary>
    /// Windows RECT structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        
        public int Width => Right - Left;
        public int Height => Bottom - Top;
        
        public RECT(int left, int top, int right, int bottom)
        {
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }
        
        public System.Drawing.Rectangle ToRectangle()
        {
            return new System.Drawing.Rectangle(Left, Top, Width, Height);
        }
        
        public static RECT FromRectangle(System.Drawing.Rectangle rect)
        {
            return new RECT(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }
    }
    
    /// <summary>
    /// Windows POINT structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
        
        public POINT(int x, int y)
        {
            X = x;
            Y = y;
        }
        
        public System.Drawing.Point ToPoint()
        {
            return new System.Drawing.Point(X, Y);
        }
        
        public static POINT FromPoint(System.Drawing.Point point)
        {
            return new POINT(point.X, point.Y);
        }
    }
    
    /// <summary>
    /// Windows WINDOWINFO structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWINFO
    {
        public uint cbSize;
        public RECT rcWindow;
        public RECT rcClient;
        public uint dwStyle;
        public uint dwExStyle;
        public uint dwWindowStatus;
        public uint cxWindowBorders;
        public uint cyWindowBorders;
        public ushort atomWindowType;
        public ushort wCreatorVersion;
        
        public static WINDOWINFO Create()
        {
            var info = new WINDOWINFO();
            info.cbSize = (uint)Marshal.SizeOf<WINDOWINFO>();
            return info;
        }
    }
    
    /// <summary>
    /// Windows WINDOWPLACEMENT structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        public uint length;
        public uint flags;
        public uint showCmd;
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT rcNormalPosition;
        
        public static WINDOWPLACEMENT Create()
        {
            var placement = new WINDOWPLACEMENT();
            placement.length = (uint)Marshal.SizeOf<WINDOWPLACEMENT>();
            return placement;
        }
    }
    
    /// <summary>
    /// Monitor information structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct MONITORINFO
    {
        public uint cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;
        
        public static MONITORINFO Create()
        {
            var info = new MONITORINFO();
            info.cbSize = (uint)Marshal.SizeOf<MONITORINFO>();
            return info;
        }
    }
    
    #endregion
    
    #region Delegates
    
    /// <summary>
    /// Delegate for EnumWindows callback
    /// </summary>
    /// <param name="hWnd">Handle to the window</param>
    /// <param name="lParam">Application-defined value</param>
    /// <returns>True to continue enumeration, false to stop</returns>
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    
    /// <summary>
    /// Delegate for EnumDisplayMonitors callback
    /// </summary>
    /// <param name="hMonitor">Handle to the monitor</param>
    /// <param name="hdcMonitor">Handle to the monitor DC</param>
    /// <param name="lprcMonitor">Pointer to the monitor rectangle</param>
    /// <param name="dwData">Application-defined data</param>
    /// <returns>True to continue enumeration, false to stop</returns>
    public delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);
    
    #endregion
}