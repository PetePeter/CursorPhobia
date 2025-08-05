using System.Collections.Concurrent;
using CursorPhobia.Core.Utilities;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Implements error recovery management with circuit breaker patterns and self-healing capabilities
/// Part of Phase 3 WI#8: Build Automation & Error Recovery
/// </summary>
public class ErrorRecoveryManager : IErrorRecoveryManager
{
    private readonly ILogger _logger;
    private readonly ISystemTrayManager? _trayManager;
    private readonly ConcurrentDictionary<string, RecoveryComponent> _components = new();
    private readonly System.Threading.Timer _maintenanceTimer;
    private readonly object _lock = new();
    private bool _disposed = false;
    private bool _initialized = false;
    
    /// <summary>
    /// Event raised when a recovery operation is started
    /// </summary>
    public event EventHandler<RecoveryOperationEventArgs>? RecoveryStarted;
    
    /// <summary>
    /// Event raised when a recovery operation completes successfully
    /// </summary>
    public event EventHandler<RecoveryOperationEventArgs>? RecoveryCompleted;
    
    /// <summary>
    /// Event raised when a recovery operation fails
    /// </summary>
    public event EventHandler<RecoveryOperationEventArgs>? RecoveryFailed;
    
    /// <summary>
    /// Event raised when circuit breaker state changes
    /// </summary>
    public event EventHandler<CircuitBreakerStateChangedEventArgs>? CircuitBreakerStateChanged;
    
    /// <summary>
    /// Gets whether the error recovery manager is initialized and operational
    /// </summary>
    public bool IsInitialized => _initialized && !_disposed;
    
    /// <summary>
    /// Creates a new ErrorRecoveryManager instance
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="trayManager">Optional system tray manager for notifications</param>
    public ErrorRecoveryManager(ILogger logger, ISystemTrayManager? trayManager = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _trayManager = trayManager;
        
        // Setup maintenance timer to run every 30 seconds
        _maintenanceTimer = new System.Threading.Timer(PerformMaintenance, null, Timeout.Infinite, Timeout.Infinite);
    }
    
    /// <summary>
    /// Initializes the error recovery manager
    /// </summary>
    /// <returns>True if initialization was successful</returns>
    public async Task<bool> InitializeAsync()
    {
        if (_disposed)
        {
            _logger.LogWarning("Cannot initialize disposed ErrorRecoveryManager");
            return false;
        }
        
        if (_initialized)
        {
            _logger.LogDebug("ErrorRecoveryManager is already initialized");
            return true;
        }
        
        try
        {
            _logger.LogInformation("Initializing error recovery manager...");
            
            // Start maintenance timer
            _maintenanceTimer.Change(TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            
            _initialized = true;
            _logger.LogInformation("Error recovery manager initialized successfully");
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize error recovery manager");
            return false;
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Gets the current circuit breaker state for a specific component
    /// </summary>
    /// <param name="componentName">Name of the component to check</param>
    /// <returns>Current circuit breaker state</returns>
    public CircuitBreakerState GetCircuitBreakerState(string componentName)
    {
        if (string.IsNullOrEmpty(componentName) || !_components.TryGetValue(componentName, out var component))
        {
            return CircuitBreakerState.Disabled;
        }
        
        return component.CircuitBreakerState;
    }
    
    /// <summary>
    /// Gets recovery statistics for a specific component
    /// </summary>
    /// <param name="componentName">Name of the component</param>
    /// <returns>Recovery statistics or null if component not found</returns>
    public RecoveryStatistics? GetRecoveryStatistics(string componentName)
    {
        if (string.IsNullOrEmpty(componentName) || !_components.TryGetValue(componentName, out var component))
        {
            return null;
        }
        
        return new RecoveryStatistics
        {
            ComponentName = componentName,
            TotalFailures = component.TotalFailures,
            SuccessfulRecoveries = component.SuccessfulRecoveries,
            FailedRecoveries = component.FailedRecoveries,
            ConsecutiveFailures = component.ConsecutiveFailures,
            LastFailureTime = component.LastFailureTime,
            LastRecoveryTime = component.LastRecoveryTime,
            CircuitBreakerState = component.CircuitBreakerState,
            AverageRecoveryTime = component.AverageRecoveryTime
        };
    }
    
    /// <summary>
    /// Registers a component for error recovery monitoring
    /// </summary>
    /// <param name="componentName">Name of the component</param>
    /// <param name="recoveryAction">Action to execute when recovery is needed</param>
    /// <param name="options">Recovery options and configuration</param>
    /// <returns>True if registration was successful</returns>
    public async Task<bool> RegisterComponentAsync(string componentName, Func<Task<bool>> recoveryAction, RecoveryOptions? options = null)
    {
        if (string.IsNullOrEmpty(componentName))
        {
            _logger.LogWarning("Cannot register component with null or empty name");
            return false;
        }
        
        if (recoveryAction == null)
        {
            _logger.LogWarning("Cannot register component '{ComponentName}' with null recovery action", componentName);
            return false;
        }
        
        if (_disposed)
        {
            _logger.LogWarning("Cannot register component with disposed ErrorRecoveryManager");
            return false;
        }
        
        try
        {
            var effectiveOptions = options ?? new RecoveryOptions();
            var component = new RecoveryComponent
            {
                Name = componentName,
                RecoveryAction = recoveryAction,
                Options = effectiveOptions,
                CircuitBreakerState = effectiveOptions.EnableCircuitBreaker ? CircuitBreakerState.Closed : CircuitBreakerState.Disabled,
                LastStateChange = DateTime.UtcNow
            };
            
            _components.AddOrUpdate(componentName, component, (key, existing) =>
            {
                _logger.LogInformation("Updating existing component registration: {ComponentName}", componentName);
                existing.RecoveryAction = recoveryAction;
                existing.Options = effectiveOptions;
                return existing;
            });
            
            _logger.LogInformation("Registered component for error recovery: {ComponentName} (Priority: {Priority}, Circuit Breaker: {CircuitBreaker})",
                componentName, effectiveOptions.Priority, effectiveOptions.EnableCircuitBreaker);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register component: {ComponentName}", componentName);
            return false;
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Unregisters a component from error recovery monitoring
    /// </summary>
    /// <param name="componentName">Name of the component to unregister</param>
    /// <returns>True if unregistration was successful</returns>
    public async Task<bool> UnregisterComponentAsync(string componentName)
    {
        if (string.IsNullOrEmpty(componentName))
        {
            return false;
        }
        
        try
        {
            if (_components.TryRemove(componentName, out var component))
            {
                _logger.LogInformation("Unregistered component from error recovery: {ComponentName}", componentName);
                return true;
            }
            
            _logger.LogDebug("Component not found for unregistration: {ComponentName}", componentName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unregister component: {ComponentName}", componentName);
            return false;
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Reports a failure for a specific component and triggers recovery if needed
    /// </summary>
    /// <param name="componentName">Name of the component that failed</param>
    /// <param name="exception">Exception that occurred</param>
    /// <param name="context">Additional context information</param>
    /// <returns>Recovery result</returns>
    public async Task<RecoveryResult> ReportFailureAsync(string componentName, Exception exception, string? context = null)
    {
        if (string.IsNullOrEmpty(componentName) || !_components.TryGetValue(componentName, out var component))
        {
            return new RecoveryResult
            {
                Success = false,
                ErrorMessage = "Component not found or not registered",
                Context = context
            };
        }
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            _logger.LogWarning("Failure reported for component '{ComponentName}': {ExceptionType} - {Message}",
                componentName, exception.GetType().Name, exception.Message);
            
            // Update failure statistics
            component.TotalFailures++;
            component.ConsecutiveFailures++;
            component.ConsecutiveSuccesses = 0;
            component.LastFailureTime = DateTime.UtcNow;
            
            // Check circuit breaker state
            if (component.Options.EnableCircuitBreaker)
            {
                await UpdateCircuitBreakerStateAsync(component, isFailure: true);
                
                // If circuit is open, don't attempt recovery
                if (component.CircuitBreakerState == CircuitBreakerState.Open)
                {
                    _logger.LogWarning("Circuit breaker is open for component '{ComponentName}', skipping recovery attempt", componentName);
                    return new RecoveryResult
                    {
                        Success = false,
                        ErrorMessage = "Circuit breaker is open",
                        Context = context,
                        ShouldDisable = true,
                        NextRetryDelay = component.Options.CircuitBreakerTimeout
                    };
                }
            }
            
            // Attempt recovery
            return await AttemptRecoveryAsync(component, exception, context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during failure handling for component: {ComponentName}", componentName);
            return new RecoveryResult
            {
                Success = false,
                Duration = stopwatch.Elapsed,
                ErrorMessage = $"Error during failure handling: {ex.Message}",
                Exception = ex,
                Context = context
            };
        }
    }
    
    /// <summary>
    /// Reports successful operation for a component (resets failure counters)
    /// </summary>
    /// <param name="componentName">Name of the component</param>
    /// <returns>True if reported successfully</returns>
    public async Task<bool> ReportSuccessAsync(string componentName)
    {
        if (string.IsNullOrEmpty(componentName) || !_components.TryGetValue(componentName, out var component))
        {
            return false;
        }
        
        try
        {
            // Reset failure counters
            component.ConsecutiveFailures = 0;
            component.ConsecutiveSuccesses++;
            
            // Update circuit breaker state
            if (component.Options.EnableCircuitBreaker)
            {
                await UpdateCircuitBreakerStateAsync(component, isFailure: false);
            }
            
            _logger.LogDebug("Success reported for component: {ComponentName}", componentName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reporting success for component: {ComponentName}", componentName);
            return false;
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Manually triggers recovery for a specific component
    /// </summary>
    /// <param name="componentName">Name of the component</param>
    /// <param name="reason">Reason for manual recovery</param>
    /// <returns>Recovery result</returns>
    public async Task<RecoveryResult> TriggerRecoveryAsync(string componentName, string reason = "Manual trigger")
    {
        if (string.IsNullOrEmpty(componentName) || !_components.TryGetValue(componentName, out var component))
        {
            return new RecoveryResult
            {
                Success = false,
                ErrorMessage = "Component not found or not registered",
                Context = reason
            };
        }
        
        _logger.LogInformation("Manual recovery triggered for component '{ComponentName}': {Reason}", componentName, reason);
        
        return await AttemptRecoveryAsync(component, new InvalidOperationException(reason), reason);
    }
    
    /// <summary>
    /// Resets the circuit breaker for a specific component
    /// </summary>
    /// <param name="componentName">Name of the component</param>
    /// <returns>True if reset was successful</returns>
    public async Task<bool> ResetCircuitBreakerAsync(string componentName)
    {
        if (string.IsNullOrEmpty(componentName) || !_components.TryGetValue(componentName, out var component))
        {
            return false;
        }
        
        try
        {
            var previousState = component.CircuitBreakerState;
            component.CircuitBreakerState = CircuitBreakerState.Closed;
            component.ConsecutiveFailures = 0;
            component.LastStateChange = DateTime.UtcNow;
            
            _logger.LogInformation("Circuit breaker reset for component: {ComponentName}", componentName);
            
            // Raise state change event
            CircuitBreakerStateChanged?.Invoke(this, new CircuitBreakerStateChangedEventArgs
            {
                ComponentName = componentName,
                PreviousState = previousState,
                NewState = CircuitBreakerState.Closed,
                Reason = "Manual reset",
                FailureCount = 0
            });
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting circuit breaker for component: {ComponentName}", componentName);
            return false;
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Gets a list of all registered components
    /// </summary>
    /// <returns>List of component names</returns>
    public IReadOnlyList<string> GetRegisteredComponents()
    {
        return _components.Keys.ToList();
    }
    
    /// <summary>
    /// Performs health check on all registered components
    /// </summary>
    /// <returns>Overall health status</returns>
    public async Task<HealthCheckResult> PerformHealthCheckAsync()
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new HealthCheckResult
        {
            CheckTime = DateTime.UtcNow,
            ComponentResults = new Dictionary<string, ComponentHealthResult>()
        };
        
        try
        {
            var tasks = _components.Values.Select(async component =>
            {
                var componentResult = new ComponentHealthResult
                {
                    CircuitBreakerState = component.CircuitBreakerState,
                    RecentFailures = component.ConsecutiveFailures,
                    LastSuccessTime = component.LastRecoveryTime,
                    Details = $"Total failures: {component.TotalFailures}, Success rate: {component.SuccessRate:P1}"
                };
                
                // Determine health based on circuit breaker state and recent failures
                componentResult.IsHealthy = component.CircuitBreakerState == CircuitBreakerState.Closed &&
                                          component.ConsecutiveFailures < component.Options.FailureThreshold;
                
                if (!componentResult.IsHealthy && component.LastFailureTime.HasValue)
                {
                    var timeSinceLastFailure = DateTime.UtcNow - component.LastFailureTime.Value;
                    componentResult.LastError = $"Last failure: {timeSinceLastFailure.TotalMinutes:F1} minutes ago";
                }
                
                return new { Name = component.Name, Result = componentResult };
            });
            
            var componentResults = await Task.WhenAll(tasks);
            
            foreach (var componentResult in componentResults)
            {
                result.ComponentResults[componentResult.Name] = componentResult.Result;
            }
            
            // Determine overall health
            result.IsHealthy = result.ComponentResults.Values.All(r => r.IsHealthy);
            
            result.CheckDuration = stopwatch.Elapsed;
            
            _logger.LogDebug("Health check completed: {HealthyComponents}/{TotalComponents} components healthy",
                result.HealthyComponents, result.TotalComponents);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during health check");
            result.IsHealthy = false;
            result.CheckDuration = stopwatch.Elapsed;
            return result;
        }
    }
    
    /// <summary>
    /// Shuts down the error recovery manager and releases resources
    /// </summary>
    public async Task ShutdownAsync()
    {
        if (_disposed)
            return;
        
        try
        {
            _logger.LogInformation("Shutting down error recovery manager...");
            
            // Stop maintenance timer
            _maintenanceTimer.Change(Timeout.Infinite, Timeout.Infinite);
            
            // Clear components
            _components.Clear();
            
            _initialized = false;
            _logger.LogInformation("Error recovery manager shutdown completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during error recovery manager shutdown");
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Attempts recovery for a component
    /// </summary>
    private async Task<RecoveryResult> AttemptRecoveryAsync(RecoveryComponent component, Exception originalException, string? context)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = new RecoveryResult
        {
            Context = context,
            AttemptsCount = 0
        };
        
        // Raise recovery started event
        var eventArgs = new RecoveryOperationEventArgs
        {
            ComponentName = component.Name,
            Result = result,
            Context = context,
            Priority = component.Options.Priority
        };
        RecoveryStarted?.Invoke(this, eventArgs);
        
        for (int attempt = 1; attempt <= component.Options.MaxRetryAttempts; attempt++)
        {
            result.AttemptsCount = attempt;
            
            try
            {
                _logger.LogInformation("Attempting recovery for component '{ComponentName}' (attempt {Attempt}/{MaxAttempts})",
                    component.Name, attempt, component.Options.MaxRetryAttempts);
                
                // Calculate delay with exponential backoff
                if (attempt > 1)
                {
                    var delay = CalculateRetryDelay(component.Options, attempt - 1);
                    await Task.Delay(delay);
                }
                
                // Attempt recovery
                var recoverySuccess = await component.RecoveryAction();
                
                if (recoverySuccess)
                {
                    result.Success = true;
                    result.Duration = stopwatch.Elapsed;
                    
                    // Update component statistics
                    component.SuccessfulRecoveries++;
                    component.ConsecutiveFailures = 0;
                    component.LastRecoveryTime = DateTime.UtcNow;
                    component.UpdateAverageRecoveryTime(result.Duration);
                    
                    // Update circuit breaker
                    if (component.Options.EnableCircuitBreaker)
                    {
                        await UpdateCircuitBreakerStateAsync(component, isFailure: false);
                    }
                    
                    _logger.LogInformation("Recovery successful for component '{ComponentName}' after {Attempts} attempts in {Duration}ms",
                        component.Name, attempt, result.Duration.TotalMilliseconds);
                    
                    // Show user notification if enabled
                    if (component.Options.ShowUserNotifications && _trayManager != null)
                    {
                        await _trayManager.ShowNotificationAsync("CursorPhobia Recovery",
                            $"Component '{component.Name}' recovered successfully", false);
                    }
                    
                    // Raise success event
                    RecoveryCompleted?.Invoke(this, eventArgs);
                    
                    return result;
                }
                else
                {
                    _logger.LogWarning("Recovery attempt {Attempt} failed for component '{ComponentName}': Recovery action returned false",
                        attempt, component.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Recovery attempt {Attempt} failed for component '{ComponentName}': {ExceptionType} - {Message}",
                    attempt, component.Name, ex.GetType().Name, ex.Message);
                
                result.Exception = ex;
                result.ErrorMessage = ex.Message;
            }
        }
        
        // All recovery attempts failed
        result.Success = false;
        result.Duration = stopwatch.Elapsed;
        component.FailedRecoveries++;
        
        // Update circuit breaker
        if (component.Options.EnableCircuitBreaker)
        {
            await UpdateCircuitBreakerStateAsync(component, isFailure: true);
        }
        
        _logger.LogError("Recovery failed for component '{ComponentName}' after {Attempts} attempts in {Duration}ms",
            component.Name, result.AttemptsCount, result.Duration.TotalMilliseconds);
        
        // Show user notification if enabled
        if (component.Options.ShowUserNotifications && _trayManager != null)
        {
            await _trayManager.ShowNotificationAsync("CursorPhobia Recovery",
                $"Failed to recover component '{component.Name}' after {result.AttemptsCount} attempts", true);
        }
        
        // Raise failure event
        RecoveryFailed?.Invoke(this, eventArgs);
        
        return result;
    }
    
    /// <summary>
    /// Updates circuit breaker state based on success/failure
    /// </summary>
    private async Task UpdateCircuitBreakerStateAsync(RecoveryComponent component, bool isFailure)
    {
        var previousState = component.CircuitBreakerState;
        var newState = previousState;
        var stateChanged = false;
        
        switch (component.CircuitBreakerState)
        {
            case CircuitBreakerState.Closed:
                if (isFailure && component.ConsecutiveFailures >= component.Options.FailureThreshold)
                {
                    newState = CircuitBreakerState.Open;
                    stateChanged = true;
                }
                break;
                
            case CircuitBreakerState.Open:
                var timeSinceLastFailure = DateTime.UtcNow - (component.LastStateChange ?? DateTime.UtcNow);
                if (timeSinceLastFailure >= component.Options.CircuitBreakerTimeout)
                {
                    newState = CircuitBreakerState.HalfOpen;
                    stateChanged = true;
                }
                break;
                
            case CircuitBreakerState.HalfOpen:
                if (isFailure)
                {
                    newState = CircuitBreakerState.Open;
                    stateChanged = true;
                }
                else
                {
                    newState = CircuitBreakerState.Closed;
                    stateChanged = true;
                }
                break;
        }
        
        if (stateChanged)
        {
            component.CircuitBreakerState = newState;
            component.LastStateChange = DateTime.UtcNow;
            
            _logger.LogInformation("Circuit breaker state changed for component '{ComponentName}': {PreviousState} -> {NewState}",
                component.Name, previousState, newState);
            
            // Raise state change event
            CircuitBreakerStateChanged?.Invoke(this, new CircuitBreakerStateChangedEventArgs
            {
                ComponentName = component.Name,
                PreviousState = previousState,
                NewState = newState,
                Reason = isFailure ? "Failure threshold exceeded" : "Recovery successful",
                FailureCount = component.ConsecutiveFailures
            });
        }
        
        await Task.CompletedTask;
    }
    
    /// <summary>
    /// Calculates retry delay with exponential backoff
    /// </summary>
    private TimeSpan CalculateRetryDelay(RecoveryOptions options, int attemptNumber)
    {
        var delay = TimeSpan.FromMilliseconds(options.RetryDelay.TotalMilliseconds * Math.Pow(2, attemptNumber));
        return delay > options.MaxRetryDelay ? options.MaxRetryDelay : delay;
    }
    
    /// <summary>
    /// Performs periodic maintenance tasks
    /// </summary>
    private void PerformMaintenance(object? state)
    {
        try
        {
            if (_disposed || !_initialized)
                return;
            
            var now = DateTime.UtcNow;
            
            foreach (var component in _components.Values)
            {
                // Check for stale circuit breaker states
                if (component.CircuitBreakerState == CircuitBreakerState.Open && component.LastStateChange.HasValue)
                {
                    var timeSinceStateChange = now - component.LastStateChange.Value;
                    if (timeSinceStateChange >= component.Options.CircuitBreakerTimeout)
                    {
                        // Move to half-open to test recovery
                        Task.Run(async () => await UpdateCircuitBreakerStateAsync(component, isFailure: false));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error during error recovery maintenance: {Message}", ex.Message);
        }
    }
    
    /// <summary>
    /// Disposes the error recovery manager
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        
        try
        {
            _maintenanceTimer?.Dispose();
            Task.Run(async () => await ShutdownAsync()).Wait(TimeSpan.FromSeconds(5));
            _disposed = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ErrorRecoveryManager disposal");
        }
        
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Internal class representing a recovery component
/// </summary>
internal class RecoveryComponent
{
    public string Name { get; set; } = string.Empty;
    public Func<Task<bool>> RecoveryAction { get; set; } = null!;
    public RecoveryOptions Options { get; set; } = new();
    public CircuitBreakerState CircuitBreakerState { get; set; } = CircuitBreakerState.Closed;
    public DateTime? LastStateChange { get; set; }
    public int TotalFailures { get; set; }
    public int SuccessfulRecoveries { get; set; }
    public int FailedRecoveries { get; set; }
    public int ConsecutiveFailures { get; set; }
    public int ConsecutiveSuccesses { get; set; }
    public DateTime? LastFailureTime { get; set; }
    public DateTime? LastRecoveryTime { get; set; }
    public TimeSpan AverageRecoveryTime { get; set; }
    
    private readonly List<TimeSpan> _recoveryTimes = new();
    
    public double SuccessRate => TotalFailures > 0 ? (double)SuccessfulRecoveries / TotalFailures : 1.0;
    
    public void UpdateAverageRecoveryTime(TimeSpan recoveryTime)
    {
        _recoveryTimes.Add(recoveryTime);
        
        // Keep only the last 100 recovery times to prevent memory bloat
        if (_recoveryTimes.Count > 100)
        {
            _recoveryTimes.RemoveAt(0);
        }
        
        AverageRecoveryTime = TimeSpan.FromMilliseconds(_recoveryTimes.Average(t => t.TotalMilliseconds));
    }
}