using System.Windows.Forms;
using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Global exception handler for comprehensive crash prevention and recovery
/// Hooks into Application.ThreadException and AppDomain.UnhandledException
/// Provides graceful degradation patterns and user-friendly error reporting
/// </summary>
public class GlobalExceptionHandler : IGlobalExceptionHandler
{
    private readonly ILogger _logger;
    private readonly object _lock = new();
    private bool _disposed = false;
    private bool _isActive = false;
    private int _totalExceptionsHandled = 0;
    private int _criticalExceptionsCount = 0;
    private readonly Dictionary<string, DateTime> _serviceRecoveryAttempts = new();
    private readonly Dictionary<string, int> _serviceRecoveryCount = new();
    private const int MaxServiceRecoveryAttempts = 3;
    private const int ServiceRecoveryThrottleMinutes = 5;
    
    /// <summary>
    /// Event raised when a recoverable exception is handled gracefully
    /// </summary>
    public event EventHandler<ExceptionHandledEventArgs>? ExceptionHandled;
    
    /// <summary>
    /// Event raised when a critical exception occurs that may require application restart
    /// </summary>
    public event EventHandler<CriticalExceptionEventArgs>? CriticalExceptionOccurred;
    
    /// <summary>
    /// Gets whether the global exception handler is currently active
    /// </summary>
    public bool IsActive => _isActive && !_disposed;
    
    /// <summary>
    /// Gets the total number of exceptions handled since initialization
    /// </summary>
    public int TotalExceptionsHandled => _totalExceptionsHandled;
    
    /// <summary>
    /// Gets the number of critical exceptions that have occurred
    /// </summary>
    public int CriticalExceptionsCount => _criticalExceptionsCount;
    
    /// <summary>
    /// Creates a new GlobalExceptionHandler instance
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    public GlobalExceptionHandler(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Initializes the global exception handler and hooks into system exception events
    /// </summary>
    /// <returns>True if initialization was successful, false otherwise</returns>
    public async Task<bool> InitializeAsync()
    {
        if (_disposed)
        {
            _logger.LogWarning("Cannot initialize disposed GlobalExceptionHandler");
            return false;
        }
        
        if (_isActive)
        {
            _logger.LogDebug("GlobalExceptionHandler is already initialized");
            return true;
        }
        
        try
        {
            _logger.LogInformation("Initializing global exception handler...");
            
            // Hook into Windows Forms unhandled exception handling
            Application.ThreadException += OnApplicationThreadException;
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            
            // Hook into AppDomain unhandled exception handling
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            
            // Hook into task scheduler unobserved task exceptions
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            
            // Hook into WPF DispatcherUnhandledException if available (future compatibility)
            await InitializeWpfExceptionHandlingAsync();
            
            _isActive = true;
            _logger.LogInformation("Global exception handler initialized successfully");
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize global exception handler");
            return false;
        }
    }
    
    /// <summary>
    /// Handles an exception with appropriate recovery strategies
    /// </summary>
    /// <param name="exception">The exception to handle</param>
    /// <param name="context">Context information about where the exception occurred</param>
    /// <param name="canRecover">Whether recovery is possible for this exception</param>
    /// <returns>True if the exception was handled successfully, false if the application should terminate</returns>
    public async Task<bool> HandleExceptionAsync(Exception exception, string context, bool canRecover = true)
    {
        if (_disposed || exception == null)
            return false;
        
        bool recoverySuccessful = false;
        bool isCritical = false;
        
        try
        {
            lock (_lock)
            {
                _totalExceptionsHandled++;
            }
            
            // Determine exception severity
            isCritical = IsCriticalException(exception);
            
            if (isCritical)
            {
                lock (_lock)
                {
                    _criticalExceptionsCount++;
                }
                
                _logger.LogCritical(exception, "Critical exception in {Context}", context);
                
                // Raise critical exception event
                var criticalArgs = new CriticalExceptionEventArgs(exception, context, !canRecover);
                CriticalExceptionOccurred?.Invoke(this, criticalArgs);
                
                if (!canRecover)
                {
                    await ShowUserErrorAsync(
                        "CursorPhobia encountered a critical error and must close. Please restart the application.",
                        "Critical Error",
                        true);
                    return false;
                }
            }
            else
            {
                _logger.LogError(exception, "Exception handled in {Context}", context);
            }
            
            // Attempt recovery based on exception type and context
            if (canRecover)
            {
                recoverySuccessful = await AttemptRecoveryAsync(exception, context);
                
                if (!recoverySuccessful && isCritical)
                {
                    await ShowUserErrorAsync(
                        "CursorPhobia encountered an error but will continue running. Some features may be temporarily unavailable.",
                        "Error",
                        false);
                }
                else if (!recoverySuccessful)
                {
                    await ShowUserErrorAsync(
                        "An error occurred in CursorPhobia. The application will continue running but some features may not work correctly.",
                        "Warning",
                        false);
                }
            }
            else
            {
                // If we can't recover, don't attempt recovery
                recoverySuccessful = false;
            }
            
            // Raise handled exception event
            var handledArgs = new ExceptionHandledEventArgs(exception, context, recoverySuccessful);
            ExceptionHandled?.Invoke(this, handledArgs);
            
            return recoverySuccessful;
        }
        catch (Exception handlerException)
        {
            // Avoid recursive exceptions in the exception handler
            _logger.LogError(handlerException, "Error in exception handler while processing {Context}", context);
            return false;
        }
    }
    
    /// <summary>
    /// Shows a user-friendly error message without technical details
    /// </summary>
    /// <param name="message">User-friendly error message</param>
    /// <param name="title">Dialog title</param>
    /// <param name="isCritical">Whether this is a critical error</param>
    public async Task ShowUserErrorAsync(string message, string title = "CursorPhobia Error", bool isCritical = false)
    {
        try
        {
            var icon = isCritical ? MessageBoxIcon.Error : MessageBoxIcon.Warning;
            var buttons = isCritical ? MessageBoxButtons.OK : MessageBoxButtons.OK;
            
            // Show on UI thread if available
            if (Application.MessageLoop)
            {
                MessageBox.Show(message, title, buttons, icon);
            }
            else
            {
                // For console applications or when UI thread is not available
                _logger.LogWarning("User error message (no UI available): {Title} - {Message}", title, message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error showing user error dialog: {Message}", message);
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Attempts to recover from a service failure by restarting the service
    /// </summary>
    /// <param name="serviceName">Name of the failed service</param>
    /// <param name="restartAction">Action to restart the service</param>
    /// <returns>True if recovery was successful, false otherwise</returns>
    public async Task<bool> AttemptServiceRecoveryAsync(string serviceName, Func<Task<bool>> restartAction)
    {
        if (_disposed || string.IsNullOrEmpty(serviceName) || restartAction == null)
            return false;
        
        try
        {
            lock (_lock)
            {
                // Check recovery throttling
                if (_serviceRecoveryAttempts.TryGetValue(serviceName, out var lastAttempt))
                {
                    if (DateTime.UtcNow - lastAttempt < TimeSpan.FromMinutes(ServiceRecoveryThrottleMinutes))
                    {
                        _logger.LogWarning("Service recovery throttled for {ServiceName}", serviceName);
                        return false;
                    }
                }
                
                // Check recovery attempt count
                var currentCount = _serviceRecoveryCount.GetValueOrDefault(serviceName, 0);
                if (currentCount >= MaxServiceRecoveryAttempts)
                {
                    _logger.LogWarning("Maximum service recovery attempts reached for {ServiceName}", serviceName);
                    return false;
                }
                
                // Update recovery tracking
                _serviceRecoveryAttempts[serviceName] = DateTime.UtcNow;
                _serviceRecoveryCount[serviceName] = currentCount + 1;
            }
            
            _logger.LogInformation("Attempting service recovery for {ServiceName} (attempt {Attempt}/{Max})", 
                serviceName, _serviceRecoveryCount[serviceName], MaxServiceRecoveryAttempts);
            
            // Attempt service restart
            bool success = await restartAction();
            
            if (success)
            {
                _logger.LogInformation("Service recovery successful for {ServiceName}", serviceName);
                
                // Reset recovery count on success
                lock (_lock)
                {
                    _serviceRecoveryCount[serviceName] = 0;
                }
            }
            else
            {
                _logger.LogWarning("Service recovery failed for {ServiceName}", serviceName);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during service recovery for {ServiceName}", serviceName);
            return false;
        }
    }
    
    /// <summary>
    /// Shuts down the global exception handler and unhooks from system events
    /// </summary>
    public async Task ShutdownAsync()
    {
        if (_disposed || !_isActive)
            return;
        
        try
        {
            _logger.LogDebug("Shutting down global exception handler");
            
            // Unhook from exception events
            Application.ThreadException -= OnApplicationThreadException;
            AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;
            TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
            
            // Unhook from WPF exception events if they were hooked
            await ShutdownWpfExceptionHandlingAsync();
            
            _isActive = false;
            
            _logger.LogInformation("Global exception handler shutdown completed. Handled {Total} exceptions ({Critical} critical)", 
                _totalExceptionsHandled, _criticalExceptionsCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during global exception handler shutdown");
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Handles Windows Forms thread exceptions
    /// </summary>
    private async void OnApplicationThreadException(object sender, ThreadExceptionEventArgs e)
    {
        await HandleExceptionAsync(e.Exception, "Windows Forms Thread", true);
    }
    
    /// <summary>
    /// Handles AppDomain unhandled exceptions
    /// </summary>
    private async void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception exception)
        {
            await HandleExceptionAsync(exception, "AppDomain Unhandled", !e.IsTerminating);
        }
        else
        {
            _logger.LogError("AppDomain unhandled non-exception object: {Object}", e.ExceptionObject?.ToString() ?? "null");
        }
    }
    
    /// <summary>
    /// Handles unobserved task exceptions
    /// </summary>
    private async void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        // Mark as observed to prevent application termination
        e.SetObserved();
        
        await HandleExceptionAsync(e.Exception, "Unobserved Task", true);
    }
    
    /// <summary>
    /// Determines if an exception is critical and may require application termination
    /// </summary>
    private static bool IsCriticalException(Exception exception)
    {
        return exception switch
        {
            OutOfMemoryException => true,
            StackOverflowException => true,
            AccessViolationException => true,
            BadImageFormatException => true,
            CannotUnloadAppDomainException => true,
            InvalidProgramException => true,
            _ when exception.GetType().Name.Contains("Fatal") => true,
            _ => false
        };
    }
    
    /// <summary>
    /// Attempts to recover from an exception based on its type and context
    /// </summary>
    private async Task<bool> AttemptRecoveryAsync(Exception exception, string context)
    {
        try
        {
            // Recovery strategies based on exception type
            switch (exception)
            {
                case UnauthorizedAccessException:
                    _logger.LogInformation("Attempting recovery from unauthorized access in {Context}", context);
                    // Could implement retry with elevated permissions or alternative approach
                    return false;
                
                case TimeoutException:
                    _logger.LogInformation("Attempting recovery from timeout in {Context}", context);
                    // Timeouts are often recoverable by retrying
                    return true;
                
                case System.IO.IOException:
                    _logger.LogInformation("Attempting recovery from IO exception in {Context}", context);
                    // IO exceptions might be temporary
                    await Task.Delay(1000); // Brief delay before retry
                    return true;
                
                case System.Net.NetworkInformation.NetworkInformationException:
                case System.Net.Sockets.SocketException:
                    _logger.LogInformation("Attempting recovery from network exception in {Context}", context);
                    // Network issues are often temporary
                    return true;
                
                case ArgumentNullException:
                case ArgumentOutOfRangeException:
                case ArgumentException:
                    _logger.LogWarning("Argument exception in {Context} - may indicate programming error", context);
                    return false;
                
                default:
                    // Generic recovery approach
                    _logger.LogDebug("Attempting generic recovery for {ExceptionType} in {Context}", 
                        exception.GetType().Name, context);
                    return true;
            }
        }
        catch (Exception recoveryException)
        {
            _logger.LogError(recoveryException, "Error during exception recovery for {Context}", context);
            return false;
        }
    }
    
    /// <summary>
    /// Disposes the global exception handler and releases all resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        
        _logger.LogDebug("Disposing GlobalExceptionHandler");
        
        try
        {
            // Shutdown synchronously with timeout
            var shutdownTask = ShutdownAsync();
            if (!shutdownTask.Wait(TimeSpan.FromSeconds(5)))
            {
                _logger.LogWarning("GlobalExceptionHandler disposal timed out");
            }
            
            _disposed = true;
            _logger.LogDebug("GlobalExceptionHandler disposed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during GlobalExceptionHandler disposal");
            _disposed = true;
        }
        
        GC.SuppressFinalize(this);
    }
    
    /// <summary>
    /// Attempts to initialize WPF exception handling if WPF is available (future compatibility)
    /// </summary>
    private async Task InitializeWpfExceptionHandlingAsync()
    {
        try
        {
            // Use reflection to check if System.Windows.Application is available and has current instance
            var wpfApplicationType = Type.GetType("System.Windows.Application, PresentationFramework");
            if (wpfApplicationType != null)
            {
                var currentProperty = wpfApplicationType.GetProperty("Current", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var currentApp = currentProperty?.GetValue(null);
                
                if (currentApp != null)
                {
                    // Get the DispatcherUnhandledException event
                    var dispatcherUnhandledExceptionEvent = wpfApplicationType.GetEvent("DispatcherUnhandledException");
                    if (dispatcherUnhandledExceptionEvent != null)
                    {
                        // Create delegate for WPF exception handler
                        var handlerMethod = typeof(GlobalExceptionHandler).GetMethod(nameof(OnWpfDispatcherUnhandledException), 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        if (handlerMethod != null)
                        {
                            var handler = Delegate.CreateDelegate(dispatcherUnhandledExceptionEvent.EventHandlerType!, this, handlerMethod);
                            dispatcherUnhandledExceptionEvent.AddEventHandler(currentApp, handler);
                            
                            _logger.LogDebug("WPF DispatcherUnhandledException handler registered successfully");
                        }
                    }
                }
            }
            else
            {
                _logger.LogDebug("WPF not available - skipping WPF exception handling initialization");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize WPF exception handling - continuing without WPF support");
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Shuts down WPF exception handling if it was initialized
    /// </summary>
    private async Task ShutdownWpfExceptionHandlingAsync()
    {
        try
        {
            // Use reflection to unhook WPF exception handling if it was hooked
            var wpfApplicationType = Type.GetType("System.Windows.Application, PresentationFramework");
            if (wpfApplicationType != null)
            {
                var currentProperty = wpfApplicationType.GetProperty("Current", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var currentApp = currentProperty?.GetValue(null);
                
                if (currentApp != null)
                {
                    var dispatcherUnhandledExceptionEvent = wpfApplicationType.GetEvent("DispatcherUnhandledException");
                    if (dispatcherUnhandledExceptionEvent != null)
                    {
                        var handlerMethod = typeof(GlobalExceptionHandler).GetMethod(nameof(OnWpfDispatcherUnhandledException), 
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        
                        if (handlerMethod != null)
                        {
                            var handler = Delegate.CreateDelegate(dispatcherUnhandledExceptionEvent.EventHandlerType!, this, handlerMethod);
                            dispatcherUnhandledExceptionEvent.RemoveEventHandler(currentApp, handler);
                            
                            _logger.LogDebug("WPF DispatcherUnhandledException handler unregistered successfully");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during WPF exception handling shutdown");
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Handles WPF DispatcherUnhandledException events
    /// </summary>
    private async void OnWpfDispatcherUnhandledException(object sender, object e)
    {
        try
        {
            // Use reflection to get exception and handled properties from WPF DispatcherUnhandledExceptionEventArgs
            var eventArgsType = e.GetType();
            var exceptionProperty = eventArgsType.GetProperty("Exception");
            var handledProperty = eventArgsType.GetProperty("Handled");
            
            if (exceptionProperty?.GetValue(e) is Exception exception)
            {
                var handled = await HandleExceptionAsync(exception, "WPF Dispatcher", true);
                
                // Mark as handled if we successfully handled it
                if (handled && handledProperty != null)
                {
                    handledProperty.SetValue(e, true);
                }
            }
        }
        catch (Exception handlerException)
        {
            _logger.LogError(handlerException, "Error in WPF dispatcher exception handler");
        }
    }
}