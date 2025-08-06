using CursorPhobia.Core.Models;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Interface for error recovery management with circuit breaker patterns and self-healing capabilities
/// Part of Phase 3 WI#8: Build Automation & Error Recovery
/// </summary>
public interface IErrorRecoveryManager : IDisposable
{
    /// <summary>
    /// Event raised when a recovery operation is started
    /// </summary>
    event EventHandler<RecoveryOperationEventArgs>? RecoveryStarted;

    /// <summary>
    /// Event raised when a recovery operation completes successfully
    /// </summary>
    event EventHandler<RecoveryOperationEventArgs>? RecoveryCompleted;

    /// <summary>
    /// Event raised when a recovery operation fails
    /// </summary>
    event EventHandler<RecoveryOperationEventArgs>? RecoveryFailed;

    /// <summary>
    /// Event raised when circuit breaker state changes
    /// </summary>
    event EventHandler<CircuitBreakerStateChangedEventArgs>? CircuitBreakerStateChanged;

    /// <summary>
    /// Gets whether the error recovery manager is initialized and operational
    /// </summary>
    bool IsInitialized { get; }

    /// <summary>
    /// Gets the current circuit breaker state for a specific component
    /// </summary>
    /// <param name="componentName">Name of the component to check</param>
    /// <returns>Current circuit breaker state</returns>
    CircuitBreakerState GetCircuitBreakerState(string componentName);

    /// <summary>
    /// Gets recovery statistics for a specific component
    /// </summary>
    /// <param name="componentName">Name of the component</param>
    /// <returns>Recovery statistics or null if component not found</returns>
    RecoveryStatistics? GetRecoveryStatistics(string componentName);

    /// <summary>
    /// Initializes the error recovery manager
    /// </summary>
    /// <returns>True if initialization was successful</returns>
    Task<bool> InitializeAsync();

    /// <summary>
    /// Registers a component for error recovery monitoring
    /// </summary>
    /// <param name="componentName">Name of the component</param>
    /// <param name="recoveryAction">Action to execute when recovery is needed</param>
    /// <param name="options">Recovery options and configuration</param>
    /// <returns>True if registration was successful</returns>
    Task<bool> RegisterComponentAsync(string componentName, Func<Task<bool>> recoveryAction, RecoveryOptions? options = null);

    /// <summary>
    /// Unregisters a component from error recovery monitoring
    /// </summary>
    /// <param name="componentName">Name of the component to unregister</param>
    /// <returns>True if unregistration was successful</returns>
    Task<bool> UnregisterComponentAsync(string componentName);

    /// <summary>
    /// Reports a failure for a specific component and triggers recovery if needed
    /// </summary>
    /// <param name="componentName">Name of the component that failed</param>
    /// <param name="exception">Exception that occurred</param>
    /// <param name="context">Additional context information</param>
    /// <returns>Recovery result</returns>
    Task<RecoveryResult> ReportFailureAsync(string componentName, Exception exception, string? context = null);

    /// <summary>
    /// Reports successful operation for a component (resets failure counters)
    /// </summary>
    /// <param name="componentName">Name of the component</param>
    /// <returns>True if reported successfully</returns>
    Task<bool> ReportSuccessAsync(string componentName);

    /// <summary>
    /// Manually triggers recovery for a specific component
    /// </summary>
    /// <param name="componentName">Name of the component</param>
    /// <param name="reason">Reason for manual recovery</param>
    /// <returns>Recovery result</returns>
    Task<RecoveryResult> TriggerRecoveryAsync(string componentName, string reason = "Manual trigger");

    /// <summary>
    /// Resets the circuit breaker for a specific component
    /// </summary>
    /// <param name="componentName">Name of the component</param>
    /// <returns>True if reset was successful</returns>
    Task<bool> ResetCircuitBreakerAsync(string componentName);

    /// <summary>
    /// Gets a list of all registered components
    /// </summary>
    /// <returns>List of component names</returns>
    IReadOnlyList<string> GetRegisteredComponents();

    /// <summary>
    /// Performs health check on all registered components
    /// </summary>
    /// <returns>Overall health status</returns>
    Task<HealthCheckResult> PerformHealthCheckAsync();

    /// <summary>
    /// Shuts down the error recovery manager and releases resources
    /// </summary>
    Task ShutdownAsync();
}

/// <summary>
/// Circuit breaker states for error recovery
/// </summary>
public enum CircuitBreakerState
{
    /// <summary>
    /// Circuit is closed - operations are allowed
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open - operations are blocked due to failures
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is half-open - testing if service has recovered
    /// </summary>
    HalfOpen,

    /// <summary>
    /// Circuit is disabled - no circuit breaking behavior
    /// </summary>
    Disabled
}

/// <summary>
/// Recovery options for component registration
/// </summary>
public class RecoveryOptions
{
    /// <summary>
    /// Maximum number of retry attempts before giving up
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay between retry attempts (exponential backoff applied)
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum delay between retry attempts
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Number of failures before circuit breaker opens
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Time to wait before attempting to close an open circuit
    /// </summary>
    public TimeSpan CircuitBreakerTimeout { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Whether to enable circuit breaker for this component
    /// </summary>
    public bool EnableCircuitBreaker { get; set; } = true;

    /// <summary>
    /// Whether to show user notifications for recovery operations
    /// </summary>
    public bool ShowUserNotifications { get; set; } = true;

    /// <summary>
    /// Priority level for recovery operations
    /// </summary>
    public RecoveryPriority Priority { get; set; } = RecoveryPriority.Normal;
}

/// <summary>
/// Priority levels for recovery operations
/// </summary>
public enum RecoveryPriority
{
    /// <summary>
    /// Low priority - can be delayed
    /// </summary>
    Low,

    /// <summary>
    /// Normal priority - standard recovery
    /// </summary>
    Normal,

    /// <summary>
    /// High priority - critical component
    /// </summary>
    High,

    /// <summary>
    /// Critical priority - immediate recovery required
    /// </summary>
    Critical
}

/// <summary>
/// Result of a recovery operation
/// </summary>
public class RecoveryResult
{
    /// <summary>
    /// Whether the recovery was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Number of attempts made
    /// </summary>
    public int AttemptsCount { get; set; }

    /// <summary>
    /// Time taken for recovery operation
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Error message if recovery failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Exception that occurred during recovery (if any)
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Additional context information
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Whether the component should be temporarily disabled
    /// </summary>
    public bool ShouldDisable { get; set; }

    /// <summary>
    /// Recommended retry delay for next attempt
    /// </summary>
    public TimeSpan? NextRetryDelay { get; set; }
}

/// <summary>
/// Recovery statistics for a component
/// </summary>
public class RecoveryStatistics
{
    /// <summary>
    /// Component name
    /// </summary>
    public string ComponentName { get; set; } = string.Empty;

    /// <summary>
    /// Total number of failures reported
    /// </summary>
    public int TotalFailures { get; set; }

    /// <summary>
    /// Total number of successful recoveries
    /// </summary>
    public int SuccessfulRecoveries { get; set; }

    /// <summary>
    /// Total number of failed recoveries
    /// </summary>
    public int FailedRecoveries { get; set; }

    /// <summary>
    /// Current consecutive failure count
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Time of last failure
    /// </summary>
    public DateTime? LastFailureTime { get; set; }

    /// <summary>
    /// Time of last successful recovery
    /// </summary>
    public DateTime? LastRecoveryTime { get; set; }

    /// <summary>
    /// Current circuit breaker state
    /// </summary>
    public CircuitBreakerState CircuitBreakerState { get; set; }

    /// <summary>
    /// Average recovery time
    /// </summary>
    public TimeSpan AverageRecoveryTime { get; set; }

    /// <summary>
    /// Success rate of recovery operations (0.0 - 1.0)
    /// </summary>
    public double RecoverySuccessRate => TotalFailures > 0 ? (double)SuccessfulRecoveries / TotalFailures : 1.0;
}

/// <summary>
/// Event arguments for recovery operations
/// </summary>
public class RecoveryOperationEventArgs : EventArgs
{
    /// <summary>
    /// Name of the component being recovered
    /// </summary>
    public string ComponentName { get; set; } = string.Empty;

    /// <summary>
    /// Recovery operation details
    /// </summary>
    public RecoveryResult Result { get; set; } = new();

    /// <summary>
    /// Additional context information
    /// </summary>
    public string? Context { get; set; }

    /// <summary>
    /// Priority of the recovery operation
    /// </summary>
    public RecoveryPriority Priority { get; set; }
}

/// <summary>
/// Event arguments for circuit breaker state changes
/// </summary>
public class CircuitBreakerStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Name of the component
    /// </summary>
    public string ComponentName { get; set; } = string.Empty;

    /// <summary>
    /// Previous circuit breaker state
    /// </summary>
    public CircuitBreakerState PreviousState { get; set; }

    /// <summary>
    /// New circuit breaker state
    /// </summary>
    public CircuitBreakerState NewState { get; set; }

    /// <summary>
    /// Reason for state change
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Number of failures that triggered the state change
    /// </summary>
    public int FailureCount { get; set; }
}

/// <summary>
/// Health check result for all components
/// </summary>
public class HealthCheckResult
{
    /// <summary>
    /// Overall health status
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Individual component health results
    /// </summary>
    public Dictionary<string, ComponentHealthResult> ComponentResults { get; set; } = new();

    /// <summary>
    /// Total number of components checked
    /// </summary>
    public int TotalComponents => ComponentResults.Count;

    /// <summary>
    /// Number of healthy components
    /// </summary>
    public int HealthyComponents => ComponentResults.Values.Count(r => r.IsHealthy);

    /// <summary>
    /// Number of unhealthy components
    /// </summary>
    public int UnhealthyComponents => TotalComponents - HealthyComponents;

    /// <summary>
    /// Time when health check was performed
    /// </summary>
    public DateTime CheckTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Duration of health check operation
    /// </summary>
    public TimeSpan CheckDuration { get; set; }
}

/// <summary>
/// Health result for individual component
/// </summary>
public class ComponentHealthResult
{
    /// <summary>
    /// Whether the component is healthy
    /// </summary>
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Circuit breaker state
    /// </summary>
    public CircuitBreakerState CircuitBreakerState { get; set; }

    /// <summary>
    /// Number of recent failures
    /// </summary>
    public int RecentFailures { get; set; }

    /// <summary>
    /// Last error message (if any)
    /// </summary>
    public string? LastError { get; set; }

    /// <summary>
    /// Time of last successful operation
    /// </summary>
    public DateTime? LastSuccessTime { get; set; }

    /// <summary>
    /// Additional health information
    /// </summary>
    public string? Details { get; set; }
}