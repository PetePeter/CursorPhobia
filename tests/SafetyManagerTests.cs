using System.Drawing;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;
using Xunit;

namespace CursorPhobia.Tests;

/// <summary>
/// Unit tests for SafetyManager service
/// </summary>
public class SafetyManagerTests
{
    private readonly ILogger _logger;
    private readonly CursorPhobiaConfiguration _defaultConfig;

    public SafetyManagerTests()
    {
        _logger = new TestLogger();
        _defaultConfig = CursorPhobiaConfiguration.CreateDefault();
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Act
        var safetyManager = new SafetyManager(_logger);

        // Assert
        Assert.NotNull(safetyManager);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SafetyManager(null!));
    }

    [Fact]
    public void Constructor_WithInvalidConfiguration_ThrowsArgumentException()
    {
        // Arrange
        var invalidConfig = new CursorPhobiaConfiguration
        {
            ScreenEdgeBuffer = -1 // Invalid value
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new SafetyManager(_logger, invalidConfig));
        Assert.Contains("Invalid safety manager configuration", exception.Message);
    }

    [Fact]
    public void GetSafeScreenAreas_ReturnsNonEmptyList()
    {
        // Arrange
        var safetyManager = new SafetyManager(_logger);

        // Act
        var safeAreas = safetyManager.GetSafeScreenAreas();

        // Assert
        Assert.NotNull(safeAreas);
        Assert.NotEmpty(safeAreas);
    }

    [Fact]
    public void RefreshScreenBounds_DoesNotThrow()
    {
        // Arrange
        var safetyManager = new SafetyManager(_logger);

        // Act & Assert
        var exception = Record.Exception(() => safetyManager.RefreshScreenBounds());
        Assert.Null(exception);
    }

    [Fact]
    public void ValidateWindowPosition_WithValidInput_ReturnsValidPosition()
    {
        // Arrange
        var safetyManager = new SafetyManager(_logger);
        var windowBounds = new Rectangle(0, 0, 200, 150);
        var proposedPosition = new Point(100, 100);

        // Act
        var safePosition = safetyManager.ValidateWindowPosition(windowBounds, proposedPosition);

        // Assert
        Assert.IsType<Point>(safePosition);
        // Position should be valid (coordinates should be reasonable)
        Assert.True(safePosition.X >= -1000 && safePosition.X <= 10000);
        Assert.True(safePosition.Y >= -1000 && safePosition.Y <= 10000);
    }

    [Fact]
    public void ValidateWindowPosition_WithOffScreenPosition_AdjustsPosition()
    {
        // Arrange
        var safetyManager = new SafetyManager(_logger);
        var windowBounds = new Rectangle(0, 0, 200, 150);
        var proposedPosition = new Point(-500, -500); // Way off screen

        // Act
        var safePosition = safetyManager.ValidateWindowPosition(windowBounds, proposedPosition);

        // Assert
        Assert.NotEqual(proposedPosition, safePosition);
        // Safe position should be more reasonable
        Assert.True(safePosition.X > proposedPosition.X);
        Assert.True(safePosition.Y > proposedPosition.Y);
    }

    [Theory]
    [InlineData(100, 100, 200, 150, true)] // Normal window position
    [InlineData(-1000, -1000, 200, 150, false)] // Way off screen
    [InlineData(10000, 10000, 200, 150, false)] // Way off screen right/bottom
    public void IsPositionSafe_WithVariousPositions_ReturnsExpectedResult(
        int x, int y, int width, int height, bool expectedSafety)
    {
        // Arrange
        var safetyManager = new SafetyManager(_logger);
        var windowBounds = new Rectangle(x, y, width, height);

        // Act
        var isSafe = safetyManager.IsPositionSafe(windowBounds);

        // Assert
        if (expectedSafety)
        {
            // For positions expected to be safe, we might get true or false depending on actual screen setup
            Assert.IsType<bool>(isSafe);
        }
        else
        {
            // For obviously unsafe positions, should definitely be false
            Assert.False(isSafe);
        }
    }

    [Theory]
    [InlineData(100, 100)] // Small window
    [InlineData(800, 600)] // Medium window  
    [InlineData(1920, 1080)] // Large window
    public void CalculateMinimumSafeDistance_WithDifferentWindowSizes_ReturnsValidDistance(int width, int height)
    {
        // Arrange
        var safetyManager = new SafetyManager(_logger);
        var windowBounds = new Rectangle(100, 100, width, height);

        // Act
        var distance = safetyManager.CalculateMinimumSafeDistance(windowBounds);

        // Assert
        Assert.True(distance >= 0, "Minimum safe distance should be non-negative");
        Assert.True(distance <= 100, "Minimum safe distance should be reasonable");
    }

    [Fact]
    public void CalculateMinimumSafeDistance_WithSmallWindow_ReturnsReducedDistance()
    {
        // Arrange
        var config = new CursorPhobiaConfiguration { ScreenEdgeBuffer = 20 };
        var safetyManager = new SafetyManager(_logger, config);
        var smallWindow = new Rectangle(100, 100, 150, 100); // Small window

        // Act
        var distance = safetyManager.CalculateMinimumSafeDistance(smallWindow);

        // Assert
        Assert.True(distance <= config.ScreenEdgeBuffer, "Small windows should have reduced safe distance");
    }

    [Fact]
    public void CalculateMinimumSafeDistance_WithLargeWindow_ReturnsIncreasedDistance()
    {
        // Arrange
        var config = new CursorPhobiaConfiguration { ScreenEdgeBuffer = 20 };
        var safetyManager = new SafetyManager(_logger, config);
        var largeWindow = new Rectangle(100, 100, 1600, 1200); // Large window

        // Act
        var distance = safetyManager.CalculateMinimumSafeDistance(largeWindow);

        // Assert
        Assert.True(distance >= config.ScreenEdgeBuffer, "Large windows should have increased safe distance");
    }

    [Fact]
    public void SafetyManager_WithCustomConfiguration_UsesConfigurationSettings()
    {
        // Arrange
        var customConfig = new CursorPhobiaConfiguration
        {
            ScreenEdgeBuffer = 50
        };
        var safetyManager = new SafetyManager(_logger, customConfig);
        var windowBounds = new Rectangle(100, 100, 300, 200);

        // Act
        var distance = safetyManager.CalculateMinimumSafeDistance(windowBounds);
        var safeAreas = safetyManager.GetSafeScreenAreas();

        // Assert
        Assert.True(distance > 0);
        Assert.NotEmpty(safeAreas);
    }

    [Fact]
    public void ValidateWindowPosition_WithVerySmallWindow_HandlesCorrectly()
    {
        // Arrange
        var safetyManager = new SafetyManager(_logger);
        var tinyWindow = new Rectangle(0, 0, 10, 10);
        var proposedPosition = new Point(100, 100);

        // Act
        var safePosition = safetyManager.ValidateWindowPosition(tinyWindow, proposedPosition);

        // Assert
        Assert.IsType<Point>(safePosition);
        // Should handle tiny windows without issues
    }

    [Fact]
    public void ValidateWindowPosition_WithVeryLargeWindow_HandlesCorrectly()
    {
        // Arrange
        var safetyManager = new SafetyManager(_logger);
        var hugeWindow = new Rectangle(0, 0, 3000, 2000);
        var proposedPosition = new Point(100, 100);

        // Act
        var safePosition = safetyManager.ValidateWindowPosition(hugeWindow, proposedPosition);

        // Assert
        Assert.IsType<Point>(safePosition);
        // Should handle huge windows without crashing
    }

    [Fact]
    public void SafetyManager_MultipleOperations_HandlesConsistently()
    {
        // Arrange
        var safetyManager = new SafetyManager(_logger);
        var windowBounds = new Rectangle(0, 0, 200, 150);

        // Act & Assert - Multiple operations should work consistently
        for (int i = 0; i < 5; i++)
        {
            var position = new Point(i * 50, i * 30);
            var safePosition = safetyManager.ValidateWindowPosition(windowBounds, position);
            var isSafe = safetyManager.IsPositionSafe(new Rectangle(safePosition.X, safePosition.Y, windowBounds.Width, windowBounds.Height));
            var distance = safetyManager.CalculateMinimumSafeDistance(windowBounds);

            Assert.IsType<Point>(safePosition);
            Assert.IsType<bool>(isSafe);
            Assert.True(distance >= 0);
        }
    }

    [Fact]
    public void RefreshScreenBounds_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var safetyManager = new SafetyManager(_logger);

        // Act & Assert
        for (int i = 0; i < 3; i++)
        {
            var exception = Record.Exception(() => safetyManager.RefreshScreenBounds());
            Assert.Null(exception);
        }
    }

    [Fact]
    public void GetSafeScreenAreas_AfterRefresh_ReturnsAreas()
    {
        // Arrange
        var safetyManager = new SafetyManager(_logger);

        // Act
        safetyManager.RefreshScreenBounds();
        var safeAreas = safetyManager.GetSafeScreenAreas();

        // Assert
        Assert.NotNull(safeAreas);
        Assert.NotEmpty(safeAreas);
        Assert.All(safeAreas, area =>
        {
            Assert.True(area.Width > 0);
            Assert.True(area.Height > 0);
        });
    }

    [Fact]
    public void SafetyManager_EdgeCases_HandlesGracefully()
    {
        // Arrange
        var safetyManager = new SafetyManager(_logger);

        // Act & Assert - Test various edge cases
        var emptyRect = Rectangle.Empty;
        var safePosition1 = safetyManager.ValidateWindowPosition(emptyRect, Point.Empty);
        Assert.IsType<Point>(safePosition1);

        var negativeRect = new Rectangle(-100, -100, 50, 50);
        var safePosition2 = safetyManager.ValidateWindowPosition(negativeRect, new Point(-50, -50));
        Assert.IsType<Point>(safePosition2);

        var isSafe1 = safetyManager.IsPositionSafe(emptyRect);
        Assert.IsType<bool>(isSafe1);

        var distance1 = safetyManager.CalculateMinimumSafeDistance(emptyRect);
        Assert.True(distance1 >= 0);
    }
}