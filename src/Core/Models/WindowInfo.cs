using System;
using System.Drawing;

namespace CursorPhobia.Core.Models
{
    /// <summary>
    /// Represents information about a window.
    /// </summary>
    public class WindowInfo
    {
        /// <summary>
        /// The handle of the window.
        /// </summary>
        public IntPtr WindowHandle { get; set; }

        /// <summary>
        /// The title of the window.
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// The class name of the window.
        /// </summary>
        public string ClassName { get; set; } = string.Empty;

        /// <summary>
        /// The bounds of the window.
        /// </summary>
        public Rectangle Bounds { get; set; }

        /// <summary>
        /// The process ID owning the window.
        /// </summary>
        public int ProcessId { get; set; }

        /// <summary>
        /// The thread ID owning the window.
        /// </summary>
        public int ThreadId { get; set; }

        /// <summary>
        /// Indicates whether the window is visible.
        /// </summary>
        public bool IsVisible { get; set; }

        /// <summary>
        /// Indicates whether the window is topmost.
        /// </summary>
        public bool IsTopmost { get; set; }

        /// <summary>
        /// Indicates whether the window is minimized.
        /// </summary>
        public bool IsMinimized { get; set; }
    }
}
