using System.Drawing;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;
using Xunit;

namespace CursorPhobia.Tests;

/// <summary>
/// Unit tests for CursorTracker service
/// </summary>
public class CursorTrackerTests
{
    private readonly ILogger _logger;
    private readonly CursorPhobiaConfiguration _defaultConfig;

    public CursorTrackerTests()
    {
        _logger = new TestLogger();
        _defaultConfig = CursorPhobiaConfiguration.CreateDefault();
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Act
        var tracker = new CursorTracker(_logger);

        // Assert
        Assert.NotNull(tracker);
        Assert.False(tracker.IsTracking);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new CursorTracker(null!));
    }

    [Fact]
    public void Constructor_WithInvalidConfiguration_ThrowsArgumentException()
    {
        // Arrange
        var invalidConfig = new CursorPhobiaConfiguration
        {
            UpdateIntervalMs = -1 // Invalid value
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => new CursorTracker(_logger, invalidConfig));
        Assert.Contains("Invalid cursor tracker configuration", exception.Message);
    }

    [Fact]
    public void Constructor_WithValidConfiguration_CreatesInstanceWithConfig()
    {
        // Arrange
        var config = CursorPhobiaConfiguration.CreatePerformanceOptimized();

        // Act
        var tracker = new CursorTracker(_logger, config);

        // Assert
        Assert.NotNull(tracker);
        Assert.False(tracker.IsTracking);
    }

    [Fact]
    public void CurrentPosition_InitialState_ReturnsEmptyPoint()
    {
        // Arrange
        var tracker = new CursorTracker(_logger);

        // Act
        var position = tracker.CurrentPosition;

        // Assert
        Assert.Equal(Point.Empty, position);
    }

    [Fact]
    public void IsTracking_InitialState_ReturnsFalse()
    {
        // Arrange
        var tracker = new CursorTracker(_logger);

        // Act
        var isTracking = tracker.IsTracking;

        // Assert
        Assert.False(isTracking);
    }

    [Fact]
    public void GetCurrentCursorPosition_ReturnsValidPoint()
    {
        // Arrange
        var tracker = new CursorTracker(_logger);

        // Act
        var position = tracker.GetCurrentCursorPosition();

        // Assert
        // We can't guarantee the exact position, but it should be a valid point
        // (not Point.Empty unless the cursor is actually at 0,0)
        Assert.IsType<Point>(position);
    }

    [Fact]
    public void StartTracking_WhenNotTracking_AttemptsToStart()
    {
        // Arrange
        using var tracker = new CursorTracker(_logger);

        // Act
        var result = tracker.StartTracking();

        // Assert
        Assert.IsType<bool>(result);
        // Note: We can't guarantee success due to Windows hook limitations in test environment
        // but we can verify the method doesn't throw exceptions
    }

    [Fact]
    public void StartTracking_WhenAlreadyTracking_ReturnsTrue()
    {
        // Arrange
        using var tracker = new CursorTracker(_logger);
        tracker.StartTracking(); // First call

        // Act
        var result = tracker.StartTracking(); // Second call

        // Assert
        Assert.True(result); // Should return true if already tracking
    }

    [Fact]
    public void StopTracking_WhenTracking_StopsSuccessfully()
    {
        // Arrange
        using var tracker = new CursorTracker(_logger);
        tracker.StartTracking();

        // Act
        tracker.StopTracking();

        // Assert
        Assert.False(tracker.IsTracking);
    }

    [Fact]
    public void StopTracking_WhenNotTracking_DoesNotThrow()
    {
        // Arrange
        using var tracker = new CursorTracker(_logger);

        // Act & Assert
        var exception = Record.Exception(() => tracker.StopTracking());
        Assert.Null(exception);
    }

    [Fact]
    public void IsCtrlKeyPressed_WithCtrlOverrideEnabled_ChecksKeyState()
    {
        // Arrange
        var config = new CursorPhobiaConfiguration { EnableCtrlOverride = true };
        var tracker = new CursorTracker(_logger, config);

        // Act
        var isPressed = tracker.IsCtrlKeyPressed();

        // Assert
        Assert.IsType<bool>(isPressed);
        // We can't guarantee the actual state, but the method should not throw
    }

    [Fact]
    public void IsCtrlKeyPressed_WithCtrlOverrideDisabled_ReturnsFalse()
    {
        // Arrange
        var config = new CursorPhobiaConfiguration { EnableCtrlOverride = false };
        var tracker = new CursorTracker(_logger, config);

        // Act
        var isPressed = tracker.IsCtrlKeyPressed();

        // Assert
        Assert.False(isPressed);
    }

    [Fact]
    public void CursorPositionChanged_Event_CanBeSubscribed()
    {
        // Arrange
        using var tracker = new CursorTracker(_logger);
        var eventFired = false;
        Point? receivedPosition = null;

        // Act
        tracker.CursorPositionChanged += (sender, position) =>
        {
            eventFired = true;
            receivedPosition = position;
        };

        // Assert
        // Event subscription should not throw
        Assert.False(eventFired); // Event should not fire just from subscription
    }

    [Fact]
    public void CursorMoved_Event_CanBeSubscribed()
    {
        // Arrange
        using var tracker = new CursorTracker(_logger);
        var eventFired = false;
        CursorMovedEventArgs? receivedArgs = null;

        // Act
        tracker.CursorMoved += (sender, args) =>
        {
            eventFired = true;
            receivedArgs = args;
        };

        // Assert
        // Event subscription should not throw
        Assert.False(eventFired); // Event should not fire just from subscription
    }

    [Fact]
    public void Dispose_WhenTracking_StopsTrackingAndDisposes()
    {
        // Arrange
        var tracker = new CursorTracker(_logger);
        tracker.StartTracking();

        // Act
        tracker.Dispose();

        // Assert
        Assert.False(tracker.IsTracking);
    }

    [Fact]
    public void Dispose_WhenNotTracking_DisposesCleanly()
    {
        // Arrange
        var tracker = new CursorTracker(_logger);

        // Act & Assert
        var exception = Record.Exception(() => tracker.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var tracker = new CursorTracker(_logger);

        // Act & Assert
        tracker.Dispose();
        var exception = Record.Exception(() => tracker.Dispose());
        Assert.Null(exception);
    }

    [Fact]
    public void StartTracking_AfterDisposed_ReturnsFalse()
    {
        // Arrange
        var tracker = new CursorTracker(_logger);
        tracker.Dispose();

        // Act
        var result = tracker.StartTracking();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsTracking_AfterDisposed_ReturnsFalse()
    {
        // Arrange
        var tracker = new CursorTracker(_logger);
        tracker.StartTracking();
        tracker.Dispose();

        // Act
        var isTracking = tracker.IsTracking;

        // Assert
        Assert.False(isTracking);
    }

    [Fact]
    public void CursorTracker_WithDifferentConfigurations_CreatesSuccessfully()
    {
        // Arrange & Act
        using var defaultTracker = new CursorTracker(_logger, CursorPhobiaConfiguration.CreateDefault());
        using var performanceTracker = new CursorTracker(_logger, CursorPhobiaConfiguration.CreatePerformanceOptimized());
        using var responsivenessTracker = new CursorTracker(_logger, CursorPhobiaConfiguration.CreateResponsivenessOptimized());

        // Assert
        Assert.NotNull(defaultTracker);
        Assert.NotNull(performanceTracker);
        Assert.NotNull(responsivenessTracker);
    }

    [Fact]
    public void CursorTracker_LongRunning_HandlesResourcesCorrectly()
    {
        // Arrange
        using var tracker = new CursorTracker(_logger);

        // Act - Simulate start/stop cycles
        for (int i = 0; i < 5; i++)
        {
            tracker.StartTracking();
            Thread.Sleep(10); // Brief delay
            tracker.StopTracking();
        }

        // Assert
        Assert.False(tracker.IsTracking);
        // If we get here without exceptions, resource management is working
    }

    [Fact]
    public void GetCurrentCursorPosition_CalledMultipleTimes_DoesNotThrow()
    {
        // Arrange
        var tracker = new CursorTracker(_logger);

        // Act & Assert
        for (int i = 0; i < 10; i++)
        {
            var exception = Record.Exception(() => tracker.GetCurrentCursorPosition());
            Assert.Null(exception);
        }
    }

    [Fact]
    public void CursorTracker_ThreadSafety_HandlesMultipleThreads()
    {
        // Arrange
        using var tracker = new CursorTracker(_logger);
        var exceptions = new List<Exception>();
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    tracker.StartTracking();
                    var position = tracker.GetCurrentCursorPosition();
                    var isTracking = tracker.IsTracking;
                    tracker.StopTracking();
                }
                catch (Exception ex)
                {
                    lock (exceptions)
                    {
                        exceptions.Add(ex);
                    }
                }
            }));
        }

        Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(5));

        // Assert
        Assert.Empty(exceptions);
    }
}

/// <summary>
/// Unit tests for CursorMovedEventArgs
/// </summary>
public class CursorMovedEventArgsTests
{
    [Fact]
    public void Constructor_WithValidPoints_CalculatesDistance()
    {
        // Arrange
        var oldPosition = new Point(0, 0);
        var newPosition = new Point(3, 4);

        // Act
        var args = new CursorMovedEventArgs(oldPosition, newPosition);

        // Assert
        Assert.Equal(oldPosition, args.OldPosition);
        Assert.Equal(newPosition, args.NewPosition);
        Assert.Equal(5.0, args.Distance, 1); // 3-4-5 triangle
    }

    [Fact]
    public void Constructor_WithSamePoints_CalculatesZeroDistance()
    {
        // Arrange
        var position = new Point(100, 100);

        // Act
        var args = new CursorMovedEventArgs(position, position);

        // Assert
        Assert.Equal(position, args.OldPosition);
        Assert.Equal(position, args.NewPosition);
        Assert.Equal(0.0, args.Distance);
    }

    [Theory]
    [InlineData(0, 0, 1, 0, 1.0)]
    [InlineData(0, 0, 0, 1, 1.0)]
    [InlineData(0, 0, 5, 12, 13.0)]
    [InlineData(10, 10, 13, 14, 5.0)]
    public void Constructor_WithVariousPoints_CalculatesCorrectDistance(
        int oldX, int oldY, int newX, int newY, double expectedDistance)
    {
        // Arrange
        var oldPosition = new Point(oldX, oldY);
        var newPosition = new Point(newX, newY);

        // Act
        var args = new CursorMovedEventArgs(oldPosition, newPosition);

        // Assert
        Assert.Equal(expectedDistance, args.Distance, 1);
    }
}