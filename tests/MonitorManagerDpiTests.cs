using System.Drawing;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Services;
using Xunit;

namespace CursorPhobia.Tests;

/// <summary>
/// Unit tests for MonitorManager DPI functionality
/// </summary>
public class MonitorManagerDpiTests
{
    #region DPI Interface Tests
    
    [Fact]
    public void MonitorManager_ImplementsIMonitorManager()
    {
        // Arrange & Act
        var monitorManager = new MonitorManager();
        
        // Assert
        Assert.IsAssignableFrom<IMonitorManager>(monitorManager);
    }
    
    [Fact]
    public void GetMonitorDpi_WithValidMonitor_ReturnsDpiInfo()
    {
        // Arrange
        var monitorManager = new MonitorManager();
        var monitors = monitorManager.GetAllMonitors();
        
        if (monitors.Count == 0)
        {
            // Skip test if no monitors available
            return;
        }
        
        var firstMonitor = monitors[0];
        
        // Act
        var dpiInfo = monitorManager.GetMonitorDpi(firstMonitor);
        
        // Assert
        Assert.NotNull(dpiInfo);
        Assert.True(dpiInfo.DpiX > 0);
        Assert.True(dpiInfo.DpiY > 0);
        Assert.True(dpiInfo.ScaleFactor > 0);
    }
    
    [Fact]
    public void GetDpiForPoint_WithValidPoint_ReturnsDpiInfo()
    {
        // Arrange
        var monitorManager = new MonitorManager();
        var monitors = monitorManager.GetAllMonitors();
        
        if (monitors.Count == 0)
        {
            // Skip test if no monitors available
            return;
        }
        
        var monitor = monitors[0];
        var pointInMonitor = new Point(
            monitor.monitorBounds.X + monitor.monitorBounds.Width / 2,
            monitor.monitorBounds.Y + monitor.monitorBounds.Height / 2
        );
        
        // Act
        var dpiInfo = monitorManager.GetDpiForPoint(pointInMonitor);
        
        // Assert
        Assert.NotNull(dpiInfo);
        Assert.True(dpiInfo.DpiX > 0);
        Assert.True(dpiInfo.DpiY > 0);
    }
    
    [Fact]
    public void GetDpiForPoint_WithPointOutsideMonitors_ReturnsDefaultDpi()
    {
        // Arrange
        var monitorManager = new MonitorManager();
        var pointOutside = new Point(-10000, -10000);
        
        // Act
        var dpiInfo = monitorManager.GetDpiForPoint(pointOutside);
        
        // Assert
        Assert.NotNull(dpiInfo);
        // Should return default DPI values when no monitor found
        Assert.Equal(96u, dpiInfo.DpiX);
        Assert.Equal(96u, dpiInfo.DpiY);
    }
    
    [Fact]
    public void GetDpiForRectangle_WithValidRectangle_ReturnsDpiInfo()
    {
        // Arrange
        var monitorManager = new MonitorManager();
        var monitors = monitorManager.GetAllMonitors();
        
        if (monitors.Count == 0)
        {
            // Skip test if no monitors available
            return;
        }
        
        var monitor = monitors[0];
        var rectInMonitor = new Rectangle(
            monitor.monitorBounds.X + 10,
            monitor.monitorBounds.Y + 10,
            100, 100
        );
        
        // Act
        var dpiInfo = monitorManager.GetDpiForRectangle(rectInMonitor);
        
        // Assert
        Assert.NotNull(dpiInfo);
        Assert.True(dpiInfo.DpiX > 0);
        Assert.True(dpiInfo.DpiY > 0);
    }
    
    [Fact]
    public void GetDpiForRectangle_WithRectangleOutsideMonitors_ReturnsDefaultDpi()
    {
        // Arrange
        var monitorManager = new MonitorManager();
        var rectOutside = new Rectangle(-10000, -10000, 100, 100);
        
        // Act
        var dpiInfo = monitorManager.GetDpiForRectangle(rectOutside);
        
        // Assert
        Assert.NotNull(dpiInfo);
        // Should return default DPI values when no monitor found
        Assert.Equal(96u, dpiInfo.DpiX);
        Assert.Equal(96u, dpiInfo.DpiY);
    }
    
    #endregion
    
    #region Mock MonitorManager Tests
    
    [Fact]
    public void MockMonitorManager_ImplementsIMonitorManager()
    {
        // Arrange & Act
        var mockMonitorManager = new MockMonitorManager();
        
        // Assert
        Assert.IsAssignableFrom<IMonitorManager>(mockMonitorManager);
    }
    
    [Fact]
    public void MockMonitorManager_GetMonitorDpi_ReturnsConfiguredDpi()
    {
        // Arrange
        var mockMonitorManager = new MockMonitorManager();
        var monitor = new MonitorInfo(new IntPtr(1), new Rectangle(0, 0, 1920, 1080), 
            new Rectangle(0, 0, 1920, 1080), true);
        var expectedDpi = new DpiInfo(144, 120);
        
        mockMonitorManager.AddMonitor(monitor);
        mockMonitorManager.SetMonitorDpi(monitor, expectedDpi);
        
        // Act
        var actualDpi = mockMonitorManager.GetMonitorDpi(monitor);
        
        // Assert
        Assert.Equal(expectedDpi.DpiX, actualDpi.DpiX);
        Assert.Equal(expectedDpi.DpiY, actualDpi.DpiY);
    }
    
    [Fact]
    public void MockMonitorManager_SetMonitorDpi_WithDpiValues_ConfiguresCorrectly()
    {
        // Arrange
        var mockMonitorManager = new MockMonitorManager();
        var monitor = new MonitorInfo(new IntPtr(1), new Rectangle(0, 0, 1920, 1080), 
            new Rectangle(0, 0, 1920, 1080), true);
        uint expectedDpiX = 192;
        uint expectedDpiY = 144;
        
        mockMonitorManager.AddMonitor(monitor);
        mockMonitorManager.SetMonitorDpi(monitor, expectedDpiX, expectedDpiY);
        
        // Act
        var actualDpi = mockMonitorManager.GetMonitorDpi(monitor);
        
        // Assert
        Assert.Equal(expectedDpiX, actualDpi.DpiX);
        Assert.Equal(expectedDpiY, actualDpi.DpiY);
        Assert.Equal(2.0, actualDpi.ScaleFactorX, 3);
        Assert.Equal(1.5, actualDpi.ScaleFactorY, 3);
    }
    
    [Fact]
    public void MockMonitorManager_GetMonitorDpi_WithoutConfiguration_ReturnsDefaultDpi()
    {
        // Arrange
        var mockMonitorManager = new MockMonitorManager();
        var monitor = new MonitorInfo(new IntPtr(1), new Rectangle(0, 0, 1920, 1080), 
            new Rectangle(0, 0, 1920, 1080), true);
        
        mockMonitorManager.AddMonitor(monitor);
        // Don't set DPI - should return default
        
        // Act
        var dpiInfo = mockMonitorManager.GetMonitorDpi(monitor);
        
        // Assert
        Assert.Equal(96u, dpiInfo.DpiX);
        Assert.Equal(96u, dpiInfo.DpiY);
        Assert.Equal(1.0, dpiInfo.ScaleFactor, 3);
    }
    
    [Fact]
    public void MockMonitorManager_GetDpiForPoint_UsesCorrectMonitor()
    {
        // Arrange
        var mockMonitorManager = new MockMonitorManager();
        
        // Monitor 1: Standard DPI
        var monitor1 = new MonitorInfo(new IntPtr(1), new Rectangle(0, 0, 1920, 1080), 
            new Rectangle(0, 0, 1920, 1080), true);
        mockMonitorManager.AddMonitor(monitor1);
        mockMonitorManager.SetMonitorDpi(monitor1, 96, 96);
        
        // Monitor 2: High DPI
        var monitor2 = new MonitorInfo(new IntPtr(2), new Rectangle(1920, 0, 1920, 1080), 
            new Rectangle(1920, 0, 1920, 1080), false);
        mockMonitorManager.AddMonitor(monitor2);
        mockMonitorManager.SetMonitorDpi(monitor2, 144, 144);
        
        var pointOnMonitor1 = new Point(500, 500);
        var pointOnMonitor2 = new Point(2000, 500);
        
        // Act
        var dpi1 = mockMonitorManager.GetDpiForPoint(pointOnMonitor1);
        var dpi2 = mockMonitorManager.GetDpiForPoint(pointOnMonitor2);
        
        // Assert
        Assert.Equal(96u, dpi1.DpiX);
        Assert.Equal(96u, dpi1.DpiY);
        Assert.Equal(144u, dpi2.DpiX);
        Assert.Equal(144u, dpi2.DpiY);
    }
    
    [Fact]
    public void MockMonitorManager_GetDpiForRectangle_UsesCorrectMonitor()
    {
        // Arrange
        var mockMonitorManager = new MockMonitorManager();
        
        // Monitor 1: Standard DPI
        var monitor1 = new MonitorInfo(new IntPtr(1), new Rectangle(0, 0, 1920, 1080), 
            new Rectangle(0, 0, 1920, 1080), true);
        mockMonitorManager.AddMonitor(monitor1);
        mockMonitorManager.SetMonitorDpi(monitor1, 96, 96);
        
        // Monitor 2: High DPI
        var monitor2 = new MonitorInfo(new IntPtr(2), new Rectangle(1920, 0, 1920, 1080), 
            new Rectangle(1920, 0, 1920, 1080), false);
        mockMonitorManager.AddMonitor(monitor2);
        mockMonitorManager.SetMonitorDpi(monitor2, 192, 192);
        
        var rectOnMonitor1 = new Rectangle(100, 100, 200, 150);
        var rectOnMonitor2 = new Rectangle(2000, 100, 200, 150);
        
        // Act
        var dpi1 = mockMonitorManager.GetDpiForRectangle(rectOnMonitor1);
        var dpi2 = mockMonitorManager.GetDpiForRectangle(rectOnMonitor2);
        
        // Assert
        Assert.Equal(96u, dpi1.DpiX);
        Assert.Equal(96u, dpi1.DpiY);
        Assert.Equal(192u, dpi2.DpiX);
        Assert.Equal(192u, dpi2.DpiY);
    }
    
    #endregion
    
    #region Mixed DPI Scenario Tests
    
    [Fact]
    public void MockMonitorManager_ComplexMixedDpiScenario_HandlesCorrectly()
    {
        // Arrange - Complex multi-monitor setup with different DPI values
        var mockMonitorManager = new MockMonitorManager();
        
        // Primary monitor: Standard DPI, left side
        var primaryMonitor = new MonitorInfo(new IntPtr(1), new Rectangle(0, 0, 1920, 1080), 
            new Rectangle(0, 40, 1920, 1040), true);
        mockMonitorManager.AddMonitor(primaryMonitor);
        mockMonitorManager.SetMonitorDpi(primaryMonitor, 96, 96); // 1.0x scale
        
        // Secondary monitor: High DPI, right side
        var secondaryMonitor = new MonitorInfo(new IntPtr(2), new Rectangle(1920, 0, 2560, 1440), 
            new Rectangle(1920, 0, 2560, 1440), false);
        mockMonitorManager.AddMonitor(secondaryMonitor);
        mockMonitorManager.SetMonitorDpi(secondaryMonitor, 144, 144); // 1.5x scale
        
        // Tertiary monitor: Very high DPI, above primary
        var tertiaryMonitor = new MonitorInfo(new IntPtr(3), new Rectangle(0, -1080, 1920, 1080), 
            new Rectangle(0, -1080, 1920, 1080), false);
        mockMonitorManager.AddMonitor(tertiaryMonitor);
        mockMonitorManager.SetMonitorDpi(tertiaryMonitor, 192, 192); // 2.0x scale
        
        // Act & Assert
        var monitors = mockMonitorManager.GetAllMonitors();
        Assert.Equal(3, monitors.Count);
        
        // Test DPI for points on each monitor
        var dpiPrimary = mockMonitorManager.GetDpiForPoint(new Point(960, 540));
        var dpiSecondary = mockMonitorManager.GetDpiForPoint(new Point(3200, 720));
        var dpiTertiary = mockMonitorManager.GetDpiForPoint(new Point(960, -540));
        
        Assert.Equal(1.0, dpiPrimary.ScaleFactor, 3);
        Assert.Equal(1.5, dpiSecondary.ScaleFactor, 3);
        Assert.Equal(2.0, dpiTertiary.ScaleFactor, 3);
        
        // Test adjacent monitor functionality still works
        mockMonitorManager.SetAdjacentMonitor(primaryMonitor, EdgeDirection.Right, secondaryMonitor);
        mockMonitorManager.SetAdjacentMonitor(primaryMonitor, EdgeDirection.Up, tertiaryMonitor);
        
        var rightMonitor = mockMonitorManager.GetMonitorInDirection(primaryMonitor, EdgeDirection.Right);
        var upMonitor = mockMonitorManager.GetMonitorInDirection(primaryMonitor, EdgeDirection.Up);
        
        Assert.Equal(secondaryMonitor.monitorHandle, rightMonitor?.monitorHandle);
        Assert.Equal(tertiaryMonitor.monitorHandle, upMonitor?.monitorHandle);
    }
    
    [Fact]
    public void MockMonitorManager_WindowSpanningMultipleMonitors_UsesCorrectDpi()
    {
        // Arrange
        var mockMonitorManager = new MockMonitorManager();
        
        var monitor1 = new MonitorInfo(new IntPtr(1), new Rectangle(0, 0, 1920, 1080), 
            new Rectangle(0, 0, 1920, 1080), true);
        mockMonitorManager.AddMonitor(monitor1);
        mockMonitorManager.SetMonitorDpi(monitor1, 96, 96);
        
        var monitor2 = new MonitorInfo(new IntPtr(2), new Rectangle(1920, 0, 1920, 1080), 
            new Rectangle(1920, 0, 1920, 1080), false);
        mockMonitorManager.AddMonitor(monitor2);
        mockMonitorManager.SetMonitorDpi(monitor2, 144, 144);
        
        // Rectangle spanning both monitors, but center point is on monitor1
        var spanningRect = new Rectangle(1800, 400, 240, 200);  // Spans from x=1800 to x=2040
        
        // Act
        var dpiInfo = mockMonitorManager.GetDpiForRectangle(spanningRect);
        
        // Assert
        // Should use DPI from monitor containing the center point (1920, 500)
        // Center point is on monitor2, so should use high DPI
        Assert.Equal(144u, dpiInfo.DpiX);
        Assert.Equal(144u, dpiInfo.DpiY);
    }
    
    #endregion
    
    #region Performance Tests
    
    [Fact]
    public void GetMonitorDpi_CalledMultipleTimes_PerformsReasonably()
    {
        // Arrange
        var mockMonitorManager = new MockMonitorManager();
        var monitor = new MonitorInfo(new IntPtr(1), new Rectangle(0, 0, 1920, 1080), 
            new Rectangle(0, 0, 1920, 1080), true);
        mockMonitorManager.AddMonitor(monitor);
        mockMonitorManager.SetMonitorDpi(monitor, 144, 144);
        
        // Act - Call DPI method many times
        var startTime = DateTime.Now;
        for (int i = 0; i < 1000; i++)
        {
            var dpiInfo = mockMonitorManager.GetMonitorDpi(monitor);
            Assert.Equal(144u, dpiInfo.DpiX);
        }
        var elapsed = DateTime.Now - startTime;
        
        // Assert - Should complete in reasonable time (less than 1 second)
        Assert.True(elapsed.TotalSeconds < 1.0, $"DPI queries took too long: {elapsed.TotalSeconds} seconds");
    }
    
    #endregion
}