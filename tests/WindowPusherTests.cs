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
        var windowDetectionService = new MockWindowDetectionService();
        var monitorManager = new MockMonitorManager();
        var edgeWrapHandler = new MockEdgeWrapHandler(monitorManager);
        using var pusher = new WindowPusher(logger, windowService, safetyManager, proximityDetector, windowDetectionService, monitorManager, edgeWrapHandler);
        
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
        var windowDetectionService = new MockWindowDetectionService();
        var monitorManager = new MockMonitorManager();
        var edgeWrapHandler = new MockEdgeWrapHandler(monitorManager);
        Assert.Throws<ArgumentNullException>(() =>
            new WindowPusher(null!, windowService, safetyManager, proximityDetector, windowDetectionService, monitorManager, edgeWrapHandler));
    }

    [Fact]
    public void Constructor_WithNullWindowService_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = new TestLogger();
        var safetyManager = new MockSafetyManager();
        var proximityDetector = new MockProximityDetector();

        // Act & Assert
        var windowDetectionService = new MockWindowDetectionService();
        var monitorManager = new MockMonitorManager();
        var edgeWrapHandler = new MockEdgeWrapHandler(monitorManager);
        Assert.Throws<ArgumentNullException>(() =>
            new WindowPusher(logger, null!, safetyManager, proximityDetector, windowDetectionService, monitorManager, edgeWrapHandler));
    }

    [Fact]
    public void Constructor_WithNullSafetyManager_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = new TestLogger();
        var windowService = new MockWindowManipulationService();
        var proximityDetector = new MockProximityDetector();

        // Act & Assert
        var windowDetectionService = new MockWindowDetectionService();
        var monitorManager = new MockMonitorManager();
        var edgeWrapHandler = new MockEdgeWrapHandler(monitorManager);
        Assert.Throws<ArgumentNullException>(() =>
            new WindowPusher(logger, windowService, null!, proximityDetector, windowDetectionService, monitorManager, edgeWrapHandler));
    }

    [Fact]
    public void Constructor_WithNullProximityDetector_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = new TestLogger();
        var windowService = new MockWindowManipulationService();
        var safetyManager = new MockSafetyManager();

        // Act & Assert
        var windowDetectionService = new MockWindowDetectionService();
        var monitorManager = new MockMonitorManager();
        var edgeWrapHandler = new MockEdgeWrapHandler(monitorManager);
        Assert.Throws<ArgumentNullException>(() =>
            new WindowPusher(logger, windowService, safetyManager, null!, windowDetectionService, monitorManager, edgeWrapHandler));
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
        var windowDetectionService = new MockWindowDetectionService();
        var monitorManager = new MockMonitorManager();
        var edgeWrapHandler = new MockEdgeWrapHandler(monitorManager);
        Assert.Throws<ArgumentException>(() => 
            new WindowPusher(logger, windowService, safetyManager, proximityDetector, windowDetectionService, monitorManager, edgeWrapHandler, invalidConfig));
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

    
    #region MockWindowDetectionService Tests
    
    [Fact]
    public void MockWindowDetectionService_AddTopMostWindow_ReturnsInTopMostList()
    {
        // Arrange
        var mockDetectionService = new MockWindowDetectionService();
        var handle = new IntPtr(12345);
        var bounds = new Rectangle(100, 100, 200, 150);
        
        // Act
        mockDetectionService.AddTopMostWindow(handle, "Test Window", bounds);
        var topMostWindows = mockDetectionService.GetAllTopMostWindows();
        
        // Assert
        Assert.Single(topMostWindows);
        Assert.Equal(handle, topMostWindows[0].WindowHandle);
        Assert.Equal("Test Window", topMostWindows[0].Title);
        Assert.True(mockDetectionService.IsWindowAlwaysOnTop(handle));
    }
    
    [Fact]
    public void MockWindowDetectionService_SetWindowAlwaysOnTop_UpdatesTopMostStatus()
    {
        // Arrange
        var mockDetectionService = new MockWindowDetectionService();
        var handle = new IntPtr(12345);
        var bounds = new Rectangle(100, 100, 200, 150);
        
        mockDetectionService.AddRegularWindow(handle, "Test Window", bounds);
        
        // Act
        mockDetectionService.SetWindowAlwaysOnTop(handle, true);
        
        // Assert
        Assert.True(mockDetectionService.IsWindowAlwaysOnTop(handle));
        Assert.Single(mockDetectionService.GetAllTopMostWindows());
    }
    
    [Fact]
    public void MockWindowDetectionService_GetWindowInformation_ReturnsCorrectInfo()
    {
        // Arrange
        var mockDetectionService = new MockWindowDetectionService();
        var handle = new IntPtr(12345);
        var bounds = new Rectangle(100, 100, 200, 150);
        
        mockDetectionService.AddTopMostWindow(handle, "Test Window", bounds, "TestClass");
        
        // Act
        var windowInfo = mockDetectionService.GetWindowInformation(handle);
        
        // Assert
        Assert.NotNull(windowInfo);
        Assert.Equal(handle, windowInfo.WindowHandle);
        Assert.Equal("Test Window", windowInfo.Title);
        Assert.Equal("TestClass", windowInfo.ClassName);
        Assert.Equal(bounds, windowInfo.Bounds);
        Assert.True(windowInfo.IsVisible);
        Assert.True(windowInfo.IsTopmost);
        Assert.False(windowInfo.IsMinimized);
    }
    
    #endregion
    
    #region Helper Methods

    private static WindowPusher CreateWindowPusher(
        IWindowManipulationService? windowService = null,
        ISafetyManager? safetyManager = null,
        IProximityDetector? proximityDetector = null,
        CursorPhobiaConfiguration? config = null,
        IWindowDetectionService? windowDetectionService = null,
        MonitorManager? monitorManager = null,
        EdgeWrapHandler? edgeWrapHandler = null)
    {
        var mockMonitorManager = monitorManager ?? new MockMonitorManager();
        var mockEdgeWrapHandler = edgeWrapHandler ?? new MockEdgeWrapHandler(mockMonitorManager);

        return new WindowPusher(
            new TestLogger(),
            windowService ?? new MockWindowManipulationService(),
            safetyManager ?? new MockSafetyManager(),
            proximityDetector ?? new MockProximityDetector(),
            windowDetectionService ?? new MockWindowDetectionService(),
            mockMonitorManager,
            mockEdgeWrapHandler,
            config
        );
    }

    #endregion
}

#region Mock Services

/// <summary>
/// Mock implementation of MonitorManager for testing
/// </summary>
public class MockMonitorManager : MonitorManager
{
    private readonly List<MonitorInfo> _monitors = new();
    private readonly Dictionary<(MonitorInfo, EdgeDirection), MonitorInfo?> _adjacentMonitors = new();
    private readonly Dictionary<Rectangle, MonitorInfo?> _rectangleMonitorOverrides = new();
    private readonly Dictionary<Point, MonitorInfo?> _pointMonitorOverrides = new();
    private readonly Dictionary<MonitorInfo, DpiInfo> _monitorDpiOverrides = new();

    public void AddMonitor(MonitorInfo monitor)
    {
        _monitors.Add(monitor);
    }

    public void SetAdjacentMonitor(MonitorInfo sourceMonitor, EdgeDirection direction, MonitorInfo? adjacentMonitor)
    {
        _adjacentMonitors[(sourceMonitor, direction)] = adjacentMonitor;
    }

    public void SetMonitorForRectangle(Rectangle rectangle, MonitorInfo? monitor)
    {
        _rectangleMonitorOverrides[rectangle] = monitor;
    }

    public void SetMonitorForPoint(Point point, MonitorInfo? monitor)
    {
        _pointMonitorOverrides[point] = monitor;
    }

    public void SetMonitorDpi(MonitorInfo monitor, DpiInfo dpiInfo)
    {
        _monitorDpiOverrides[monitor] = dpiInfo;
    }

    public void SetMonitorDpi(MonitorInfo monitor, uint dpiX, uint dpiY)
    {
        _monitorDpiOverrides[monitor] = new DpiInfo(dpiX, dpiY);
    }

    public override List<MonitorInfo> GetAllMonitors()
    {
        return new List<MonitorInfo>(_monitors);
    }

    public override MonitorInfo? GetMonitorContaining(Point point)
    {
        if (_pointMonitorOverrides.TryGetValue(point, out var overrideMonitor))
            return overrideMonitor;

        return _monitors.FirstOrDefault(m => m.ContainsPoint(point));
    }

    public override MonitorInfo? GetMonitorContaining(Rectangle windowRect)
    {
        if (_rectangleMonitorOverrides.TryGetValue(windowRect, out var overrideMonitor))
            return overrideMonitor;

        // Use window center point to determine containing monitor
        // This handles edge cases where window touches monitor edge
        var windowCenter = new Point(
            windowRect.X + windowRect.Width / 2,
            windowRect.Y + windowRect.Height / 2
        );

        foreach (var monitor in _monitors)
        {
            if (monitor.workAreaBounds.Contains(windowCenter))
            {
                return monitor;
            }
        }

        // Fallback: check against full monitor bounds if not in work area
        foreach (var monitor in _monitors)
        {
            if (monitor.monitorBounds.Contains(windowCenter))
            {
                return monitor;
            }
        }

        return null;
    }

    public override MonitorInfo? GetMonitorInDirection(MonitorInfo sourceMonitor, EdgeDirection direction)
    {
        return _adjacentMonitors.TryGetValue((sourceMonitor, direction), out var adjacent) ? adjacent : null;
    }

    public override DpiInfo GetMonitorDpi(MonitorInfo monitor)
    {
        return _monitorDpiOverrides.TryGetValue(monitor, out var dpiInfo) ? dpiInfo : new DpiInfo();
    }

    public override DpiInfo GetDpiForPoint(Point point)
    {
        var monitor = GetMonitorContaining(point);
        return monitor != null ? GetMonitorDpi(monitor) : new DpiInfo();
    }

    public override DpiInfo GetDpiForRectangle(Rectangle windowRect)
    {
        var monitor = GetMonitorContaining(windowRect);
        return monitor != null ? GetMonitorDpi(monitor) : new DpiInfo();
    }

    /// <summary>
    /// Sets the monitor list for testing configuration changes
    /// </summary>
    public void SetMonitorList(List<MonitorInfo> monitors)
    {
        _monitors.Clear();
        _monitors.AddRange(monitors);
    }

    /// <summary>
    /// Simulates a monitor configuration change event
    /// </summary>
    public void SimulateConfigurationChange(MonitorChangeEventArgs eventArgs)
    {
        OnMonitorConfigurationChanged(eventArgs);
    }
}

/// <summary>
/// Mock implementation of EdgeWrapHandler for testing
/// </summary>
public class MockEdgeWrapHandler : EdgeWrapHandler
{
    private Point? _wrapDestination;
    private bool _isWrapSafe = true;

    public MockEdgeWrapHandler(MonitorManager monitorManager) : base(monitorManager)
    {
    }

    public void SetWrapDestination(Point? destination)
    {
        _wrapDestination = destination;
    }

    public void SetWrapSafe(bool isSafe)
    {
        _isWrapSafe = isSafe;
    }

    public new Point? CalculateWrapDestination(Rectangle windowRect, Point pushVector, WrapBehavior wrapBehavior)
    {
        return _wrapDestination;
    }

    public new bool IsWrapSafe(Point originalPosition, Point newPosition, Size windowSize)
    {
        return _isWrapSafe;
    }
}

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

    public async Task<bool> MoveWindowAsync(IntPtr hWnd, int x, int y)
    {
        await Task.Delay(1); // Simulate async behavior
        return MoveWindow(hWnd, x, y);
    }

    public async Task<Rectangle> GetWindowBoundsAsync(IntPtr hWnd)
    {
        await Task.Delay(1); // Simulate async behavior
        return GetWindowBounds(hWnd);
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

    public Rectangle GetOverallDesktopSafeArea()
    {
        return new Rectangle(0, 0, 1920, 1080); // Default desktop area for testing
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

/// <summary>
/// Mock implementation of IWindowDetectionService for testing
/// </summary>
public class MockWindowDetectionService : IWindowDetectionService
{
    private readonly List<WindowInfo> _topMostWindows = new();
    private readonly Dictionary<IntPtr, bool> _alwaysOnTopWindows = new();
    private readonly Dictionary<IntPtr, WindowInfo> _windowInfos = new();
    private readonly List<WindowInfo> _visibleWindows = new();
    private readonly Dictionary<IntPtr, WindowInfo> _windows = new();
    private readonly Dictionary<IntPtr, bool> _topMostFlags = new();

    public void AddTopMostWindow(WindowInfo window)
    {
        _topMostWindows.Add(window);
        _alwaysOnTopWindows[window.WindowHandle] = true;
    }

    public void SetWindowAlwaysOnTop(IntPtr handle, bool isAlwaysOnTop)
    {
        _alwaysOnTopWindows[handle] = isAlwaysOnTop;
        
        if (isAlwaysOnTop)
        {
            // Add to topmost collection if window exists and not already there
            if (_windows.TryGetValue(handle, out var window) && !_topMostWindows.Any(w => w.WindowHandle == handle))
            {
                window.IsTopmost = true;
                _topMostWindows.Add(window);
                _topMostFlags[handle] = true;
            }
        }
        else
        {
            // Remove from topmost collection
            _topMostWindows.RemoveAll(w => w.WindowHandle == handle);
            _topMostFlags.Remove(handle);
            if (_windows.TryGetValue(handle, out var window))
            {
                window.IsTopmost = false;
            }
        }
    }

    public void SetWindowInfo(IntPtr handle, WindowInfo info)
    {
        _windowInfos[handle] = info;
    }

    public void AddVisibleWindow(WindowInfo window)
    {
        _visibleWindows.Add(window);
    }


    /// <summary>
    /// Adds a window to the mock service
    /// </summary>
    public void AddWindow(WindowInfo window)
    {
        _windows[window.WindowHandle] = window;
        if (window.IsVisible)
        {
            _visibleWindows.Add(window);
        }
        if (window.IsTopmost)
        {
            _topMostWindows.Add(window);
            _topMostFlags[window.WindowHandle] = true;
        }
    }

    /// <summary>
    /// Adds a topmost window with specified parameters
    /// </summary>
    public void AddTopMostWindow(IntPtr handle, string title, Rectangle bounds, string className = "TestWindow")
    {
        var window = new WindowInfo
        {
            WindowHandle = handle,
            Title = title,
            ClassName = className,
            Bounds = bounds,
            ProcessId = 1234,
            ThreadId = 5678,
            IsVisible = true,
            IsTopmost = true,
            IsMinimized = false
        };
        AddWindow(window);
    }

    /// <summary>
    /// Adds a regular (non-topmost) window with specified parameters
    /// </summary>
    public void AddRegularWindow(IntPtr handle, string title, Rectangle bounds, string className = "TestWindow")
    {
        var window = new WindowInfo
        {
            WindowHandle = handle,
            Title = title,
            ClassName = className,
            Bounds = bounds,
            ProcessId = 1234,
            ThreadId = 5678,
            IsVisible = true,
            IsTopmost = false,
            IsMinimized = false
        };
        AddWindow(window);
    }


    /// <summary>
    /// Sets the visibility of a window
    /// </summary>
    public void SetWindowVisible(IntPtr handle, bool isVisible)
    {
        if (_windows.TryGetValue(handle, out var window))
        {
            window.IsVisible = isVisible;
            
            // Update the visible windows list
            _visibleWindows.RemoveAll(w => w.WindowHandle == handle);
            if (isVisible)
            {
                _visibleWindows.Add(window);
            }
        }
    }

    /// <summary>
    /// Sets the minimized state of a window
    /// </summary>
    public void SetWindowMinimized(IntPtr handle, bool isMinimized)
    {
        if (_windows.TryGetValue(handle, out var window))
        {
            window.IsMinimized = isMinimized;
        }
    }

    /// <summary>
    /// Updates the bounds of an existing window
    /// </summary>
    public void UpdateWindowBounds(IntPtr handle, Rectangle bounds)
    {
        if (_windows.TryGetValue(handle, out var window))
        {
            window.Bounds = bounds;
        }
    }

    /// <summary>
    /// Removes a window from the mock service
    /// </summary>
    public void RemoveWindow(IntPtr handle)
    {
        _windows.Remove(handle);
        _topMostFlags.Remove(handle);
        _visibleWindows.RemoveAll(w => w.WindowHandle == handle);
        _topMostWindows.RemoveAll(w => w.WindowHandle == handle);
    }

    /// <summary>
    /// Clears all windows from the mock service
    /// </summary>
    public void Clear()
    {
        _windows.Clear();
        _topMostFlags.Clear();
        _visibleWindows.Clear();
        _topMostWindows.Clear();
    }

    // IWindowDetectionService implementation

    public List<WindowInfo> GetAllTopMostWindows()
    {
        return new List<WindowInfo>(_topMostWindows);
    }

    public bool IsWindowAlwaysOnTop(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return false;
            
        // Check both the alwaysOnTop collection and topMost flags for compatibility
        return (_alwaysOnTopWindows.TryGetValue(hWnd, out var isAlwaysOnTop) && isAlwaysOnTop) ||
               (_topMostFlags.TryGetValue(hWnd, out var isTopmost) && isTopmost);
    }

    public WindowInfo? GetWindowInformation(IntPtr hWnd)
    {
        if (hWnd == IntPtr.Zero)
            return null;
            
        // Check the windowInfos collection first, then fall back to the windows collection
        return _windowInfos.TryGetValue(hWnd, out var info) ? info : 
               (_windows.TryGetValue(hWnd, out var window) ? window : null);
    }

    public List<WindowInfo> EnumerateVisibleWindows()
    {
        return new List<WindowInfo>(_visibleWindows);
    }
}

#endregion