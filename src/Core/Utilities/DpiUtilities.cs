using System.Drawing;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Services;

namespace CursorPhobia.Core.Utilities;

/// <summary>
/// Utility methods for DPI-aware coordinate conversions
/// </summary>
public static class DpiUtilities
{
    /// <summary>
    /// Converts a logical point to physical coordinates using the monitor's DPI
    /// </summary>
    /// <param name="logicalPoint">Point in logical coordinates</param>
    /// <param name="monitorManager">Monitor manager to query DPI information</param>
    /// <returns>Point in physical coordinates</returns>
    public static Point LogicalToPhysical(Point logicalPoint, IMonitorManager monitorManager)
    {
        var dpiInfo = monitorManager.GetDpiForPoint(logicalPoint);
        return dpiInfo.LogicalToPhysical(logicalPoint);
    }

    /// <summary>
    /// Converts a physical point to logical coordinates using the monitor's DPI
    /// </summary>
    /// <param name="physicalPoint">Point in physical coordinates</param>
    /// <param name="monitorManager">Monitor manager to query DPI information</param>
    /// <returns>Point in logical coordinates</returns>
    public static Point PhysicalToLogical(Point physicalPoint, IMonitorManager monitorManager)
    {
        var dpiInfo = monitorManager.GetDpiForPoint(physicalPoint);
        return dpiInfo.PhysicalToLogical(physicalPoint);
    }

    /// <summary>
    /// Converts a logical rectangle to physical coordinates using the monitor's DPI
    /// </summary>
    /// <param name="logicalRect">Rectangle in logical coordinates</param>
    /// <param name="monitorManager">Monitor manager to query DPI information</param>
    /// <returns>Rectangle in physical coordinates</returns>
    public static Rectangle LogicalToPhysical(Rectangle logicalRect, IMonitorManager monitorManager)
    {
        var dpiInfo = monitorManager.GetDpiForRectangle(logicalRect);
        return dpiInfo.LogicalToPhysical(logicalRect);
    }

    /// <summary>
    /// Converts a physical rectangle to logical coordinates using the monitor's DPI
    /// </summary>
    /// <param name="physicalRect">Rectangle in physical coordinates</param>
    /// <param name="monitorManager">Monitor manager to query DPI information</param>
    /// <returns>Rectangle in logical coordinates</returns>
    public static Rectangle PhysicalToLogical(Rectangle physicalRect, IMonitorManager monitorManager)
    {
        var dpiInfo = monitorManager.GetDpiForRectangle(physicalRect);
        return dpiInfo.PhysicalToLogical(physicalRect);
    }

    /// <summary>
    /// Scales a distance value from logical to physical coordinates
    /// </summary>
    /// <param name="logicalDistance">Distance in logical coordinates</param>
    /// <param name="referencePoint">Reference point to determine which monitor to use for scaling</param>
    /// <param name="monitorManager">Monitor manager to query DPI information</param>
    /// <returns>Distance in physical coordinates</returns>
    public static int ScaleDistance(int logicalDistance, Point referencePoint, IMonitorManager monitorManager)
    {
        var dpiInfo = monitorManager.GetDpiForPoint(referencePoint);
        return dpiInfo.ScaleDistance(logicalDistance);
    }

    /// <summary>
    /// Scales a distance value from physical to logical coordinates
    /// </summary>
    /// <param name="physicalDistance">Distance in physical coordinates</param>
    /// <param name="referencePoint">Reference point to determine which monitor to use for scaling</param>
    /// <param name="monitorManager">Monitor manager to query DPI information</param>
    /// <returns>Distance in logical coordinates</returns>
    public static int UnscaleDistance(int physicalDistance, Point referencePoint, IMonitorManager monitorManager)
    {
        var dpiInfo = monitorManager.GetDpiForPoint(referencePoint);
        return dpiInfo.UnscaleDistance(physicalDistance);
    }

    /// <summary>
    /// Converts coordinates between different DPI contexts
    /// </summary>
    /// <param name="point">Point to convert</param>
    /// <param name="sourceDpi">Source DPI information</param>
    /// <param name="targetDpi">Target DPI information</param>
    /// <returns>Point converted to target DPI context</returns>
    public static Point ConvertBetweenDpiContexts(Point point, DpiInfo sourceDpi, DpiInfo targetDpi)
    {
        // Convert to logical coordinates using source DPI, then to physical using target DPI
        var logicalPoint = sourceDpi.PhysicalToLogical(point);
        return targetDpi.LogicalToPhysical(logicalPoint);
    }

    /// <summary>
    /// Converts a rectangle between different DPI contexts
    /// </summary>
    /// <param name="rectangle">Rectangle to convert</param>
    /// <param name="sourceDpi">Source DPI information</param>
    /// <param name="targetDpi">Target DPI information</param>
    /// <returns>Rectangle converted to target DPI context</returns>
    public static Rectangle ConvertBetweenDpiContexts(Rectangle rectangle, DpiInfo sourceDpi, DpiInfo targetDpi)
    {
        // Convert to logical coordinates using source DPI, then to physical using target DPI
        var logicalRect = sourceDpi.PhysicalToLogical(rectangle);
        return targetDpi.LogicalToPhysical(logicalRect);
    }

    /// <summary>
    /// Checks if two DPI values are significantly different (threshold for triggering DPI-aware adjustments)
    /// </summary>
    /// <param name="dpi1">First DPI information</param>
    /// <param name="dpi2">Second DPI information</param>
    /// <param name="threshold">Threshold for considering DPI values different (default 0.1 = 10%)</param>
    /// <returns>True if DPI values are significantly different</returns>
    public static bool AreDpiValuesSignificantlyDifferent(DpiInfo dpi1, DpiInfo dpi2, double threshold = 0.1)
    {
        var scaleDiff = Math.Abs(dpi1.ScaleFactor - dpi2.ScaleFactor);
        return scaleDiff > threshold;
    }

    /// <summary>
    /// Gets the maximum scale factor among all monitors
    /// </summary>
    /// <param name="monitorManager">Monitor manager to query DPI information</param>
    /// <returns>Maximum scale factor across all monitors</returns>
    public static double GetMaximumScaleFactor(IMonitorManager monitorManager)
    {
        var monitors = monitorManager.GetAllMonitors();
        double maxScaleFactor = 1.0;

        foreach (var monitor in monitors)
        {
            var dpiInfo = monitorManager.GetMonitorDpi(monitor);
            maxScaleFactor = Math.Max(maxScaleFactor, dpiInfo.ScaleFactor);
        }

        return maxScaleFactor;
    }

    /// <summary>
    /// Gets the minimum scale factor among all monitors
    /// </summary>
    /// <param name="monitorManager">Monitor manager to query DPI information</param>
    /// <returns>Minimum scale factor across all monitors</returns>
    public static double GetMinimumScaleFactor(IMonitorManager monitorManager)
    {
        var monitors = monitorManager.GetAllMonitors();
        double minScaleFactor = double.MaxValue;

        foreach (var monitor in monitors)
        {
            var dpiInfo = monitorManager.GetMonitorDpi(monitor);
            minScaleFactor = Math.Min(minScaleFactor, dpiInfo.ScaleFactor);
        }

        return minScaleFactor == double.MaxValue ? 1.0 : minScaleFactor;
    }

    /// <summary>
    /// Determines if the system has mixed DPI monitors (different scale factors)
    /// </summary>
    /// <param name="monitorManager">Monitor manager to query DPI information</param>
    /// <param name="threshold">Threshold for considering scale factors different (default 0.1 = 10%)</param>
    /// <returns>True if system has monitors with significantly different DPI values</returns>
    public static bool HasMixedDpiMonitors(IMonitorManager monitorManager, double threshold = 0.1)
    {
        var monitors = monitorManager.GetAllMonitors();
        if (monitors.Count <= 1) return false;

        var firstDpi = monitorManager.GetMonitorDpi(monitors[0]);

        for (int i = 1; i < monitors.Count; i++)
        {
            var currentDpi = monitorManager.GetMonitorDpi(monitors[i]);
            if (AreDpiValuesSignificantlyDifferent(firstDpi, currentDpi, threshold))
            {
                return true;
            }
        }

        return false;
    }
}