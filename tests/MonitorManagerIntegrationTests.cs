using System.Drawing;
using CursorPhobia.Core.Models;
using CursorPhobia.Core.Services;
using Xunit;

namespace CursorPhobia.Tests;

/// <summary>
/// Integration tests for MonitorManager with configuration change detection
/// </summary>
public class MonitorManagerIntegrationTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithConfigurationWatcher_CreatesInstance()
    {
        // Arrange
        var mockWatcher = new MockConfigurationWatcher();
        var logger = new TestLogger();

        // Act
        using var monitorManager = new MonitorManager(mockWatcher, logger);

        // Assert
        Assert.NotNull(monitorManager);
        Assert.False(monitorManager.IsMonitoring);
        Assert.Null(monitorManager.LastConfigurationChangeDetected);
    }

    [Fact]
    public void Constructor_WithNullWatcher_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = new TestLogger();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MonitorManager(null!, logger));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var mockWatcher = new MockConfigurationWatcher();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MonitorManager(mockWatcher, null!));
    }

    [Fact]
    public void DefaultConstructor_CreatesBasicInstance()
    {
        // Act
        using var monitorManager = new MonitorManager();

        // Assert
        Assert.NotNull(monitorManager);
        Assert.False(monitorManager.IsMonitoring);
        Assert.Null(monitorManager.LastConfigurationChangeDetected);
    }

    #endregion

    #region Monitoring Tests

    [Fact]
    public void StartMonitoring_WithConfigurationWatcher_StartsSuccessfully()
    {
        // Arrange
        var mockWatcher = new MockConfigurationWatcher();
        var logger = new TestLogger();
        using var monitorManager = new MonitorManager(mockWatcher, logger);

        // Act
        monitorManager.StartMonitoring();

        // Assert
        Assert.True(monitorManager.IsMonitoring);
        Assert.True(mockWatcher.StartMonitoringCalled);
    }

    [Fact]
    public void StopMonitoring_WithConfigurationWatcher_StopsSuccessfully()
    {
        // Arrange
        var mockWatcher = new MockConfigurationWatcher();
        var logger = new TestLogger();
        using var monitorManager = new MonitorManager(mockWatcher, logger);
        monitorManager.StartMonitoring();

        // Act
        monitorManager.StopMonitoring();

        // Assert
        Assert.False(monitorManager.IsMonitoring);
        Assert.True(mockWatcher.StopMonitoringCalled);
    }

    [Fact]
    public void StartMonitoring_WithoutConfigurationWatcher_DoesNotThrow()
    {
        // Arrange
        using var monitorManager = new MonitorManager();

        // Act & Assert - should not throw
        monitorManager.StartMonitoring();

        // Should still not be monitoring
        Assert.False(monitorManager.IsMonitoring);
    }

    #endregion

    #region Cache Invalidation Tests

    [Fact]
    public void OnConfigurationChange_InvalidatesCache()
    {
        // Arrange
        var mockWatcher = new MockConfigurationWatcher();
        var logger = new TestLogger();
        using var monitorManager = new MonitorManager(mockWatcher, logger);

        // Simulate initial cache population by getting monitors
        var initialMonitors = monitorManager.GetAllMonitors();

        // Act - simulate configuration change
        var monitor1 = CreateTestMonitor(1, new Rectangle(0, 0, 1920, 1080), true);
        var monitor2 = CreateTestMonitor(2, new Rectangle(1920, 0, 1920, 1080), false);
        var eventArgs = new MonitorChangeEventArgs(
            MonitorChangeType.MonitorsAdded,
            new List<MonitorInfo> { monitor1 },
            new List<MonitorInfo> { monitor1, monitor2 });

        mockWatcher.SimulateConfigurationChange(eventArgs);

        // Assert
        Assert.Contains(logger.Logs, log => log.Contains("Monitor cache invalidated"));
    }

    [Fact]
    public void OnConfigurationChange_ForwardsEvent()
    {
        // Arrange
        var mockWatcher = new MockConfigurationWatcher();
        var logger = new TestLogger();
        using var monitorManager = new MonitorManager(mockWatcher, logger);

        MonitorChangeEventArgs? receivedEventArgs = null;
        monitorManager.MonitorConfigurationChanged += (_, args) => receivedEventArgs = args;

        // Act - simulate configuration change
        var monitor1 = CreateTestMonitor(1, new Rectangle(0, 0, 1920, 1080), true);
        var monitor2 = CreateTestMonitor(2, new Rectangle(1920, 0, 1920, 1080), false);
        var eventArgs = new MonitorChangeEventArgs(
            MonitorChangeType.MonitorsAdded,
            new List<MonitorInfo> { monitor1 },
            new List<MonitorInfo> { monitor1, monitor2 });

        mockWatcher.SimulateConfigurationChange(eventArgs);

        // Assert
        Assert.NotNull(receivedEventArgs);
        Assert.Equal(MonitorChangeType.MonitorsAdded, receivedEventArgs.ChangeType);
        Assert.Single(receivedEventArgs.PreviousMonitors);
        Assert.Equal(2, receivedEventArgs.CurrentMonitors.Count);
    }

    [Fact]
    public void OnConfigurationChange_AfterDispose_DoesNotThrow()
    {
        // Arrange
        var mockWatcher = new MockConfigurationWatcher();
        var logger = new TestLogger();
        var monitorManager = new MonitorManager(mockWatcher, logger);

        // Act - dispose first, then simulate change
        monitorManager.Dispose();

        var monitor1 = CreateTestMonitor(1, new Rectangle(0, 0, 1920, 1080), true);
        var eventArgs = new MonitorChangeEventArgs(
            MonitorChangeType.MonitorsAdded,
            new List<MonitorInfo>(),
            new List<MonitorInfo> { monitor1 });

        // Should not throw
        mockWatcher.SimulateConfigurationChange(eventArgs);

        // Assert - no exception thrown
        Assert.True(true);
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    public void GetAllMonitors_ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        var mockWatcher = new MockConfigurationWatcher();
        var logger = new TestLogger();
        using var monitorManager = new MonitorManager(mockWatcher, logger);

        var tasks = new List<Task>();
        var results = new List<List<MonitorInfo>>();
        var resultLock = new object();

        // Act - access monitors from multiple threads
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    var monitors = monitorManager.GetAllMonitors();
                    lock (resultLock)
                    {
                        results.Add(monitors);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError($"Thread error: {ex.Message}");
                }
            }));
        }

        Task.WaitAll(tasks.ToArray(), TimeSpan.FromSeconds(5));

        // Assert - all calls should succeed
        Assert.Equal(10, results.Count);
        Assert.False(logger.Logs.Any(log => log.Contains("Thread error")));
    }

    [Fact]
    public void ConfigurationChange_ConcurrentWithGetAllMonitors_ThreadSafe()
    {
        // Arrange
        var mockWatcher = new MockConfigurationWatcher();
        var logger = new TestLogger();
        using var monitorManager = new MonitorManager(mockWatcher, logger);

        var accessTasks = new List<Task>();
        var results = new List<List<MonitorInfo>>();
        var resultLock = new object();
        var cancellation = new CancellationTokenSource();

        // Start continuous access tasks
        for (int i = 0; i < 5; i++)
        {
            accessTasks.Add(Task.Run(async () =>
            {
                while (!cancellation.Token.IsCancellationRequested)
                {
                    try
                    {
                        var monitors = monitorManager.GetAllMonitors();
                        lock (resultLock)
                        {
                            results.Add(monitors);
                        }
                        await Task.Delay(10, cancellation.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"Access error: {ex.Message}");
                    }
                }
            }, cancellation.Token));
        }

        // Act - simulate configuration changes while accessing monitors
        for (int i = 0; i < 5; i++)
        {
            Thread.Sleep(20);
            var monitor = CreateTestMonitor(i + 1, new Rectangle(i * 1920, 0, 1920, 1080), i == 0);
            var eventArgs = new MonitorChangeEventArgs(
                MonitorChangeType.MonitorsAdded,
                new List<MonitorInfo>(),
                new List<MonitorInfo> { monitor });

            mockWatcher.SimulateConfigurationChange(eventArgs);
        }

        // Wait a bit then cancel
        Thread.Sleep(100);
        cancellation.Cancel();

        try
        {
            Task.WaitAll(accessTasks.ToArray(), TimeSpan.FromSeconds(2));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
        {
            // Expected cancellation
        }

        // Assert - should have gotten some results without errors
        Assert.True(results.Count > 0);
        Assert.False(logger.Logs.Any(log => log.Contains("Access error")));
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_WithConfigurationWatcher_DisposesWatcher()
    {
        // Arrange
        var mockWatcher = new MockConfigurationWatcher();
        var logger = new TestLogger();
        var monitorManager = new MonitorManager(mockWatcher, logger);

        // Act
        monitorManager.Dispose();

        // Assert
        Assert.True(mockWatcher.IsDisposed);
    }

    [Fact]
    public void GetAllMonitors_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var mockWatcher = new MockConfigurationWatcher();
        var logger = new TestLogger();
        var monitorManager = new MonitorManager(mockWatcher, logger);
        monitorManager.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => monitorManager.GetAllMonitors());
    }

    [Fact]
    public void StartMonitoring_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var mockWatcher = new MockConfigurationWatcher();
        var logger = new TestLogger();
        var monitorManager = new MonitorManager(mockWatcher, logger);
        monitorManager.Dispose();

        // Act & Assert
        Assert.Throws<ObjectDisposedException>(() => monitorManager.StartMonitoring());
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Creates a test monitor with specified properties
    /// </summary>
    private static MonitorInfo CreateTestMonitor(int id, Rectangle bounds, bool isPrimary)
    {
        var handle = new IntPtr(id);
        var workArea = new Rectangle(bounds.X, bounds.Y + 40, bounds.Width, bounds.Height - 40); // Simulate taskbar
        return new MonitorInfo(handle, bounds, workArea, isPrimary);
    }

    #endregion
}

/// <summary>
/// Mock implementation of IMonitorConfigurationWatcher for testing
/// </summary>
public class MockConfigurationWatcher : IMonitorConfigurationWatcher
{
    private bool _isMonitoring;
    private bool _disposed;

    public event EventHandler<MonitorChangeEventArgs>? MonitorConfigurationChanged;

    public bool IsMonitoring => _isMonitoring && !_disposed;
    public DateTime? LastChangeDetected { get; private set; }
    public int? PollingIntervalMs => null;

    public bool StartMonitoringCalled { get; private set; }
    public bool StopMonitoringCalled { get; private set; }
    public bool IsDisposed => _disposed;

    public void StartMonitoring()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(MockConfigurationWatcher));

        _isMonitoring = true;
        StartMonitoringCalled = true;
    }

    public void StopMonitoring()
    {
        if (_disposed)
            return;

        _isMonitoring = false;
        StopMonitoringCalled = true;
    }

    public bool CheckForChanges()
    {
        return false; // No changes in mock
    }

    public void SimulateConfigurationChange(MonitorChangeEventArgs eventArgs)
    {
        if (_disposed)
            return;

        LastChangeDetected = DateTime.UtcNow;
        MonitorConfigurationChanged?.Invoke(this, eventArgs);
    }

    public void Dispose()
    {
        _disposed = true;
        _isMonitoring = false;
    }
}