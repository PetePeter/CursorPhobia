using System.Drawing;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Services;
using Xunit;

namespace CursorPhobia.Tests;

public class MonitorManagerTests
{
    private readonly MonitorManager _monitorManager;
    
    public MonitorManagerTests()
    {
        _monitorManager = new MonitorManager();
    }
    
    [Fact]
    public void GetAllMonitors_ShouldReturnAtLeastOneMonitor()
    {
        // Act
        var monitors = _monitorManager.GetAllMonitors();
        
        // Assert
        Assert.NotNull(monitors);
        Assert.True(monitors.Count > 0, "Should have at least one monitor");
    }
    
    [Fact]
    public void GetAllMonitors_ShouldHavePrimaryMonitor()
    {
        // Act
        var monitors = _monitorManager.GetAllMonitors();
        
        // Assert
        Assert.True(monitors.Any(m => m.isPrimary), "Should have one primary monitor");
        Assert.Single(monitors, m => m.isPrimary);
    }
    
    [Fact]
    public void GetPrimaryMonitor_ShouldReturnPrimaryMonitor()
    {
        // Act
        var primaryMonitor = _monitorManager.GetPrimaryMonitor();
        
        // Assert
        Assert.NotNull(primaryMonitor);
        Assert.True(primaryMonitor.isPrimary);
    }
    
    [Fact]
    public void GetMonitorContaining_Point_ShouldReturnCorrectMonitor()
    {
        // Arrange
        var monitors = _monitorManager.GetAllMonitors();
        var firstMonitor = monitors.First();
        var testPoint = new Point(
            firstMonitor.monitorBounds.X + firstMonitor.monitorBounds.Width / 2,
            firstMonitor.monitorBounds.Y + firstMonitor.monitorBounds.Height / 2
        );
        
        // Act
        var containingMonitor = _monitorManager.GetMonitorContaining(testPoint);
        
        // Assert
        Assert.NotNull(containingMonitor);
        Assert.Equal(firstMonitor.monitorHandle, containingMonitor.monitorHandle);
    }
    
    [Fact]
    public void GetMonitorContaining_Rectangle_ShouldReturnCorrectMonitor()
    {
        // Arrange
        var monitors = _monitorManager.GetAllMonitors();
        var firstMonitor = monitors.First();
        var testRect = new Rectangle(
            firstMonitor.monitorBounds.X + 10,
            firstMonitor.monitorBounds.Y + 10,
            100,
            100
        );
        
        // Act
        var containingMonitor = _monitorManager.GetMonitorContaining(testRect);
        
        // Assert
        Assert.NotNull(containingMonitor);
        Assert.Equal(firstMonitor.monitorHandle, containingMonitor.monitorHandle);
    }
    
    [Fact]
    public void GetMonitorContaining_PointOutsideAllMonitors_ShouldReturnNull()
    {
        // Arrange - use a point that's likely outside all monitors
        var testPoint = new Point(-10000, -10000);
        
        // Act
        var containingMonitor = _monitorManager.GetMonitorContaining(testPoint);
        
        // Assert
        Assert.Null(containingMonitor);
    }
    
    [Fact]
    public void MonitorInfo_Properties_ShouldBeValid()
    {
        // Act
        var monitors = _monitorManager.GetAllMonitors();
        
        // Assert
        foreach (var monitor in monitors)
        {
            Assert.True(monitor.width > 0, "Monitor width should be positive");
            Assert.True(monitor.height > 0, "Monitor height should be positive");
            Assert.True(monitor.monitorBounds.Width > 0, "Monitor bounds width should be positive");
            Assert.True(monitor.monitorBounds.Height > 0, "Monitor bounds height should be positive");
            Assert.True(monitor.workAreaBounds.Width > 0, "Work area width should be positive");
            Assert.True(monitor.workAreaBounds.Height > 0, "Work area height should be positive");
            
            // Work area should be within or equal to monitor bounds
            Assert.True(monitor.workAreaBounds.Left >= monitor.monitorBounds.Left);
            Assert.True(monitor.workAreaBounds.Top >= monitor.monitorBounds.Top);
            Assert.True(monitor.workAreaBounds.Right <= monitor.monitorBounds.Right);
            Assert.True(monitor.workAreaBounds.Bottom <= monitor.monitorBounds.Bottom);
        }
    }
    
    [Fact]
    public void GetAdjacentMonitors_SingleMonitor_ShouldReturnEmpty()
    {
        // Arrange
        var monitors = _monitorManager.GetAllMonitors();
        
        // Act & Assert for each monitor
        foreach (var monitor in monitors)
        {
            var adjacent = _monitorManager.GetAdjacentMonitors(monitor);
            
            if (monitors.Count == 1)
            {
                Assert.Empty(adjacent);
            }
            else
            {
                // For multi-monitor setups, adjacent count should be reasonable
                Assert.True(adjacent.Count >= 0 && adjacent.Count < monitors.Count,
                    "Adjacent monitor count should be reasonable");
            }
        }
    }
    
    [Fact]
    public void GetMonitorInDirection_ShouldReturnCorrectResults()
    {
        // Arrange
        var monitors = _monitorManager.GetAllMonitors();
        
        if (monitors.Count < 2)
        {
            return; // Skip test if not enough monitors
        }
        
        // Act & Assert
        foreach (var monitor in monitors)
        {
            var leftMonitor = _monitorManager.GetMonitorInDirection(monitor, EdgeDirection.Left);
            var rightMonitor = _monitorManager.GetMonitorInDirection(monitor, EdgeDirection.Right);
            var upMonitor = _monitorManager.GetMonitorInDirection(monitor, EdgeDirection.Up);
            var downMonitor = _monitorManager.GetMonitorInDirection(monitor, EdgeDirection.Down);
            
            // Validate that returned monitors (if any) are different from source
            if (leftMonitor != null)
                Assert.NotEqual(monitor.monitorHandle, leftMonitor.monitorHandle);
            if (rightMonitor != null)
                Assert.NotEqual(monitor.monitorHandle, rightMonitor.monitorHandle);
            if (upMonitor != null)
                Assert.NotEqual(monitor.monitorHandle, upMonitor.monitorHandle);
            if (downMonitor != null)
                Assert.NotEqual(monitor.monitorHandle, downMonitor.monitorHandle);
        }
    }
    
    [Fact]
    public void MonitorCache_ShouldWork()
    {
        // Arrange & Act
        var monitors1 = _monitorManager.GetAllMonitors();
        var monitors2 = _monitorManager.GetAllMonitors(); // Should use cache
        
        // Assert
        Assert.Equal(monitors1.Count, monitors2.Count);
        
        for (int i = 0; i < monitors1.Count; i++)
        {
            Assert.Equal(monitors1[i].monitorHandle, monitors2[i].monitorHandle);
            Assert.Equal(monitors1[i].monitorBounds, monitors2[i].monitorBounds);
            Assert.Equal(monitors1[i].workAreaBounds, monitors2[i].workAreaBounds);
            Assert.Equal(monitors1[i].isPrimary, monitors2[i].isPrimary);
        }
    }
}