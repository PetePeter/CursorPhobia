using System.Drawing;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;
using Xunit;

namespace CursorPhobia.Tests;

/// <summary>
/// Unit tests for DpiUtilities class
/// </summary>
public class DpiUtilitiesTests
{
    #region Basic Coordinate Conversion Tests

    [Fact]
    public void LogicalToPhysical_Point_WithMonitorManager_ConvertsCorrectly()
    {
        // Arrange
        var monitorManager = new MockMonitorManager();
        var monitor = new MonitorInfo(new IntPtr(1), new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1080), true);
        monitorManager.AddMonitor(monitor);
        monitorManager.SetMonitorDpi(monitor, 144, 144); // 1.5x scale

        var logicalPoint = new Point(100, 200);

        // Act
        var physicalPoint = DpiUtilities.LogicalToPhysical(logicalPoint, monitorManager);

        // Assert
        Assert.Equal(150, physicalPoint.X);
        Assert.Equal(300, physicalPoint.Y);
    }

    [Fact]
    public void PhysicalToLogical_Point_WithMonitorManager_ConvertsCorrectly()
    {
        // Arrange
        var monitorManager = new MockMonitorManager();
        var monitor = new MonitorInfo(new IntPtr(1), new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1080), true);
        monitorManager.AddMonitor(monitor);
        monitorManager.SetMonitorDpi(monitor, 144, 144); // 1.5x scale

        var physicalPoint = new Point(150, 300);

        // Act
        var logicalPoint = DpiUtilities.PhysicalToLogical(physicalPoint, monitorManager);

        // Assert
        Assert.Equal(100, logicalPoint.X);
        Assert.Equal(200, logicalPoint.Y);
    }

    [Fact]
    public void LogicalToPhysical_Rectangle_WithMonitorManager_ConvertsCorrectly()
    {
        // Arrange
        var monitorManager = new MockMonitorManager();
        var monitor = new MonitorInfo(new IntPtr(1), new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1080), true);
        monitorManager.AddMonitor(monitor);
        monitorManager.SetMonitorDpi(monitor, 192, 192); // 2.0x scale

        var logicalRect = new Rectangle(50, 100, 200, 150);

        // Act
        var physicalRect = DpiUtilities.LogicalToPhysical(logicalRect, monitorManager);

        // Assert
        Assert.Equal(100, physicalRect.X);
        Assert.Equal(200, physicalRect.Y);
        Assert.Equal(400, physicalRect.Width);
        Assert.Equal(300, physicalRect.Height);
    }

    [Fact]
    public void PhysicalToLogical_Rectangle_WithMonitorManager_ConvertsCorrectly()
    {
        // Arrange
        var monitorManager = new MockMonitorManager();
        var monitor = new MonitorInfo(new IntPtr(1), new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1080), true);
        monitorManager.AddMonitor(monitor);
        monitorManager.SetMonitorDpi(monitor, 192, 192); // 2.0x scale

        var physicalRect = new Rectangle(100, 200, 400, 300);

        // Act
        var logicalRect = DpiUtilities.PhysicalToLogical(physicalRect, monitorManager);

        // Assert
        Assert.Equal(50, logicalRect.X);
        Assert.Equal(100, logicalRect.Y);
        Assert.Equal(200, logicalRect.Width);
        Assert.Equal(150, logicalRect.Height);
    }

    #endregion

    #region Distance Scaling Tests

    [Fact]
    public void ScaleDistance_WithHighDpiMonitor_ScalesCorrectly()
    {
        // Arrange
        var monitorManager = new MockMonitorManager();
        var monitor = new MonitorInfo(new IntPtr(1), new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1080), true);
        monitorManager.AddMonitor(monitor);
        monitorManager.SetMonitorDpi(monitor, 120, 120); // 1.25x scale

        var referencePoint = new Point(500, 500);
        var logicalDistance = 100;

        // Act
        var physicalDistance = DpiUtilities.ScaleDistance(logicalDistance, referencePoint, monitorManager);

        // Assert
        Assert.Equal(125, physicalDistance);
    }

    [Fact]
    public void UnscaleDistance_WithHighDpiMonitor_ScalesCorrectly()
    {
        // Arrange
        var monitorManager = new MockMonitorManager();
        var monitor = new MonitorInfo(new IntPtr(1), new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1080), true);
        monitorManager.AddMonitor(monitor);
        monitorManager.SetMonitorDpi(monitor, 120, 120); // 1.25x scale

        var referencePoint = new Point(500, 500);
        var physicalDistance = 125;

        // Act
        var logicalDistance = DpiUtilities.UnscaleDistance(physicalDistance, referencePoint, monitorManager);

        // Assert
        Assert.Equal(100, logicalDistance);
    }

    #endregion

    #region Multi-Monitor DPI Tests

    [Fact]
    public void Conversions_WithMultipleMonitors_UsesCorrectMonitor()
    {
        // Arrange
        var monitorManager = new MockMonitorManager();

        // Monitor 1: Standard DPI
        var monitor1 = new MonitorInfo(new IntPtr(1), new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1080), true);
        monitorManager.AddMonitor(monitor1);
        monitorManager.SetMonitorDpi(monitor1, 96, 96); // 1.0x scale

        // Monitor 2: High DPI
        var monitor2 = new MonitorInfo(new IntPtr(2), new Rectangle(1920, 0, 1920, 1080),
            new Rectangle(1920, 0, 1920, 1080), false);
        monitorManager.AddMonitor(monitor2);
        monitorManager.SetMonitorDpi(monitor2, 144, 144); // 1.5x scale

        var pointOnMonitor1 = new Point(500, 500);
        var pointOnMonitor2 = new Point(2000, 500);

        // Act
        var scaledDistance1 = DpiUtilities.ScaleDistance(100, pointOnMonitor1, monitorManager);
        var scaledDistance2 = DpiUtilities.ScaleDistance(100, pointOnMonitor2, monitorManager);

        // Assert
        Assert.Equal(100, scaledDistance1); // No scaling on standard DPI monitor
        Assert.Equal(150, scaledDistance2); // 1.5x scaling on high DPI monitor
    }

    [Fact]
    public void ConvertBetweenDpiContexts_Point_ConvertsCorrectly()
    {
        // Arrange
        var sourceDpi = new DpiInfo(96, 96);   // 1.0x scale
        var targetDpi = new DpiInfo(144, 144); // 1.5x scale
        var point = new Point(100, 200);

        // Act
        var convertedPoint = DpiUtilities.ConvertBetweenDpiContexts(point, sourceDpi, targetDpi);

        // Assert
        Assert.Equal(150, convertedPoint.X); // 100 * 1.5
        Assert.Equal(300, convertedPoint.Y); // 200 * 1.5
    }

    [Fact]
    public void ConvertBetweenDpiContexts_Rectangle_ConvertsCorrectly()
    {
        // Arrange
        var sourceDpi = new DpiInfo(144, 144); // 1.5x scale
        var targetDpi = new DpiInfo(96, 96);   // 1.0x scale
        var rectangle = new Rectangle(150, 300, 300, 225);

        // Act
        var convertedRect = DpiUtilities.ConvertBetweenDpiContexts(rectangle, sourceDpi, targetDpi);

        // Assert
        Assert.Equal(100, convertedRect.X);      // 150 / 1.5
        Assert.Equal(200, convertedRect.Y);      // 300 / 1.5
        Assert.Equal(200, convertedRect.Width);  // 300 / 1.5
        Assert.Equal(150, convertedRect.Height); // 225 / 1.5
    }

    #endregion

    #region System DPI Analysis Tests

    [Fact]
    public void GetMaximumScaleFactor_WithMultipleMonitors_ReturnsMax()
    {
        // Arrange
        var monitorManager = new MockMonitorManager();

        var monitor1 = new MonitorInfo(new IntPtr(1), new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1080), true);
        monitorManager.AddMonitor(monitor1);
        monitorManager.SetMonitorDpi(monitor1, 96, 96); // 1.0x scale

        var monitor2 = new MonitorInfo(new IntPtr(2), new Rectangle(1920, 0, 1920, 1080),
            new Rectangle(1920, 0, 1920, 1080), false);
        monitorManager.AddMonitor(monitor2);
        monitorManager.SetMonitorDpi(monitor2, 192, 192); // 2.0x scale

        var monitor3 = new MonitorInfo(new IntPtr(3), new Rectangle(3840, 0, 1920, 1080),
            new Rectangle(3840, 0, 1920, 1080), false);
        monitorManager.AddMonitor(monitor3);
        monitorManager.SetMonitorDpi(monitor3, 144, 144); // 1.5x scale

        // Act
        var maxScale = DpiUtilities.GetMaximumScaleFactor(monitorManager);

        // Assert
        Assert.Equal(2.0, maxScale, 3);
    }

    [Fact]
    public void GetMinimumScaleFactor_WithMultipleMonitors_ReturnsMin()
    {
        // Arrange
        var monitorManager = new MockMonitorManager();

        var monitor1 = new MonitorInfo(new IntPtr(1), new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1080), true);
        monitorManager.AddMonitor(monitor1);
        monitorManager.SetMonitorDpi(monitor1, 96, 96); // 1.0x scale

        var monitor2 = new MonitorInfo(new IntPtr(2), new Rectangle(1920, 0, 1920, 1080),
            new Rectangle(1920, 0, 1920, 1080), false);
        monitorManager.AddMonitor(monitor2);
        monitorManager.SetMonitorDpi(monitor2, 192, 192); // 2.0x scale

        // Act
        var minScale = DpiUtilities.GetMinimumScaleFactor(monitorManager);

        // Assert
        Assert.Equal(1.0, minScale, 3);
    }

    [Fact]
    public void HasMixedDpiMonitors_WithSameDpi_ReturnsFalse()
    {
        // Arrange
        var monitorManager = new MockMonitorManager();

        var monitor1 = new MonitorInfo(new IntPtr(1), new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1080), true);
        monitorManager.AddMonitor(monitor1);
        monitorManager.SetMonitorDpi(monitor1, 144, 144);

        var monitor2 = new MonitorInfo(new IntPtr(2), new Rectangle(1920, 0, 1920, 1080),
            new Rectangle(1920, 0, 1920, 1080), false);
        monitorManager.AddMonitor(monitor2);
        monitorManager.SetMonitorDpi(monitor2, 144, 144);

        // Act
        var hasMixedDpi = DpiUtilities.HasMixedDpiMonitors(monitorManager);

        // Assert
        Assert.False(hasMixedDpi);
    }

    [Fact]
    public void HasMixedDpiMonitors_WithDifferentDpi_ReturnsTrue()
    {
        // Arrange
        var monitorManager = new MockMonitorManager();

        var monitor1 = new MonitorInfo(new IntPtr(1), new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1080), true);
        monitorManager.AddMonitor(monitor1);
        monitorManager.SetMonitorDpi(monitor1, 96, 96);  // 1.0x scale

        var monitor2 = new MonitorInfo(new IntPtr(2), new Rectangle(1920, 0, 1920, 1080),
            new Rectangle(1920, 0, 1920, 1080), false);
        monitorManager.AddMonitor(monitor2);
        monitorManager.SetMonitorDpi(monitor2, 144, 144); // 1.5x scale

        // Act
        var hasMixedDpi = DpiUtilities.HasMixedDpiMonitors(monitorManager);

        // Assert
        Assert.True(hasMixedDpi);
    }

    [Fact]
    public void AreDpiValuesSignificantlyDifferent_WithSmallDifference_ReturnsFalse()
    {
        // Arrange
        var dpi1 = new DpiInfo(96, 96);   // 1.0x scale
        var dpi2 = new DpiInfo(100, 100); // ~1.04x scale

        // Act
        var areDifferent = DpiUtilities.AreDpiValuesSignificantlyDifferent(dpi1, dpi2, 0.1);

        // Assert
        Assert.False(areDifferent); // Difference is less than 10%
    }

    [Fact]
    public void AreDpiValuesSignificantlyDifferent_WithLargeDifference_ReturnsTrue()
    {
        // Arrange
        var dpi1 = new DpiInfo(96, 96);   // 1.0x scale
        var dpi2 = new DpiInfo(144, 144); // 1.5x scale

        // Act
        var areDifferent = DpiUtilities.AreDpiValuesSignificantlyDifferent(dpi1, dpi2, 0.1);

        // Assert
        Assert.True(areDifferent); // Difference is 50%, much more than 10%
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    public void GetMaximumScaleFactor_WithNoMonitors_ReturnsDefault()
    {
        // Arrange
        var monitorManager = new MockMonitorManager();

        // Act
        var maxScale = DpiUtilities.GetMaximumScaleFactor(monitorManager);

        // Assert
        Assert.Equal(1.0, maxScale, 3);
    }

    [Fact]
    public void GetMinimumScaleFactor_WithNoMonitors_ReturnsDefault()
    {
        // Arrange
        var monitorManager = new MockMonitorManager();

        // Act
        var minScale = DpiUtilities.GetMinimumScaleFactor(monitorManager);

        // Assert
        Assert.Equal(1.0, minScale, 3);
    }

    [Fact]
    public void HasMixedDpiMonitors_WithSingleMonitor_ReturnsFalse()
    {
        // Arrange
        var monitorManager = new MockMonitorManager();
        var monitor = new MonitorInfo(new IntPtr(1), new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1080), true);
        monitorManager.AddMonitor(monitor);
        monitorManager.SetMonitorDpi(monitor, 144, 144);

        // Act
        var hasMixedDpi = DpiUtilities.HasMixedDpiMonitors(monitorManager);

        // Assert
        Assert.False(hasMixedDpi);
    }

    [Fact]
    public void Conversions_WithPointOutsideAllMonitors_UsesDefaultDpi()
    {
        // Arrange
        var monitorManager = new MockMonitorManager();
        var monitor = new MonitorInfo(new IntPtr(1), new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 0, 1920, 1080), true);
        monitorManager.AddMonitor(monitor);
        monitorManager.SetMonitorDpi(monitor, 144, 144);

        var pointOutside = new Point(-1000, -1000); // Outside all monitors

        // Act
        var scaledDistance = DpiUtilities.ScaleDistance(100, pointOutside, monitorManager);

        // Assert
        Assert.Equal(100, scaledDistance); // Should use default 1.0x scale when no monitor found
    }

    #endregion
}