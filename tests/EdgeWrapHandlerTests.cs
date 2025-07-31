using System.Drawing;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Services;
using Xunit;
using Moq;

namespace CursorPhobia.Tests;

public class EdgeWrapHandlerTests
{
    private readonly Mock<MonitorManager> _mockMonitorManager;
    private readonly EdgeWrapHandler _edgeWrapHandler;
    private readonly MonitorInfo _primaryMonitor;
    private readonly MonitorInfo _secondaryMonitor;
    
    public EdgeWrapHandlerTests()
    {
        _mockMonitorManager = new Mock<MonitorManager>();
        _edgeWrapHandler = new EdgeWrapHandler(_mockMonitorManager.Object);
        
        // Setup test monitors
        _primaryMonitor = new MonitorInfo(
            new IntPtr(1),
            new Rectangle(0, 0, 1920, 1080),
            new Rectangle(0, 40, 1920, 1040), // Taskbar at bottom
            true,
            "Primary"
        );
        
        _secondaryMonitor = new MonitorInfo(
            new IntPtr(2),
            new Rectangle(1920, 0, 1920, 1080),
            new Rectangle(1920, 0, 1920, 1080),
            false,
            "Secondary"
        );
    }
    
    [Fact]
    public void Constructor_NullMonitorManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new EdgeWrapHandler(null!));
    }
    
    [Fact]
    public void CalculateWrapDestination_WrappingDisabled_ReturnsNull()
    {
        // Arrange
        var windowRect = new Rectangle(100, 100, 300, 200);
        var pushVector = new Point(-50, 0);
        var wrapBehavior = new WrapBehavior { EnableWrapping = false };
        
        // Act
        var result = _edgeWrapHandler.CalculateWrapDestination(windowRect, pushVector, wrapBehavior);
        
        // Assert
        Assert.Null(result);
    }
    
    [Fact]
    public void CalculateWrapDestination_NoCurrentMonitor_ReturnsNull()
    {
        // Arrange
        var windowRect = new Rectangle(100, 100, 300, 200);
        var pushVector = new Point(-50, 0);
        var wrapBehavior = new WrapBehavior { EnableWrapping = true };
        
        _mockMonitorManager.Setup(m => m.GetMonitorContaining(windowRect)).Returns((MonitorInfo?)null);
        
        // Act
        var result = _edgeWrapHandler.CalculateWrapDestination(windowRect, pushVector, wrapBehavior);
        
        // Assert
        Assert.Null(result);
    }
    
    [Fact]
    public void CalculateWrapDestination_NoEdgeCrossed_ReturnsNull()
    {
        // Arrange
        var windowRect = new Rectangle(500, 500, 300, 200); // Center of screen
        var pushVector = new Point(10, 10); // Small push not reaching edge
        var wrapBehavior = new WrapBehavior { EnableWrapping = true };
        
        _mockMonitorManager.Setup(m => m.GetMonitorContaining(windowRect)).Returns(_primaryMonitor);
        
        // Act
        var result = _edgeWrapHandler.CalculateWrapDestination(windowRect, pushVector, wrapBehavior);
        
        // Assert
        Assert.Null(result);
    }
    
    [Fact]
    public void CalculateWrapDestination_LeftEdge_OppositeWrap_ReturnsCorrectPosition()
    {
        // Arrange
        var windowRect = new Rectangle(0, 300, 300, 200); // At left edge
        var pushVector = new Point(-50, 0); // Pushing left
        var wrapBehavior = new WrapBehavior
        {
            EnableWrapping = true,
            PreferredBehavior = WrapPreference.Opposite
        };
        
        _mockMonitorManager.Setup(m => m.GetMonitorContaining(windowRect)).Returns(_primaryMonitor);
        
        // Act
        var result = _edgeWrapHandler.CalculateWrapDestination(windowRect, pushVector, wrapBehavior);
        
        // Assert
        Assert.NotNull(result);
        // Should wrap to right edge: monitor width - window width
        Assert.Equal(_primaryMonitor.workAreaBounds.Right - windowRect.Width, result.Value.X);
        Assert.Equal(windowRect.Y, result.Value.Y); // Y should remain the same
    }
    
    [Fact]
    public void CalculateWrapDestination_RightEdge_OppositeWrap_ReturnsCorrectPosition()
    {
        // Arrange
        var windowRect = new Rectangle(1620, 300, 300, 200); // At right edge (1920 - 300)
        var pushVector = new Point(50, 0); // Pushing right
        var wrapBehavior = new WrapBehavior
        {
            EnableWrapping = true,
            PreferredBehavior = WrapPreference.Opposite
        };
        
        _mockMonitorManager.Setup(m => m.GetMonitorContaining(windowRect)).Returns(_primaryMonitor);
        
        // Act
        var result = _edgeWrapHandler.CalculateWrapDestination(windowRect, pushVector, wrapBehavior);
        
        // Assert
        Assert.NotNull(result);
        // Should wrap to left edge
        Assert.Equal(_primaryMonitor.workAreaBounds.Left, result.Value.X);
        Assert.Equal(windowRect.Y, result.Value.Y);
    }
    
    [Fact]
    public void CalculateWrapDestination_TopEdge_OppositeWrap_ReturnsCorrectPosition()
    {
        // Arrange
        var windowRect = new Rectangle(500, 40, 300, 200); // At top edge (considering taskbar)
        var pushVector = new Point(0, -50); // Pushing up
        var wrapBehavior = new WrapBehavior
        {
            EnableWrapping = true,
            PreferredBehavior = WrapPreference.Opposite
        };
        
        _mockMonitorManager.Setup(m => m.GetMonitorContaining(windowRect)).Returns(_primaryMonitor);
        
        // Act
        var result = _edgeWrapHandler.CalculateWrapDestination(windowRect, pushVector, wrapBehavior);
        
        // Assert
        Assert.NotNull(result);
        // Should wrap to bottom edge
        Assert.Equal(windowRect.X, result.Value.X);
        Assert.Equal(_primaryMonitor.workAreaBounds.Bottom - windowRect.Height, result.Value.Y);
    }
    
    [Fact]
    public void CalculateWrapDestination_BottomEdge_OppositeWrap_ReturnsCorrectPosition()
    {
        // Arrange
        var windowRect = new Rectangle(500, 880, 300, 200); // At bottom edge (1080 - 40 - 200)
        var pushVector = new Point(0, 50); // Pushing down
        var wrapBehavior = new WrapBehavior
        {
            EnableWrapping = true,
            PreferredBehavior = WrapPreference.Opposite
        };
        
        _mockMonitorManager.Setup(m => m.GetMonitorContaining(windowRect)).Returns(_primaryMonitor);
        
        // Act
        var result = _edgeWrapHandler.CalculateWrapDestination(windowRect, pushVector, wrapBehavior);
        
        // Assert
        Assert.NotNull(result);
        // Should wrap to top edge
        Assert.Equal(windowRect.X, result.Value.X);
        Assert.Equal(_primaryMonitor.workAreaBounds.Top, result.Value.Y);
    }
    
    [Fact]
    public void CalculateWrapDestination_AdjacentWrap_WithAdjacentMonitor_ReturnsCorrectPosition()
    {
        // Arrange
        var windowRect = new Rectangle(1620, 300, 300, 200); // At right edge of primary
        var pushVector = new Point(50, 0); // Pushing right
        var wrapBehavior = new WrapBehavior
        {
            EnableWrapping = true,
            PreferredBehavior = WrapPreference.Adjacent
        };
        
        _mockMonitorManager.Setup(m => m.GetMonitorContaining(windowRect)).Returns(_primaryMonitor);
        _mockMonitorManager.Setup(m => m.GetMonitorInDirection(_primaryMonitor, EdgeDirection.Right))
                          .Returns(_secondaryMonitor);
        
        // Act
        var result = _edgeWrapHandler.CalculateWrapDestination(windowRect, pushVector, wrapBehavior);
        
        // Assert
        Assert.NotNull(result);
        // Should wrap to left edge of secondary monitor
        Assert.Equal(_secondaryMonitor.workAreaBounds.Left, result.Value.X);
        Assert.Equal(windowRect.Y, result.Value.Y); // Y should be constrained to secondary work area
    }
    
    [Fact]
    public void CalculateWrapDestination_AdjacentWrap_NoAdjacentMonitor_ReturnsOppositeWrap()
    {
        // Arrange
        var windowRect = new Rectangle(0, 300, 300, 200); // At left edge
        var pushVector = new Point(-50, 0); // Pushing left
        var wrapBehavior = new WrapBehavior
        {
            EnableWrapping = true,
            PreferredBehavior = WrapPreference.Adjacent
        };
        
        _mockMonitorManager.Setup(m => m.GetMonitorContaining(windowRect)).Returns(_primaryMonitor);
        _mockMonitorManager.Setup(m => m.GetMonitorInDirection(_primaryMonitor, EdgeDirection.Left))
                          .Returns((MonitorInfo?)null); // No adjacent monitor
        
        // Act
        var result = _edgeWrapHandler.CalculateWrapDestination(windowRect, pushVector, wrapBehavior);
        
        // Assert
        Assert.NotNull(result);
        // Should fallback to opposite edge wrap
        Assert.Equal(_primaryMonitor.workAreaBounds.Right - windowRect.Width, result.Value.X);
        Assert.Equal(windowRect.Y, result.Value.Y);
    }
    
    [Fact]
    public void CalculateWrapDestination_SmartWrap_WithAdjacentMonitor_ReturnsAdjacentWrap()
    {
        // Arrange
        var windowRect = new Rectangle(1620, 300, 300, 200);
        var pushVector = new Point(50, 0);
        var wrapBehavior = new WrapBehavior
        {
            EnableWrapping = true,
            PreferredBehavior = WrapPreference.Smart
        };
        
        _mockMonitorManager.Setup(m => m.GetMonitorContaining(windowRect)).Returns(_primaryMonitor);
        _mockMonitorManager.Setup(m => m.GetMonitorInDirection(_primaryMonitor, EdgeDirection.Right))
                          .Returns(_secondaryMonitor);
        
        // Act
        var result = _edgeWrapHandler.CalculateWrapDestination(windowRect, pushVector, wrapBehavior);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(_secondaryMonitor.workAreaBounds.Left, result.Value.X);
    }
    
    [Fact]
    public void CalculateWrapDestination_SmartWrap_NoAdjacentMonitor_ReturnsOppositeWrap()
    {
        // Arrange
        var windowRect = new Rectangle(0, 300, 300, 200);
        var pushVector = new Point(-50, 0);
        var wrapBehavior = new WrapBehavior
        {
            EnableWrapping = true,
            PreferredBehavior = WrapPreference.Smart
        };
        
        _mockMonitorManager.Setup(m => m.GetMonitorContaining(windowRect)).Returns(_primaryMonitor);
        _mockMonitorManager.Setup(m => m.GetMonitorInDirection(_primaryMonitor, EdgeDirection.Left))
                          .Returns((MonitorInfo?)null);
        
        // Act
        var result = _edgeWrapHandler.CalculateWrapDestination(windowRect, pushVector, wrapBehavior);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(_primaryMonitor.workAreaBounds.Right - windowRect.Width, result.Value.X);
    }
    
    [Fact]
    public void IsWrapSafe_MinimumDistance_ReturnsFalse()
    {
        // Arrange
        var originalPosition = new Point(100, 100);
        var newPosition = new Point(110, 100); // Only 10 pixels away
        var windowSize = new Size(300, 200);
        
        // Act
        var result = _edgeWrapHandler.IsWrapSafe(originalPosition, newPosition, windowSize);
        
        // Assert
        Assert.False(result, "Should reject wrap with insufficient distance");
    }
    
    [Fact]
    public void IsWrapSafe_SufficientDistance_ValidMonitor_ReturnsTrue()
    {
        // Arrange
        var originalPosition = new Point(100, 100);
        var newPosition = new Point(200, 100); // 100 pixels away
        var windowSize = new Size(300, 200);
        var newRect = new Rectangle(newPosition, windowSize);
        
        _mockMonitorManager.Setup(m => m.GetMonitorContaining(newRect)).Returns(_primaryMonitor);
        
        // Act
        var result = _edgeWrapHandler.IsWrapSafe(originalPosition, newPosition, windowSize);
        
        // Assert
        Assert.True(result, "Should accept wrap with sufficient distance and valid monitor");
    }
    
    [Fact]
    public void IsWrapSafe_SufficientDistance_NoValidMonitor_ReturnsFalse()
    {
        // Arrange
        var originalPosition = new Point(100, 100);
        var newPosition = new Point(200, 100);
        var windowSize = new Size(300, 200);
        var newRect = new Rectangle(newPosition, windowSize);
        
        _mockMonitorManager.Setup(m => m.GetMonitorContaining(newRect)).Returns((MonitorInfo?)null);
        
        // Act
        var result = _edgeWrapHandler.IsWrapSafe(originalPosition, newPosition, windowSize);
        
        // Assert
        Assert.False(result, "Should reject wrap when no monitor contains the new position");
    }
}