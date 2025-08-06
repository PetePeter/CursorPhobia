using System.Drawing;
using CursorPhobia.Core.WindowsAPI;
using static CursorPhobia.Core.WindowsAPI.WindowsStructures;

namespace CursorPhobia.Core.Models;

/// <summary>
/// Represents DPI information for a monitor
/// </summary>
public class DpiInfo
{
    /// <summary>
    /// DPI value along the x-axis
    /// </summary>
    public uint DpiX { get; set; }

    /// <summary>
    /// DPI value along the y-axis
    /// </summary>
    public uint DpiY { get; set; }

    /// <summary>
    /// Scaling factor for the x-axis (relative to 96 DPI)
    /// </summary>
    public double ScaleFactorX => (double)DpiX / DEFAULT_DPI;

    /// <summary>
    /// Scaling factor for the y-axis (relative to 96 DPI)
    /// </summary>
    public double ScaleFactorY => (double)DpiY / DEFAULT_DPI;

    /// <summary>
    /// Gets the average scaling factor
    /// </summary>
    public double ScaleFactor => (ScaleFactorX + ScaleFactorY) / 2.0;

    /// <summary>
    /// Indicates whether this is high DPI (> 96 DPI)
    /// </summary>
    public bool IsHighDpi => DpiX > DEFAULT_DPI || DpiY > DEFAULT_DPI;

    /// <summary>
    /// Creates a new DpiInfo instance
    /// </summary>
    public DpiInfo()
    {
        DpiX = DEFAULT_DPI;
        DpiY = DEFAULT_DPI;
    }

    /// <summary>
    /// Creates a new DpiInfo instance with specified DPI values
    /// </summary>
    /// <param name="dpiX">DPI value along the x-axis</param>
    /// <param name="dpiY">DPI value along the y-axis</param>
    public DpiInfo(uint dpiX, uint dpiY)
    {
        DpiX = dpiX;
        DpiY = dpiY;
    }

    /// <summary>
    /// Creates DpiInfo by querying a monitor handle
    /// </summary>
    /// <param name="hMonitor">Handle to the monitor</param>
    /// <returns>DpiInfo for the monitor, or default values if query fails</returns>
    public static DpiInfo FromMonitor(IntPtr hMonitor)
    {
        try
        {
            var result = User32.GetDpiForMonitor(hMonitor, MDT_EFFECTIVE_DPI, out uint dpiX, out uint dpiY);
            if (result == 0) // S_OK
            {
                return new DpiInfo(dpiX, dpiY);
            }
        }
        catch
        {
            // Fall back to default DPI if API fails
        }

        return new DpiInfo();
    }

    /// <summary>
    /// Converts logical coordinates to physical coordinates
    /// </summary>
    /// <param name="logicalPoint">Point in logical coordinates</param>
    /// <returns>Point in physical coordinates</returns>
    public Point LogicalToPhysical(Point logicalPoint)
    {
        return new Point(
            (int)(logicalPoint.X * ScaleFactorX),
            (int)(logicalPoint.Y * ScaleFactorY)
        );
    }

    /// <summary>
    /// Converts physical coordinates to logical coordinates
    /// </summary>
    /// <param name="physicalPoint">Point in physical coordinates</param>
    /// <returns>Point in logical coordinates</returns>
    public Point PhysicalToLogical(Point physicalPoint)
    {
        return new Point(
            (int)(physicalPoint.X / ScaleFactorX),
            (int)(physicalPoint.Y / ScaleFactorY)
        );
    }

    /// <summary>
    /// Converts logical rectangle to physical rectangle
    /// </summary>
    /// <param name="logicalRect">Rectangle in logical coordinates</param>
    /// <returns>Rectangle in physical coordinates</returns>
    public Rectangle LogicalToPhysical(Rectangle logicalRect)
    {
        return new Rectangle(
            (int)(logicalRect.X * ScaleFactorX),
            (int)(logicalRect.Y * ScaleFactorY),
            (int)(logicalRect.Width * ScaleFactorX),
            (int)(logicalRect.Height * ScaleFactorY)
        );
    }

    /// <summary>
    /// Converts physical rectangle to logical rectangle
    /// </summary>
    /// <param name="physicalRect">Rectangle in physical coordinates</param>
    /// <returns>Rectangle in logical coordinates</returns>
    public Rectangle PhysicalToLogical(Rectangle physicalRect)
    {
        return new Rectangle(
            (int)(physicalRect.X / ScaleFactorX),
            (int)(physicalRect.Y / ScaleFactorY),
            (int)(physicalRect.Width / ScaleFactorX),
            (int)(physicalRect.Height / ScaleFactorY)
        );
    }

    /// <summary>
    /// Scales a distance value from logical to physical coordinates
    /// </summary>
    /// <param name="logicalDistance">Distance in logical coordinates</param>
    /// <returns>Distance in physical coordinates</returns>
    public int ScaleDistance(int logicalDistance)
    {
        return (int)(logicalDistance * ScaleFactor);
    }

    /// <summary>
    /// Scales a distance value from physical to logical coordinates
    /// </summary>
    /// <param name="physicalDistance">Distance in physical coordinates</param>
    /// <returns>Distance in logical coordinates</returns>
    public int UnscaleDistance(int physicalDistance)
    {
        return (int)(physicalDistance / ScaleFactor);
    }

    /// <summary>
    /// Returns a string representation of the DPI information
    /// </summary>
    public override string ToString()
    {
        return $"DPI: {DpiX}x{DpiY} (Scale: {ScaleFactor:F2}x)";
    }

    /// <summary>
    /// Determines if two DpiInfo instances are equal
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is DpiInfo other)
        {
            return DpiX == other.DpiX && DpiY == other.DpiY;
        }
        return false;
    }

    /// <summary>
    /// Gets the hash code for this DpiInfo instance
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(DpiX, DpiY);
    }
}