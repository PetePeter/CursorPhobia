using System.Drawing;
using CursorPhobia.Core.Models;
using Xunit;
using static CursorPhobia.Core.WindowsAPI.WindowsStructures;

namespace CursorPhobia.Tests;

/// <summary>
/// Unit tests for DpiInfo class
/// </summary>
public class DpiInfoTests
{
    #region Constructor Tests
    
    [Fact]
    public void Constructor_Default_CreatesWithDefaultDpi()
    {
        // Act
        var dpiInfo = new DpiInfo();
        
        // Assert
        Assert.Equal((uint)DEFAULT_DPI, dpiInfo.DpiX);
        Assert.Equal((uint)DEFAULT_DPI, dpiInfo.DpiY);
        Assert.Equal(1.0, dpiInfo.ScaleFactorX, 3);
        Assert.Equal(1.0, dpiInfo.ScaleFactorY, 3);
        Assert.Equal(1.0, dpiInfo.ScaleFactor, 3);
        Assert.False(dpiInfo.IsHighDpi);
    }
    
    [Fact]
    public void Constructor_WithParameters_CreatesWithSpecifiedDpi()
    {
        // Arrange
        uint dpiX = 144;
        uint dpiY = 120;
        
        // Act
        var dpiInfo = new DpiInfo(dpiX, dpiY);
        
        // Assert
        Assert.Equal(dpiX, dpiInfo.DpiX);
        Assert.Equal(dpiY, dpiInfo.DpiY);
        Assert.Equal(1.5, dpiInfo.ScaleFactorX, 3);
        Assert.Equal(1.25, dpiInfo.ScaleFactorY, 3);
        Assert.Equal(1.375, dpiInfo.ScaleFactor, 3);
        Assert.True(dpiInfo.IsHighDpi);
    }
    
    #endregion
    
    #region Property Tests
    
    [Theory]
    [InlineData(96u, 96u, 1.0, 1.0, 1.0, false)]
    [InlineData(120u, 120u, 1.25, 1.25, 1.25, true)]
    [InlineData(144u, 144u, 1.5, 1.5, 1.5, true)]
    [InlineData(192u, 192u, 2.0, 2.0, 2.0, true)]
    [InlineData(144u, 120u, 1.5, 1.25, 1.375, true)]
    public void Properties_WithVariousDpiValues_CalculateCorrectly(uint dpiX, uint dpiY, 
        double expectedScaleX, double expectedScaleY, double expectedScale, bool expectedHighDpi)
    {
        // Act
        var dpiInfo = new DpiInfo(dpiX, dpiY);
        
        // Assert
        Assert.Equal(expectedScaleX, dpiInfo.ScaleFactorX, 3);
        Assert.Equal(expectedScaleY, dpiInfo.ScaleFactorY, 3);
        Assert.Equal(expectedScale, dpiInfo.ScaleFactor, 3);
        Assert.Equal(expectedHighDpi, dpiInfo.IsHighDpi);
    }
    
    #endregion
    
    #region Coordinate Conversion Tests
    
    [Fact]
    public void LogicalToPhysical_WithDefaultDpi_ReturnsUnchanged()
    {
        // Arrange
        var dpiInfo = new DpiInfo();
        var logicalPoint = new Point(100, 200);
        
        // Act
        var physicalPoint = dpiInfo.LogicalToPhysical(logicalPoint);
        
        // Assert
        Assert.Equal(logicalPoint, physicalPoint);
    }
    
    [Fact]
    public void LogicalToPhysical_WithHighDpi_ScalesCorrectly()
    {
        // Arrange
        var dpiInfo = new DpiInfo(144, 144); // 1.5x scale
        var logicalPoint = new Point(100, 200);
        
        // Act
        var physicalPoint = dpiInfo.LogicalToPhysical(logicalPoint);
        
        // Assert
        Assert.Equal(150, physicalPoint.X);
        Assert.Equal(300, physicalPoint.Y);
    }
    
    [Fact]
    public void PhysicalToLogical_WithHighDpi_ScalesCorrectly()
    {
        // Arrange
        var dpiInfo = new DpiInfo(144, 144); // 1.5x scale
        var physicalPoint = new Point(150, 300);
        
        // Act
        var logicalPoint = dpiInfo.PhysicalToLogical(physicalPoint);
        
        // Assert
        Assert.Equal(100, logicalPoint.X);
        Assert.Equal(200, logicalPoint.Y);
    }
    
    [Fact]
    public void LogicalToPhysical_Rectangle_ScalesCorrectly()
    {
        // Arrange
        var dpiInfo = new DpiInfo(192, 192); // 2.0x scale
        var logicalRect = new Rectangle(50, 100, 200, 150);
        
        // Act
        var physicalRect = dpiInfo.LogicalToPhysical(logicalRect);
        
        // Assert
        Assert.Equal(100, physicalRect.X);
        Assert.Equal(200, physicalRect.Y);
        Assert.Equal(400, physicalRect.Width);
        Assert.Equal(300, physicalRect.Height);
    }
    
    [Fact]
    public void PhysicalToLogical_Rectangle_ScalesCorrectly()
    {
        // Arrange
        var dpiInfo = new DpiInfo(192, 192); // 2.0x scale
        var physicalRect = new Rectangle(100, 200, 400, 300);
        
        // Act
        var logicalRect = dpiInfo.PhysicalToLogical(physicalRect);
        
        // Assert
        Assert.Equal(50, logicalRect.X);
        Assert.Equal(100, logicalRect.Y);
        Assert.Equal(200, logicalRect.Width);
        Assert.Equal(150, logicalRect.Height);
    }
    
    [Fact]
    public void ScaleDistance_WithHighDpi_ScalesCorrectly()
    {
        // Arrange
        var dpiInfo = new DpiInfo(120, 120); // 1.25x scale
        var logicalDistance = 100;
        
        // Act
        var physicalDistance = dpiInfo.ScaleDistance(logicalDistance);
        
        // Assert
        Assert.Equal(125, physicalDistance);
    }
    
    [Fact]
    public void UnscaleDistance_WithHighDpi_ScalesCorrectly()
    {
        // Arrange
        var dpiInfo = new DpiInfo(120, 120); // 1.25x scale
        var physicalDistance = 125;
        
        // Act
        var logicalDistance = dpiInfo.UnscaleDistance(physicalDistance);
        
        // Assert
        Assert.Equal(100, logicalDistance);
    }
    
    [Fact]
    public void LogicalToPhysical_ThenPhysicalToLogical_RoundTrip()
    {
        // Arrange
        var dpiInfo = new DpiInfo(144, 144);
        var originalPoint = new Point(100, 200);
        
        // Act
        var physicalPoint = dpiInfo.LogicalToPhysical(originalPoint);
        var backToLogical = dpiInfo.PhysicalToLogical(physicalPoint);
        
        // Assert
        Assert.Equal(originalPoint.X, backToLogical.X);
        Assert.Equal(originalPoint.Y, backToLogical.Y);
    }
    
    #endregion
    
    #region Mixed DPI Tests
    
    [Fact]
    public void LogicalToPhysical_WithMixedDpi_ScalesIndependently()
    {
        // Arrange - Different X and Y DPI values
        var dpiInfo = new DpiInfo(144, 120); // 1.5x X, 1.25x Y
        var logicalPoint = new Point(100, 200);
        
        // Act
        var physicalPoint = dpiInfo.LogicalToPhysical(logicalPoint);
        
        // Assert
        Assert.Equal(150, physicalPoint.X); // 100 * 1.5
        Assert.Equal(250, physicalPoint.Y); // 200 * 1.25
    }
    
    [Fact]
    public void Rectangle_WithMixedDpi_ScalesIndependently()
    {
        // Arrange
        var dpiInfo = new DpiInfo(192, 144); // 2.0x X, 1.5x Y
        var logicalRect = new Rectangle(50, 100, 200, 150);
        
        // Act
        var physicalRect = dpiInfo.LogicalToPhysical(logicalRect);
        
        // Assert
        Assert.Equal(100, physicalRect.X);  // 50 * 2.0
        Assert.Equal(150, physicalRect.Y);  // 100 * 1.5
        Assert.Equal(400, physicalRect.Width);  // 200 * 2.0
        Assert.Equal(225, physicalRect.Height); // 150 * 1.5
    }
    
    #endregion
    
    #region Precision Tests
    
    [Fact]
    public void CoordinateConversion_WithFractionalScaling_HandlesRounding()
    {
        // Arrange - DPI that results in fractional scaling
        var dpiInfo = new DpiInfo(100, 100); // ~1.04x scale
        var logicalPoint = new Point(96, 192);
        
        // Act
        var physicalPoint = dpiInfo.LogicalToPhysical(logicalPoint);
        var backToLogical = dpiInfo.PhysicalToLogical(physicalPoint);
        
        // Assert - Should handle rounding gracefully
        Assert.True(Math.Abs(logicalPoint.X - backToLogical.X) <= 1);
        Assert.True(Math.Abs(logicalPoint.Y - backToLogical.Y) <= 1);
    }
    
    [Theory]
    [InlineData(1, 1, 1)]    // Very small values
    [InlineData(10000, 10000, 10000)] // Large values
    [InlineData(0, 0, 0)]    // Zero values
    public void CoordinateConversion_WithEdgeCaseValues_HandlesCorrectly(int x, int y, int distance)
    {
        // Arrange
        var dpiInfo = new DpiInfo(144, 144);
        var point = new Point(x, y);
        
        // Act & Assert - Should not throw
        var physicalPoint = dpiInfo.LogicalToPhysical(point);
        var logicalPoint = dpiInfo.PhysicalToLogical(physicalPoint);
        var scaledDistance = dpiInfo.ScaleDistance(distance);
        var unscaledDistance = dpiInfo.UnscaleDistance(scaledDistance);
        
        // Verify round-trip accuracy within reasonable bounds
        Assert.True(Math.Abs(point.X - logicalPoint.X) <= 1);
        Assert.True(Math.Abs(point.Y - logicalPoint.Y) <= 1);
        Assert.True(Math.Abs(distance - unscaledDistance) <= 1);
    }
    
    #endregion
    
    #region Equality and ToString Tests
    
    [Fact]
    public void Equals_WithSameDpiValues_ReturnsTrue()
    {
        // Arrange
        var dpi1 = new DpiInfo(144, 120);
        var dpi2 = new DpiInfo(144, 120);
        
        // Act & Assert
        Assert.True(dpi1.Equals(dpi2));
        Assert.Equal(dpi1.GetHashCode(), dpi2.GetHashCode());
    }
    
    [Fact]
    public void Equals_WithDifferentDpiValues_ReturnsFalse()
    {
        // Arrange
        var dpi1 = new DpiInfo(144, 120);
        var dpi2 = new DpiInfo(120, 144);
        
        // Act & Assert
        Assert.False(dpi1.Equals(dpi2));
    }
    
    [Fact]
    public void Equals_WithNullOrDifferentType_ReturnsFalse()
    {
        // Arrange
        var dpiInfo = new DpiInfo(144, 120);
        
        // Act & Assert
        Assert.False(dpiInfo.Equals(null));
        Assert.False(dpiInfo.Equals("not a DpiInfo"));
    }
    
    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        // Arrange
        var dpiInfo = new DpiInfo(144, 120);
        
        // Act
        var result = dpiInfo.ToString();
        
        // Assert
        Assert.Contains("144", result);
        Assert.Contains("120", result);
        Assert.Contains("1.38", result); // Scale factor should be ~1.375
    }
    
    #endregion
}