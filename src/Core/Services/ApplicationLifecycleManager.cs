using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Manages application lifecycle events and coordinates clean shutdown
/// Handles service registration/disposal and ensures graceful application termination
/// </summary>
public class ApplicationLifecycleManager : IApplicationLifecycleManager
{
    private readonly ILogger _logger;
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
    public ApplicationLifecycleManager(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
        
        try
        {
            _logger.LogInformation("Initializing application lifecycle manager...");
            
            // Setup console cancel event handler for graceful shutdown
            Console.CancelKeyPress += OnConsoleCancelKeyPress;
            
            // Setup application domain unhandled exception handler
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            
            // Setup task scheduler unobserved task exception handler
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            
            _initialized = true;
            _logger.LogInformation("Application lifecycle manager initialized successfully");
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize application lifecycle manager");
            return false;
        }
        
        await Task.CompletedTask;
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
                try
                {
                    var serviceName = name ?? service.GetType().Name;
                    _logger.LogDebug("Disposing service: {ServiceName}", serviceName);
                    
                    service.Dispose();
                    
                    _logger.LogDebug("Successfully disposed service: {ServiceName}", serviceName);
                }
                catch (Exception ex)
                {
                    var serviceName = name ?? service.GetType().Name;
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during application shutdown");
            Environment.ExitCode = 1;
        }
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
        _logger.LogCritical(exception, "Unhandled exception in application domain. Terminating: {IsTerminating}", e.IsTerminating);
        
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
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
            
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