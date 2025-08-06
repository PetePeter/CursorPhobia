using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Manages application lifecycle events and coordinates clean shutdown
/// Handles service registration/disposal and ensures graceful application termination
/// Integrates with GlobalExceptionHandler for comprehensive error handling (Phase 1 WI#8)
/// Enhanced with Phase 3 WI#8: Health monitoring integration
/// </summary>
public class ApplicationLifecycleManager : IApplicationLifecycleManager
{
    private readonly ILogger _logger;
    private readonly IGlobalExceptionHandler? _globalExceptionHandler;
    private readonly IServiceHealthMonitor? _healthMonitor;
    private readonly IErrorRecoveryManager? _errorRecoveryManager;
    private readonly List<(IDisposable Service, string? Name)> _registeredServices = new();
    private readonly object _lock = new();
    private bool _disposed = false;
    private bool _initialized = false;
    private bool _isShuttingDown = false;

    /// <summary>
    /// Event raised when the application should exit
    /// </summary>
    public event EventHandler? ApplicationExitRequested;

    /// <summary>
    /// Gets whether the lifecycle manager is currently initialized
    /// </summary>
    public bool IsInitialized => _initialized && !_disposed;

    /// <summary>
    /// Gets whether a shutdown is currently in progress
    /// </summary>
    public bool IsShuttingDown => _isShuttingDown;

    /// <summary>
    /// Creates a new ApplicationLifecycleManager instance
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="globalExceptionHandler">Optional global exception handler for enhanced error handling</param>
    /// <param name="healthMonitor">Optional health monitor for service health tracking</param>
    /// <param name="errorRecoveryManager">Optional error recovery manager for self-healing capabilities</param>
    public ApplicationLifecycleManager(ILogger logger, IGlobalExceptionHandler? globalExceptionHandler = null,
        IServiceHealthMonitor? healthMonitor = null, IErrorRecoveryManager? errorRecoveryManager = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _globalExceptionHandler = globalExceptionHandler;
        _healthMonitor = healthMonitor;
        _errorRecoveryManager = errorRecoveryManager;
    }

    /// <summary>
    /// Initializes the application lifecycle manager
    /// </summary>
    /// <returns>True if initialization was successful, false otherwise</returns>
    public async Task<bool> InitializeAsync()
    {
        if (_disposed)
        {
            _logger.LogWarning("Cannot initialize disposed ApplicationLifecycleManager");
            return false;
        }

        if (_initialized)
        {
            _logger.LogDebug("ApplicationLifecycleManager is already initialized");
            return true;
        }

        // Phase 2 WI#8: Performance logging for initialization
        using var perfScope = _logger is Logger loggerWithPerf ?
            loggerWithPerf.BeginPerformanceScope("Initialize", ("Component", "ApplicationLifecycleManager")) :
            null;

        try
        {
            _logger.LogInformation("Initializing application lifecycle manager...");

            // Initialize global exception handler first (Phase 1 WI#8)
            if (_globalExceptionHandler != null)
            {
                if (await _globalExceptionHandler.InitializeAsync())
                {
                    _logger.LogInformation("Global exception handler initialized");

                    // Hook into global exception handler events
                    _globalExceptionHandler.CriticalExceptionOccurred += OnGlobalCriticalException;
                    _globalExceptionHandler.ExceptionHandled += OnGlobalExceptionHandled;
                }
                else
                {
                    _logger.LogWarning("Failed to initialize global exception handler - continuing without enhanced error handling");
                }
            }
            else
            {
                // Fallback to basic exception handling if no global handler is available
                _logger.LogDebug("No global exception handler provided - using basic exception handling");

                // Setup application domain unhandled exception handler
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

                // Setup task scheduler unobserved task exception handler
                TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            }

            // Setup console cancel event handler for graceful shutdown
            Console.CancelKeyPress += OnConsoleCancelKeyPress;

            // Phase 3 WI#8: Initialize health monitoring and error recovery
            await InitializeHealthMonitoringAsync();
            await InitializeErrorRecoveryAsync();

            _initialized = true;
            _logger.LogInformation("Application lifecycle manager initialized successfully with health monitoring: {HealthMonitoring}, error recovery: {ErrorRecovery}",
                _healthMonitor != null, _errorRecoveryManager != null);

            return true;
        }
        catch (Exception ex)
        {
            (perfScope as IPerformanceScope)?.MarkAsFailed(ex.Message);
            _logger.LogError(ex, "Failed to initialize application lifecycle manager");
            return false;
        }
    }

    /// <summary>
    /// Initiates a graceful shutdown of all application components
    /// </summary>
    /// <param name="exitCode">Exit code for the application</param>
    public async Task ShutdownAsync(int exitCode = 0)
    {
        if (_disposed || _isShuttingDown)
            return;

        lock (_lock)
        {
            if (_isShuttingDown)
                return;
            _isShuttingDown = true;
        }

        // Phase 2 WI#8: Performance logging for shutdown
        using var perfScope = _logger is Logger loggerWithPerf ?
            loggerWithPerf.BeginPerformanceScope("Shutdown", ("Component", "ApplicationLifecycleManager"), ("ExitCode", exitCode)) :
            null;

        try
        {
            _logger.LogInformation("Initiating application shutdown with exit code {ExitCode}...", exitCode);

            // Dispose services in reverse order of registration
            var servicesToDispose = new List<(IDisposable Service, string? Name)>();
            lock (_lock)
            {
                servicesToDispose.AddRange(_registeredServices);
                servicesToDispose.Reverse();
            }

            foreach (var (service, name) in servicesToDispose)
            {
                var serviceName = name ?? service.GetType().Name;

                // Phase 2 WI#8: Performance logging for individual service disposal
                using var serviceDisposeScope = _logger is Logger serviceLogger ?
                    serviceLogger.BeginPerformanceScope("DisposeService", ("ServiceName", serviceName)) :
                    null;

                try
                {
                    _logger.LogDebug("Disposing service: {ServiceName}", serviceName);

                    service.Dispose();

                    _logger.LogDebug("Successfully disposed service: {ServiceName}", serviceName);
                }
                catch (Exception ex)
                {
                    (serviceDisposeScope as IPerformanceScope)?.MarkAsFailed(ex.Message);
                    _logger.LogError(ex, "Error disposing service: {ServiceName}", serviceName);
                }
            }

            // Clear the services list
            lock (_lock)
            {
                _registeredServices.Clear();
            }

            _logger.LogInformation("Application shutdown completed successfully");

            // Raise exit event
            ApplicationExitRequested?.Invoke(this, EventArgs.Empty);

            // Set exit code
            Environment.ExitCode = exitCode;

            (perfScope as IPerformanceScope)?.AddContext("ServicesDisposed", servicesToDispose.Count);
        }
        catch (Exception ex)
        {
            (perfScope as IPerformanceScope)?.MarkAsFailed(ex.Message);
            _logger.LogError(ex, "Error during application shutdown");
            Environment.ExitCode = 1;
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Registers a service for lifecycle management
    /// Services are disposed in reverse order of registration during shutdown
    /// </summary>
    /// <param name="service">Service to register for disposal</param>
    /// <param name="name">Optional name for the service (for logging purposes)</param>
    public void RegisterService(IDisposable service, string? name = null)
    {
        if (_disposed)
        {
            _logger.LogWarning("Cannot register service with disposed ApplicationLifecycleManager");
            return;
        }

        if (service == null)
        {
            _logger.LogWarning("Attempted to register null service");
            return;
        }

        lock (_lock)
        {
            if (_isShuttingDown)
            {
                _logger.LogWarning("Cannot register service during shutdown");
                return;
            }

            var serviceName = name ?? service.GetType().Name;
            _registeredServices.Add((service, name));
            _logger.LogDebug("Registered service for lifecycle management: {ServiceName}", serviceName);
        }
    }

    /// <summary>
    /// Unregisters a service from lifecycle management
    /// </summary>
    /// <param name="service">Service to unregister</param>
    public void UnregisterService(IDisposable service)
    {
        if (_disposed || service == null)
            return;

        lock (_lock)
        {
            var index = _registeredServices.FindIndex(s => ReferenceEquals(s.Service, service));
            if (index >= 0)
            {
                var serviceName = _registeredServices[index].Name ?? service.GetType().Name;
                _registeredServices.RemoveAt(index);
                _logger.LogDebug("Unregistered service from lifecycle management: {ServiceName}", serviceName);
            }
        }
    }

    /// <summary>
    /// Handles console cancel key press (Ctrl+C) for graceful shutdown
    /// </summary>
    private async void OnConsoleCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        _logger.LogInformation("Console cancel key press detected, initiating graceful shutdown...");

        // Cancel the default termination
        e.Cancel = true;

        // Initiate graceful shutdown
        await ShutdownAsync(0);
    }

    /// <summary>
    /// Handles unhandled exceptions in the application domain
    /// </summary>
    private async void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var exception = e.ExceptionObject as Exception;
        if (exception != null)
        {
            _logger.LogCritical(exception, "Unhandled exception in application domain. Terminating: {IsTerminating}", e.IsTerminating);
        }
        else
        {
            _logger.LogError("Unhandled non-exception object in application domain: {ExceptionObject}. Terminating: {IsTerminating}",
                e.ExceptionObject?.ToString() ?? "null", e.IsTerminating);
        }

        if (e.IsTerminating && !_isShuttingDown)
        {
            await ShutdownAsync(1);
        }
    }

    /// <summary>
    /// Handles unobserved task exceptions
    /// </summary>
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        _logger.LogError(e.Exception, "Unobserved task exception detected");

        // Mark exception as observed to prevent application termination
        e.SetObserved();
    }

    /// <summary>
    /// Handles critical exceptions from the global exception handler (Phase 1 WI#8)
    /// </summary>
    private async void OnGlobalCriticalException(object? sender, CriticalExceptionEventArgs e)
    {
        _logger.LogCritical(e.Exception, "Critical exception detected in {Context}, terminating: {IsTerminating}",
            e.Context, e.IsTerminating);

        if (e.IsTerminating && !_isShuttingDown)
        {
            _logger.LogWarning("Critical exception is causing application termination, initiating emergency shutdown");
            await ShutdownAsync(1);
        }
    }

    /// <summary>
    /// Handles regular exceptions from the global exception handler (Phase 1 WI#8)
    /// </summary>
    private void OnGlobalExceptionHandled(object? sender, ExceptionHandledEventArgs e)
    {
        if (e.RecoverySuccessful)
        {
            _logger.LogInformation("Exception handled successfully in {Context}: {ExceptionType}",
                e.Context, e.Exception.GetType().Name);
        }
        else
        {
            _logger.LogWarning("Exception handled but recovery failed in {Context}: {ExceptionType} - {Message}",
                e.Context, e.Exception.GetType().Name, e.Exception.Message);
        }
    }

    /// <summary>
    /// Phase 3 WI#8: Initializes health monitoring integration
    /// </summary>
    private async Task InitializeHealthMonitoringAsync()
    {
        if (_healthMonitor == null)
        {
            _logger.LogDebug("No health monitor provided, skipping health monitoring initialization");
            return;
        }

        try
        {
            _logger.LogInformation("Initializing health monitoring integration...");

            // Register health check for the lifecycle manager itself
            await _healthMonitor.RegisterServiceAsync("ApplicationLifecycleManager",
                PerformLifecycleHealthCheckAsync,
                new ServiceMonitoringOptions
                {
                    CheckInterval = TimeSpan.FromMinutes(1),
                    CheckTimeout = TimeSpan.FromSeconds(10),
                    IsCritical = true,
                    FailureThreshold = 2,
                    RecoveryThreshold = 1,
                    EnableAutoRestart = false, // Lifecycle manager shouldn't auto-restart itself
                    EnableNotifications = true,
                    Tags = new Dictionary<string, string> { { "Component", "Core" }, { "Type", "Lifecycle" } }
                });

            // Hook into health monitor events for system-wide health monitoring
            _healthMonitor.SystemHealthChanged += OnSystemHealthChanged;
            _healthMonitor.CriticalServiceUnhealthy += OnCriticalServiceUnhealthy;
            _healthMonitor.ServiceRestartRecommended += OnServiceRestartRecommended;

            // Start health monitoring
            if (await _healthMonitor.StartAsync())
            {
                _logger.LogInformation("Health monitoring started successfully");
            }
            else
            {
                _logger.LogWarning("Failed to start health monitoring");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize health monitoring");
        }
    }

    /// <summary>
    /// Phase 3 WI#8: Initializes error recovery integration
    /// </summary>
    private async Task InitializeErrorRecoveryAsync()
    {
        if (_errorRecoveryManager == null)
        {
            _logger.LogDebug("No error recovery manager provided, skipping error recovery initialization");
            return;
        }

        try
        {
            _logger.LogInformation("Initializing error recovery integration...");

            // Initialize error recovery manager
            if (await _errorRecoveryManager.InitializeAsync())
            {
                // Register lifecycle manager for recovery
                await _errorRecoveryManager.RegisterComponentAsync("ApplicationLifecycleManager",
                    RecoverLifecycleManagerAsync,
                    new RecoveryOptions
                    {
                        MaxRetryAttempts = 2,
                        RetryDelay = TimeSpan.FromSeconds(2),
                        MaxRetryDelay = TimeSpan.FromSeconds(10),
                        FailureThreshold = 3,
                        CircuitBreakerTimeout = TimeSpan.FromMinutes(5),
                        EnableCircuitBreaker = true,
                        ShowUserNotifications = true,
                        Priority = RecoveryPriority.Critical
                    });

                // Hook into recovery events
                _errorRecoveryManager.RecoveryStarted += OnRecoveryStarted;
                _errorRecoveryManager.RecoveryCompleted += OnRecoveryCompleted;
                _errorRecoveryManager.RecoveryFailed += OnRecoveryFailed;
                _errorRecoveryManager.CircuitBreakerStateChanged += OnCircuitBreakerStateChanged;

                _logger.LogInformation("Error recovery integration initialized successfully");
            }
            else
            {
                _logger.LogWarning("Failed to initialize error recovery manager");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize error recovery");
        }
    }

    /// <summary>
    /// Phase 3 WI#8: Performs health check for the lifecycle manager
    /// </summary>
    private async Task<ServiceHealthResult> PerformLifecycleHealthCheckAsync()
    {
        try
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            // Check if lifecycle manager is properly initialized
            if (!_initialized || _disposed)
            {
                return new ServiceHealthResult
                {
                    Status = ServiceHealthStatus.Critical,
                    Description = $"Lifecycle manager not properly initialized (initialized: {_initialized}, disposed: {_disposed})",
                    ResponseTime = stopwatch.Elapsed
                };
            }

            // Check if shutdown is in progress
            if (_isShuttingDown)
            {
                return new ServiceHealthResult
                {
                    Status = ServiceHealthStatus.Warning,
                    Description = "Application shutdown in progress",
                    ResponseTime = stopwatch.Elapsed
                };
            }

            // Count registered services
            int serviceCount;
            lock (_lock)
            {
                serviceCount = _registeredServices.Count;
            }

            stopwatch.Stop();

            return new ServiceHealthResult
            {
                Status = ServiceHealthStatus.Healthy,
                Description = $"Lifecycle manager operating normally with {serviceCount} registered services",
                ResponseTime = stopwatch.Elapsed,
                Data = new Dictionary<string, object>
                {
                    { "RegisteredServices", serviceCount },
                    { "IsInitialized", _initialized },
                    { "IsShuttingDown", _isShuttingDown },
                    { "HasGlobalExceptionHandler", _globalExceptionHandler != null },
                    { "HasHealthMonitor", _healthMonitor != null },
                    { "HasErrorRecovery", _errorRecoveryManager != null }
                }
            };
        }
        catch (Exception ex)
        {
            return new ServiceHealthResult
            {
                Status = ServiceHealthStatus.Critical,
                Description = $"Health check failed: {ex.Message}",
                Exception = ex,
                ResponseTime = TimeSpan.FromSeconds(10) // Assume timeout
            };
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Phase 3 WI#8: Recovery function for the lifecycle manager
    /// </summary>
    private async Task<bool> RecoverLifecycleManagerAsync()
    {
        try
        {
            _logger.LogInformation("Starting lifecycle manager recovery...");

            // Attempt to reinitialize if needed
            if (!_initialized && !_disposed)
            {
                return await InitializeAsync();
            }

            // Check and fix any inconsistent state
            if (_initialized && !_disposed && !_isShuttingDown)
            {
                _logger.LogInformation("Lifecycle manager appears to be in good state, recovery successful");
                return true;
            }

            _logger.LogWarning("Lifecycle manager recovery failed - cannot recover from disposed or shutdown state");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during lifecycle manager recovery");
            return false;
        }
    }

    /// <summary>
    /// Phase 3 WI#8: Handles system health changes
    /// </summary>
    private void OnSystemHealthChanged(object? sender, SystemHealthChangedEventArgs e)
    {
        _logger.LogInformation("System health changed from {PreviousStatus} to {CurrentStatus}: {Description}",
            e.PreviousStatus, e.CurrentStatus, e.Description);

        // Take action based on system health
        switch (e.CurrentStatus)
        {
            case SystemHealthStatus.Unhealthy:
                _logger.LogWarning("System health is unhealthy - monitoring for improvement and considering recovery actions");
                break;

            case SystemHealthStatus.Degraded:
                _logger.LogInformation("System health is degraded but functional");
                break;

            case SystemHealthStatus.Healthy:
                if (e.PreviousStatus != SystemHealthStatus.Healthy && e.PreviousStatus != SystemHealthStatus.Unknown)
                {
                    _logger.LogInformation("System health recovered to healthy state");
                }
                break;
        }
    }

    /// <summary>
    /// Phase 3 WI#8: Handles critical service becoming unhealthy
    /// </summary>
    private void OnCriticalServiceUnhealthy(object? sender, CriticalServiceUnhealthyEventArgs e)
    {
        _logger.LogWarning("Critical service '{ServiceName}' is unhealthy after {FailureCount} consecutive failures: {Description}",
            e.ServiceName, e.ConsecutiveFailures, e.CheckResult.Description);

        // If a critical service fails repeatedly, consider shutdown
        if (e.ConsecutiveFailures >= 5)
        {
            _logger.LogError("Critical service '{ServiceName}' has failed {FailureCount} times - system may be unstable",
                e.ServiceName, e.ConsecutiveFailures);
        }
    }

    /// <summary>
    /// Phase 3 WI#8: Handles service restart recommendations
    /// </summary>
    private void OnServiceRestartRecommended(object? sender, ServiceRestartRecommendedEventArgs e)
    {
        _logger.LogInformation("Service restart recommended for '{ServiceName}': {Reason} (automatic: {IsAutomatic}, priority: {Priority})",
            e.ServiceName, e.Reason, e.IsAutomatic, e.Priority);

        // For critical services with high priority, we might want to take immediate action
        if (e.Priority >= 5)
        {
            _logger.LogWarning("High priority restart recommended for '{ServiceName}' - immediate attention required", e.ServiceName);
        }
    }

    /// <summary>
    /// Phase 3 WI#8: Handles recovery operation events
    /// </summary>
    private void OnRecoveryStarted(object? sender, RecoveryOperationEventArgs e)
    {
        _logger.LogInformation("Recovery started for component '{ComponentName}' with priority {Priority}",
            e.ComponentName, e.Priority);
    }

    private void OnRecoveryCompleted(object? sender, RecoveryOperationEventArgs e)
    {
        _logger.LogInformation("Recovery completed successfully for component '{ComponentName}' after {Attempts} attempts in {Duration}ms",
            e.ComponentName, e.Result.AttemptsCount, e.Result.Duration.TotalMilliseconds);
    }

    private void OnRecoveryFailed(object? sender, RecoveryOperationEventArgs e)
    {
        _logger.LogError("Recovery failed for component '{ComponentName}' after {Attempts} attempts: {ErrorMessage}",
            e.ComponentName, e.Result.AttemptsCount, e.Result.ErrorMessage);
    }

    private void OnCircuitBreakerStateChanged(object? sender, CircuitBreakerStateChangedEventArgs e)
    {
        _logger.LogInformation("Circuit breaker state changed for '{ComponentName}': {PreviousState} -> {NewState} ({Reason})",
            e.ComponentName, e.PreviousState, e.NewState, e.Reason);
    }

    /// <summary>
    /// Phase 3 WI#8: Cleanup health monitoring integration
    /// </summary>
    private async Task CleanupHealthMonitoringAsync()
    {
        if (_healthMonitor == null)
            return;

        try
        {
            _logger.LogDebug("Cleaning up health monitoring integration...");

            // Remove event handlers
            _healthMonitor.SystemHealthChanged -= OnSystemHealthChanged;
            _healthMonitor.CriticalServiceUnhealthy -= OnCriticalServiceUnhealthy;
            _healthMonitor.ServiceRestartRecommended -= OnServiceRestartRecommended;

            // Unregister ourselves
            await _healthMonitor.UnregisterServiceAsync("ApplicationLifecycleManager");

            // Stop health monitoring
            await _healthMonitor.StopAsync();

            _logger.LogDebug("Health monitoring cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error during health monitoring cleanup: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Phase 3 WI#8: Cleanup error recovery integration
    /// </summary>
    private async Task CleanupErrorRecoveryAsync()
    {
        if (_errorRecoveryManager == null)
            return;

        try
        {
            _logger.LogDebug("Cleaning up error recovery integration...");

            // Remove event handlers
            _errorRecoveryManager.RecoveryStarted -= OnRecoveryStarted;
            _errorRecoveryManager.RecoveryCompleted -= OnRecoveryCompleted;
            _errorRecoveryManager.RecoveryFailed -= OnRecoveryFailed;
            _errorRecoveryManager.CircuitBreakerStateChanged -= OnCircuitBreakerStateChanged;

            // Unregister ourselves
            await _errorRecoveryManager.UnregisterComponentAsync("ApplicationLifecycleManager");

            // Shutdown error recovery
            await _errorRecoveryManager.ShutdownAsync();

            _logger.LogDebug("Error recovery cleanup completed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error during error recovery cleanup: {Message}", ex.Message);
        }
    }

    /// <summary>
    /// Disposes the lifecycle manager and releases all resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        _logger.LogInformation("Disposing ApplicationLifecycleManager");

        try
        {
            // Remove event handlers
            Console.CancelKeyPress -= OnConsoleCancelKeyPress;

            if (_globalExceptionHandler != null)
            {
                _globalExceptionHandler.CriticalExceptionOccurred -= OnGlobalCriticalException;
                _globalExceptionHandler.ExceptionHandled -= OnGlobalExceptionHandled;
            }
            else
            {
                AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
                TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
            }

            // Phase 3 WI#8: Cleanup health monitoring and error recovery
            Task.Run(async () => await CleanupHealthMonitoringAsync()).Wait(TimeSpan.FromSeconds(5));
            Task.Run(async () => await CleanupErrorRecoveryAsync()).Wait(TimeSpan.FromSeconds(5));

            // Dispose any remaining services
            if (!_isShuttingDown)
            {
                Task.Run(() => ShutdownAsync(0)).Wait(TimeSpan.FromSeconds(10));
            }

            _disposed = true;
            _logger.LogDebug("ApplicationLifecycleManager disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ApplicationLifecycleManager disposal");
        }

        GC.SuppressFinalize(this);
    }
}