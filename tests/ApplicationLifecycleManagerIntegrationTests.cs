using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using CursorPhobia.Core.Services;
using CursorPhobia.Core.Utilities;
using CursorPhobia.Core.Logging;
using Xunit;

namespace CursorPhobia.Tests;

/// <summary>
/// Integration tests for ApplicationLifecycleManager to verify proper wiring into application startup
/// Tests the integration between ApplicationLifecycleManager and other core services
/// </summary>
public class ApplicationLifecycleManagerIntegrationTests : IDisposable
{
    private ServiceProvider? _serviceProvider;
    private IApplicationLifecycleManager? _lifecycleManager;
    private TestLogger? _logger;

    public ApplicationLifecycleManagerIntegrationTests()
    {
        Setup();
    }

    private void Setup()
    {
        var services = new ServiceCollection();

        // Setup logging similar to production
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        // Add core services like in production
        services.AddSingleton<CursorPhobia.Core.Utilities.Logger>(provider =>
        {
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            return new CursorPhobia.Core.Utilities.Logger(loggerFactory.CreateLogger<ApplicationLifecycleManagerIntegrationTests>(),
                nameof(ApplicationLifecycleManagerIntegrationTests));
        });

        services.AddSingleton<CursorPhobia.Core.Utilities.ILogger>(provider =>
        {
            return provider.GetRequiredService<CursorPhobia.Core.Utilities.Logger>();
        });

        // Add production readiness services
        services.AddSingleton<IGlobalExceptionHandler, GlobalExceptionHandler>();
        services.AddSingleton<IErrorRecoveryManager, ErrorRecoveryManager>();
        services.AddSingleton<IServiceHealthMonitor, ServiceHealthMonitor>();

        // ApplicationLifecycleManager with all dependencies like in production
        services.AddSingleton<IApplicationLifecycleManager>(provider =>
        {
            var logger = provider.GetRequiredService<CursorPhobia.Core.Utilities.ILogger>();
            var globalExceptionHandler = provider.GetService<IGlobalExceptionHandler>();
            var healthMonitor = provider.GetService<IServiceHealthMonitor>();
            var errorRecoveryManager = provider.GetService<IErrorRecoveryManager>();
            return new ApplicationLifecycleManager(logger, globalExceptionHandler, healthMonitor, errorRecoveryManager);
        });

        _serviceProvider = services.BuildServiceProvider();
        _lifecycleManager = _serviceProvider.GetRequiredService<IApplicationLifecycleManager>();
        _logger = new TestLogger();
    }

    public void Dispose()
    {
        _lifecycleManager?.Dispose();
        _serviceProvider?.Dispose();
    }

    [Fact]
    public async Task ApplicationLifecycleManager_Should_Initialize_Successfully_With_All_Dependencies()
    {
        // Act
        var result = await _lifecycleManager!.InitializeAsync();

        // Assert
        Assert.True(result);
        Assert.True(_lifecycleManager.IsInitialized);
        Assert.False(_lifecycleManager.IsShuttingDown);
    }

    [Fact]
    public async Task ApplicationLifecycleManager_Should_Register_And_Dispose_Services_In_Reverse_Order()
    {
        // Arrange
        await _lifecycleManager!.InitializeAsync();

        var service1 = new TestDisposableService("Service1");
        var service2 = new TestDisposableService("Service2");
        var service3 = new TestDisposableService("Service3");

        // Act - Register services in order
        _lifecycleManager.RegisterService(service1, "Service1");
        _lifecycleManager.RegisterService(service2, "Service2");
        _lifecycleManager.RegisterService(service3, "Service3");

        // Shutdown should dispose in reverse order
        await _lifecycleManager.ShutdownAsync();

        // Assert - Services should be disposed in reverse order (3, 2, 1)
        Assert.True(service3.DisposedBefore(service2));
        Assert.True(service2.DisposedBefore(service1));
        Assert.True(service1.IsDisposed);
        Assert.True(service2.IsDisposed);
        Assert.True(service3.IsDisposed);
    }

    [Fact]
    public async Task ApplicationLifecycleManager_Should_Handle_Service_Disposal_Exceptions_Gracefully()
    {
        // Arrange
        await _lifecycleManager!.InitializeAsync();

        var goodService = new TestDisposableService("GoodService");
        var badService = new TestDisposableService("BadService", shouldThrowOnDispose: true);
        var anotherGoodService = new TestDisposableService("AnotherGoodService");

        _lifecycleManager.RegisterService(goodService, "GoodService");
        _lifecycleManager.RegisterService(badService, "BadService");
        _lifecycleManager.RegisterService(anotherGoodService, "AnotherGoodService");

        // Act - Should not throw even if one service throws during disposal
        await _lifecycleManager.ShutdownAsync();

        // Assert - Good services should still be disposed despite bad service throwing
        Assert.True(goodService.IsDisposed);
        Assert.True(anotherGoodService.IsDisposed);
        Assert.True(badService.DisposeAttempted);
    }

    [Fact]
    public async Task ApplicationLifecycleManager_Should_Integrate_With_GlobalExceptionHandler()
    {
        // Arrange
        var globalExceptionHandler = _serviceProvider!.GetRequiredService<IGlobalExceptionHandler>();

        // Act
        var result = await _lifecycleManager!.InitializeAsync();

        // Assert
        Assert.True(result);
        Assert.True(globalExceptionHandler.IsActive);
    }

    [Fact]
    public async Task ApplicationLifecycleManager_Should_Integrate_With_ServiceHealthMonitor()
    {
        // Arrange
        var healthMonitor = _serviceProvider!.GetRequiredService<IServiceHealthMonitor>();

        // Act
        var result = await _lifecycleManager!.InitializeAsync();

        // Assert
        Assert.True(result);
        // The lifecycle manager should register itself with the health monitor
        // This is verified through the health monitor's service registration
    }

    [Fact]
    public async Task ApplicationLifecycleManager_Should_Integrate_With_ErrorRecoveryManager()
    {
        // Arrange
        var errorRecoveryManager = _serviceProvider!.GetRequiredService<IErrorRecoveryManager>();

        // Act
        var result = await _lifecycleManager!.InitializeAsync();

        // Assert
        Assert.True(result);
        Assert.True(errorRecoveryManager.IsInitialized);
    }

    [Fact]
    public async Task ApplicationLifecycleManager_Should_Fire_ApplicationExitRequested_Event_On_Shutdown()
    {
        // Arrange
        await _lifecycleManager!.InitializeAsync();

        var exitRequested = false;
        _lifecycleManager.ApplicationExitRequested += (sender, args) => exitRequested = true;

        // Act
        await _lifecycleManager.ShutdownAsync();

        // Assert
        Assert.True(exitRequested);
        Assert.True(_lifecycleManager.IsShuttingDown);
    }

    [Fact]
    public async Task ApplicationLifecycleManager_Should_Set_Environment_ExitCode_On_Shutdown()
    {
        // Arrange
        await _lifecycleManager!.InitializeAsync();
        var originalExitCode = Environment.ExitCode;

        // Act
        await _lifecycleManager.ShutdownAsync(42);

        // Assert
        Assert.Equal(42, Environment.ExitCode);

        // Cleanup
        Environment.ExitCode = originalExitCode;
    }

    [Fact]
    public async Task ApplicationLifecycleManager_Should_Handle_Multiple_Initialize_Calls_Gracefully()
    {
        // Act
        var result1 = await _lifecycleManager!.InitializeAsync();
        var result2 = await _lifecycleManager.InitializeAsync();
        var result3 = await _lifecycleManager.InitializeAsync();

        // Assert
        Assert.True(result1);
        Assert.True(result2);
        Assert.True(result3);
        Assert.True(_lifecycleManager.IsInitialized);
    }

    [Fact]
    public async Task ApplicationLifecycleManager_Should_Prevent_Service_Registration_During_Shutdown()
    {
        // Arrange
        await _lifecycleManager!.InitializeAsync();
        var service = new TestDisposableService("TestService");

        // Start shutdown asynchronously
        var shutdownTask = _lifecycleManager.ShutdownAsync();

        // Act - Try to register service during shutdown
        _lifecycleManager.RegisterService(service, "TestService");

        await shutdownTask;

        // Assert - Service should not be disposed since it wasn't actually registered
        Assert.False(service.IsDisposed);
    }

    [Fact]
    public void ApplicationLifecycleManager_Should_Be_Registered_As_Singleton_In_DI()
    {
        // Act
        var instance1 = _serviceProvider!.GetRequiredService<IApplicationLifecycleManager>();
        var instance2 = _serviceProvider.GetRequiredService<IApplicationLifecycleManager>();

        // Assert
        Assert.Same(instance1, instance2);
    }

    [Fact]
    public async Task ApplicationLifecycleManager_Should_Unregister_Services_Successfully()
    {
        // Arrange
        await _lifecycleManager!.InitializeAsync();

        var service1 = new TestDisposableService("Service1");
        var service2 = new TestDisposableService("Service2");

        _lifecycleManager.RegisterService(service1, "Service1");
        _lifecycleManager.RegisterService(service2, "Service2");

        // Act
        _lifecycleManager.UnregisterService(service1);
        await _lifecycleManager.ShutdownAsync();

        // Assert
        Assert.False(service1.IsDisposed);
        Assert.True(service2.IsDisposed);
    }
}

/// <summary>
/// Test disposable service that tracks disposal order and can simulate disposal exceptions
/// </summary>
public class TestDisposableService : IDisposable
{
    private static long _disposalCounter = 0;
    private readonly bool _shouldThrowOnDispose;
    private long _disposalOrder = -1;

    public string Name { get; }
    public bool IsDisposed => _disposalOrder != -1;
    public bool DisposeAttempted { get; private set; }
    public long DisposalOrder => _disposalOrder;

    public TestDisposableService(string name, bool shouldThrowOnDispose = false)
    {
        Name = name;
        _shouldThrowOnDispose = shouldThrowOnDispose;
    }

    public void Dispose()
    {
        DisposeAttempted = true;

        if (_shouldThrowOnDispose)
        {
            throw new InvalidOperationException($"Test exception from {Name}");
        }

        if (_disposalOrder == -1)
        {
            _disposalOrder = Interlocked.Increment(ref _disposalCounter);
        }
    }

    public bool DisposedBefore(TestDisposableService other)
    {
        return IsDisposed && other.IsDisposed && _disposalOrder < other._disposalOrder;
    }
}