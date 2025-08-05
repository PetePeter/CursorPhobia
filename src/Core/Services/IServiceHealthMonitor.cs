using CursorPhobia.Core.Models;

namespace CursorPhobia.Core.Services;

/// <summary>
/// Interface for monitoring the health of application services and components
/// Part of Phase 3 WI#8: Build Automation & Error Recovery
/// </summary>
public interface IServiceHealthMonitor : IDisposable
{
    /// <summary>
    /// Event raised when a service health status changes
    /// </summary>
    event EventHandler<ServiceHealthChangedEventArgs>? ServiceHealthChanged;
    
    /// <summary>
    /// Event raised when a critical service becomes unhealthy
    /// </summary>
    event EventHandler<CriticalServiceUnhealthyEventArgs>? CriticalServiceUnhealthy;
    
    /// <summary>
    /// Event raised when overall system health changes
    /// </summary>
    event EventHandler<SystemHealthChangedEventArgs>? SystemHealthChanged;
    
    /// <summary>
    /// Event raised when a service restart is recommended
    /// </summary>
    event EventHandler<ServiceRestartRecommendedEventArgs>? ServiceRestartRecommended;
    
    /// <summary>
    /// Gets whether the health monitor is currently running
    /// </summary>
    bool IsRunning { get; }
    
    /// <summary>
    /// Gets the current overall system health status
    /// </summary>
    SystemHealthStatus SystemHealth { get; }
    
    /// <summary>
    /// Gets the monitoring interval for health checks
    /// </summary>
    TimeSpan MonitoringInterval { get; set; }
    
    /// <summary>
    /// Starts the health monitoring service
    /// </summary>
    /// <returns>True if started successfully</returns>
    Task<bool> StartAsync();
    
    /// <summary>
    /// Stops the health monitoring service
    /// </summary>
    /// <returns>True if stopped successfully</returns>
    Task<bool> StopAsync();
    
    /// <summary>
    /// Registers a service for health monitoring
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <param name="healthCheck">Health check function</param>
    /// <param name="options">Monitoring options</param>
    /// <returns>True if registration was successful</returns>
    Task<bool> RegisterServiceAsync(string serviceName, Func<Task<ServiceHealthResult>> healthCheck, ServiceMonitoringOptions? options = null);
    
    /// <summary>
    /// Unregisters a service from health monitoring
    /// </summary>
    /// <param name="serviceName">Name of the service to unregister</param>
    /// <returns>True if unregistration was successful</returns>
    Task<bool> UnregisterServiceAsync(string serviceName);
    
    /// <summary>
    /// Gets the current health status of a specific service
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <returns>Current health status or null if service not found</returns>
    ServiceHealthStatus? GetServiceHealth(string serviceName);
    
    /// <summary>
    /// Gets detailed health information for a specific service
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <returns>Detailed health information or null if service not found</returns>
    ServiceHealthInfo? GetServiceHealthInfo(string serviceName);
    
    /// <summary>
    /// Gets health information for all monitored services
    /// </summary>
    /// <returns>Dictionary of service health information</returns>
    IReadOnlyDictionary<string, ServiceHealthInfo> GetAllServiceHealth();
    
    /// <summary>
    /// Performs an immediate health check on a specific service
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <returns>Health check result</returns>
    Task<ServiceHealthResult> CheckServiceHealthAsync(string serviceName);
    
    /// <summary>
    /// Performs an immediate health check on all registered services
    /// </summary>
    /// <returns>Overall system health check result</returns>
    Task<SystemHealthCheckResult> CheckAllServicesHealthAsync();
    
    /// <summary>
    /// Gets a list of all registered service names
    /// </summary>
    /// <returns>List of service names</returns>
    IReadOnlyList<string> GetRegisteredServices();
    
    /// <summary>
    /// Gets health monitoring statistics
    /// </summary>
    /// <returns>Health monitoring statistics</returns>
    HealthMonitoringStatistics GetStatistics();
    
    /// <summary>
    /// Resets health statistics for a specific service
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <returns>True if reset was successful</returns>
    Task<bool> ResetServiceStatisticsAsync(string serviceName);
    
    /// <summary>
    /// Updates the monitoring configuration for a service
    /// </summary>
    /// <param name="serviceName">Name of the service</param>
    /// <param name="options">New monitoring options</param>
    /// <returns>True if update was successful</returns>
    Task<bool> UpdateServiceMonitoringAsync(string serviceName, ServiceMonitoringOptions options);
}


/// <summary>
/// Individual service health status
/// </summary>
public enum ServiceHealthStatus
{
    /// <summary>
    /// Service is operating normally
    /// </summary>
    Healthy,
    
    /// <summary>
    /// Service has minor issues but is functional
    /// </summary>
    Warning,
    
    /// <summary>
    /// Service has significant issues and may not function properly
    /// </summary>
    Unhealthy,
    
    /// <summary>
    /// Service is in critical failure state
    /// </summary>
    Critical,
    
    /// <summary>
    /// Service health status is unknown
    /// </summary>
    Unknown
}

/// <summary>
/// Service monitoring configuration options
/// </summary>
public class ServiceMonitoringOptions
{
    /// <summary>
    /// How often to check the service health
    /// </summary>
    public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(30);
    
    /// <summary>
    /// Timeout for health check operations
    /// </summary>
    public TimeSpan CheckTimeout { get; set; } = TimeSpan.FromSeconds(10);
    
    /// <summary>
    /// Whether this is a critical service that affects overall system health
    /// </summary>
    public bool IsCritical { get; set; } = false;
    
    /// <summary>
    /// Number of consecutive failures before marking as unhealthy
    /// </summary>
    public int FailureThreshold { get; set; } = 3;
    
    /// <summary>
    /// Number of consecutive successes needed to mark as healthy again
    /// </summary>
    public int RecoveryThreshold { get; set; } = 2;
    
    /// <summary>
    /// Whether to automatically attempt service restart on critical failures
    /// </summary>
    public bool EnableAutoRestart { get; set; } = false;
    
    /// <summary>
    /// Maximum number of automatic restart attempts
    /// </summary>
    public int MaxRestartAttempts { get; set; } = 3;
    
    /// <summary>
    /// Delay between restart attempts
    /// </summary>
    public TimeSpan RestartDelay { get; set; } = TimeSpan.FromMinutes(1);
    
    /// <summary>
    /// Whether to send notifications for health status changes
    /// </summary>
    public bool EnableNotifications { get; set; } = true;
    
    /// <summary>
    /// Custom tags for categorizing the service
    /// </summary>
    public Dictionary<string, string> Tags { get; set; } = new();
}

/// <summary>
/// Result of a service health check
/// </summary>
public class ServiceHealthResult
{
    /// <summary>
    /// Overall health status of the service
    /// </summary>
    public ServiceHealthStatus Status { get; set; }
    
    /// <summary>
    /// Detailed description of the health status
    /// </summary>
    public string Description { get; set; } = string.Empty;
    
    /// <summary>
    /// Response time for the health check
    /// </summary>
    public TimeSpan ResponseTime { get; set; }
    
    /// <summary>
    /// Exception that occurred during health check (if any)
    /// </summary>
    public Exception? Exception { get; set; }
    
    /// <summary>
    /// Additional health data specific to the service
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();
    
    /// <summary>
    /// Timestamp when the health check was performed
    /// </summary>
    public DateTime CheckTime { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Whether the service should be restarted
    /// </summary>
    public bool RecommendRestart { get; set; }
    
    /// <summary>
    /// Optional restart reason
    /// </summary>
    public string? RestartReason { get; set; }
}

/// <summary>
/// Detailed health information for a service
/// </summary>
public class ServiceHealthInfo
{
    /// <summary>
    /// Service name
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;
    
    /// <summary>
    /// Current health status
    /// </summary>
    public ServiceHealthStatus Status { get; set; }
    
    /// <summary>
    /// Last health check result
    /// </summary>
    public ServiceHealthResult? LastCheckResult { get; set; }
    
    /// <summary>
    /// Time of last successful health check
    /// </summary>
    public DateTime? LastHealthyTime { get; set; }
    
    /// <summary>
    /// Time of last failed health check
    /// </summary>
    public DateTime? LastUnhealthyTime { get; set; }
    
    /// <summary>
    /// Number of consecutive failures
    /// </summary>
    public int ConsecutiveFailures { get; set; }
    
    /// <summary>
    /// Number of consecutive successes
    /// </summary>
    public int ConsecutiveSuccesses { get; set; }
    
    /// <summary>
    /// Total number of health checks performed
    /// </summary>
    public int TotalChecks { get; set; }
    
    /// <summary>
    /// Total number of failed health checks
    /// </summary>
    public int TotalFailures { get; set; }
    
    /// <summary>
    /// Average response time for health checks
    /// </summary>
    public TimeSpan AverageResponseTime { get; set; }
    
    /// <summary>
    /// Monitoring configuration options
    /// </summary>
    public ServiceMonitoringOptions Options { get; set; } = new();
    
    /// <summary>
    /// Number of automatic restart attempts made
    /// </summary>
    public int RestartAttempts { get; set; }
    
    /// <summary>
    /// Time of last restart attempt
    /// </summary>
    public DateTime? LastRestartTime { get; set; }
    
    /// <summary>
    /// Health check success rate (0.0 - 1.0)
    /// </summary>
    public double SuccessRate => TotalChecks > 0 ? (double)(TotalChecks - TotalFailures) / TotalChecks : 1.0;
}

/// <summary>
/// Result of system-wide health check
/// </summary>
public class SystemHealthCheckResult
{
    /// <summary>
    /// Overall system health status
    /// </summary>
    public SystemHealthStatus SystemStatus { get; set; }
    
    /// <summary>
    /// Individual service health results
    /// </summary>
    public Dictionary<string, ServiceHealthResult> ServiceResults { get; set; } = new();
    
    /// <summary>
    /// Total number of services checked
    /// </summary>
    public int TotalServices => ServiceResults.Count;
    
    /// <summary>
    /// Number of healthy services
    /// </summary>
    public int HealthyServices => ServiceResults.Values.Count(r => r.Status == ServiceHealthStatus.Healthy);
    
    /// <summary>
    /// Number of services with warnings
    /// </summary>
    public int WarningServices => ServiceResults.Values.Count(r => r.Status == ServiceHealthStatus.Warning);
    
    /// <summary>
    /// Number of unhealthy services
    /// </summary>
    public int UnhealthyServices => ServiceResults.Values.Count(r => r.Status == ServiceHealthStatus.Unhealthy);
    
    /// <summary>
    /// Number of critical services
    /// </summary>
    public int CriticalServices => ServiceResults.Values.Count(r => r.Status == ServiceHealthStatus.Critical);
    
    /// <summary>
    /// Time when system health check was performed
    /// </summary>
    public DateTime CheckTime { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Duration of the system health check
    /// </summary>
    public TimeSpan CheckDuration { get; set; }
    
    /// <summary>
    /// Summary description of system health
    /// </summary>
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Health monitoring statistics
/// </summary>
public class HealthMonitoringStatistics
{
    /// <summary>
    /// Total number of services being monitored
    /// </summary>
    public int TotalServices { get; set; }
    
    /// <summary>
    /// Number of critical services
    /// </summary>
    public int CriticalServices { get; set; }
    
    /// <summary>
    /// Total health checks performed across all services
    /// </summary>
    public long TotalHealthChecks { get; set; }
    
    /// <summary>
    /// Total failed health checks
    /// </summary>
    public long TotalFailures { get; set; }
    
    /// <summary>
    /// Average health check response time
    /// </summary>
    public TimeSpan AverageResponseTime { get; set; }
    
    /// <summary>
    /// Time when monitoring started
    /// </summary>
    public DateTime MonitoringStartTime { get; set; }
    
    /// <summary>
    /// Total monitoring uptime
    /// </summary>
    public TimeSpan MonitoringUptime { get; set; }
    
    /// <summary>
    /// Number of automatic restarts performed
    /// </summary>
    public int TotalRestarts { get; set; }
    
    /// <summary>
    /// Overall success rate (0.0 - 1.0)
    /// </summary>
    public double OverallSuccessRate => TotalHealthChecks > 0 ? (double)(TotalHealthChecks - TotalFailures) / TotalHealthChecks : 1.0;
}

/// <summary>
/// Event arguments for service health changes
/// </summary>
public class ServiceHealthChangedEventArgs : EventArgs
{
    /// <summary>
    /// Name of the service
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;
    
    /// <summary>
    /// Previous health status
    /// </summary>
    public ServiceHealthStatus PreviousStatus { get; set; }
    
    /// <summary>
    /// New health status
    /// </summary>
    public ServiceHealthStatus NewStatus { get; set; }
    
    /// <summary>
    /// Health check result that triggered the change
    /// </summary>
    public ServiceHealthResult CheckResult { get; set; } = new();
    
    /// <summary>
    /// Whether this is a critical service
    /// </summary>
    public bool IsCritical { get; set; }
}

/// <summary>
/// Event arguments for critical service becoming unhealthy
/// </summary>
public class CriticalServiceUnhealthyEventArgs : EventArgs
{
    /// <summary>
    /// Name of the critical service
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;
    
    /// <summary>
    /// Health check result
    /// </summary>
    public ServiceHealthResult CheckResult { get; set; } = new();
    
    /// <summary>
    /// Number of consecutive failures
    /// </summary>
    public int ConsecutiveFailures { get; set; }
    
    /// <summary>
    /// Whether automatic restart is recommended
    /// </summary>
    public bool RestartRecommended { get; set; }
}


/// <summary>
/// Event arguments for service restart recommendations
/// </summary>
public class ServiceRestartRecommendedEventArgs : EventArgs
{
    /// <summary>
    /// Name of the service that should be restarted
    /// </summary>
    public string ServiceName { get; set; } = string.Empty;
    
    /// <summary>
    /// Reason for restart recommendation
    /// </summary>
    public string Reason { get; set; } = string.Empty;
    
    /// <summary>
    /// Health check result that triggered the recommendation
    /// </summary>
    public ServiceHealthResult CheckResult { get; set; } = new();
    
    /// <summary>
    /// Whether this is an automatic restart or manual recommendation
    /// </summary>
    public bool IsAutomatic { get; set; }
    
    /// <summary>
    /// Priority of the restart (higher number = higher priority)
    /// </summary>
    public int Priority { get; set; } = 1;
}