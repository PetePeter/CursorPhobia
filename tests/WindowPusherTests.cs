using System.Drawing;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;
using Xunit;

namespace CursorPhobia.Tests;

/// <summary>
/// Unit tests for WindowPusher service and animation functionality
/// </summary>
public class WindowPusherTests
{
    #region Constructor Tests
    
    [Fact]
    public void Constructor_WithValidDependencies_CreatesInstance()
    {
        // Arrange
        var logger = new TestLogger();
        var windowService = new MockWindowManipulationService();
        var safetyManager = new MockSafetyManager();
        var proximityDetector = new MockProximityDetector();
        
        // Act
        using var pusher = new WindowPusher(logger, windowService, safetyManager, proximityDetector);
        
        // Assert
        Assert.NotNull(pusher);
    }
    
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var windowService = new MockWindowManipulationService();
        var safetyManager = new MockSafetyManager();
        var proximityDetector = new MockProximityDetector();
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new WindowPusher(null!, windowService, safetyManager, proximityDetector));
    }
    
    [Fact]
    public void Constructor_WithNullWindowService_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = new TestLogger();
        var safetyManager = new MockSafetyManager();
        var proximityDetector = new MockProximityDetector();
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new WindowPusher(logger, null!, safetyManager, proximityDetector));
    }
    
    [Fact]
    public void Constructor_WithNullSafetyManager_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = new TestLogger();
        var windowService = new MockWindowManipulationService();
        var proximityDetector = new MockProximityDetector();
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new WindowPusher(logger, windowService, null!, proximityDetector));
    }
    
    [Fact]
    public void Constructor_WithNullProximityDetector_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = new TestLogger();
        var windowService = new MockWindowManipulationService();
        var safetyManager = new MockSafetyManager();
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new WindowPusher(logger, windowService, safetyManager, null!));
    }
    
    [Fact]
    public void Constructor_WithInvalidConfiguration_ThrowsArgumentException()
    {
        // Arrange
        var logger = new TestLogger();
        var windowService = new MockWindowManipulationService();
        var safetyManager = new MockSafetyManager();
        var proximityDetector = new MockProximityDetector();
        var invalidConfig = new CursorPhobiaConfiguration { ProximityThreshold = -1 };
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            new WindowPusher(logger, windowService, safetyManager, proximityDetector, invalidConfig));
    }
    
    #endregion
    
    #region PushWindowAsync Tests
    
    [Fact]
    public async Task PushWindowAsync_WithZeroHandle_ReturnsFalse()
    {
        // Arrange
        using var pusher = CreateWindowPusher();
        
        // Act
        var result = await pusher.PushWindowAsync(IntPtr.Zero, new Point(100, 100), 50);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task PushWindowAsync_WithInvalidPushDistance_ReturnsFalse()
    {
        // Arrange
        using var pusher = CreateWindowPusher();
        var handle = new IntPtr(12345);
        
        // Act
        var result = await pusher.PushWindowAsync(handle, new Point(100, 100), 0);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task PushWindowAsync_WithValidParameters_ReturnsTrue()
    {
        // Arrange
        var windowService = new MockWindowManipulationService();
        var safetyManager = new MockSafetyManager();
        var proximityDetector = new MockProximityDetector();
        using var pusher = CreateWindowPusher(windowService, safetyManager, proximityDetector);
        
        var handle = new IntPtr(12345);
        var windowBounds = new Rectangle(100, 100, 200, 150);
        
        windowService.SetWindowBounds(handle, windowBounds);
        proximityDetector.SetPushVector(new Point(50, 0)); // Push right
        safetyManager.SetValidatedPosition(new Point(150, 100));
        
        // Act
        var result = await pusher.PushWindowAsync(handle, new Point(50, 125), 50);
        
        // Assert
        Assert.True(result);
        Assert.True(windowService.MoveWindowCalled);
    }
    
    [Fact]
    public async Task PushWindowAsync_WithAnimationsDisabled_MovesImmediately()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        config.EnableAnimations = false;
        
        var windowService = new MockWindowManipulationService();
        var safetyManager = new MockSafetyManager();
        var proximityDetector = new MockProximityDetector();
        using var pusher = CreateWindowPusher(windowService, safetyManager, proximityDetector, config);
        
        var handle = new IntPtr(12345);
        var windowBounds = new Rectangle(100, 100, 200, 150);
        
        windowService.SetWindowBounds(handle, windowBounds);
        proximityDetector.SetPushVector(new Point(50, 0));
        safetyManager.SetValidatedPosition(new Point(150, 100));
        
        // Act
        var result = await pusher.PushWindowAsync(handle, new Point(50, 125), 50);
        
        // Assert
        Assert.True(result);
        Assert.Equal(1, windowService.MoveWindowCallCount); // Should move only once, immediately
    }
    
    #endregion
    
    #region PushWindowToPositionAsync Tests
    
    [Fact]
    public async Task PushWindowToPositionAsync_WithZeroHandle_ReturnsFalse()
    {
        // Arrange
        using var pusher = CreateWindowPusher();
        
        // Act
        var result = await pusher.PushWindowToPositionAsync(IntPtr.Zero, new Point(200, 200));
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task PushWindowToPositionAsync_WhenAlreadyAtTarget_ReturnsTrue()
    {
        // Arrange
        var windowService = new MockWindowManipulationService();
        using var pusher = CreateWindowPusher(windowService);
        
        var handle = new IntPtr(12345);
        var currentPosition = new Point(150, 100);
        windowService.SetWindowBounds(handle, new Rectangle(currentPosition.X, currentPosition.Y, 200, 150));
        
        // Act
        var result = await pusher.PushWindowToPositionAsync(handle, currentPosition);
        
        // Assert
        Assert.True(result);
        Assert.False(windowService.MoveWindowCalled); // Should not move if already at target
    }
    
    [Fact]
    public async Task PushWindowToPositionAsync_WithValidParameters_MovesWindow()
    {
        // Arrange
        var windowService = new MockWindowManipulationService();
        using var pusher = CreateWindowPusher(windowService);
        
        var handle = new IntPtr(12345);
        var currentBounds = new Rectangle(100, 100, 200, 150);
        var targetPosition = new Point(200, 200);
        
        windowService.SetWindowBounds(handle, currentBounds);
        
        // Act
        var result = await pusher.PushWindowToPositionAsync(handle, targetPosition);
        
        // Assert
        Assert.True(result);
        Assert.True(windowService.MoveWindowCalled);
    }
    
    #endregion
    
    #region Animation State Tests
    
    [Fact]
    public void IsWindowAnimating_WithNonAnimatingWindow_ReturnsFalse()
    {
        // Arrange
        using var pusher = CreateWindowPusher();
        var handle = new IntPtr(12345);
        
        // Act
        var result = pusher.IsWindowAnimating(handle);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task IsWindowAnimating_DuringAnimation_ReturnsTrue()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        config.AnimationDurationMs = 100; // Short animation for testing
        
        var windowService = new MockWindowManipulationService();
        using var pusher = CreateWindowPusher(windowService, config: config);
        
        var handle = new IntPtr(12345);
        windowService.SetWindowBounds(handle, new Rectangle(100, 100, 200, 150));
        
        // Act
        var pushTask = pusher.PushWindowToPositionAsync(handle, new Point(200, 200));
        
        // Check if animating shortly after starting
        await Task.Delay(10); // Small delay to let animation start
        var isAnimating = pusher.IsWindowAnimating(handle);
        
        await pushTask; // Wait for completion
        
        // Assert
        Assert.True(isAnimating);
    }
    
    [Fact]
    public void CancelWindowAnimation_WithNonAnimatingWindow_DoesNotThrow()
    {
        // Arrange
        using var pusher = CreateWindowPusher();
        var handle = new IntPtr(12345);
        
        // Act & Assert
        pusher.CancelWindowAnimation(handle); // Should not throw
    }
    
    [Fact]
    public void CancelAllAnimations_WithNoAnimations_DoesNotThrow()
    {
        // Arrange
        using var pusher = CreateWindowPusher();
        
        // Act & Assert
        pusher.CancelAllAnimations(); // Should not throw
    }
    
    #endregion
    
    #region Animation Configuration Tests
    
    [Theory]
    [InlineData(AnimationEasing.Linear)]
    [InlineData(AnimationEasing.EaseIn)]
    [InlineData(AnimationEasing.EaseOut)]
    [InlineData(AnimationEasing.EaseInOut)]
    public async Task PushWindowAsync_WithDifferentEasingTypes_CompletesSuccessfully(AnimationEasing easing)
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        config.AnimationEasing = easing;
        config.AnimationDurationMs = 50; // Short for testing
        
        var windowService = new MockWindowManipulationService();
        var safetyManager = new MockSafetyManager();
        var proximityDetector = new MockProximityDetector();
        using var pusher = CreateWindowPusher(windowService, safetyManager, proximityDetector, config);
        
        var handle = new IntPtr(12345);
        windowService.SetWindowBounds(handle, new Rectangle(100, 100, 200, 150));
        proximityDetector.SetPushVector(new Point(50, 0));
        safetyManager.SetValidatedPosition(new Point(150, 100));
        
        // Act
        var result = await pusher.PushWindowAsync(handle, new Point(50, 125), 50);
        
        // Assert
        Assert.True(result);
    }
    
    [Theory]
    [InlineData(0)]    // Disabled animations
    [InlineData(50)]   // Fast animation
    [InlineData(200)]  // Default animation
    [InlineData(500)]  // Slow animation
    public async Task PushWindowAsync_WithDifferentDurations_BehavesCorrectly(int durationMs)
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreateDefault();
        config.AnimationDurationMs = durationMs;
        config.EnableAnimations = durationMs > 0;
        
        var windowService = new MockWindowManipulationService();
        var safetyManager = new MockSafetyManager();
        var proximityDetector = new MockProximityDetector();
        using var pusher = CreateWindowPusher(windowService, safetyManager, proximityDetector, config);
        
        var handle = new IntPtr(12345);
        windowService.SetWindowBounds(handle, new Rectangle(100, 100, 200, 150));
        proximityDetector.SetPushVector(new Point(50, 0));
        safetyManager.SetValidatedPosition(new Point(150, 100));
        
        // Act
        var result = await pusher.PushWindowAsync(handle, new Point(50, 125), 50);
        
        // Assert
        Assert.True(result);
        
        if (durationMs <= 0)
        {
            // Should move immediately without animation
            Assert.Equal(1, windowService.MoveWindowCallCount);
        }
        else
        {
            // Should have multiple moves for animation
            Assert.True(windowService.MoveWindowCallCount >= 1);
        }
    }
    
    #endregion
    
    #region Error Handling Tests
    
    [Fact]
    public async Task PushWindowAsync_WhenWindowServiceFails_ReturnsFalse()
    {
        // Arrange
        var windowService = new MockWindowManipulationService();
        windowService.SetMoveWindowResult(false); // Force failure
        
        var safetyManager = new MockSafetyManager();
        var proximityDetector = new MockProximityDetector();
        using var pusher = CreateWindowPusher(windowService, safetyManager, proximityDetector);
        
        var handle = new IntPtr(12345);
        windowService.SetWindowBounds(handle, new Rectangle(100, 100, 200, 150));
        proximityDetector.SetPushVector(new Point(50, 0));
        safetyManager.SetValidatedPosition(new Point(150, 100));
        
        // Act
        var result = await pusher.PushWindowAsync(handle, new Point(50, 125), 50);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact] 
    public async Task PushWindowAsync_WhenCannotGetWindowBounds_ReturnsFalse()
    {
        // Arrange
        var windowService = new MockWindowManipulationService();
        // Don't set window bounds, so GetWindowBounds returns empty
        
        using var pusher = CreateWindowPusher(windowService);
        var handle = new IntPtr(12345);
        
        // Act
        var result = await pusher.PushWindowAsync(handle, new Point(50, 125), 50);
        
        // Assert
        Assert.False(result);
    }
    
    [Fact]
    public async Task PushWindowAsync_WhenProximityDetectorFails_ReturnsFalse()
    {
        // Arrange
        var windowService = new MockWindowManipulationService();
        var proximityDetector = new MockProximityDetector();
        proximityDetector.SetPushVector(Point.Empty); // Return empty vector to simulate failure
        
        using var pusher = CreateWindowPusher(windowService, proximityDetector: proximityDetector);
        
        var handle = new IntPtr(12345);
        windowService.SetWindowBounds(handle, new Rectangle(100, 100, 200, 150));
        
        // Act
        var result = await pusher.PushWindowAsync(handle, new Point(50, 125), 50);
        
        // Assert
        Assert.False(result);
    }
    
    #endregion
    
    #region Dispose Tests
    
    [Fact]
    public void Dispose_WithActiveAnimations_CancelsAnimations()
    {
        // Arrange
        var windowService = new MockWindowManipulationService();
        var pusher = CreateWindowPusher(windowService);
        
        var handle = new IntPtr(12345);
        windowService.SetWindowBounds(handle, new Rectangle(100, 100, 200, 150));
        
        // Start an animation
        _ = pusher.PushWindowToPositionAsync(handle, new Point(200, 200));
        
        // Act
        pusher.Dispose();
        
        // Assert - should not throw and should clean up properly
        Assert.False(pusher.IsWindowAnimating(handle));
    }
    
    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var pusher = CreateWindowPusher();
        
        // Act & Assert
        pusher.Dispose();
        pusher.Dispose(); // Should not throw
    }
    
    #endregion
    
    #region Helper Methods
    
    private static WindowPusher CreateWindowPusher(
        IWindowManipulationService? windowService = null,
        ISafetyManager? safetyManager = null,
        IProximityDetector? proximityDetector = null,
        CursorPhobiaConfiguration? config = null)
    {
        return new WindowPusher(
            new TestLogger(),
            windowService ?? new MockWindowManipulationService(),
            safetyManager ?? new MockSafetyManager(),
            proximityDetector ?? new MockProximityDetector(),
            config
        );
    }
    
    #endregion
}

#region Mock Services

/// <summary>
/// Mock implementation of IWindowManipulationService for testing
/// </summary>
public class MockWindowManipulationService : IWindowManipulationService
{
    private readonly Dictionary<IntPtr, Rectangle> _windowBounds = new();
    private bool _moveWindowResult = true;
    
    public bool MoveWindowCalled { get; private set; }
    public int MoveWindowCallCount { get; private set; }
    public Point LastMovePosition { get; private set; }
    
    public void SetWindowBounds(IntPtr handle, Rectangle bounds)
    {
        _windowBounds[handle] = bounds;
    }
    
    public void SetMoveWindowResult(bool result)
    {
        _moveWindowResult = result;
    }
    
    public bool MoveWindow(IntPtr hWnd, int x, int y)
    {
        MoveWindowCalled = true;
        MoveWindowCallCount++;
        LastMovePosition = new Point(x, y);
        
        // Update stored bounds if successful
        if (_moveWindowResult && _windowBounds.ContainsKey(hWnd))
        {
            var currentBounds = _windowBounds[hWnd];
            _windowBounds[hWnd] = new Rectangle(x, y, currentBounds.Width, currentBounds.Height);
        }
        
        return _moveWindowResult;
    }
    
    public Rectangle GetWindowBounds(IntPtr hWnd)
    {
        return _windowBounds.TryGetValue(hWnd, out var bounds) ? bounds : Rectangle.Empty;
    }
    
    public bool IsWindowVisible(IntPtr hWnd)
    {
        return _windowBounds.ContainsKey(hWnd);
    }
}

/// <summary>
/// Mock implementation of ISafetyManager for testing
/// </summary>
public class MockSafetyManager : ISafetyManager
{
    private Point _validatedPosition = Point.Empty;
    
    public void SetValidatedPosition(Point position)
    {
        _validatedPosition = position;
    }
    
    public Point ValidateWindowPosition(Rectangle windowBounds, Point proposedPosition)
    {
        return _validatedPosition.IsEmpty ? proposedPosition : _validatedPosition;
    }
    
    public bool IsPositionSafe(Rectangle windowBounds)
    {
        return true; // Default to safe for testing
    }
    
    public List<Rectangle> GetSafeScreenAreas()
    {
        return new List<Rectangle> { new Rectangle(0, 0, 1920, 1080) };
    }
    
    public void RefreshScreenBounds()
    {
        // No-op for testing
    }
    
    public int CalculateMinimumSafeDistance(Rectangle windowBounds)
    {
        return 20; // Default safe distance
    }
}

/// <summary>
/// Mock implementation of IProximityDetector for testing
/// </summary>
public class MockProximityDetector : IProximityDetector
{
    private Point _pushVector = Point.Empty;
    
    public void SetPushVector(Point vector)
    {
        _pushVector = vector;
    }
    
    public double CalculateProximity(Point cursorPosition, Rectangle windowBounds)
    {
        return 50.0; // Default proximity value
    }
    
    public bool IsWithinProximity(Point cursorPosition, Rectangle windowBounds, int proximityThreshold)
    {
        return true; // Default to within proximity for testing
    }
    
    public Point CalculatePushVector(Point cursorPosition, Rectangle windowBounds, int pushDistance)
    {
        return _pushVector;
    }
}

#endregion