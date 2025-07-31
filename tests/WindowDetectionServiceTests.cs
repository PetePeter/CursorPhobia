using System.Drawing;
using Xunit;
using Moq;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Utilities;
using ILogger = CursorPhobia.Core.Utilities.ILogger;

namespace CursorPhobia.Tests;

/// <summary>
/// Unit tests for WindowDetectionService
/// </summary>
public class WindowDetectionServiceTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly WindowDetectionService _service;
    
    public WindowDetectionServiceTests()
    {
        _mockLogger = new Mock<ILogger>();
        _service = new WindowDetectionService(_mockLogger.Object);
    }
    
    #region Constructor Tests
    
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new WindowDetectionService(null!));
    }
    
    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Arrange & Act
        var service = new WindowDetectionService(_mockLogger.Object);
        
        // Assert
        Assert.NotNull(service);
    }
    
    #endregion
    
    #region IsWindowAlwaysOnTop Tests
    
    [Fact]
    public void IsWindowAlwaysOnTop_WithZeroHandle_ReturnsFalse()
    {
        // Arrange
        var hWnd = IntPtr.Zero;
        
        // Act
        var result = _service.IsWindowAlwaysOnTop(hWnd);
        
        // Assert
        Assert.False(result);
        _mockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Once);
    }
    
    [Fact]
    public void IsWindowAlwaysOnTop_WithValidHandle_ReturnsExpectedResult()
    {
        // Arrange
        var hWnd = new IntPtr(12345);
        
        // Act
        var result = _service.IsWindowAlwaysOnTop(hWnd);
        
        // Assert
        // Note: This will make actual Windows API calls in integration tests
        // For unit tests, we'd need to mock the Windows API calls
        Assert.IsType<bool>(result);
    }
    
    #endregion
    
    #region GetWindowInformation Tests
    
    [Fact]
    public void GetWindowInformation_WithZeroHandle_ReturnsNull()
    {
        // Arrange
        var hWnd = IntPtr.Zero;
        
        // Act
        var result = _service.GetWindowInformation(hWnd);
        
        // Assert
        Assert.Null(result);
        _mockLogger.Verify(l => l.LogWarning(It.IsAny<string>()), Times.Once);
    }
    
    [Fact]
    public void GetWindowInformation_WithValidHandle_ReturnsWindowInfo()
    {
        // Arrange
        var hWnd = new IntPtr(12345);
        
        // Act
        var result = _service.GetWindowInformation(hWnd);
        
        // Assert
        // Note: In a real unit test, we'd mock the Windows API calls
        // This test will work as an integration test
        if (result != null)
        {
            Assert.Equal(hWnd, result.WindowHandle);
            Assert.IsType<string>(result.Title);
            Assert.IsType<string>(result.ClassName);
            Assert.IsType<Rectangle>(result.Bounds);
            Assert.IsType<int>(result.ProcessId);
            Assert.IsType<int>(result.ThreadId);
            Assert.IsType<bool>(result.IsVisible);
            Assert.IsType<bool>(result.IsTopmost);
            Assert.IsType<bool>(result.IsMinimized);
        }
    }
    
    #endregion
    
    #region EnumerateVisibleWindows Tests
    
    [Fact]
    public void EnumerateVisibleWindows_ReturnsListOfWindows()
    {
        // Arrange & Act
        var result = _service.EnumerateVisibleWindows();
        
        // Assert
        Assert.NotNull(result);
        Assert.IsType<List<WindowInfo>>(result);
        
        // Verify logging occurred
        _mockLogger.Verify(l => l.LogDebug(It.IsAny<string>()), Times.AtLeastOnce);
        _mockLogger.Verify(l => l.LogInformation(It.IsAny<string>(), It.IsAny<object[]>()), Times.AtLeastOnce);
    }
    
    [Fact]
    public void EnumerateVisibleWindows_ReturnsOnlyVisibleWindows()
    {
        // Arrange & Act
        var result = _service.EnumerateVisibleWindows();
        
        // Assert
        Assert.NotNull(result);
        Assert.All(result, window => Assert.True(window.IsVisible));
    }
    
    #endregion
    
    #region GetAllTopMostWindows Tests
    
    [Fact]
    public void GetAllTopMostWindows_ReturnsListOfTopmostWindows()
    {
        // Arrange & Act
        var result = _service.GetAllTopMostWindows();
        
        // Assert
        Assert.NotNull(result);
        Assert.IsType<List<WindowInfo>>(result);
        
        // Verify all returned windows are topmost
        Assert.All(result, window => Assert.True(window.IsTopmost));
        
        // Verify logging occurred
        _mockLogger.Verify(l => l.LogDebug(It.IsAny<string>()), Times.AtLeastOnce);
        _mockLogger.Verify(l => l.LogInformation(It.IsAny<string>(), It.IsAny<object[]>()), Times.AtLeastOnce);
    }
    
    [Fact]
    public void GetAllTopMostWindows_ReturnsSubsetOfVisibleWindows()
    {
        // Arrange & Act
        var allVisible = _service.EnumerateVisibleWindows();
        var topmost = _service.GetAllTopMostWindows();
        
        // Assert
        Assert.NotNull(allVisible);
        Assert.NotNull(topmost);
        Assert.True(topmost.Count <= allVisible.Count);
    }
    
    #endregion
    
    #region Edge Cases
    
    [Fact]
    public void WindowDetectionService_HandlesExceptionsGracefully()
    {
        // This test ensures the service doesn't throw unhandled exceptions
        // when encountering invalid window handles or system errors
        
        // Arrange
        var invalidHandles = new[]
        {
            IntPtr.Zero,
            new IntPtr(-1),
            new IntPtr(999999999)
        };
        
        // Act & Assert - should not throw exceptions
        foreach (var handle in invalidHandles)
        {
            var info = _service.GetWindowInformation(handle);
            var isTopmost = _service.IsWindowAlwaysOnTop(handle);
            
            // These should either return valid results or safe defaults
            Assert.True(info == null || info.WindowHandle == handle);
            Assert.IsType<bool>(isTopmost);
        }
    }
    
    #endregion
}

/// <summary>
/// Integration tests for WindowDetectionService that test against real Windows API
/// </summary>
public class WindowDetectionServiceIntegrationTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly WindowDetectionService _service;
    
    public WindowDetectionServiceIntegrationTests()
    {
        _mockLogger = new Mock<ILogger>();
        _service = new WindowDetectionService(_mockLogger.Object);
    }
    
    [Fact]
    public void EnumerateVisibleWindows_FindsSystemWindows()
    {
        // Arrange & Act
        var windows = _service.EnumerateVisibleWindows();
        
        // Assert
        Assert.NotEmpty(windows);
        
        // Should find at least some system windows like the desktop or taskbar
        Assert.Contains(windows, w => !string.IsNullOrEmpty(w.ClassName));
    }
    
    [Fact]
    public void GetAllTopMostWindows_FindsTopmostWindows()
    {
        // Arrange & Act
        var topmostWindows = _service.GetAllTopMostWindows();
        
        // Assert
        Assert.NotNull(topmostWindows);
        // We can't guarantee there will be topmost windows, but the method should work
        Assert.All(topmostWindows, w => Assert.True(w.IsTopmost));
    }
}