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
        public IntPtr windowHandle { get; set; }

        /// <summary>
        /// The title of the window.
        /// </summary>
        public string title { get; set; }

        /// <summary>
        /// The class name of the window.
        /// </summary>
        public string className { get; set; }

        /// <summary>
        /// The bounds of the window.
        /// </summary>
        public Rectangle bounds { get; set; }

        /// <summary>
        /// The process ID owning the window.
        /// </summary>
        public int processId { get; set; }

        /// <summary>
        /// The thread ID owning the window.
        /// </summary>
        public int threadId { get; set; }

        /// <summary>
        /// Indicates whether the window is visible.
        /// </summary>
        public bool isVisible { get; set; }

        /// <summary>
        /// Indicates whether the window is topmost.
        /// </summary>
        public bool isTopmost { get; set; }

        /// <summary>
        /// Indicates whether the window is minimized.
        /// </summary>
        public bool isMinimized { get; set; }
    }
}
